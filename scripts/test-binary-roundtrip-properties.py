#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Generate valid GESB V1 programs independently of both production writers."""
import argparse
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('fuzz', Path(__file__).with_name('test-binary-reader-fuzz.py'))
fuzz = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(fuzz)
OUTPUT = ROOT / 'artifacts/swift/binary-roundtrip-properties'
SEED = 0x47455350


def text(value):
    data = value.encode('utf-8')
    return struct.pack('<I', len(data)) + data


def section(kind, payload, required=False):
    return struct.pack('<HHHHI', kind, int(required), 1, 0, len(payload)) + payload


def container(parts):
    payload = b''.join(parts)
    return b'GESB' + struct.pack('<HHII', 1, 0, 16, 16 + len(payload)) + payload


def instruction(opcode, dst=0, x=0, payload=0):
    return struct.pack('<BBHHHQ', opcode, 0, dst, x, 0, payload)


def programs(seed, count):
    random = fuzz.stream(seed)
    integers = (0, 1, (1 << 63) - 1, 1 << 63, (1 << 64) - 1)
    floats = (0, 1 << 63, 1, 0x0010000000000000, 0x7fefffffffffffff, 0x3fb999999999999a)
    strings = ('', 'plain', 'é', 'e\u0301', '♠🃏', 'line\nnext', 'zero\0byte')
    for index in range(count):
        registers = 1 + index % 8
        pool = ['generated.case' + str(index), 'Generated', *strings,
                strings[index % len(strings)], str(next(random))]
        lists = [[], [next(random) % len(pool) for _ in range(index % 9)], []]
        code = [instruction(0x01, x=registers)]
        for register in range(registers):
            kind = (index + register) % 4
            if kind == 0:
                code.append(instruction(0x26, register, payload=integers[(index + register) % len(integers)]))
            elif kind == 1:
                code.append(instruction(0x27, register, payload=floats[(index + register) % len(floats)]))
            elif kind == 2:
                code.append(instruction(0x29, register, x=2 + (index + register) % len(strings)))
            else:
                code.append(instruction(0x23, register))
        code.append(instruction(0x0a))
        version = (next(random) << 32) | next(random)
        required = [
            section(1, struct.pack('<HHHHQ', 0, registers, 0, 0, version), True),
            section(2, struct.pack('<I', len(pool)) + b''.join(map(text, pool)), True),
            section(3, struct.pack('<I', len(lists)) + b''.join(
                struct.pack('<I', len(items)) + b''.join(struct.pack('<H', item) for item in items) for items in lists), True),
            section(4, struct.pack('<I', 1) + struct.pack('<BB9H', 0x10, 0, 0, 1, 0, 0, 0, 0, registers, 0, 0), True),
            section(0x10, struct.pack('<I', len(code)) + b''.join(code), True),
        ]
        optional = []
        source = ('// é 🃏\non Generated() { }\n' + ('// tail' if index % 2 else '')).encode()
        source_name = f'generated-{index}.ges'
        lines = [0] + [i + 1 for i, byte in enumerate(source) if byte == 10]
        if index & 1:
            optional.append(section(0x20, struct.pack('<I', 1) + text('local') + struct.pack('<I', 1)
                                    + struct.pack('<BBHIII', 2, 0, 0, 0, 0, len(code))))
        if index & 2:
            optional.append(section(0x21, struct.pack('<I', 1) + text(source_name) + struct.pack('<I', len(source))
                                    + hashlib.sha256(source).digest() + struct.pack('<I', len(lines))
                                    + b''.join(struct.pack('<I', offset) for offset in lines)
                                    + struct.pack('<6I', 1, 0, len(code), 0, 0, len(source))))
        if index & 4:
            optional.append(section(0x22, struct.pack('<II', 1, 0) + text(source_name)
                                    + struct.pack('<I', len(source)) + source))
        if index & 8:
            optional.append(section(0x30, text('ges.property.generator') + text(str(version))))
        opaque = [section(0x8000 + item, bytes(next(random) & 255 for _ in range((index + item) % 17)))
                  for item in range((index // 16) % 3)]
        canonical = container(required + optional + opaque)
        # Reverse known section order, retaining opaque relative order.
        encoded = canonical if index % 2 == 0 else container(list(reversed(required + optional)) + opaque)
        yield {'index': index, 'expected': 'accept', 'canonical': base64.b64encode(canonical).decode()}, encoded


def verify_expected(rows, responses):
    fuzz.validate(rows, responses)
    for (metadata, _), response in zip(rows, responses, strict=True):
        if response != 'accept:' + metadata['canonical']:
            raise AssertionError(f"Field preservation/canonicalization failed for case {metadata['index']}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--skip-build', action='store_true')
    parser.add_argument('--cases', type=int, default=192)
    parser.add_argument('--seed', type=lambda value: int(value, 0), default=SEED)
    args = parser.parse_args()
    if not 48 <= args.cases <= 10000 or not 0 <= args.seed <= 0xffffffff:
        parser.error('Require 48..10000 cases and a UInt32 seed')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    fuzz.OUTPUT = OUTPUT
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
    rows = list(programs(args.seed, args.cases))
    for start in range(0, len(rows), 64):
        batch = rows[start:start + 64]
        label = f'batch-{start:06d}'
        (OUTPUT / (label + '.json')).write_text(json.dumps({'seed': args.seed, 'cases': [
            dict(metadata, bytes=base64.b64encode(data).decode()) for metadata, data in batch]}, indent=2) + '\n')
        written = {}
        for name, command in adapters.items():
            written[name] = fuzz.run_adapter(command, label + '-' + name, batch, 30)
            verify_expected(batch, written[name])
        for writer, reader in [('csharp', 'swift'), ('swift', 'csharp')]:
            exchanged = [(metadata, base64.b64decode(response.removeprefix('accept:')))
                         for (metadata, _), response in zip(batch, written[writer], strict=True)]
            responses = fuzz.run_adapter(adapters[reader], label + '-' + writer + '-to-' + reader, exchanged, 30)
            verify_expected(exchanged, responses)
    report = {'status': 'passed', 'seed': args.seed, 'programs': len(rows), 'directions': 2,
              'inputSha256': hashlib.sha256(b''.join(data for _, data in rows)).hexdigest(),
              'elapsedSeconds': round(time.monotonic() - started, 3)}
    summary.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
