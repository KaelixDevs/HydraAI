"""Windows AAPT2 ZIP-writer workaround using official resource, DEX and signing tools.

Run build-android.ps1 -DirectoryPackaging to prepare the native library and assets.
Only this app's dependency-free Java sources are supported by this fallback.
"""
import os
from pathlib import Path
import subprocess
import shutil
import zipfile

repo = Path(__file__).resolve().parent.parent
app = repo / 'apps/android/app'
sdk = Path(os.environ['ANDROID_HOME'])
java = Path(os.environ['JAVA_HOME']) / 'bin'
tools = sdk / 'build-tools/36.0.0'
platform = sdk / 'platforms/android-36/android.jar'
stage = app / 'build/directory-package'
assert stage.resolve().is_relative_to((app / 'build').resolve())
if stage.exists():
    shutil.rmtree(stage)
for name in ('resources', 'rclasses', 'dex', 'rjava'):
    (stage / name).mkdir(parents=True, exist_ok=True)
for directory in (app / 'src/main/res').iterdir():
    if directory.is_dir() and not directory.name.startswith('values'):
        (stage / 'resources/res' / directory.name).mkdir(parents=True, exist_ok=True)

def run(*args):
    subprocess.run([str(a) for a in args], check=True)

resources = sorted((app / 'build/intermediates/merged_res/release/mergeReleaseResources').glob('*.flat'))
assert resources, 'Run the Gradle resource merge first.'
manifest = app / 'build/intermediates/merged_manifests/release/processReleaseManifest/AndroidManifest.xml'
run(tools / 'aapt2.exe', 'link', '--output-to-dir', '--manifest', manifest,
    '-I', platform, '-o', stage / 'resources', '--java', stage / 'rjava', '--min-sdk-version', '26', '--target-sdk-version', '36', *resources)
run(java / 'javac.exe', '-encoding', 'UTF-8', '-source', '17', '-target', '17',
    '-d', stage / 'rclasses', *sorted((stage / 'rjava').rglob('*.java')))
rjar = app / 'build/intermediates/compile_and_runtime_not_namespaced_r_class_jar/release/processReleaseResources/R.jar'
rjar.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(rjar, 'w') as archive:
    for path in sorted((stage / 'rclasses').rglob('*.class')):
        archive.write(path, path.relative_to(stage / 'rclasses').as_posix())
classes = app / 'build/intermediates/javac/release/compileReleaseJavaWithJavac/classes'
assert (classes / 'dev/kaelix/hydra/MainActivity.class').is_file(), 'Compile Java with Gradle first.'
with zipfile.ZipFile(stage / 'classes.jar', 'w') as archive:
    for directory in (classes, stage / 'rclasses'):
        for path in sorted(directory.rglob('*.class')):
            archive.write(path, path.relative_to(directory).as_posix())
run(tools / 'd8.bat', '--release', '--min-api', '26', '--lib', platform,
    '--output', stage / 'dex', stage / 'classes.jar')
native = app / 'build/intermediates/stripped_native_libs/release/stripReleaseDebugSymbols/out/lib'
assert (native / 'arm64-v8a/libhydra_llama.so').is_file(), 'Build the ARM64 library first.'
with zipfile.ZipFile(stage / 'unsigned.apk', 'w', compression=zipfile.ZIP_DEFLATED) as archive:
    for directory, prefix in [(stage / 'resources', ''), (stage / 'dex', ''),
                              (app / 'build/generated/starter', 'assets/'), (native, 'lib/')]:
        for path in sorted(directory.rglob('*')):
            if path.is_file():
                name = prefix + path.relative_to(directory).as_posix()
                stored = path.suffix in ('.so', '.gguf', '.arsc')
                archive.write(path, name, compress_type=zipfile.ZIP_STORED if stored else zipfile.ZIP_DEFLATED)
run(tools / 'zipalign.exe', '-f', '-P', '16', '4', stage / 'unsigned.apk', stage / 'aligned.apk')
destination = repo / 'artifacts/HydraAI-0.2.0-arm64.apk'
run(tools / 'apksigner.bat', 'sign', '--ks', os.environ['HYDRA_ANDROID_KEYSTORE'],
    '--ks-key-alias', 'hydra', '--ks-pass', 'env:HYDRA_ANDROID_STORE_PASSWORD',
    '--key-pass', 'env:HYDRA_ANDROID_KEY_PASSWORD', '--out', destination, stage / 'aligned.apk')
run(tools / 'zipalign.exe', '-c', '-P', '16', '4', destination)
run(tools / 'apksigner.bat', 'verify', '--verbose', '--print-certs', destination)
print('Signed APK:', destination)
