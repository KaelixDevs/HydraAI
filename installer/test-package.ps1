param([Parameter(Mandatory=$true)][string]$PublishDir, [Parameter(Mandatory=$true)][string]$SetupPath)
$ErrorActionPreference = 'Stop'
foreach ($file in @('HydraAI.exe','HydraAI.dll','Hydra.Core.dll','coreclr.dll','hostfxr.dll','hostpolicy.dll','PresentationFramework.dll','HydraAI.runtimeconfig.json','licenses/LICENSE-dotnet.txt','licenses/LICENSE-WindowsDesktop.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PublishDir $file))) { throw "Missing packaged dependency: $file" }
}
$configuration = Get-Content -LiteralPath (Join-Path $PublishDir 'HydraAI.runtimeconfig.json') -Raw | ConvertFrom-Json
if (-not $configuration.runtimeOptions.includedFrameworks) { throw 'Package is not self-contained.' }
$stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $SetupPath))
try { if ($stream.ReadByte() -ne 77 -or $stream.ReadByte() -ne 90) { throw 'Installer is not a Windows executable.' } } finally { $stream.Dispose() }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $SetupPath))
if ($version.ProductName.Trim() -ne 'Hydra AI') { throw 'Installer product metadata mismatch.' }
$appVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath (Join-Path $PublishDir 'HydraAI.dll')))
if ($version.FileVersion.Trim() -ne $appVersion.FileVersion.Trim()) { throw 'Installer and application versions do not match.' }
Write-Output ('PASS packaged dependencies, self-contained runtime configuration, PE header, and matching installer/application version ' + $appVersion.FileVersion.Trim() + '.')
Write-Output 'This is a packaging check, not a clean-VM installation/upgrade/uninstall test.'
