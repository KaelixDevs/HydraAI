"""Package an already-published Linux x64 directory. Requires zstandard for Arch."""
from pathlib import Path
import argparse, hashlib, io, os, tarfile, time, zipfile
import zstandard

parser=argparse.ArgumentParser()
parser.add_argument('--publish',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
publish=args.publish.resolve(); output=args.output.resolve(); output.mkdir(parents=True,exist_ok=True)
assert (publish/'hydra-ai').read_bytes()[:4]==b'\x7fELF', 'Publish a linux-x64 application first.'
assert (publish/'runtime/llama-server').read_bytes()[:4]==b'\x7fELF', 'Linux runtime missing.'
files=sorted(p for p in publish.rglob('*') if p.is_file())
def add_bytes(tar,name,data,mode=0o644):
    item=tarfile.TarInfo(name);item.size=len(data);item.mode=mode;item.mtime=0;item.uid=item.gid=0;item.uname=item.gname='root';tar.addfile(item,io.BytesIO(data))
def add_file(tar,path,name):
    with path.open('rb') as stream: magic=stream.read(4)
    mode=0o755 if magic==b'\x7fELF' or path.name=='hydra-ai' else 0o644
    item=tar.gettarinfo(str(path),arcname=name);item.mode=mode;item.mtime=0;item.uid=item.gid=0;item.uname=item.gname='root'
    with path.open('rb') as stream:tar.addfile(item,stream)
portable=output/'HydraAI-0.2.0-linux-x64.tar.gz'
with tarfile.open(portable,'w:gz') as tar:
    for path in files:add_file(tar,path,'HydraAI/'+path.relative_to(publish).as_posix())
package=output/'hydra-ai-0.2.0-1-x86_64.pkg.tar.zst'
metadata='\n'.join([
    'pkgname = hydra-ai','pkgbase = hydra-ai','pkgver = 0.2.0-1','pkgdesc = Local AI chat, files and backups',
    'url = https://github.com/KaelixDevs/HydraAI','builddate = '+str(int(time.time())),
    'packager = KaelixDevs','size = '+str(sum(f.stat().st_size for f in files)), 'arch = x86_64','license = MIT',
    *('depend = '+d for d in ['glibc','gcc-libs','openssl','icu','zlib','krb5','libx11','libice','libsm','fontconfig','sqlite','libxcb','libxrandr','libxi','libxcursor','libglvnd'])
])+'\n'
with package.open('wb') as stream, zstandard.ZstdCompressor(level=9).stream_writer(stream) as compressed, tarfile.open(fileobj=compressed,mode='w|') as tar:
    add_bytes(tar,'.PKGINFO',metadata.encode())
    for path in files:add_file(tar,path,'usr/share/hydra-ai/'+path.relative_to(publish).as_posix())
    add_bytes(tar,'usr/bin/hydra-ai',b'#!/bin/sh\nexec /usr/share/hydra-ai/hydra-ai "$@"\n',0o755)
    add_file(tar,publish/'hydra-ai.desktop','usr/share/applications/hydra-ai.desktop')
    add_file(tar,publish/'hydra-ai.svg','usr/share/icons/hicolor/scalable/apps/hydra-ai.svg')
digest=hashlib.file_digest(package.open('rb'),'sha256').hexdigest()
setup='''#!/usr/bin/env bash
set -euo pipefail
if [[ $(uname -m) != x86_64 ]]; then echo "This release requires x86_64 Linux." >&2; exit 1; fi
command -v pacman >/dev/null || { echo "Use this setup on Arch, EndeavourOS, Manjaro or another pacman-based distribution." >&2; exit 1; }
if [[ ${EUID} == 0 ]]; then echo "Run this setup as your normal user; it uses sudo only for pacman." >&2; exit 1; fi
directory=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
package="$directory/hydra-ai-0.2.0-1-x86_64.pkg.tar.zst"
if [[ ! -f $package ]]; then echo "Place the Arch package beside this setup file, then run it again." >&2; exit 1; fi
printf '%s  %s\\n' 'HASH' "$package" | sha256sum --check --status || { echo "Package checksum mismatch." >&2; exit 1; }
sudo pacman -U -- "$package"
echo "Installed. Open Hydra AI from your application menu, or run hydra-ai."
'''.replace('HASH',digest)
(output/'HydraAI-Arch-Setup.sh').write_text(setup,encoding='utf-8',newline='\n')
with zipfile.ZipFile(output/'HydraAI-0.2.0-Arch-Setup.zip','w',compression=zipfile.ZIP_STORED) as archive:
    archive.write(package,package.name)
    archive.write(output/'HydraAI-Arch-Setup.sh','HydraAI-Arch-Setup.sh')
    archive.writestr('INSTALL.txt','Extract both files into one folder. On an x86_64 Arch-based desktop, open a terminal there and run:\n\nbash HydraAI-Arch-Setup.sh\n\nThen open Hydra AI, choose Models, Set up included starter, and Load locally.\n')
print('Created Linux portable, Arch package and checksum-verified Arch setup.')
