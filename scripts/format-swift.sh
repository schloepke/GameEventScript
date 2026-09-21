#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

usage() {
    echo "Usage: $0 [--fix]"
    echo "Check Swift formatting without changing files; --fix applies formatting."
}

ges_fix=false
case "$#" in
    0) ;;
    1)
        case "$1" in
            --fix) ges_fix=true ;;
            -h|--help) usage; exit 0 ;;
            *) usage >&2; exit 2 ;;
        esac
        ;;
    *) usage >&2; exit 2 ;;
esac

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ges_paths=()
for ges_manifest in "$ges_root"/implementation/swift/*/Package.swift; do
    [[ -f "$ges_manifest" ]] || continue
    ges_paths+=("$ges_manifest")
    for ges_directory in "${ges_manifest%/Package.swift}/Sources" "${ges_manifest%/Package.swift}/Tests"; do
        [[ ! -d "$ges_directory" ]] || ges_paths+=("$ges_directory")
    done
done
if [[ "${#ges_paths[@]}" -eq 0 ]]; then
    echo "No SwiftPM sources found." >&2
    exit 1
fi

ges_paths+=("$ges_root/scripts/SwiftDeclarationSpacing.swift")

if [[ "$ges_fix" == true ]]; then
    swift format format --in-place --recursive \
        --configuration "$ges_root/implementation/swift/.swift-format" "${ges_paths[@]}"
    python3 "$ges_root/scripts/format-swift-spacing.py" --fix "${ges_paths[@]}"
    swift format format --in-place --recursive \
        --configuration "$ges_root/implementation/swift/.swift-format" "${ges_paths[@]}"
else
    swift format lint --strict --recursive \
        --configuration "$ges_root/implementation/swift/.swift-format" "${ges_paths[@]}"
    python3 "$ges_root/scripts/format-swift-spacing.py" "${ges_paths[@]}"
fi
