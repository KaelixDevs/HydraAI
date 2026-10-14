param([Parameter(Mandatory=$true)][string]$VisualCppRedist)
# Explicit opt-in helper for release builders. End users use the packaged assets.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $repo 'artifacts/downloads'
$publish = Join-Path $repo 'artifacts/portable'
New-Item -ItemType Directory -Path $staging,(Join-Path $publish 'runtime'),(Join-Path $publish 'development') -Force | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $VisualCppRedist 'vcruntime140.dll'))) { throw 'Provide the licensed x64 Microsoft.VC*.CRT release redistributable directory.' }
$archive = Join-Path $staging 'llama-b11429-bin-win-cpu-x64.zip'
Invoke-WebRequest -Uri 'https://github.com/ggml-org/llama.cpp/releases/download/b11429/llama-b11429-bin-win-cpu-x64.zip' -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '1283323272b04cd07905816a597a0da810918102de958f4ff6f7bbaa70ed2efe') { throw 'Inference runtime checksum mismatch.' }
$runtime = Join-Path $staging 'llama-b11429'
Expand-Archive -LiteralPath $archive -DestinationPath $runtime -Force
Copy-Item -Path (Join-Path $runtime '*.dll'),(Join-Path $runtime 'llama-server.exe'),(Join-Path $runtime 'LICENSE*') -Destination (Join-Path $publish 'runtime')
Copy-Item -Path (Join-Path $VisualCppRedist '*.dll') -Destination (Join-Path $publish 'runtime')
$model = Join-Path $publish 'development/SmolLM2-135M-Instruct-Q4_K_M.gguf'
Invoke-WebRequest -Uri 'https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF/resolve/main/SmolLM2-135M-Instruct-Q4_K_M.gguf' -OutFile $model
if ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne '2e8040ceae7815abe0dcb3540b9995eaa1fa0d2ca9e797d0a635ae4433c68c2d') { throw 'Model checksum mismatch. Do not redistribute the artifact.' }
Invoke-WebRequest -Uri 'https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF/raw/main/README.md' -OutFile (Join-Path $publish 'development/MODEL_CARD.md')
Invoke-WebRequest -Uri 'https://www.apache.org/licenses/LICENSE-2.0.txt' -OutFile (Join-Path $publish 'development/LICENSE-Apache-2.0.txt')
Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/ggml-org/llama.cpp/b11429/LICENSE' -OutFile (Join-Path $publish 'runtime/LICENSE-llama.cpp.txt')
Write-Output 'Verified starter assets staged. Run package.ps1 to build the release.'
