#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

stage="release/.stage"
rm -rf "$stage"
trap 'rm -rf "$stage"' EXIT
mkdir -p release "$stage"
rm -f release/*.exe release/*.pdb

dotnet publish src/options.csproj -c Release -o "$stage"
cp "$stage/BlackSoulsQoLPatch.exe" release/BlackSoulsQoLPatch.exe
