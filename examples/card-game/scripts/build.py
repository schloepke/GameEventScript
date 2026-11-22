#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Build the local prototype, leaving all generated outputs below artifacts/card-game."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys
from site_assets import identity, record, version_browser_assets

EXAMPLE = Path(__file__).resolve().parents[1]
ROOT = EXAMPLE.parents[1]
ARTIFACTS = ROOT / 'artifacts/card-game'
PINNED_SDK = 'swift-6.4.0-RELEASE_wasm'
sys.path.insert(0, str(ROOT / 'scripts'))
from swift_toolchains import installed_sdk  # noqa: E402


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=['test', 'wasm'])
    parser.add_argument('--swift', help='Path to a Swift.org swift executable for Wasm builds')
    parser.add_argument('--sdk', help='Swift SDK ID; defaults to the pinned or the installed Wasm SDK')
    args = parser.parse_args()
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, CLANG_MODULE_CACHE_PATH=str(ARTIFACTS / 'clang-cache'))
    package = EXAMPLE / 'swift'
    command = ['swift', 'test' if args.mode == 'test' else 'build']
    if args.mode == 'wasm':
        # The pinned tools of setup-wasm.py --download come first, then an installed Swift.org toolchain with its
        # matching Wasm SDK. An explicit --swift uses the pinned SDK directory if present, else the installed SDKs.
        pinned = list((ARTIFACTS / 'toolchain').glob('**/usr/bin/swift'))
        system = installed_sdk('wasm')
        sdks_path, sdk = None, args.sdk
        if args.swift or len(pinned) == 1:
            command[0] = args.swift or str(pinned[0])
            if (ARTIFACTS / 'sdk').is_dir():
                sdks_path, sdk = ARTIFACTS / 'sdk', sdk or PINNED_SDK
            elif system:
                sdk = sdk or system.wasm_id
        elif system:
            command[0] = str(system.swift)
            sdk = sdk or system.wasm_id
        else:
            raise RuntimeError('Install a Swift.org toolchain with its matching Wasm SDK, run scripts/setup-wasm.py --download, '
                               'or pass --swift with a Swift.org toolchain')
        if not sdk:
            raise RuntimeError('No Wasm SDK found; pass --sdk')
        print(f'Wasm toolchain: {command[0]}, SDK {sdk}', flush=True)
        # Nested declarative setup maps need more compiler stack than WASI's 64 KiB default.
        if sdks_path:
            command += ['--swift-sdks-path', str(sdks_path)]
        command += ['--swift-sdk', sdk,
                    '--product', 'card-game-wasm', '-c', 'release',
                    '-Xswiftc', '-Xclang-linker', '-Xswiftc', '-mexec-model=reactor',
                    '-Xlinker', '-z', '-Xlinker', 'stack-size=1048576']
        for symbol in ['cardgame_alloc', 'cardgame_start', 'cardgame_action', 'cardgame_output', 'cardgame_highlight', 'cardgame_check', 'cardgame_dump']:
            command += ['-Xlinker', '--export=' + symbol]
    command += ['--package-path', str(package), '--scratch-path', str(ARTIFACTS / ('wasm-build' if args.mode == 'wasm' else 'native')),
                '--cache-path', str(ARTIFACTS / 'cache'), '--disable-sandbox', '--disable-build-manifest-caching']
    print(' '.join(command), flush=True)
    subprocess.run(command, env=env, check=True)
    if args.mode == 'wasm':
        candidates = [path for path in (ARTIFACTS / 'wasm-build').glob('**/card-game-wasm.wasm') if path.is_file()]
        if len(candidates) != 1:
            raise RuntimeError(f'Expected one Wasm product, found {candidates}')
        web = ARTIFACTS / 'web'
        if web.exists():
            shutil.rmtree(web)
        web.mkdir()
        shutil.copy2(candidates[0], web / 'card-game.wasm')
        for file in (EXAMPLE / 'web').iterdir():
            if file.is_file() and file.suffix in {".mjs", ".html", ".css", ".json"}:
                shutil.copy2(file, web / file.name)
        (web / 'examples').mkdir()
        for rules in (EXAMPLE / 'games').glob('*/*.ges'):
            shutil.copy2(rules, web / 'examples' / rules.name)
        vendor = ARTIFACTS / 'web-deps/node_modules/@bjorn3/browser_wasi_shim'
        if not (vendor / 'dist/index.js').exists():
            raise RuntimeError('Run scripts/setup-wasm.py to install the browser WASI adapter')
        shutil.copytree(vendor / 'dist', web / 'vendor', dirs_exist_ok=True)
        for license in ['LICENSE-APACHE', 'LICENSE-MIT']:
            shutil.copy2(vendor / license, web / 'vendor' / license)
        editor = ARTIFACTS / 'web-deps/node_modules/codemirror'
        for relative in ['lib/codemirror.js', 'lib/codemirror.css', 'addon/fold/foldcode.js', 'addon/fold/foldgutter.js', 'addon/fold/foldgutter.css', 'LICENSE']:
            destination = web / 'vendor/codemirror' / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(editor / relative, destination)
        licenses = web / 'licenses'
        shutil.copytree(ROOT / 'tools/distribution/licenses', licenses)
        shutil.copy2(ROOT / 'LICENSE', licenses / 'LICENSE')
        shutil.copytree(EXAMPLE / 'licenses/wasi-libc', licenses / 'wasi-libc')
        (licenses / 'THIRD-PARTY-NOTICES.txt').write_text(
            'Game Event Script card lab: Apache-2.0 (LICENSE).\n'
            'Swift 6.4.0 and its bundled libraries: see the license texts and sources.json here.\n'
            'wasi-libc: see wasi-libc/LICENSE and the accompanying component notices.\n'
            'browser_wasi_shim 0.4.2: MIT OR Apache-2.0; see ../vendor/LICENSE-MIT and LICENSE-APACHE.\n'
            'CodeMirror 5.65.20: MIT; see ../vendor/codemirror/LICENSE.\n'
            'This directory retains the Swift distribution notice set; not every component is linked by this demo.\n')
        version_browser_assets(web, identity())
        record()
        print(f'Browser assets: {web}')


if __name__ == '__main__':
    main()
