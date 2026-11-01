#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

stage="release/.stage"
rm -rf "$stage"
trap 'rm -rf "$stage"' EXIT
mkdir -p release "$stage/cn" "$stage/jp" "$stage/en"

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

dotnet publish src/options.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:Locale=en \
  -p:AssemblyName="BLACK SOULS EN options" \
  -o "$stage/en"

rm -f release/*.exe
cp "$stage/cn/BLACK SOULS CN options.exe" release/BLACK-SOULS-CN-options.exe
cp "$stage/jp/BLACK SOULS JP options.exe" release/BLACK-SOULS-JP-options.exe
cp "$stage/en/BLACK SOULS EN options.exe" release/BLACK-SOULS-EN-options.exe
rm -f release/*.pdb
