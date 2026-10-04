#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Bind the browser build to its sources and stage it into the static website."""
import hashlib
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'scripts'))
from website_assets import copy_assets, is_metadata, validate_static_assets

WEB = ROOT / 'artifacts/card-game/web'
MANIFEST = 'build-info.json'


def identity():
    files = []
    for directory in ['examples/card-game', 'tools/distribution/licenses']:
        files += [p for p in (ROOT / directory).rglob('*') if p.is_file()
                  and not is_metadata(p) and not any(x in p.parts for x in ['__pycache__', '.build', '.swiftpm'])]
    for module in ['GameEventScriptRuntime', 'GameEventScriptCompiler', 'GameEventScriptSyntaxHighlighter']:
        base = ROOT / 'implementation/swift' / module
        files.append(base / 'Package.swift')
        files += [p for p in (base / 'Sources').rglob('*') if p.is_file() and not is_metadata(p)]
    files.append(ROOT / 'LICENSE')
    digest = hashlib.sha256()
    for file in sorted(files):
        digest.update(file.relative_to(ROOT).as_posix().encode() + b'\0' + file.read_bytes() + b'\0')
    return digest.hexdigest()


def contents(directory):
    return {p.relative_to(directory).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(directory.rglob('*')) if p.is_file() and p.name != MANIFEST}


def record():
    validate_static_assets(WEB)
    (WEB / MANIFEST).write_text(json.dumps({'source': identity(), 'files': contents(WEB)}, indent=2) + '\n')


def validate(directory, expected):
    validate_static_assets(directory)
    manifest = directory / MANIFEST
    if not manifest.is_file():
        raise RuntimeError('Missing card-game build; run examples/card-game/scripts/build.py wasm')
    info = json.loads(manifest.read_text())
    if info.get('source') != expected or info.get('files') != contents(directory):
        raise RuntimeError('Stale or modified card-game build; run examples/card-game/scripts/build.py wasm')
    for name in ['index.html', 'card-game.wasm', 'worker.mjs', 'app.mjs', 'examples/mau-mau.ges', 'examples/high-card.ges', 'examples/skat.ges', 'examples.json', 'vendor/index.js', 'licenses/LICENSE']:
        if not (directory / name).is_file():
            raise RuntimeError(f'Incomplete card-game build: {name}')


def stage():
    validate(WEB, identity())
    destination = ROOT / 'artifacts/website/public/examples/card-game'
    if destination.exists():
        shutil.rmtree(destination)
    copy_assets(WEB, destination)
    (destination / MANIFEST).unlink()
    print('Staged verified card-game browser assets.')


if __name__ == '__main__':
    stage()
