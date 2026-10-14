param(
    [Parameter(Mandatory=$true)][string]$AndroidSdk,
    [Parameter(Mandatory=$true)][string]$JavaHome,
    [Parameter(Mandatory=$true)][string]$GradleExecutable,
    [Parameter(Mandatory=$true)][string]$LlamaSource,
    [string]$GradleHome,
    [string]$StarterDirectory,
    [switch]$DirectoryPackaging,
    [string]$PythonExecutable='python'
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$project=Join-Path $repo 'apps/android'
$env:JAVA_HOME=(Resolve-Path $JavaHome).Path
$env:ANDROID_HOME=(Resolve-Path $AndroidSdk).Path
$env:ANDROID_PREFS_ROOT=Join-Path $repo 'artifacts/android-home'
$env:ANDROID_USER_HOME=Join-Path $env:ANDROID_PREFS_ROOT '.android'
$env:TEMP=Join-Path $repo 'artifacts/temp'
$env:TMP=$env:TEMP
$env:PATH=(Join-Path $env:JAVA_HOME 'bin')+';'+$env:PATH
if ($GradleHome) {$env:GRADLE_USER_HOME=[IO.Path]::GetFullPath($GradleHome)}
New-Item -ItemType Directory -Path $env:ANDROID_USER_HOME,$env:ANDROID_PREFS_ROOT,$env:TEMP -Force | Out-Null
if (-not $StarterDirectory) {$StarterDirectory=Join-Path $repo 'artifacts/portable/development'}
$assets=Join-Path $project 'app/build/generated/starter'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
$starter=Join-Path $StarterDirectory 'SmolLM2-135M-Instruct-Q4_K_M.gguf'
if ((Get-FileHash -LiteralPath $starter -Algorithm SHA256).Hash -ne '2e8040ceae7815abe0dcb3540b9995eaa1fa0d2ca9e797d0a635ae4433c68c2d') {throw 'Starter checksum mismatch'}
Copy-Item -LiteralPath $starter -Destination (Join-Path $assets 'starter.gguf')
Copy-Item -LiteralPath (Join-Path $StarterDirectory 'MODEL_CARD.md'),(Join-Path $StarterDirectory 'LICENSE-Apache-2.0.txt'),(Join-Path $repo 'LICENSE'),(Join-Path $repo 'THIRD_PARTY.md') -Destination $assets
Copy-Item -LiteralPath (Join-Path $LlamaSource 'LICENSE') -Destination (Join-Path $assets 'LICENSE-llama.cpp.txt')
Copy-Item -LiteralPath (Join-Path $env:ANDROID_HOME 'ndk/28.2.13676358/NOTICE') -Destination (Join-Path $assets 'NOTICE-Android-NDK.txt')
Copy-Item -LiteralPath (Join-Path $env:ANDROID_HOME 'ndk/28.2.13676358/NOTICE.toolchain') -Destination (Join-Path $assets 'NOTICE-Android-toolchain.txt')

# This private signing material is excluded from Git and all public packages.
# Keep it to sign later APK updates with the same identity.
if (-not $env:HYDRA_ANDROID_KEYSTORE) {
    $signing=Join-Path $repo 'artifacts/private-android-signing'
    New-Item -ItemType Directory -Path $signing -Force | Out-Null
    $credentials=Join-Path $signing 'credentials.json'
    if (-not (Test-Path $credentials)) {
        $random=New-Object byte[] 32
        $rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($random);$rng.Dispose()
        @{password=[Convert]::ToBase64String($random)} | ConvertTo-Json | Set-Content -LiteralPath $credentials
    }
    $password=(Get-Content $credentials -Raw | ConvertFrom-Json).password
    $env:HYDRA_ANDROID_KEYSTORE=Join-Path $signing 'hydra-release.jks'
    $env:HYDRA_ANDROID_STORE_PASSWORD=$password
    $env:HYDRA_ANDROID_KEY_PASSWORD=$password
    if (-not (Test-Path $env:HYDRA_ANDROID_KEYSTORE)) {
        & (Join-Path $env:JAVA_HOME 'bin/keytool.exe') -genkeypair -keystore $env:HYDRA_ANDROID_KEYSTORE -storepass:env HYDRA_ANDROID_STORE_PASSWORD -keypass:env HYDRA_ANDROID_KEY_PASSWORD -alias hydra -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Hydra AI, O=KaelixDevs'
        if ($LASTEXITCODE -ne 0) {throw 'APK signing key creation failed'}
    }
}
if ($DirectoryPackaging) {
    & $GradleExecutable -p $project --no-daemon --stacktrace ('-PllamaDir='+[IO.Path]::GetFullPath($LlamaSource).Replace('\','/')) mergeReleaseResources processReleaseManifest stripReleaseDebugSymbols compileReleaseJavaWithJavac -x processReleaseResources
    if ($LASTEXITCODE -ne 0) {throw 'Android native build or lint failed'}
    & $PythonExecutable (Join-Path $PSScriptRoot 'package-android-directory.py')
    if ($LASTEXITCODE -ne 0) {throw 'Android directory packaging failed'}
    & $GradleExecutable -p $project --no-daemon ('-PllamaDir='+[IO.Path]::GetFullPath($LlamaSource).Replace('\','/')) lintRelease -x processReleaseResources
    if ($LASTEXITCODE -ne 0) {throw 'Android lint failed'}
    return
}
& $GradleExecutable -p $project --no-daemon --stacktrace ('-PllamaDir='+[IO.Path]::GetFullPath($LlamaSource).Replace('\','/')) assembleRelease lintRelease
if ($LASTEXITCODE -ne 0) {throw 'Android build or lint failed'}
$apk=Join-Path $project 'app/build/outputs/apk/release/app-release.apk'
$destination=Join-Path $repo 'artifacts/HydraAI-0.2.0-arm64.apk'
Copy-Item -LiteralPath $apk -Destination $destination
& (Join-Path $env:ANDROID_HOME 'build-tools/36.0.0/apksigner.bat') verify --verbose --print-certs $destination
if ($LASTEXITCODE -ne 0) {throw 'APK signature verification failed'}
Write-Output "APK ready: $destination"
