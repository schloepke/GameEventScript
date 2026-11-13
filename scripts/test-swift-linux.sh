#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

# Verifies the Swift port on Linux without .NET: the unit tests of the packages that need no C# inputs, the shared
# corpus through the native Swift compiler and Runtime, and the CLI process and terminal checks. The macOS job runs the
# cross-port comparisons, which need the C# tools.
set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build=(--disable-build-manifest-caching)

swift test --package-path "$ges_root/implementation/swift/GameEventScriptSyntax" \
    --scratch-path "$ges_root/artifacts/swift/linux/syntax" "${build[@]}" --configuration debug
swift test --package-path "$ges_root/implementation/swift/GameEventScriptGenerator" \
    --scratch-path "$ges_root/artifacts/swift/linux/generator" "${build[@]}" --configuration debug
swift test --package-path "$ges_root/implementation/swift/GameEventScriptSwiftBridge" \
    --scratch-path "$ges_root/artifacts/swift/linux/swiftbridge" "${build[@]}" --configuration debug

swift build --package-path "$ges_root/implementation/swift/GameEventScriptConformance" \
    --scratch-path "$ges_root/artifacts/swift/linux/conformance" "${build[@]}" --configuration release --product ges-conformance-adapter
python3 "$ges_root/conformance/runner/main.py" run --kind supported \
    --output "$ges_root/artifacts/swift/linux/ConformanceResults.json" \
    --adapter "$ges_root/artifacts/swift/linux/conformance/release/ges-conformance-adapter"

swift test --package-path "$ges_root/implementation/swift/GameEventScriptTool" \
    --scratch-path "$ges_root/artifacts/swift/linux/tool" "${build[@]}" --configuration debug
python3 "$ges_root/scripts/test-swift-tool.py" "$ges_root/artifacts/swift/linux/tool/debug/ges"
python3 "$ges_root/scripts/test-cli-delayed.py" "$ges_root/artifacts/swift/linux/tool/debug/ges"
python3 "$ges_root/scripts/test-cli-editor.py" "$ges_root/artifacts/swift/linux/tool/debug/ges"
python3 "$ges_root/scripts/test-cli-bench.py" --swift "$ges_root/artifacts/swift/linux/tool/debug/ges"
