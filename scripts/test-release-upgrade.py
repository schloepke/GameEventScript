#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Isolated 0.3.0 consumer upgrade smoke tests; never publishes packages or tags. Each version builds the consumer
source committed with it, so breaking pre-1.0 API changes are checked as the documented migration."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from verification_workspace import verification_workspace

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '0.3.0'
BASELINE_COMMIT = '4ed9d44ded085ddccebff943365fb924bd0a3ce5'
CANDIDATE = '0.4.0-upgrade-test'
MODULES = ('Runtime', 'Compiler', 'SwiftBridge', 'SyntaxHighlighter')


def run(args, cwd, log, timeout=1800):
    with log.open('a') as output:
        output.write('COMMAND ' + repr(args) + '\n')
        output.flush()
        result = subprocess.run(args, cwd=cwd, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
    if result.returncode:
        raise RuntimeError(f'Command failed; see {log}\n{log.read_text()[-6000:]}')


def snapshot(revision, destination, workspace):
    destination.mkdir(exist_ok=True)
    archive = workspace / 'source.tar'
    subprocess.run(['git', 'archive', '--format=tar', '-o', str(archive), revision], cwd=ROOT, check=True)
    subprocess.run(['tar', '-xf', str(archive), '-C', str(destination)], check=True)
    archive.unlink()


CSHARP_CONSUMERS = ('PackageConsumer', 'RuntimeConsumer')
SWIFT_CONSUMERS = ('SwiftPMConsumer', 'RuntimeConsumer')


def consumer_path(language, name):
    if language == 'csharp':
        return f'implementation/csharp/verification/GameEventScript.{name}/Program.cs'
    return f'implementation/swift/verification/{name}/main.swift'


def consumer_source(revision, language, name):
    """The consumer source as committed in `revision`. Each version builds the consumer written for it, so the gate
    checks the documented migration across breaking pre-1.0 API changes instead of assuming source compatibility."""
    return subprocess.check_output(['git', 'show', f'{revision}:{consumer_path(language, name)}'], cwd=ROOT)


def require_versions(actual, expected, names):
    for name in names:
        if actual.get(name) != expected:
            raise AssertionError(f'{name}: expected resolved {expected}, got {actual.get(name)}')


def require_fixture(language, data):
    expected = (ROOT / 'conformance/fixtures/Release030' / (language + '-consumer.gesb')).read_bytes()
    if data != expected:
        raise AssertionError(f'{language}: baseline compiler output differs from the frozen release fixture')


def csharp(workspace, candidate):
    source = workspace / 'candidate-source'
    snapshot(candidate, source, workspace)
    feed = workspace / 'feed'
    run([str(source / 'scripts/pack-csharp.sh'), CANDIDATE, str(feed)], source, workspace / 'pack.log')
    # Isolate consumer MSBuild settings from this repository's artifact paths.
    (workspace / 'Directory.Build.props').write_text('<Project />\n')
    (workspace / 'Directory.Build.targets').write_text('<Project />\n')
    paths = {}
    for name in CSHARP_CONSUMERS:
        directory = workspace / name
        directory.mkdir()
        project = 'GameEventScript.' + name + '.csproj'
        shutil.copy2(ROOT / 'implementation/csharp/verification' / ('GameEventScript.' + name) / project, directory / project)
        paths[name] = directory
    baseline = workspace / 'baseline.gesb'
    for version, revision in ((BASELINE, BASELINE_COMMIT), (CANDIDATE, candidate)):
        for name, directory in paths.items():
            (directory / 'Program.cs').write_bytes(consumer_source(revision, 'csharp', name))
            project = directory / ('GameEventScript.' + name + '.csproj')
            sources = 'https://api.nuget.org/v3/index.json' if version == BASELINE else str(feed)
            properties = [f'-p:GesPackageVersion={version}', f'-p:GesPackageSource={sources}',
                          '-p:GesUsePackages=true', f'-p:RestorePackagesPath={workspace / "nuget"}']
            log = workspace / (name + '-' + version + '.log')
            run(['dotnet', 'restore', str(project), '--force', '--no-cache', *properties], workspace, log)
            assets = json.loads((directory / 'obj/project.assets.json').read_text())
            versions = {key.split('/')[0]: key.split('/')[1] for key in assets['libraries']}
            names = ['GameEventScript.Runtime', 'GameEventScript.CSharpBridge']
            if name == 'PackageConsumer':
                names += ['GameEventScript.Compiler', 'GameEventScript.SyntaxHighlighter']
            require_versions(versions, version, names)
            fixture = baseline if version == BASELINE else workspace / 'candidate.gesb'
            if name == 'RuntimeConsumer':
                fixture = baseline  # Old producer, upgraded Runtime/Bridge consumer.
            # Reproducible package payloads can share file sizes/timestamps across
            # versions. Rebuild prevents CopyLocal retaining an old assembly.
            run(['dotnet', 'build', str(project), '--configuration', 'Release', '--no-restore',
                 '-t:Rebuild', *properties], workspace, log)
            run(['dotnet', 'run', '--project', str(project), '--configuration', 'Release', '--no-restore', '--no-build',
                 *properties, '--', str(fixture)], workspace, log)
    return baseline


def swift(workspace, candidate):
    repository = workspace / 'GameEventScript'
    snapshot(BASELINE_COMMIT, repository, workspace)
    log = workspace / 'repository.log'
    run(['git', 'init'], repository, log)
    for version, revision in ((BASELINE, None), (CANDIDATE, candidate)):
        if revision:
            for child in repository.iterdir():
                if child.name == '.git':
                    continue
                if child.is_dir():
                    shutil.rmtree(child)
                else:
                    child.unlink()
            snapshot(revision, repository, workspace)
        run(['git', 'add', '.'], repository, log)
        run(['git', '-c', 'user.name=Upgrade verification', '-c', 'user.email=test@example.invalid',
             '-c', 'commit.gpgsign=false', 'commit', '-m', version], repository, log)
        run(['git', '-c', 'tag.gpgsign=false', 'tag', version], repository, log)
    baseline = workspace / 'baseline.gesb'
    for version, revision in ((BASELINE, BASELINE_COMMIT), (CANDIDATE, candidate)):
        for name in SWIFT_CONSUMERS:
            directory = workspace / name
            sources = directory / 'Sources' / name
            sources.mkdir(parents=True, exist_ok=True)
            (sources / 'main.swift').write_bytes(consumer_source(revision, 'swift', name))
            modules = MODULES if name == 'SwiftPMConsumer' else ('Runtime',)
            products = ', '.join(f'.product(name: "GameEventScript{module}", package: "GameEventScript")' for module in modules)
            (directory / 'Package.swift').write_text(f'''// swift-tools-version: 6.0
import PackageDescription
let package = Package(name: "{name}", platforms: [.macOS("10.15.4")],
    dependencies: [.package(url: {json.dumps(str(repository))}, exact: "{version}")],
    targets: [.executableTarget(name: "{name}", dependencies: [{products}])])
''')
            scratch = workspace / (name + '-build')
            options = ['--package-path', str(directory), '--scratch-path', str(scratch),
                       '--disable-build-manifest-caching', '--configuration', 'release']
            log = workspace / (name + '-' + version + '.log')
            run(['swift', 'build', *options], directory, log)
            pins = json.loads((directory / 'Package.resolved').read_text())['pins']
            require_versions({p['identity']: p['state'].get('version') for p in pins}, version, ['gameeventscript'])
            binary = subprocess.check_output(['swift', 'build', *options, '--show-bin-path'], cwd=directory, text=True).strip()
            fixture = baseline if version == BASELINE or name == 'RuntimeConsumer' else workspace / 'candidate.gesb'
            run([str(Path(binary) / name), str(fixture)], directory, log)
    return baseline


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--language', choices=('csharp', 'swift'), required=True)
    args = parser.parse_args()
    baseline = subprocess.check_output(['git', 'rev-parse', BASELINE + '^{commit}'], cwd=ROOT, text=True).strip()
    if baseline != BASELINE_COMMIT:
        raise RuntimeError('Release baseline tag does not match the reviewed commit')
    candidate = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    with verification_workspace(ROOT / 'artifacts/release-upgrade' / args.language, 'upgrade ') as workspace:
        fixture = (csharp if args.language == 'csharp' else swift)(workspace, candidate)
        data = fixture.read_bytes()
        require_fixture(args.language, data)
        consumers = CSHARP_CONSUMERS if args.language == 'csharp' else SWIFT_CONSUMERS
        (workspace / 'summary.log').write_text(json.dumps({
            'status': 'passed', 'language': args.language, 'baselineVersion': BASELINE,
            'baselineCommit': baseline, 'candidateCommit': candidate, 'candidatePackageVersion': CANDIDATE,
            'binaryPolicy': 'selected-smoke-only-not-general-0.x-compatibility',
            'consumerSourceSha256': {f'{version}:{consumer_path(args.language, name)}':
                                     hashlib.sha256(consumer_source(revision, args.language, name)).hexdigest()
                                     for version, revision in ((BASELINE, baseline), (CANDIDATE, candidate)) for name in consumers},
            'baselineFixtureSha256': hashlib.sha256(data).hexdigest(),
            'baselineFixtureBase64': base64.b64encode(data).decode(),
        }, indent=2) + '\n')
    print(f'{args.language}: 0.3.0 to candidate consumer upgrade and selected old binary passed')


if __name__ == '__main__':
    main()
