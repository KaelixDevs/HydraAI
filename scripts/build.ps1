$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $repo 'apps/desktop/Hydra.Desktop.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
