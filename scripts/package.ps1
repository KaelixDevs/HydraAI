param([string]$InnoCompiler = 'ISCC.exe')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $repo 'artifacts/portable'
dotnet publish (Join-Path $repo 'apps/desktop/Hydra.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $publish -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE'),(Join-Path $repo 'PRIVACY.md'),(Join-Path $repo 'THIRD_PARTY.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repo 'docs/FILE_EXPORTS.md'),(Join-Path $repo 'docs/RELEASE_0.1.1.md') -Destination $publish
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$runtimeConfig = Get-Content -LiteralPath (Join-Path $publish 'HydraAI.runtimeconfig.json') -Raw | ConvertFrom-Json
$version = $runtimeConfig.runtimeOptions.includedFrameworks[0].version
$licenses = Join-Path $publish 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $packages "microsoft.netcore.app.runtime.win-x64/$version/LICENSE.TXT") -Destination (Join-Path $licenses 'LICENSE-dotnet.txt')
Copy-Item -LiteralPath (Join-Path $packages "microsoft.netcore.app.runtime.win-x64/$version/THIRD-PARTY-NOTICES.TXT") -Destination (Join-Path $licenses 'THIRD-PARTY-dotnet.txt')
Copy-Item -LiteralPath (Join-Path $packages "microsoft.windowsdesktop.app.runtime.win-x64/$version/LICENSE") -Destination (Join-Path $licenses 'LICENSE-WindowsDesktop.txt')
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath (Join-Path $repo 'artifacts/HydraAI-Portable.zip') -Force
if (-not (Get-Command $InnoCompiler -ErrorAction SilentlyContinue)) {
    foreach ($candidate in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 7\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe")) {
        if (Test-Path -LiteralPath $candidate) { $InnoCompiler = $candidate; break }
    }
}
if (Get-Command $InnoCompiler -ErrorAction SilentlyContinue) {
    & $InnoCompiler (Join-Path $repo 'installer/HydraAI.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
} else { Write-Warning 'Portable build complete. Install Inno Setup 6 or 7 to compile the installer.' }
