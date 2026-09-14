param([switch]$Install)
$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$oldAppData = $env:APPDATA
$oldCliHome = $env:DOTNET_CLI_HOME
$oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$oldPackages = $env:NUGET_PACKAGES
try {
    $env:APPDATA = Join-Path $PSScriptRoot '.build-home'
    $env:DOTNET_CLI_HOME = $env:APPDATA
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget/packages'
    dotnet restore (Join-Path $PSScriptRoot 'AtkInspector.csproj') --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build (Join-Path $PSScriptRoot 'AtkInspector.csproj') -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Install) {
        $destination = Join-Path $gameRoot 'BepInEx/plugins/TSKAtkInspector.dll'
        if (Test-Path -LiteralPath $destination) {
            $backup = Join-Path $PSScriptRoot ('backups/' + (Get-Date -Format yyyyMMdd_HHmmss))
            New-Item -ItemType Directory -Path $backup -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $backup
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin/Release/net6.0/TSKAtkInspector.dll') -Destination $destination -Force
        Write-Output "Installed: $destination"
    }
} finally {
    $env:APPDATA = $oldAppData
    $env:DOTNET_CLI_HOME = $oldCliHome
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $oldTelemetry
    $env:NUGET_PACKAGES = $oldPackages
}
