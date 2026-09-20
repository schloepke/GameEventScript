#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
swift test --package-path "$ges_root/implementation/swift/GameEventScriptSwiftBridge" \
    --scratch-path "$ges_root/artifacts/swift/swiftbridge" --build-system native --configuration release
swift test --package-path "$ges_root/implementation/swift/GameEventScriptConformance" \
    --scratch-path "$ges_root/artifacts/swift/conformance" --build-system native --configuration release \
    --filter SwiftBridgeIntegrationTests
