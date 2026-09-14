$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$oldAppData = $env:APPDATA
$oldCliHome = $env:DOTNET_CLI_HOME
try {
    $env:APPDATA = Join-Path $PSScriptRoot '.build-home'
    $env:DOTNET_CLI_HOME = $env:APPDATA
    dotnet restore (Join-Path $PSScriptRoot 'tests/Tests.csproj') --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) { throw 'Test restore failed.' }
    dotnet run --no-restore --project (Join-Path $PSScriptRoot 'tests/Tests.csproj') -- $gameRoot
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally {
    $env:APPDATA = $oldAppData
    $env:DOTNET_CLI_HOME = $oldCliHome
}
