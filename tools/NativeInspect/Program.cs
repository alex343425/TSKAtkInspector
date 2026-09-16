using Mono.Cecil;
using Iced.Intel;
using System.Reflection.PortableExecutable;

var root = Directory.GetCurrentDirectory();
using var module = ModuleDefinition.ReadModule(Path.Combine(root, "BepInEx/interop/Assembly-CSharp.dll"));
using var reader = new BinaryReader(File.OpenRead(Path.Combine(root, "BepInEx/interop/MethodAddressToToken.db")));
reader.ReadInt32(); reader.ReadInt32(); var assemblies = reader.ReadInt32(); var count = reader.ReadInt32(); var offset = reader.ReadInt32();
var names = Enumerable.Range(0, assemblies).Select(_ => reader.ReadString()).ToArray();
reader.BaseStream.Position = offset;
var addresses = Enumerable.Range(0, count).Select(_ => reader.ReadUInt64()).ToArray();
var methods = new SortedDictionary<ulong, MethodDefinition>();
for (var i = 0; i < count; i++) {
    var token = reader.ReadUInt32(); var assembly = reader.ReadInt32();
    if (names[assembly].StartsWith("Assembly-CSharp,")) {
        if (module.LookupToken((int)token) is MethodDefinition method) methods[addresses[i]] = method;
    }
}
using var stream = File.OpenRead(Path.Combine(root, "GameAssembly.dll"));
using var pe = new PEReader(stream);
var baseAddress = pe.PEHeaders.PEHeader.ImageBase;
foreach (var argument in args.Where(a => a.StartsWith('?')))
{
    var query = argument[1..];
    var targets = methods.Where(pair => (pair.Value.DeclaringType.Name + "." + pair.Value.Name).Equals(query)).ToArray();
    foreach (var (targetAddress, targetMethod) in targets)
    {
        Console.WriteLine($"Callers of {targetMethod.FullName} @ {targetAddress:X}");
        foreach (var (callerAddress, callerMethod) in methods)
        {
            var next = addresses.FirstOrDefault(x => x > callerAddress);
            var callerRva = callerAddress >= baseAddress ? callerAddress - baseAddress : callerAddress;
            var callerLength = (int)Math.Min(next > callerAddress ? next - callerAddress : 1024, 40000);
            var callerBytes = pe.GetSectionData((int)callerRva).GetContent(0, callerLength).ToArray();
            var callerDecoder = Decoder.Create(64, new ByteArrayCodeReader(callerBytes));
            callerDecoder.IP = callerAddress;
            while (callerDecoder.IP < callerAddress + (ulong)callerBytes.Length)
            {
                var instruction = callerDecoder.Decode();
                if (instruction.NearBranchTarget != targetAddress) continue;
                Console.WriteLine($"  {callerMethod.FullName} @ {instruction.IP:X}");
            }
        }
    }
}
foreach (var argument in args.Where(a => a.StartsWith('#')))
{
    var address = Convert.ToInt32(argument[1..], 16);
    var bytes = pe.GetSectionData(address).GetContent(0, 16).ToArray();
    Console.WriteLine($"Raw data RVA {address:X}: {Convert.ToHexString(bytes)}");
    Console.WriteLine($"float={BitConverter.ToSingle(bytes):R} int32={BitConverter.ToInt32(bytes)}");
}
foreach (var argument in args.Where(a => a.StartsWith('@')))
{
    var address = Convert.ToUInt64(argument[1..], 16);
    var bytes = pe.GetSectionData((int)address).GetContent(0, 96).ToArray();
    var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
    Console.WriteLine($"Raw RVA {address:X}");
    for (int i = 0; i < 20; i++) { var ins = decoder.Decode(); Console.WriteLine($"{ins.IP:X}: {ins}"); }
}
foreach (var (address, method) in methods) {
    if (!args.Any(a => (method.DeclaringType.Name + "." + method.Name).Equals(a))) continue;
    var next = addresses.FirstOrDefault(x => x > address);
    var rva = address >= baseAddress ? address - baseAddress : address;
    var length = (int)Math.Min(next > address ? next - address : 1024, 40000);
    var bytes = pe.GetSectionData((int)rva).GetContent(0, length).ToArray();
    Console.WriteLine($"{method.FullName} @ {address:X}");
    var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
    while (decoder.IP < address + (ulong)bytes.Length) {
        var instruction = decoder.Decode();
        var target = instruction.NearBranchTarget;
        var name = methods.TryGetValue(target, out var called) ? " ; " + called.FullName : "";
        Console.WriteLine($"{instruction.IP:X}: {instruction}{name}");
    }
}
