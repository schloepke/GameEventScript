#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ges_mode="${1:---verify}"
case "$ges_mode" in
    --verify) ges_flag=--performance; ges_default=performance ;;
    --calibrate) ges_flag=--calibrate-performance; ges_default=performance-calibration ;;
    *) echo "Usage: $0 [--verify|--calibrate] [output-directory]" >&2; exit 2 ;;
esac
ges_output="${2:-$ges_root/artifacts/swift/$ges_default}"
ges_package="$ges_root/implementation/swift/GameEventScriptConformance"
ges_scratch="$ges_root/artifacts/swift/conformance"
swift build --package-path "$ges_package" --scratch-path "$ges_scratch" --build-system native --disable-build-manifest-caching --configuration release
"$ges_scratch/release/ges-conformance" --corpus "$ges_root/conformance/suites/performance" --output "$ges_output" "$ges_flag"
