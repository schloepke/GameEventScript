#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Build public C# (DocFX) or Swift (DocC) reference sites, never publish them."""

import argparse
import hashlib
import html
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
ARTIFACTS = ROOT / 'artifacts/website'
CSHARP = ['GameEventScript.Runtime', 'GameEventScript.Compiler', 'GameEventScript.CSharpBridge', 'GameEventScript.SyntaxHighlighter']
SWIFT = ['GameEventScriptRuntime', 'GameEventScriptCompiler', 'GameEventScriptSwiftBridge', 'GameEventScriptSyntaxHighlighter']
DOCFX_VERSION = '2.78.5'


def run(args, **kwargs):
    subprocess.run([str(a) for a in args], cwd=ROOT, check=True, **kwargs)


def identity():
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    paths = subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard',
                                     'implementation', 'Package.swift', 'Directory.Build.props',
                                     'scripts/build-api-docs.py', 'website/api',
                                     'website/brand/pixel-duo/mark.svg'], cwd=ROOT).split(b'\0')
    digest = hashlib.sha256()
    for entry in sorted(set(paths) - {b''}):
        path = ROOT / os.fsdecode(entry)
        if path.is_file():
            digest.update(entry + b'\0' + path.read_bytes() + b'\0')
    return {'revision': revision, 'sourceHash': digest.hexdigest(), 'label': 'Unreleased'}


def reset(path):
    # Only script-owned generated directories below artifacts/website.
    path.relative_to(ARTIFACTS)
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)


def csharp(output, version):
    work = ARTIFACTS / 'api-build/csharp'
    reset(work)
    tools = ARTIFACTS / 'tools'
    docfx = tools / ('docfx.exe' if os.name == 'nt' else 'docfx')
    if not docfx.exists():
        run(['dotnet', 'tool', 'install', 'docfx', '--version', DOCFX_VERSION,
             '--tool-path', tools, '--allow-roll-forward'])
    actual = subprocess.check_output([str(docfx), '--version'], text=True).strip()
    if actual.split('+', 1)[0] != DOCFX_VERSION:
        raise RuntimeError(f'DocFX {DOCFX_VERSION} required, found {actual}')
    inputs = work / 'assemblies'
    inputs.mkdir()
    for module in CSHARP:
        project = ROOT / 'implementation/csharp' / module / 'src' / (module + '.csproj')
        run(['dotnet', 'build', project, '--configuration', 'Release', '--artifacts-path', work / 'build'])
        dlls = list((work / 'build/bin' / module).rglob(module + '.dll'))
        if len(dlls) != 1:
            raise RuntimeError(f'Expected one public assembly for {module}, found {dlls}')
        for extension in ['.dll', '.xml']:
            shutil.copy2(dlls[0].with_suffix(extension), inputs)
    title = f"Game Event Script C# API — {version['label']} ({version['revision'][:12]})"
    (work / 'index.md').write_text(f'# {title}\n\nGenerated from public assemblies and XML comments.\n\n'
                                 '[Documentation and version information](/api/csharp/)\n\n'
                                 '[Browse namespaces](xref:GameEventScript.Api)\n')
    (work / 'toc.yml').write_text('- name: Documentation\n  href: /api/csharp/\n'
                                '- name: API reference\n  href: api/toc.yml\n')
    assets = work / 'assets'
    assets.mkdir()
    shutil.copy2(ROOT / 'website/brand/pixel-duo/mark.svg', assets / 'logo.svg')
    config = {
        'metadata': [{'src': [{'files': [m + '.dll' for m in CSHARP], 'src': 'assemblies'}],
                      'dest': 'api', 'disableGitFeatures': True}],
        'build': {'content': [{'files': ['api/**.yml', 'index.md', 'toc.yml']}],
                  'resource': [{'files': ['assets/**']}],
                  'dest': str(output / 'reference'), 'template': ['default', 'modern'],
                  'globalMetadata': {'_appTitle': title, '_appName': 'Game Event Script',
                                     '_appFooter': html.escape(title), '_enableSearch': True,
                                     '_disableContribution': True,
                                     '_appLogoPath': 'assets/logo.svg', '_appFaviconPath': 'assets/logo.svg'}}
    }
    config_path = work / 'docfx.json'
    config_path.write_text(json.dumps(config, indent=2) + '\n')
    run([docfx, 'metadata', config_path])
    # The renderer links namespace segments individually. The assemblies have
    # members only in child namespaces, so give their implicit parent a page.
    namespaces = []
    for path in (work / 'api').glob('*.yml'):
        source = path.read_text()
        if '\n  type: Namespace\n' in source:
            namespaces.append(re.search(r'^- uid: ([\w.]+)$', source, re.MULTILINE).group(1))
    for namespace in namespaces:
        parts = namespace.split('.')
        for count in range(1, len(parts)):
            parent = '.'.join(parts[:count])
            path = work / 'api' / (parent + '.yml')
            if not path.exists():
                path.write_text('### YamlMime:ManagedReference\nitems:\n'
                                f'- uid: {parent}\n  id: {parent}\n  name: {parent}\n  fullName: {parent}\n'
                                '  type: Namespace\n  langs: [csharp]\n'
                                '  summary: Public APIs for the Game Event Script libraries.\n')
    run([docfx, 'build', config_path, '--warningsAsErrors'])
    # The pinned generator and bundled presentation resources are MIT licensed.
    licenses = list((tools / '.store/docfx' / DOCFX_VERSION).rglob('THIRD-PARTY-NOTICES.TXT'))
    if not licenses:
        raise RuntimeError('DocFX distribution license was not found')
    notices = output / 'reference/third-party-licenses'
    notices.mkdir()
    for index, license_path in enumerate(p for p in licenses if p.is_file()):
        shutil.copy2(license_path, notices / f'{index}-{license_path.name}')
    shutil.copy2(ROOT / 'website/api/licenses/DocFX.txt', notices)


