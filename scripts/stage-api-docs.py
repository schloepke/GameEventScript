#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Validate matching native API builds before including them in the static site."""

import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.dont_write_bytecode = True
from website_assets import copy_assets

spec = importlib.util.spec_from_file_location('build_api_docs', ROOT / 'scripts/build-api-docs.py')
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)

SWIFT_EXTENSION_PAGES = [
    'gameeventscriptswiftbridge/documentation/gameeventscriptswiftbridge/gameeventscriptruntime/' + member + '/index.html'
    for member in [
        'gameeventscripthost/subscribe(_:matchingtags:withouttags:priority:handler:)',
        'gameeventscriptmessage/init(name:swiftarguments:tags:)',
        'gameeventscriptcontext/publish(_:swiftarguments:tags:)',
    ]
]


def validate(language, expected):
    source = build.ARTIFACTS / 'api' / language
    manifest = source / 'build-info.json'
    if not manifest.is_file() or json.loads(manifest.read_text()) != expected:
        raise RuntimeError(f'Missing or stale {language} API reference. Run python3 scripts/build-api-docs.py --language {language}')
    required = ['reference/index.html', *[
        f'reference/api/{symbol}.html' for symbol in [
            'GameEventScript.Api.GameEventScriptHost', 'GameEventScript.Api.GameEventScriptBuilder',
            'GameEventScript.CSharpBridge.GameEventScriptCSharpHostRunner',
            'GameEventScript.SyntaxHighlighter.GameEventScriptSyntaxHighlighter']]] if language == 'csharp' else [
        f'{module.lower()}/documentation/{module.lower()}/index.html' for module in build.SWIFT] + SWIFT_EXTENSION_PAGES
    for relative in required:
        if not (source / relative).is_file():
            raise RuntimeError(f'Incomplete API reference: {source / relative}')
    return source


def main():
    expected = build.identity()
    sources = [(language, validate(language, expected)) for language in ['csharp', 'swift']]
    destination = build.ARTIFACTS / 'public/api'
    if destination.exists():
        shutil.rmtree(destination)
    for language, source in sources:
        copy_assets(source, destination / language)
    print('Staged matching C# and Swift API references.')


if __name__ == '__main__':
    main()
