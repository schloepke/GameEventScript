#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Build the local prototype, leaving all generated outputs below artifacts/card-game."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
from site_assets import identity, record, version_browser_assets

EXAMPLE = Path(__file__).resolve().parents[1]
ROOT = EXAMPLE.parents[1]
ARTIFACTS = ROOT / 'artifacts/card-game'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=['test', 'wasm'])
    parser.add_argument('--swift', help='Path to a Swift.org swift executable for Wasm builds')
    parser.add_argument('--sdk', default='swift-6.4.0-RELEASE_wasm')
    args = parser.parse_args()
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, CLANG_MODULE_CACHE_PATH=str(ARTIFACTS / 'clang-cache'))
    package = EXAMPLE / 'swift'
    command = ['swift', 'test' if args.mode == 'test' else 'build']
    if args.mode == 'wasm':
        if args.swift:
            command[0] = args.swift
        else:
            installed = list((ARTIFACTS / 'toolchain').glob('**/usr/bin/swift'))
            if len(installed) != 1:
                raise RuntimeError('Run scripts/setup-wasm.py or pass --swift with a Swift.org 6.4 toolchain')
            command[0] = str(installed[0])
        # Temporary compatibility experiment: never alter the main branch's Runtime.
        stage = ARTIFACTS / 'wasm-source'
        for source in [package, ROOT / 'implementation/swift/GameEventScriptRuntime', ROOT / 'implementation/swift/GameEventScriptCompiler', ROOT / 'implementation/swift/GameEventScriptSyntaxHighlighter']:
            target = stage / source.relative_to(ROOT)
            target.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source / 'Package.swift', target / 'Package.swift')
            for subtree in ['Sources', 'Tests']:
                if (source / subtree).exists():
                    if (target / subtree).exists():
                        shutil.rmtree(target / subtree)
                    shutil.copytree(source / subtree, target / subtree)
        for filename in ['Clock.swift', 'VmMath.swift', 'VmNavigation.swift']:
            path = stage / 'implementation/swift/GameEventScriptRuntime/Sources/GameEventScriptRuntime' / filename
            original = path.read_text()
            anchor = '#elseif canImport(Musl)\n    import Musl\n#endif'
            if anchor not in original:
                raise RuntimeError(f'Platform import anchor changed: {filename}')
            path.write_text(original.replace(anchor, '#elseif canImport(Musl)\n    import Musl\n#elseif canImport(WASILibc)\n    import WASILibc\n#endif', 1))
        # WASI's CLOCK_MONOTONIC macro takes an address and is not imported into Swift.
        runtime = stage / 'implementation/swift/GameEventScriptRuntime'
        shim = runtime / 'Sources/CardGameWasiClock'
        (shim / 'include').mkdir(parents=True, exist_ok=True)
        notice = '// Copyright 2026 Stephan Schlöpke\n// SPDX-License-Identifier: Apache-2.0\n'
        (shim / 'include/CardGameWasiClock.h').write_text(notice + '#include <stdint.h>\nint64_t card_game_monotonic_microseconds(void);\n')
        (shim / 'clock.c').write_text(notice + '#include "CardGameWasiClock.h"\n#include <time.h>\nint64_t card_game_monotonic_microseconds(void) { struct timespec t; if (clock_gettime(CLOCK_MONOTONIC, &t) != 0) return -1; return (int64_t)t.tv_sec * 1000000 + t.tv_nsec / 1000; }\n')
        manifest = runtime / 'Package.swift'
        runtime_manifest = manifest.read_text()
        target_anchor = 'targets: [.target(name: "GameEventScriptRuntime")]'
        if target_anchor not in runtime_manifest:
            raise RuntimeError('Runtime manifest changed; review the isolated Wasm patch')
        manifest.write_text(runtime_manifest.replace(target_anchor, 'targets: [.target(name: "CardGameWasiClock"), .target(name: "GameEventScriptRuntime", dependencies: ["CardGameWasiClock"])]'))
        clock = runtime / 'Sources/GameEventScriptRuntime/Clock.swift'
        clock_text = clock.read_text().replace('    import WASILibc', '    import WASILibc\n    import CardGameWasiClock')
        body = '        var value = timespec()\n        clock_gettime(CLOCK_MONOTONIC, &value)\n        return Int64(value.tv_sec) * 1_000_000 + Int64(value.tv_nsec) / 1_000'
        if body not in clock_text:
            raise RuntimeError('Clock body changed; review the isolated Wasm patch')
        clock.write_text(clock_text.replace(body, '        let value = card_game_monotonic_microseconds()\n        precondition(value >= 0, "WASI monotonic clock unavailable")\n        return value'))
        package = stage / package.relative_to(ROOT)
        # Nested declarative setup maps need more compiler stack than WASI's 64 KiB default.
        command += ['--swift-sdks-path', str(ARTIFACTS / 'sdk'), '--swift-sdk', args.sdk,
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
