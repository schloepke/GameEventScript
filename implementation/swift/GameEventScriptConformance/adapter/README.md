<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Internal Swift Conformance adapter

The independent [Python runner](../../../../conformance/runner/README.md) parses
Markdown and owns every portable assertion. This executable implements the
[observation protocol](../../../../specs/Conformance/PythonRunner.md) through the
real Swift Compiler and Runtime APIs. It receives source/input requests on stdin
and returns one JSON observation batch on stdout. It never receives expectations,
parses Markdown or decides whether a case passes.

Build and run from the repository root:

```bash
swift build --package-path implementation/swift/GameEventScriptConformance \
  --scratch-path artifacts/swift/conformance --disable-build-manifest-caching -c release
python3 conformance/runner/main.py run --kind supported \
  --adapter artifacts/swift/conformance/release/ges-conformance-adapter
python3 implementation/swift/GameEventScriptConformance/adapter/tests/test_adapter.py
```

`--describe` advertises capabilities, supported kinds and available measurement
profiles. `--measure` accepts directives without budgets and returns native
measurements plus instrumentation evidence; Python checks correctness before
measurement and evaluates the profile bounds. Use `scripts/test-swift-performance.sh`.

`--programs <file>` is the repository-only interoperability input. It replaces
native compilation with supplied binary Programs and never falls back to the
Swift compiler. `scripts/test-swift-runtime.py` verifies the C# export manifest,
source-document hashes and binary hashes before creating this side input.
Runtime reports identify the C# input compiler separately.

Sources live under `../Sources/GameEventScriptConformanceAdapter`. These executables
and instrumentation are not shipped as libraries or included in the public API
snapshot. The separate `ges-conformance-probe` executable retains binary-fuzz,
numeric-text and product-JSON exchange entry points. Native register/compiler/
Bridge tests remain in the SwiftPM test target; transport controls live here.
