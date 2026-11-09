#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

stage="release/.stage"
rm -rf "$stage"
trap 'rm -rf "$stage"' EXIT
mkdir -p release "$stage"
rm -f release/*.exe release/*.pdb

generations=(1 2)
generation_assets=("BlackSouls" "BlackSoulsII")
locales=(zh ja en)
locale_names=(CN JP EN)

for generation_index in "${!generations[@]}"; do
  generation="${generations[$generation_index]}"
  generation_asset="${generation_assets[$generation_index]}"

  for locale_index in "${!locales[@]}"; do
    locale="${locales[$locale_index]}"
    locale_name="${locale_names[$locale_index]}"
    assembly_name="$generation_asset-QoL-$locale_name"
    output_dir="$stage/BlackSouls-$generation-$locale"

    mkdir -p "$output_dir"
    dotnet publish src/options.csproj \
      -c Release \
      -p:GameGeneration="$generation" \
      -p:Locale="$locale" \
      -p:AssemblyName="$assembly_name" \
      -o "$output_dir"

    cp "$output_dir/$assembly_name.exe" "release/$assembly_name.exe"
  done
done
