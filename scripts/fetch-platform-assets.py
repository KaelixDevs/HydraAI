"""Explicit release-builder download step. Executables and weights are hash pinned."""
from pathlib import Path
import argparse
import hashlib
import shutil
import tarfile
import urllib.request
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--linux-publish', type=Path)
parser.add_argument('--android-assets', type=Path)
parser.add_argument('--llama-source', type=Path)
parser.add_argument('--cache', type=Path, default=Path('artifacts/downloads'))
args = parser.parse_args()
args.cache.mkdir(parents=True, exist_ok=True)

def fetch(url, path, digest=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    def valid():
        return path.is_file() and (digest is None or hashlib.file_digest(path.open('rb'), 'sha256').hexdigest() == digest)
    if valid():
        return path
    temporary = path.with_name(path.name + '.downloading')
    request = urllib.request.Request(url, headers={'User-Agent': 'HydraAI-release-builder'})
    with urllib.request.urlopen(request, timeout=120) as response, temporary.open('wb') as output:
        shutil.copyfileobj(response, output)
    if digest and hashlib.file_digest(temporary.open('rb'), 'sha256').hexdigest() != digest:
        temporary.unlink()
        raise ValueError('Checksum mismatch: ' + url)
    temporary.replace(path)
    return path

if args.linux_publish or args.android_assets:
    model = fetch('https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF/resolve/main/SmolLM2-135M-Instruct-Q4_K_M.gguf',
                  args.cache / 'SmolLM2-135M-Instruct-Q4_K_M.gguf', '2e8040ceae7815abe0dcb3540b9995eaa1fa0d2ca9e797d0a635ae4433c68c2d')
    card = fetch('https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF/raw/main/README.md', args.cache / 'MODEL_CARD.md')
    license = fetch('https://www.apache.org/licenses/LICENSE-2.0.txt', args.cache / 'LICENSE-Apache-2.0.txt')
    for destination in filter(None, (args.linux_publish / 'development' if args.linux_publish else None, args.android_assets)):
        destination.mkdir(parents=True, exist_ok=True)
        for path in (model, card, license):
            if path.resolve() != (destination / path.name).resolve(): shutil.copy2(path, destination / path.name)

if args.linux_publish:
    archive = fetch('https://github.com/ggml-org/llama.cpp/releases/download/b11429/llama-b11429-bin-ubuntu-x64.tar.gz',
                    args.cache / 'llama-linux.tar.gz', 'f6d25dde8f51133143d1453da4fd5f73b145127177612a283bf7995957af3392')
    runtime = args.linux_publish / 'runtime'; runtime.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive) as tar:
        for member in tar.getmembers():
            name = Path(member.name).name
            if not member.isdir() and (name == 'llama-server' or '.so' in name or name.startswith('LICENSE')):
                with tar.extractfile(member) as source, (runtime / name).open('wb') as output:
                    shutil.copyfileobj(source, output)
                (runtime / name).chmod(0o755 if name == 'llama-server' or '.so' in name else 0o644)
    fetch('https://raw.githubusercontent.com/ggml-org/llama.cpp/b11429/LICENSE', runtime / 'LICENSE-llama.cpp.txt')

if args.llama_source:
    archive = fetch('https://github.com/ggml-org/llama.cpp/archive/refs/tags/b11429.zip',
                    args.cache / 'llama-b11429.zip', '2c14039274aaf94ad0e4cbdeb575b32cc3d75c3b8bae28b052c9ff9c20ffea46')
    root = args.llama_source.resolve(); root.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as zipped:
        for entry in zipped.infolist():
            parts = Path(entry.filename).parts[1:]
            if not parts or entry.is_dir(): continue
            target = root.joinpath(*parts).resolve()
            if not target.is_relative_to(root): raise ValueError('Unsafe source archive path')
            target.parent.mkdir(parents=True, exist_ok=True)
            with zipped.open(entry) as source, target.open('wb') as output: shutil.copyfileobj(source, output)
print('Platform assets verified and staged.')
