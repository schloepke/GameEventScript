#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

usage() {
    echo "Usage: $0 [--tool-path DIRECTORY]"
    echo "Remove this repository's Swift ges installation (default: \$HOME/.local/bin)."
}
ges_destination="$HOME/.local/bin"
case "$#" in
    0) ;;
    1) case "$1" in -h|--help) usage; exit 0 ;; *) usage >&2; exit 2 ;; esac ;;
    2) if [[ "$1" != --tool-path || -z "$2" ]]; then usage >&2; exit 2; fi; ges_destination="$2" ;;
    *) usage >&2; exit 2 ;;
esac
case "$ges_destination" in /*) ;; *) ges_destination="$PWD/$ges_destination" ;; esac

ges_binary="$ges_destination/ges"
ges_marker="$ges_destination/.ges-swift-tool.sha256"
if [[ ! -e "$ges_binary" && ! -L "$ges_binary" ]]; then
    echo "Swift ges is not installed in $ges_destination; nothing to uninstall."
    exit 0
fi
if [[ -L "$ges_binary" || ! -f "$ges_binary" || ! -f "$ges_marker" || -L "$ges_marker" ]]; then
    echo "Refusing to remove an unowned ges at $ges_binary." >&2
    exit 1
fi
if command -v shasum >/dev/null 2>&1; then ges_hash=$(shasum -a 256 "$ges_binary" | awk '{print $1}')
else ges_hash=$(sha256sum "$ges_binary" | awk '{print $1}'); fi
if [[ "$ges_hash" != "$(cat "$ges_marker")" ]]; then
    echo "Refusing to remove a modified ges at $ges_binary." >&2
    exit 1
fi
rm -- "$ges_binary" "$ges_marker"
rm -f -- "$ges_destination/.ges-swift-tool.LICENSE"
echo "Uninstalled Swift ges from $ges_destination."
