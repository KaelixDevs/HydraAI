"""Retain redistributed runtime and renderer notices in Linux builds."""
from pathlib import Path
import argparse, json, os, shutil, urllib.request
parser = argparse.ArgumentParser()
parser.add_argument('--publish', type=Path, required=True)
args = parser.parse_args()
repo = Path(__file__).resolve().parent.parent
assets = json.loads((repo / 'apps/linux/obj/project.assets.json').read_text(encoding='utf-8'))
packages = Path(next(iter(assets['packageFolders'])))
destination = args.publish / 'licenses'; destination.mkdir(parents=True, exist_ok=True)
runtime = json.loads((args.publish / 'hydra-ai.runtimeconfig.json').read_text())['runtimeOptions']['includedFrameworks'][0]['version']
for source, name in [(packages / 'microsoft.netcore.app.runtime.linux-x64' / runtime / 'LICENSE.TXT', 'LICENSE-dotnet.txt'),
                     (packages / 'microsoft.netcore.app.runtime.linux-x64' / runtime / 'THIRD-PARTY-NOTICES.TXT', 'THIRD-PARTY-dotnet.txt')]:
    shutil.copy2(source, destination / name)
for package in ('SkiaSharp', 'SkiaSharp.NativeAssets.Linux', 'HarfBuzzSharp', 'HarfBuzzSharp.NativeAssets.Linux'):
    keys = [key for key in assets['libraries'] if key.startswith(package + '/')]
    for key in keys:
        folder = packages / key.lower()
        for source in folder.iterdir():
            if source.is_file() and ('license' in source.name.lower() or 'notices' in source.name.lower()):
                shutil.copy2(source, destination / (package + '-' + source.name))
target = destination / 'LICENSE-Avalonia.txt'
if not target.exists():
    with urllib.request.urlopen('https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/licence.md', timeout=60) as response:
        target.write_bytes(response.read())
print('Linux third-party notices retained.')
