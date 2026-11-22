#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ges_package="$ges_root/implementation/swift/GameEventScriptConformance"
ges_scratch="$ges_root/artifacts/swift/conformance"

# Without an argument both parts run. Swift CI runs them as two parallel jobs:
# - packages: the Debug package and CLI tests and the cross-port checks that need only the two CLIs;
# - conformance: the Release Conformance adapter, the shared corpus and the cross-port checks that need the adapters.
# Unit-test packages build in Debug, which compiles several times faster and checks overflow and exclusivity at run time;
# the Conformance package, whose adapter runs the shared corpus and the cross-port checks, stays in Release.
ges_part="${1:-all}"
case "$ges_part" in
    all | packages | conformance) ;;
    *)
        echo "usage: $0 [all|packages|conformance]" >&2
        exit 2
        ;;
esac

build_csharp_tool() {
    dotnet build "$ges_root/implementation/csharp/GameEventScript.Tool/src/GameEventScript.Tool.csproj" --configuration Release
}

run_packages() {
    swift test --package-path "$ges_root/implementation/swift/GameEventScriptSyntax" \
        --scratch-path "$ges_root/artifacts/swift/syntax" --disable-build-manifest-caching --configuration debug

    swift test --package-path "$ges_root/implementation/swift/GameEventScriptSwiftBridge" \
        --scratch-path "$ges_root/artifacts/swift/swiftbridge" --disable-build-manifest-caching --configuration debug

    swift test --package-path "$ges_root/implementation/swift/GameEventScriptGenerator" \
        --scratch-path "$ges_root/artifacts/swift/generator" --disable-build-manifest-caching --configuration debug

    swift test --package-path "$ges_root/implementation/swift/GameEventScriptNetwork" \
        --scratch-path "$ges_root/artifacts/swift/network" --disable-build-manifest-caching --configuration debug

    swift test --package-path "$ges_root/implementation/swift/GameEventScriptNetworkSockets" \
        --scratch-path "$ges_root/artifacts/swift/networksockets" --disable-build-manifest-caching --configuration debug

    "$ges_root/scripts/test-swift-tool.sh"
    python3 "$ges_root/scripts/verify-swift-bytecode.py"
    python3 "$ges_root/scripts/verify-swift-parser-tokens.py"
    python3 "$ges_root/scripts/test-swift-parser-tokens.py"
    python3 "$ges_root/scripts/verify-swift-portable-imports.py"
    python3 "$ges_root/scripts/test-swift-portable-imports.py"

    build_csharp_tool
    python3 "$ges_root/scripts/test-cli-bench.py" --swift "$ges_root/artifacts/swift/tool/debug/ges" \
        --csharp "$ges_root/artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
    python3 "$ges_root/scripts/test-compiler-output-controls.py"
    python3 "$ges_root/scripts/compare-compiler-output.py" --mutants 1500
    python3 "$ges_root/scripts/compare-schema-generation.py" --self-test
    python3 "$ges_root/scripts/compare-schema-generation.py"
    python3 "$ges_root/scripts/test-generated-schemas.py" --self-test
    python3 "$ges_root/scripts/test-generated-schemas.py"
    python3 "$ges_root/scripts/test-computed-fields.py" --self-test
    python3 "$ges_root/scripts/test-computed-fields.py"
    python3 "$ges_root/scripts/test-network-deflate.py" --self-test
    python3 "$ges_root/scripts/test-network-deflate.py"
    python3 "$ges_root/scripts/test-network-crypto.py" --self-test
    python3 "$ges_root/scripts/test-network-crypto.py"
}

run_conformance() {
    python3 "$ges_root/scripts/prepare-runtime-fixtures.py" "$ges_root/conformance/suites" "$ges_root/artifacts/swift/runtime-fixtures/requests.json"
    dotnet run --project "$ges_root/implementation/csharp/GameEventScript.Conformance/fixture-exporter" \
        --configuration Release --artifacts-path "$ges_root/artifacts/swift/csharp-exporter" -- \
        "$ges_root/artifacts/swift/runtime-fixtures/requests.json" "$ges_root/artifacts/swift/runtime-fixtures"

    swift test --package-path "$ges_package" --scratch-path "$ges_scratch" --disable-build-manifest-caching --configuration release
    python3 "$ges_root/conformance/runner/main.py" run --kind supported \
        --output "$ges_root/artifacts/swift/conformance-results/ConformanceResults.json" \
        --adapter "$ges_scratch/release/ges-conformance-adapter"
    python3 "$ges_root/scripts/test-performance-correctness.py" \
        --profile swift-6.4-release-macos27-arm64-m3max \
        --output "$ges_root/artifacts/swift/conformance-results/PerformanceCorrectness.json" \
        --adapter "$ges_scratch/release/ges-conformance-adapter"
    python3 "$ges_root/scripts/test-swift-runtime-controls.py"
    python3 "$ges_root/scripts/test-swift-runtime.py"
    python3 "$ges_root/implementation/swift/GameEventScriptConformance/adapter/tests/test_adapter.py" --adapter "$ges_scratch/release/ges-conformance-adapter"

    python3 "$ges_root/scripts/test-number-text-roundtrip.py" --skip-build

    python3 "$ges_root/scripts/test-message-json-roundtrip.py" --skip-build

    # Builds the C# Conformance adapter; the Swift adapter is already current, and building it again without testing
    # support would recompile the whole package.
    python3 "$ges_root/scripts/test-document-roundtrip.py" --skip-swift-build
    python3 "$ges_root/scripts/test-syntax-roundtrip.py" --skip-build --trees csharp --trees swift --mutants 3000

    python3 "$ges_root/scripts/test-binary-reader-fuzz-controls.py"
    python3 "$ges_root/scripts/test-binary-reader-fuzz.py" --skip-build

    python3 "$ges_root/scripts/test-binary-roundtrip-controls.py"
    python3 "$ges_root/scripts/test-binary-roundtrip-properties.py" --skip-build

    # The schema roundtrip's generated parties and the constructor forms run both CLIs; the packages part tests the
    # Swift CLI, which this part only builds.
    swift build --package-path "$ges_root/implementation/swift/GameEventScriptTool" \
        --scratch-path "$ges_root/artifacts/swift/tool" --disable-build-manifest-caching --configuration debug --product ges
    build_csharp_tool
    # Needs the C# Conformance adapter built by the document roundtrip.
    python3 "$ges_root/scripts/test-schema-roundtrip.py" --skip-build
    python3 "$ges_root/scripts/test-constructor-forms.py"
}

if [ "$ges_part" != conformance ]; then
    run_packages
fi
if [ "$ges_part" != packages ]; then
    run_conformance
fi
