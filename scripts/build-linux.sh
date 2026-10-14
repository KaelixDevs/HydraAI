#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
output="$PWD/artifacts/releases"
publish="$PWD/artifacts/linux-x64"
dotnet publish apps/linux/Hydra.Linux.csproj -c Release -r linux-x64 --self-contained true -o "$publish"
python3 scripts/fetch-platform-assets.py --linux-publish "$publish"
python3 scripts/linux-notices.py --publish "$publish"
mkdir -p "$output"
dotnet msbuild apps/linux/Hydra.Linux.csproj -t:CreateRpm -p:Configuration=Release -p:RuntimeIdentifier=linux-x64 -p:SelfContained=true "-p:PublishDir=$publish/" "-p:RpmPath=$output/HydraAI-0.2.0-1.x86_64.rpm" -p:Release=1
python3 scripts/package-linux.py --publish "$publish" --output "$output"
