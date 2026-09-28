#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
# Internal native installation helper; use install/uninstall-csharp-tool.sh.
set -eu
ges_action=$1
ges_destination=$2
shift 2
ges_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
ges_binary="$ges_destination/dotnet-ges"
ges_marker="$ges_destination/.ges-csharp-aot.sha256"
ges_license="$ges_destination/.ges-csharp-aot.LICENSE"
ges_notices="$ges_destination/.ges-csharp-aot.NOTICES"
hash_file() {
    if command -v shasum >/dev/null 2>&1; then shasum -a 256 "$1" | awk '{print $1}'
    else sha256sum "$1" | awk '{print $1}'; fi
}
check_owned() {
    for ges_file in "$ges_marker" "$ges_license" "$ges_notices"; do
        if [ -L "$ges_file" ] || { [ -e "$ges_file" ] && [ ! -f "$ges_file" ]; }; then
            echo "Invalid native installation metadata: $ges_file" >&2; exit 1
        fi
    done
    if [ ! -f "$ges_marker" ] || [ ! -f "$ges_binary" ] || [ -L "$ges_binary" ] \
        || [ "$(hash_file "$ges_binary")" != "$(cat "$ges_marker")" ]; then
        echo "Refusing to replace/remove an unowned or modified native CLI: $ges_binary" >&2; exit 1
    fi
}
case "$ges_action" in
    check) check_owned; exit 0 ;;
    remove)
        check_owned
        rm -- "$ges_binary" "$ges_marker"
        rm -f -- "$ges_license" "$ges_notices"
        echo "Uninstalled native C# dotnet-ges from $ges_destination."
        exit 0 ;;
    install) ;;
    *) exit 2 ;;
esac
case "$(uname -s)" in
    Darwin) ges_os=osx ;;
    Linux) ges_os=linux ;;
    *) echo "Native installation through this script supports macOS and Linux." >&2; exit 2 ;;
esac
case "$(uname -m)" in
    arm64|aarch64) ges_arch=arm64 ;;
    x86_64|amd64) ges_arch=x64 ;;
    *) echo "Unsupported native architecture." >&2; exit 2 ;;
esac
ges_rid="$ges_os-$ges_arch"
# Inspect package ownership before building; never overwrite unrelated commands.
mkdir -p "$ges_destination"
ges_tools=$(dotnet tool list "$@")
ges_packages=$(printf '%s\n' "$ges_tools" | awk 'tolower($1) == "gameeventscript.tool" || tolower($1) == "gameeventscript.tool.aot" || tolower($1) == "steph.gameeventscript.tool" { print $1 }')
check_destination() {
    if [ -e "$ges_marker" ] || [ -L "$ges_marker" ]; then
        check_owned
    elif { [ -e "$ges_binary" ] || [ -L "$ges_binary" ]; } && [ -z "$ges_packages" ]; then
        echo "Refusing to replace an unrelated dotnet-ges at $ges_binary." >&2; exit 1
    fi
    for ges_file in "$ges_license" "$ges_notices"; do
        if [ -L "$ges_file" ] || { [ -e "$ges_file" ] && [ ! -f "$ges_file" ]; }; then
            echo "Invalid native installation metadata: $ges_file" >&2; exit 1
        fi
    done
}
check_destination
mkdir -p "$ges_root/artifacts/csharp/tool"
ges_build=$(mktemp -d "$ges_root/artifacts/csharp/tool/aot-install.XXXXXX")
ges_stage=""
trap 'rm -rf "$ges_build"; if [ -n "$ges_stage" ]; then rm -rf "$ges_stage"; fi' 0
trap 'exit 1' 1 2 15
cd "$ges_root"
dotnet publish implementation/csharp/GameEventScript.Tool/src/GameEventScript.Tool.csproj \
    --configuration Release --runtime "$ges_rid" -p:GesPublishAot=true --output "$ges_build"
"$ges_build/GameEventScript.Tool" --version
mkdir -p "$ges_destination"
ges_stage=$(mktemp -d "$ges_destination/.ges-aot-install.XXXXXX")
cp "$ges_build/GameEventScript.Tool" "$ges_stage/dotnet-ges"
chmod 755 "$ges_stage/dotnet-ges"
hash_file "$ges_stage/dotnet-ges" > "$ges_stage/marker"
cp "$ges_root/LICENSE" "$ges_stage/license"
cp "$ges_root/implementation/csharp/GameEventScript.Tool/THIRD-PARTY-NOTICES.md" "$ges_stage/notices"
check_destination
# Only remove the old installation after publishing and checking its replacement.
for ges_package in $ges_packages; do dotnet tool uninstall "$ges_package" "$@"; done
if [ ! -f "$ges_marker" ] && { [ -e "$ges_binary" ] || [ -L "$ges_binary" ]; }; then
    echo "Existing dotnet-ges was not removed by its package owner." >&2; exit 1
fi
mv -f "$ges_stage/dotnet-ges" "$ges_binary"
mv -f "$ges_stage/marker" "$ges_marker"
mv -f "$ges_stage/license" "$ges_license"
mv -f "$ges_stage/notices" "$ges_notices"
echo "Installed native C# dotnet-ges ($ges_rid) in $ges_destination. Add this directory to PATH."
