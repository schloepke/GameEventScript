#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

usage() {
    echo "Usage: $0 [--tool-path DIRECTORY]"
    echo "Build the Swift packages, then install or update ges (default: \$HOME/.local/bin)."
}
ges_destination="$HOME/.local/bin"
case "$#" in
    0) ;;
    1) case "$1" in -h|--help) usage; exit 0 ;; *) usage >&2; exit 2 ;; esac ;;
    2) if [[ "$1" != --tool-path || -z "$2" ]]; then usage >&2; exit 2; fi; ges_destination="$2" ;;
    *) usage >&2; exit 2 ;;
esac
case "$ges_destination" in /*) ;; *) ges_destination="$PWD/$ges_destination" ;; esac

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ges_binary="$ges_destination/ges"
ges_marker="$ges_destination/.ges-swift-tool.sha256"
ges_license="$ges_destination/.ges-swift-tool.LICENSE"
hash_file() {
    if command -v shasum >/dev/null 2>&1; then shasum -a 256 "$1" | awk '{print $1}'
    else sha256sum "$1" | awk '{print $1}'; fi
}
check_destination() {
    if [[ -L "$ges_license" || -e "$ges_license" && ! -f "$ges_license" ]]; then
        echo "Invalid Swift license destination: $ges_license" >&2
        exit 1
    fi
    if [[ -e "$ges_binary" || -L "$ges_binary" ]]; then
        if [[ -L "$ges_binary" || ! -f "$ges_binary" || ! -f "$ges_marker" || -L "$ges_marker" || "$(hash_file "$ges_binary")" != "$(cat "$ges_marker")" ]]; then
            echo "Refusing to replace an unrelated or modified ges at $ges_binary. Select another --tool-path or uninstall its owning tool first." >&2
            exit 1
        fi
    fi
    if [[ -e "$ges_marker" && ! -f "$ges_marker" || -L "$ges_marker" ]]; then
        echo "Invalid Swift installation marker: $ges_marker" >&2
        exit 1
    fi
}
check_destination
"$ges_root/scripts/build-swift.sh"
ges_product="$ges_root/artifacts/swift/tool/release/ges"
"$ges_product" --version
mkdir -p "$ges_destination"
ges_temporary=$(mktemp -d "$ges_destination/.ges-install.XXXXXX")
trap 'rm -rf "$ges_temporary"' EXIT
trap 'exit 1' HUP INT TERM
cp "$ges_product" "$ges_temporary/ges"
chmod 755 "$ges_temporary/ges"
hash_file "$ges_temporary/ges" > "$ges_temporary/marker"
cp "$ges_root/LICENSE" "$ges_temporary/license"
# Re-check after building so an installation created meanwhile is not overwritten.
check_destination
mv -f "$ges_temporary/ges" "$ges_binary"
mv -f "$ges_temporary/marker" "$ges_marker"
mv -f "$ges_temporary/license" "$ges_license"
echo "Installed Swift ges in $ges_destination. Add this directory to PATH to use ges."
