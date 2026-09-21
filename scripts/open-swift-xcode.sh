#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

if [[ $# -gt 1 || ( $# -eq 1 && "$1" != "--configure-only" ) ]]; then
    echo "Usage: $0 [--configure-only]" >&2
    exit 2
fi

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ges_workspace="$ges_root/implementation/swift/GameEventScript.xcworkspace"
ges_settings="$ges_workspace/xcuserdata/$(id -un).xcuserdatad/WorkspaceSettings.xcsettings"

# Xcode stores Derived Data locations in per-user settings, not shared settings.
# Preserve other workspace preferences; never change global Xcode preferences.
python3 - "$ges_settings" <<'PY'
import plistlib
import sys
from pathlib import Path

path = Path(sys.argv[1])
settings = plistlib.loads(path.read_bytes()) if path.exists() else {}
settings.update({
    "DerivedDataLocationStyle": "WorkspaceRelativePath",
    "DerivedDataCustomLocation": "../../artifacts/swift/xcode",
    "BuildLocationStyle": "CustomLocation",
    "CustomBuildLocationType": "RelativeToWorkspace",
    "CustomBuildProductsPath": "../../artifacts/swift/xcode/Build/Products",
    "CustomBuildIntermediatesPath": "../../artifacts/swift/xcode/Build/Intermediates.noindex",
})
path.parent.mkdir(parents=True, exist_ok=True)
path.write_bytes(plistlib.dumps(settings))
PY

if [[ "${1:-}" != "--configure-only" ]]; then
    open "$ges_workspace"
fi