def swift(output, version):
    work = ARTIFACTS / 'api-build/swift'
    work.mkdir(parents=True, exist_ok=True)
    scratch = work / 'build'
    for path in scratch.rglob('*.symbols.json'):
        path.unlink()
    options = ['--package-path', ROOT, '--scratch-path', scratch, '--build-system', 'native',
               '--disable-build-manifest-caching']
    # SwiftPM also extracts synthesized test-runner modules when tests exist in
    # the root package. Build them first; only public library graphs are copied.
    run(['swift', 'build', *options, '--build-tests'])
    run(['swift', 'package', *options, 'dump-symbol-graph', '--skip-synthesized-members',
         '--minimum-access-level', 'public', '--emit-extension-block-symbols'])
    docc = shutil.which('docc') or subprocess.check_output(['xcrun', '--find', 'docc'], text=True).strip()
    for module in SWIFT:
        graphs = work / 'graphs' / module
        reset(graphs)
        for path in scratch.rglob('*.symbols.json'):
            if path.name == module + '.symbols.json' or path.name.startswith(module + '@'):
                shutil.copy2(path, graphs)
        if not (graphs / (module + '.symbols.json')).is_file():
            raise RuntimeError(f'Missing public symbol graph for {module}')
        catalog = work / (module + '.docc')
        reset(catalog)
        (catalog / (module + '.md')).write_text(
            f'# ``{module}``\n\nGame Event Script {version["label"]} API, source {version["revision"][:12]}.\n\n'
            '## Overview\n\nThis reference is generated from the public Swift documentation comments. '
            'It describes the development checkout, not necessarily a published package.\n\n'
            '[Guides and version information](https://gameeventscript.org/api/swift/)\n')
        (catalog / 'header.html').write_text(
            '<div style="padding:10px 20px;background:#142641;color:#dce6ff;font:14px system-ui">'
            '<a href="/api/swift/" style="color:#a9c3ff">Game Event Script · Swift API</a>'
            f' — {html.escape(version["label"])} · {version["revision"][:12]}</div>\n')
        run([docc, 'convert', catalog, '--additional-symbol-graph-dir', graphs,
             '--output-path', output / module.lower(), '--fallback-bundle-identifier', 'org.gameeventscript.' + module,
             '--hosting-base-path', '/api/swift/' + module.lower(), '--transform-for-static-hosting',
             '--experimental-enable-custom-templates'])
    notices = output / 'third-party-licenses'
    notices.mkdir()
    for path in (ROOT / 'website/api/licenses').glob('Swift-DocC-Render*'):
        shutil.copy2(path, notices)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--language', choices=['csharp', 'swift'], required=True)
    args = parser.parse_args()
    version = identity()
    output = ARTIFACTS / 'api' / args.language
    reset(output)
    {'csharp': csharp, 'swift': swift}[args.language](output, version)
    (output / 'build-info.json').write_text(json.dumps(version, indent=2) + '\n')
    print(f'Built {args.language} API documentation below {output}')


if __name__ == '__main__':
    main()
