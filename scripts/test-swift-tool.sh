#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$ges_root/scripts/sync-highlighter-grammars.py" --check
swift test --package-path "$ges_root/implementation/swift/GameEventScriptTool" \
    --scratch-path "$ges_root/artifacts/swift/tool" --build-system native --disable-build-manifest-caching --configuration release
python3 "$ges_root/scripts/test-swift-tool.py" "$ges_root/artifacts/swift/tool/release/ges"
python3 "$ges_root/scripts/test-cli-delayed.py" "$ges_root/artifacts/swift/tool/release/ges"
