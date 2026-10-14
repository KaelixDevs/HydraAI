param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $repo 'tests/Hydra.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
& $Python -m unittest discover -s (Join-Path $repo 'hydra-ml/tests') -v
if ($LASTEXITCODE -ne 0) { throw 'ML pipeline tests failed.' }
$env:HYDRA_DATA_DIR = Join-Path $repo 'artifacts/ui-test-data'
$app = Join-Path $repo 'apps/desktop/bin/Release/net10.0-windows/HydraAI.exe'
$output = Join-Path $repo 'artifacts/ui-smoke'
$process = Start-Process -FilePath $app -ArgumentList @('--smoke', '--output', ('"' + $output + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw 'UI smoke tests failed.' }
