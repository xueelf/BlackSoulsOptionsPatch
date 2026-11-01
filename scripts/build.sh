#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

stage="release/.stage"
rm -rf "$stage"
trap 'rm -rf "$stage"' EXIT
mkdir -p release "$stage/cn" "$stage/jp"

dotnet publish src/options.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:Locale=zh \
  -p:AssemblyName="BLACK SOULS CN options" \
  -o "$stage/cn"

dotnet publish src/options.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:Locale=ja \
  -p:AssemblyName="BLACK SOULS JP options" \
  -o "$stage/jp"

cp "$stage/cn/BLACK SOULS CN options.exe" release/
cp "$stage/jp/BLACK SOULS JP options.exe" release/
rm -f release/*.pdb
