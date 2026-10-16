#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ges_mode="${1:---verify}"
case "$ges_mode" in
    --verify) ges_default=performance ;;
    --calibrate) ges_default=performance-calibration ;;
    *) echo "Usage: $0 [--verify|--calibrate] [output-directory]" >&2; exit 2 ;;
esac
ges_output="${2:-$ges_root/artifacts/swift/$ges_default}"
ges_package="$ges_root/implementation/swift/GameEventScriptConformance"
ges_scratch="$ges_root/artifacts/swift/conformance"
swift build --package-path "$ges_package" --scratch-path "$ges_scratch" --disable-build-manifest-caching --configuration release
if [[ "$ges_mode" == --calibrate ]]; then
    python3 "$ges_root/scripts/calibrate-swift-performance.py" "$ges_output" --adapter "$ges_scratch/release/ges-conformance-adapter"
else
    python3 "$ges_root/conformance/runner/main.py" run --corpus "$ges_root/conformance/suites/performance" \
        --performance-profile swift-6.4-release-macos27-arm64-m3max \
        --output "$ges_output/ConformanceResults.json" --adapter "$ges_scratch/release/ges-conformance-adapter"
fi
