#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ges_package="$ges_root/implementation/swift/GameEventScriptConformance"
ges_scratch="$ges_root/artifacts/swift/conformance"

swift test --package-path "$ges_package" --scratch-path "$ges_scratch" --build-system native --configuration release
swift run --package-path "$ges_package" --scratch-path "$ges_scratch" --build-system native --configuration release --skip-build ges-conformance \
    --corpus "$ges_root/conformance/suites" \
    --fixtures "$ges_root/conformance/fixtures/MarkdownV1" \
    --output "$ges_root/artifacts/swift/conformance-results" \
    --allow-incomplete
