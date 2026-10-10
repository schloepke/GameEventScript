#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Run existing performance gates serially and retain revision-bound evidence."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent


def command(*args):
    return subprocess.check_output(args, cwd=ROOT, text=True).strip()


def source_identity():
    """Include dirty and untracked source files; ignored build outputs are excluded."""
    names = subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT)
    digest = hashlib.sha256()
    for name in sorted(set(names.split(b'\0')) - {b''}):
        path = ROOT / name.decode('utf-8')
        digest.update(name + b'\0')
        digest.update(hashlib.sha256(path.read_bytes()).digest() if path.is_file() else b'deleted')
    return {'revision': command('git', 'rev-parse', 'HEAD'),
            'status': command('git', 'status', '--porcelain'), 'sourceSha256': digest.hexdigest()}


def validate_report(report):
    summary = report['summary']
    cases = report['cases']
    if (report['status'] != 'passed' or not cases or summary['total'] != len(cases)
            or summary['passed'] != len(cases)
            or any(summary[key] != 0 for key in ('failed', 'skipped', 'error'))
            or len({case['id'] for case in cases}) != len(cases)):
        raise ValueError('Incomplete, failed or skipped Conformance results')
    measured = 0
    for case in cases:
        if case['status'] != 'passed':
            raise ValueError('Case did not pass')
        if case['kind'] == 'performance':
            metrics = (case.get('performance') or {}).get('metrics', {})
            if not metrics or any(metric.get('passed') is not True or metric.get('measured') is None for metric in metrics.values()):
                raise ValueError('Missing or failed measurements')
            measured += 1
    if not measured:
        raise ValueError('No measured workloads')
    return measured


def validate_trx(paths):
    total = 0
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    for path in paths:
        root = ET.parse(path).getroot()
        counters = root.find('t:ResultSummary/t:Counters', ns)
        if counters is None or counters.get('total') != counters.get('passed'):
            raise ValueError('Native test failure or skipped test')
        results = root.findall('.//t:UnitTestResult', ns)
        if len(results) != int(counters.get('total')) or any(item.get('outcome') != 'Passed' for item in results):
            raise ValueError('Incomplete native test results')
        total += len(results)
    if not total:
        raise ValueError('No native test executions')
    return total


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--language', choices=['csharp', 'swift', 'all'], default='all')
    args = parser.parse_args()
    if platform.system() != 'Darwin' or platform.machine() != 'arm64':
        parser.error('Current timing profiles require macOS ARM64; use allocation-only CI on other hosts')
    parent = ROOT / 'artifacts/release-performance'
    parent.mkdir(parents=True, exist_ok=True)
    output = Path(tempfile.mkdtemp(prefix='run-', dir=parent))
    before = source_identity()
    receipt = {'schemaVersion': 1, 'source': before, 'startedAt': datetime.now(timezone.utc).isoformat(),
               'system': platform.platform(), 'cpu': command('sysctl', '-n', 'machdep.cpu.brand_string'),
               'status': 'failed', 'checks': {}}
    print(f'Evidence: {output}', flush=True)
    try:
        for language in (['csharp', 'swift'] if args.language == 'all' else [args.language]):
            receipt[language + 'Toolchain'] = command('dotnet', '--info') if language == 'csharp' else command('xcrun', 'swift', '--version')
            destination = output / language
            destination.mkdir()
            start = time.time_ns()
            if language == 'csharp':
                invocation = ['bash', 'scripts/test-csharp-performance.sh', '--logger', 'trx;LogFilePrefix=performance', '--results-directory', str(destination)]
            else:
                invocation = ['bash', 'scripts/test-swift-performance.sh', '--verify', str(destination)]
            with (output / (language + '.log')).open('w') as log:
                process = subprocess.run(invocation, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
            if language == 'csharp':
                sources = ROOT / 'artifacts/conformance/received/performance'
                reports = []
                for document in sorted((ROOT / 'conformance/suites/performance').glob('*.md')):
                    # Suite identity is declared in the Markdown front matter.
                    suite = next(line.split(':', 1)[1].strip().strip('"') for line in document.read_text().splitlines() if line.startswith('suiteId:'))
                    report = sources / (suite + '.results.json')
                    if report.stat().st_mtime_ns < start:
                        raise ValueError('Stale performance report: ' + str(report))
                    reports.append(destination / report.name)
                    for item in sources.glob(suite + '.*'):
                        if item.stat().st_mtime_ns >= start:
                            shutil.copy2(item, destination / item.name)
            else:
                reports = [destination / 'ConformanceResults.json']
            if process.returncode:
                raise RuntimeError(f'{language} performance gate exited with {process.returncode}; see retained logs and reports')
            if language == 'csharp':
                receipt['checks']['nativeTests'] = validate_trx(list(destination.glob('*.trx')))
            receipt['checks'][language] = sum(validate_report(json.loads(path.read_text())) for path in reports)
            print(f'{language}: {receipt["checks"][language]} measured workloads passed', flush=True)
        if source_identity() != before:
            raise ValueError('Source changed while measurements were running')
        receipt['status'] = 'passed'
    except BaseException as error:
        receipt['error'] = str(error)
        raise
    finally:
        receipt['finishedAt'] = datetime.now(timezone.utc).isoformat()
        receipt['files'] = {str(path.relative_to(output)): hashlib.sha256(path.read_bytes()).hexdigest()
                            for path in sorted(output.rglob('*')) if path.is_file()}
        (output / 'evidence.json').write_text(json.dumps(receipt, indent=2) + '\n')


if __name__ == '__main__':
    main()
