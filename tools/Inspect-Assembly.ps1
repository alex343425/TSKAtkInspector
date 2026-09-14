param(
    [string]$Pattern = 'Battle|Buff|Abnormal',
    [string]$Assembly = 'Assembly-CSharp',
    [switch]$NamesOnly,
    [switch]$IL
)
$gameRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
Add-Type -Path (Join-Path $gameRoot 'BepInEx/core/Mono.Cecil.dll')
$path = Join-Path $gameRoot "BepInEx/interop/$Assembly.dll"
if (!(Test-Path $path)) { $path = Join-Path $gameRoot "BepInEx/core/$Assembly.dll" }
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($path)
foreach ($type in $module.GetTypes()) {
    if ($type.FullName -notmatch $Pattern) { continue }
    if ($NamesOnly) { $type.FullName; continue }
    "TYPE $($type.FullName) : $($type.BaseType)"
    foreach ($field in $type.Fields) {
        if ($field.Name -notmatch '^Native(Method|Field|Class)') { "  FIELD $field $(if ($field.HasConstant) { '= ' + $field.Constant })" }
    }
    foreach ($property in $type.Properties) { "  PROP $property" }
    foreach ($method in $type.Methods) {
        if ($method.IsGetter -or $method.IsSetter -or $method.Name -eq '.cctor') { continue }
        "  METHOD $method"
        if ($IL) { $method.Body.Instructions | ForEach-Object { "    $_" } }
    }
}
