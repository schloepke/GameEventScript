#!/usr/bin/env bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

ges_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$ges_root/scripts/sync-editor-bundles.py" --check
mkdir -p "$ges_root/artifacts/editors"
ges_stage="$ges_root/artifacts/editors/vscode-package"
mkdir -p "$ges_stage"
# Keep npm dependencies and intermediate packaging files below artifacts.
python3 - "$ges_root/tools/editors/VS Code" "$ges_stage" <<'PY'
import shutil
import sys
from pathlib import Path
source, target = map(Path, sys.argv[1:])
for item in target.iterdir():
    if item.name == 'node_modules':
        continue
    if item.is_dir():
        shutil.rmtree(item)
    else:
        item.unlink()
for item in source.iterdir():
    if item.suffix == '.vsix':
        continue
    if item.is_dir():
        shutil.copytree(item, target / item.name, dirs_exist_ok=True)
    else:
        shutil.copy2(item, target / item.name)
PY
cd "$ges_stage"
npm ci --cache "$ges_root/artifacts/npm-cache" --omit=dev --ignore-scripts --no-audit --no-fund
npm exec --yes --cache "$ges_root/artifacts/npm-cache" --package @vscode/vsce@4.0.0 -- \
    vsce package --out "$ges_root/artifacts/editors/GameEventScript-VSCode.vsix"
cp "$ges_root/artifacts/editors/GameEventScript-VSCode.vsix" "$ges_root/tools/editors/VS Code/GameEventScript-VSCode.vsix"
