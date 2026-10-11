#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Bounded deterministic reader mutations; reuses the existing executable adapters."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'artifacts/swift/binary-reader-fuzz'
SEED = 0x47455342
MAX_BYTES = 32768
FIXTURES = ('valid-runtime.gesb', 'valid-debug.gesb', 'valid-opaque.gesb',
            'valid-nested-call.gesb', 'valid-result-send.gesb', 'valid-parse-literal.gesb')


def stream(seed):
    # Fully specified LCG, independent of the product RNG and Python version.
    while True:
        seed = (1664525 * seed + 1013904223) & 0xffffffff
        yield seed


def sections(data):
    offset = 16
    while offset + 12 <= len(data):
        yield offset
        offset += 12 + struct.unpack_from('<I', data, offset + 8)[0]


def cases(seed, count):
    fixtures = [(name, (ROOT / 'conformance/fixtures/GesbV1' / name).read_bytes()) for name in FIXTURES]
    for name, data in fixtures:
        if len(data) > MAX_BYTES:
            raise ValueError('Fuzz seed exceeds configured input limit: ' + name)
        yield {'name': name, 'operation': 'control-valid', 'expected': 'accept'}, data
    yield {'operation': 'control-invalid', 'expected': 'reject'}, b'broken'
    yield {'operation': 'control-limit', 'expected': 'reject'}, bytes(MAX_BYTES + 1)
    random = stream(seed)
    for index in range(count):
        name, original = fixtures[next(random) % len(fixtures)]
        data = bytearray(original)
        offset = next(random) % len(data)
        mode = index % 7
        if mode == 0:
            del data[offset:]
        elif mode == 1:
            data[offset] ^= 1 << (next(random) % 8)
        elif mode == 2:
            width = min(4, len(data) - offset)
            data[offset:offset + width] = (next(random)).to_bytes(4, 'little')[:width]
        elif mode == 3:
            offset = list(sections(original))[next(random) % len(list(sections(original)))]
            field = (0, 2, 4, 6, 8)[next(random) % 5]
            width = 4 if field == 8 else 2
            data[offset + field:offset + field + width] = bytes([255]) * width
        elif mode == 4:
            # Corrupt counts at the start of a section, keeping framing intact.
            offset = list(sections(original))[next(random) % len(list(sections(original)))] + 12
            width = min(4, len(data) - offset)
            data[offset:offset + width] = bytes([255]) * width
        elif mode == 5:
            data[offset:offset] = bytes([next(random) & 255])
        else:
            del data[offset:offset + 1]
        if mode in (0, 5, 6) and len(data) >= 16:
            # Reach deeper parsing instead of only failing the outer size check.
            struct.pack_into('<I', data, 12, len(data))
        yield {'index': index, 'fixture': name, 'operation': mode, 'offset': offset}, bytes(data)


def validate(rows, responses):
    if len(rows) != len(responses):
        raise AssertionError(f'Incomplete adapter output: {len(responses)}/{len(rows)}')
    for (metadata, _), response in zip(rows, responses):
        kind, separator, payload = response.partition(':')
        if not separator or kind not in ('accept', 'reject') or not payload:
            raise AssertionError(f'Invalid probe response: {response!r}')
        if metadata.get('expected', kind) != kind:
            raise AssertionError(f'Failed probe control: {metadata}')
        if kind == 'accept':
            if not base64.b64decode(payload, validate=True):
                raise AssertionError('Empty accepted binary')


def compare(left, right):
    """Diagnostic ordering may differ, but acceptance and canonical bytes may not."""
    for index, (first, second) in enumerate(zip(left, right, strict=True)):
        if first.split(':')[0] != second.split(':')[0] or (first.startswith('accept:') and first != second):
            raise AssertionError(f'Reader disagreement at batch case {index}')


def run_adapter(command, name, rows, timeout):
    path = OUTPUT / (name + '.input.txt')
    result = OUTPUT / (name + '.output.txt')
    result.unlink(missing_ok=True)
    path.write_text(''.join(base64.b64encode(data).decode() + '\n' for _, data in rows))
    with (OUTPUT / (name + '.log')).open('w') as log:
        subprocess.run([*command, '--binary-fuzz', str(path), str(result)], cwd=ROOT,
                       check=True, timeout=timeout, stdout=log, stderr=subprocess.STDOUT)
    responses = result.read_text().splitlines()
    validate(rows, responses)
    return responses


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--skip-build', action='store_true')
    parser.add_argument('--cases', type=int, default=1024)
    parser.add_argument('--seed', type=lambda value: int(value, 0), default=SEED)
    parser.add_argument('--timeout', type=float, default=30, help='seconds per adapter batch')
    args = parser.parse_args()
    if not 1 <= args.cases <= 100000 or not 0 <= args.seed <= 0xffffffff or not 0 < args.timeout <= 300:
        parser.error('Require 1..100000 cases, UInt32 seed and timeout in (0, 300]')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    summary = OUTPUT / 'summary.json'
    summary.unlink(missing_ok=True)
    if not args.skip_build:
        subprocess.run(['dotnet', 'build', 'implementation/csharp/GameEventScript.Conformance/fixture-exporter',
                        '--configuration', 'Release', '--artifacts-path', 'artifacts/swift/csharp-exporter'], cwd=ROOT, check=True)
        subprocess.run(['swift', 'build', '--package-path', 'implementation/swift/GameEventScriptConformance',
                        '--scratch-path', 'artifacts/swift/conformance', '--disable-build-manifest-caching',
                        '--configuration', 'release'], cwd=ROOT, check=True)
    adapters = {
        'csharp': ['dotnet', str(ROOT / 'artifacts/csharp/runtime-fixture-exporter/bin/release/GameEventScript.RuntimeFixtureExporter.dll')],
        'swift': [str(ROOT / 'artifacts/swift/conformance/release/ges-conformance')],
    }
    started = time.monotonic()
    rows = list(cases(args.seed, args.cases))
    digest = hashlib.sha256(b''.join(len(data).to_bytes(4, 'little') + data for _, data in rows)).hexdigest()
    totals = {name: {'accept': 0, 'reject': 0} for name in adapters}
    for start in range(0, len(rows), 128):
        batch = rows[start:start + 128]
        label = f'batch-{start:06d}'
        # Persist the exact bytes and metadata before invoking potentially crashing readers.
        (OUTPUT / (label + '.json')).write_text(json.dumps({'seed': args.seed, 'cases': [
            dict(metadata, bytes=base64.b64encode(data).decode()) for metadata, data in batch]}, indent=2) + '\n')
        results = {name: run_adapter(command, label + '-' + name, batch, args.timeout) for name, command in adapters.items()}
        try:
            compare(results['csharp'], results['swift'])
        except (AssertionError, ValueError) as error:
            raise AssertionError(f'{label}: {error}; see {OUTPUT}') from error
        for name, responses in results.items():
            for response in responses:
                totals[name][response.split(':')[0]] += 1
    report = {'status': 'passed', 'seed': args.seed, 'mutations': args.cases, 'inputSha256': digest,
              'casesPerAdapter': len(rows), 'elapsedSeconds': round(time.monotonic() - started, 3), 'results': totals}
    summary.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
