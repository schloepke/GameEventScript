#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

usage() {
    echo "Usage: $0 [--configuration release|debug]"
    echo "Build each SwiftPM package independently; default configuration: release."
}

ges_configuration=release
case "$#" in
    0) ;;
    1) case "$1" in -h|--help) usage; exit 0 ;; *) usage >&2; exit 2 ;; esac ;;
    2)
        if [[ "$1" != "--configuration" || ( "$2" != release && "$2" != debug ) ]]; then
            usage >&2
            exit 2
        fi
        ges_configuration="$2"
        ;;
    *) usage >&2; exit 2 ;;
esac

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Replan local package sources on every invocation. Native SwiftPM can otherwise
# miss added/renamed files in sibling dependencies; compiled objects remain cached.
ges_count=0
for ges_manifest in "$ges_root"/implementation/swift/*/Package.swift; do
    [[ -f "$ges_manifest" ]] || continue
    ges_package="${ges_manifest%/Package.swift}"
    ges_name="${ges_package##*/}"
    ges_component="$(printf '%s' "${ges_name#GameEventScript}" | tr '[:upper:]' '[:lower:]')"
    echo "Building $ges_name ($ges_configuration)"
    swift build --package-path "$ges_package" \
        --scratch-path "$ges_root/artifacts/swift/$ges_component" \
        --build-system native --disable-build-manifest-caching --configuration "$ges_configuration"
    ges_count=$((ges_count + 1))
done
if [[ "$ges_count" -eq 0 ]]; then
    echo "No SwiftPM packages found." >&2
    exit 1
fi
