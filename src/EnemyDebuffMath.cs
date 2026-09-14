namespace TSKAtkInspector;

internal enum NamedDebuffRequirement
{
    None,
    Fire,
    Water,
    Thunder,
    Light,
    Dark,
    Magic,
    Slash,
    Strike,
    Human,
    Deity,
    Demon
}

internal static class EnemyDebuffMath
{
    internal const long BaseRate = 10000;

    private static readonly HashSet<string> NamedTypes = new(StringComparer.Ordinal)
    {
        "Target", "Purgatory", "Collapse", "Stigmata", "ExtremelyCold",
        "DemonicHindrance", "Electrification", "Crushing", "Laceration",
        "ReserveDark", "Domination", "Refereeing", "Chaos"
    };

    internal static bool IsNamedDebuff(string typeName) => NamedTypes.Contains(typeName);

    internal static string Name(string typeName) => typeName switch
    {
        "Target" => "標的",
        "Purgatory" => "融解",
        "Collapse" => "崩壞",
        "Stigmata" => "聖痕",
        "ExtremelyCold" => "極冷",
        "DemonicHindrance" => "魔障",
        "Electrification" => "帶電",
        "Crushing" => "破碎",
        "Laceration" => "裂傷",
        "ReserveDark" => "ReserveDark",
        "Domination" => "支配",
        "Refereeing" => "審判",
        "Chaos" => "混沌",
        _ => typeName
    };

    internal static NamedDebuffRequirement Requirement(string typeName) => typeName switch
    {
        "Purgatory" => NamedDebuffRequirement.Fire,
        "ExtremelyCold" => NamedDebuffRequirement.Water,
        "Electrification" => NamedDebuffRequirement.Thunder,
        "Stigmata" => NamedDebuffRequirement.Light,
        "ReserveDark" or "Collapse" => NamedDebuffRequirement.Dark,
        "DemonicHindrance" => NamedDebuffRequirement.Magic,
        "Laceration" => NamedDebuffRequirement.Slash,
        "Crushing" => NamedDebuffRequirement.Strike,
        "Domination" => NamedDebuffRequirement.Human,
        "Refereeing" => NamedDebuffRequirement.Deity,
        "Chaos" => NamedDebuffRequirement.Demon,
        _ => NamedDebuffRequirement.None
    };

    internal static bool Applies(NamedDebuffRequirement requirement, int attribute, int attackKind,
        bool human, bool deity, bool demon) => requirement switch
    {
        NamedDebuffRequirement.Fire => attribute == 1,
        NamedDebuffRequirement.Water => attribute == 2,
        NamedDebuffRequirement.Thunder => attribute == 3,
        NamedDebuffRequirement.Light => attribute == 4,
        NamedDebuffRequirement.Dark => attribute == 5,
        NamedDebuffRequirement.Magic => attackKind == 1,
        NamedDebuffRequirement.Slash => attackKind == 2,
        NamedDebuffRequirement.Strike => attackKind == 3,
        NamedDebuffRequirement.Human => human,
        NamedDebuffRequirement.Deity => deity,
        NamedDebuffRequirement.Demon => demon,
        _ => true
    };

    internal static string RequirementName(NamedDebuffRequirement requirement) => requirement switch
    {
        NamedDebuffRequirement.Fire => "炎屬性",
        NamedDebuffRequirement.Water => "水屬性",
        NamedDebuffRequirement.Thunder => "雷屬性",
        NamedDebuffRequirement.Light => "光屬性",
        NamedDebuffRequirement.Dark => "闇屬性",
        NamedDebuffRequirement.Magic => "魔法攻擊",
        NamedDebuffRequirement.Slash => "斬擊",
        NamedDebuffRequirement.Strike => "打擊",
        NamedDebuffRequirement.Human => "人族",
        NamedDebuffRequirement.Deity => "神族",
        NamedDebuffRequirement.Demon => "魔族",
        _ => "全部傷害"
    };

    internal static long Value(string typeName, int value1, int value2, int value3, int value5, int stateLevel)
    {
        if (typeName == "Domination") return value1 + (long)Math.Max(0, stateLevel - 1) * value2;
        if (typeName != "Collapse" || value2 <= 0) return value1;
        int maxStep = Math.Max(0, value5 - 1);
        int step = Math.Min(Math.Max(0, stateLevel) / value2, maxStep);
        return value1 + (long)step * value3;
    }
}
