#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Compare real schema bytes, hashes and decoding between the C# and Swift adapters.

Random values, values at each bound and half-step ties encode to the bytes of an independent oracle in both ports. Random
and mutated bytes decode to the same result in both ports, and every schema of the corpus has the same canonical form and
hash. Negative controls qualify the comparison.
"""
import argparse
import json
import math
from pathlib import Path
import random
import struct
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'conformance/runner'))
from checks.numeric_checks import canonical_text, number as parsed_number  # noqa: E402

REPORTS = ROOT / 'artifacts/swift/schema-roundtrip'
SEED = 20261104
SCHEMA = """schema :Inner as {
  v: :Number integer between 0 and 3
  c: :Text max 1 | :Boolean
}

schema :Sample as {
  id: :Number integer between -5 and 65530 key
  x: :Quantity(m) between -100m and 5000m step 1cm
  y: :Quantity(m) between 0m and 3km step 5cm
  bearing: :Quantity(°) wrapping between 0° and 360° step 0.01°
  wide: :Number integer
  ratio: :Percentage between 0% and 100% step 1%
  raw: :Number
  flag: :Boolean
  state: :Tag(#a, #b, #c)
  label: :Text max 6 optional
  items: :List(:Number integer between 0 and 7) max 3
  choice: :Number integer between 0 and 7 | :Text max 1 | :Nothing
  rolls: :Dice max 3 between 1 and 6
  mixed: :List(:Boolean | :Nothing) max 2
  pick: :Inner | :Text max 1
  boxed: :List(:Boolean) max 2 | :Boolean
  maybe: :Boolean | :Tag(#a, #b) optional
  tail: :Boolean
}
"""


def even(quotient):
    floor = math.floor(quotient)
    rest = quotient - floor
    if rest < 0.5:
        return floor
    if rest > 0.5:
        return floor + 1
    return floor if floor % 2 == 0 else floor + 1


class Bits:
    def __init__(self):
        self.value = 0
        self.count = 0

    def put(self, value, width):
        self.value |= (value & ((1 << width) - 1)) << self.count
        self.count += width

    def hex(self):
        return self.value.to_bytes((self.count + 7) // 8, 'little').hex()


def text(value):
    return canonical_text(parsed_number(repr(value)))


def number(value):
    if isinstance(value, int) or value.is_integer():
        return {'type': ':Number.int64', 'value': str(int(value))}
    return {'type': ':Number.binary64', 'value': text(value)}


def quantity(value, unit):
    if float(value).is_integer():
        return {'type': ':Quantity.int64', 'value': str(int(value)), 'unit': ':' + unit}
    return {'type': ':Quantity.binary64', 'value': text(float(value)), 'unit': ':' + unit}


def sample(rng):
    """A random record with its expected bytes."""
    def scaled(low, high, scale):
        kind = rng.randrange(4)
        if kind == 0:
            return float(rng.choice((low, high)))
        if kind == 1:
            # A half-step tie.
            return (rng.randrange(int(low * scale), int(high * scale)) + 0.5) / scale
        if kind == 2:
            return rng.randrange(int(low * scale), int(high * scale)) / scale
        return rng.uniform(low, high)

    identifier = rng.choice((-5, 65530, rng.randrange(-5, 65531)))
    x = scaled(-100, 5000, 100)
    y = scaled(0, 3000, 20)
    bearing = rng.choice((0.0, 360.0, -90.0, 359.995, rng.uniform(-1000, 1000)))
    wide = rng.choice((-(2**63), 2**63 - 1, rng.randrange(-(2**40), 2**40)))
    ratio = rng.randrange(0, 101) / 100
    raw = rng.choice((1.5, -2.25, rng.uniform(-1e6, 1e6)))
    flag = rng.random() < 0.5
    state = rng.randrange(3)
    label = rng.choice((None, '', 'äb', 'abcdef'))
    items = [rng.randrange(8) for _ in range(rng.randrange(4))]
    bits = Bits()
    bits.put(identifier + 5, 16)
    bits.put(even(x * 100.0 / 1.0) + 10000, 19)
    bits.put(even(y * 100.0 / 5.0), 16)
    bits.put(even(bearing * 100.0 / 1.0) % 36000, 16)
    bits.put(wide & (2**64 - 1), 64)
    bits.put(even(ratio * 100.0 / 1.0), 7)
    bits.put(struct.unpack('<Q', struct.pack('<d', raw))[0], 64)
    bits.put(1 if flag else 0, 1)
    bits.put(state, 2)
    if label is None:
        bits.put(0, 1)
    else:
        bits.put(1, 1)
        encoded = label.encode()
        bits.put(len(encoded), 3)
        for byte in encoded:
            bits.put(byte, 8)
    bits.put(len(items), 2)
    for item in items:
        bits.put(item, 3)
    choice = rng.choice((None, 'a', rng.randrange(8)))
    if isinstance(choice, int):
        bits.put(0, 2)
        bits.put(choice, 3)
    elif choice is None:
        bits.put(2, 2)
    else:
        bits.put(1, 2)
        bits.put(1, 1)
        bits.put(ord(choice), 8)
    rolls = sorted((rng.randrange(1, 7) for _ in range(rng.randrange(4))), reverse=True)
    bits.put(len(rolls), 2)
    for roll in rolls:
        bits.put(roll - 1, 3)
    mixed = [rng.choice((None, False, True)) for _ in range(rng.randrange(3))]
    bits.put(len(mixed), 2)
    for item in mixed:
        if item is None:
            bits.put(1, 1)
        else:
            bits.put(0, 1)
            bits.put(1 if item else 0, 1)
    if rng.random() < 0.5:
        inner, inner_text = rng.randrange(4), rng.choice(('a', False, True))
        bits.put(0, 1)
        bits.put(inner, 2)
        if isinstance(inner_text, str):
            bits.put(0, 1)
            bits.put(1, 1)
            bits.put(ord(inner_text), 8)
            inner_value = {'type': ':Text', 'value': inner_text}
        else:
            bits.put(1, 1)
            bits.put(1 if inner_text else 0, 1)
            inner_value = {'type': ':Boolean', 'value': inner_text}
        pick = {'type': ':Inner', 'entries': [{'key': 'c', 'value': inner_value}, {'key': 'v', 'value': number(inner)}]}
    else:
        picked = rng.choice(('', 'a'))
        bits.put(1, 1)
        bits.put(len(picked), 1)
        for byte in picked.encode():
            bits.put(byte, 8)
        pick = {'type': ':Text', 'value': picked}
    if rng.random() < 0.5:
        flags = [rng.random() < 0.5 for _ in range(rng.randrange(3))]
        bits.put(0, 1)
        bits.put(len(flags), 2)
        for item in flags:
            bits.put(1 if item else 0, 1)
        boxed = {'type': ':List', 'items': [{'type': ':Boolean', 'value': item} for item in flags]}
    else:
        single = rng.random() < 0.5
        bits.put(1, 1)
        bits.put(1 if single else 0, 1)
        boxed = {'type': ':Boolean', 'value': single}
    maybe_kind = rng.randrange(3)
    if maybe_kind == 0:
        bits.put(0, 1)
        maybe = {'type': ':Nothing'}
    elif maybe_kind == 1:
        single = rng.random() < 0.5
        bits.put(1, 1)
        bits.put(0, 1)
        bits.put(1 if single else 0, 1)
        maybe = {'type': ':Boolean', 'value': single}
    else:
        tag = rng.randrange(2)
        bits.put(1, 1)
        bits.put(1, 1)
        bits.put(tag, 1)
        maybe = {'type': ':Tag', 'value': 'ab'[tag]}
    tail = rng.random() < 0.5
    bits.put(1 if tail else 0, 1)
    fields = {
        'id': number(identifier), 'x': quantity(x, 'meter'), 'y': quantity(y, 'meter'), 'bearing': quantity(bearing, 'degree'),
        'wide': number(wide), 'ratio': {'type': ':Percentage', 'value': text(ratio) if ratio else '0'}, 'raw': number(raw),
        'flag': {'type': ':Boolean', 'value': flag}, 'state': {'type': ':Tag', 'value': 'abc'[state]},
        'label': {'type': ':Nothing'} if label is None else {'type': ':Text', 'value': label},
        'items': {'type': ':List', 'items': [number(item) for item in items]},
        'choice': {'type': ':Nothing'} if choice is None else {'type': ':Text', 'value': choice} if isinstance(choice, str) else number(choice),
        'rolls': {'type': ':Dice', 'rolls': rolls},
        'mixed': {'type': ':List', 'items': [{'type': ':Nothing'} if item is None else {'type': ':Boolean', 'value': item} for item in mixed]},
        'pick': pick, 'boxed': boxed, 'maybe': maybe, 'tail': {'type': ':Boolean', 'value': tail},
    }
    entries = [{'key': key, 'value': fields[key]} for key in sorted(fields)]
    return {'type': ':Sample', 'entries': entries}, bits.hex()


def block(value, indent):
    pad = ' ' * indent
    if isinstance(value, dict):
        return '\n' + ''.join(f'{pad}{key}:{block(item, indent + 2)}' for key, item in value.items()) if value else ' {}\n'
    if isinstance(value, list):
        if not value:
            return ' []\n'
        lines = '\n'
        for item in value:
            if isinstance(item, dict) and item:
                lines += f'{pad}- ' + block(item, indent + 2)[1 + indent + 2:]
            else:
                lines += f'{pad}-{block(item, indent + 2)}'
        return lines
    return ' ' + json.dumps(value, ensure_ascii=False) + '\n'


def suite(cases):
    head = '---\nformatVersion: 1\nsuiteId: temp.schema\ntitle: Temporary schema exchange\nkind: schemaApi\nlevel: atomic\ncategories: [conformance]\n---\n\n# Temporary schema exchange\n'
    body = ''
    for identifier, api in cases:
        body += f'\n---\n\n## Test: {identifier}\n\nExchange.\n\n### Case description\n\n```yaml\ngesBlock: case\nid: {identifier}\nschemaApi:{block(api, 2)}```\n\n### Expectation\n\n```yaml\ngesBlock: expect\nschema:\n  canonical: ""\n```\n'
    return head + body


def observe(adapter, requests, output):
    with requests.open('rb') as source, output.open('wb') as target:
        subprocess.run(adapter, stdin=source, stdout=target, cwd=ROOT, check=True, timeout=600)
    return json.loads(output.read_text())['observations']


def normalized(value):
    """Numeric transport text in its canonical spelling, so that `0` and `0.0` compare by their binary64 value."""
    if isinstance(value, dict):
        kind = value.get('type', '')
        numeric = isinstance(kind, str) and kind in (':Percentage', ':Number.binary64', ':Quantity.binary64', ':Vector', ':Point')
        result = {}
        for key, item in value.items():
            result[key] = canonical_text(parsed_number(item)) if numeric and key in ('value', 'x', 'y', 'z') and isinstance(item, str) else normalized(item)
        return result
    if isinstance(value, list):
        return [normalized(item) for item in value]
    return value


def compare(left, right):
    """The IDs whose observations differ between two ports."""
    return sorted(key for key in set(left) | set(right) if json.dumps(normalized(left.get(key)), sort_keys=True) != json.dumps(normalized(right.get(key)), sort_keys=True))


def difference(left, right, path=''):
    """The first differing path, for the report."""
    if isinstance(left, dict) and isinstance(right, dict):
        for key in sorted(set(left) | set(right)):
            if left.get(key) != right.get(key):
                return difference(left.get(key), right.get(key), path + '/' + key)
    if isinstance(left, list) and isinstance(right, list) and len(left) == len(right):
        for index, (first, second) in enumerate(zip(left, right)):
            if first != second:
                return difference(first, second, path + '/' + str(index))
    return f'{path}: {left!r} / {right!r}'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--skip-build', action='store_true')
    parser.add_argument('--count', type=int, default=1000)
    options = parser.parse_args()
    if not options.skip_build:
        subprocess.run(['dotnet', 'build', 'implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj', '--configuration', 'Release'], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
        subprocess.run(['swift', 'build', '--package-path', 'implementation/swift/GameEventScriptConformance', '--scratch-path', 'artifacts/swift/conformance', '--disable-build-manifest-caching', '--configuration', 'release', '--product', 'ges-conformance-adapter'], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
    adapters = {
        'csharp': ['dotnet', 'artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll'],
        'swift': ['artifacts/swift/conformance/release/ges-conformance-adapter'],
    }
    rng = random.Random(SEED)
    REPORTS.mkdir(parents=True, exist_ok=True)
    expected = {}
    cases = []
    encoded = []
    for index in range(options.count):
        value, data = sample(rng)
        encoded.append(bytes.fromhex(data))
        identifier = f'encode-{index}'
        expected[identifier] = data
        cases.append((identifier, {'sources': [SCHEMA], 'root': 'Sample', 'encode': value, 'decode': data}))
    # Random and mutated bytes must decode to the same result or the same error in both ports.
    for index in range(options.count):
        if index % 2:
            data = bytes(rng.randrange(256) for _ in range(rng.randrange(40)))
        else:
            mutated = bytearray(rng.choice(encoded))
            for _ in range(rng.randrange(1, 4)):
                mutated[rng.randrange(len(mutated))] ^= 1 << rng.randrange(8)
            data = bytes(mutated[:rng.randrange(len(mutated) + 1)] if rng.random() < 0.3 else mutated)
        cases.append((f'fuzz-{index}', {'sources': [SCHEMA], 'root': 'Sample', 'decode': data.hex()}))
    respelled = ('schema :Force as { force: :Quantity(N) between 0kN and 50kN step 0.01kN }\n', '// other\n\nschema :Force as {\n  force: :Quantity(N) between 0N and 50000N step 10N // comment\n}\n')
    for index, source in enumerate(respelled):
        cases.append((f'respelled-{index}', {'sources': [source], 'root': 'Force', 'bind': False}))
    with tempfile.TemporaryDirectory(dir=REPORTS) as directory:
        temporary = Path(directory)
        corpus = temporary / 'exchange.md'
        corpus.write_text(suite(cases))
        observations = {}
        for source, name in ((corpus, 'exchange'), (ROOT / 'conformance/suites/api/schemas.md', 'corpus')):
            requests = temporary / f'{name}-requests.json'
            subprocess.run([sys.executable, 'conformance/runner/main.py', 'requests', '--corpus', str(source), '--output', str(requests)], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
            for port, adapter in adapters.items():
                observations[(port, name)] = observe(adapter, requests, temporary / f'{port}-{name}.json')
    failures = []
    for name in ('exchange', 'corpus'):
        different = compare(observations[('csharp', name)], observations[('swift', name)])
        if different:
            failures.append(f'{name}: {len(different)} observations differ, first {different[0]}: ' + difference(normalized(observations[("csharp", name)][different[0]]), normalized(observations[("swift", name)][different[0]])))
    def schema_of(port, identifier):
        for key, value in observations[(port, 'exchange')].items():
            if key.endswith('/' + identifier):
                return value['schema']
        raise KeyError(identifier)
    accepted = 0
    for port in adapters:
        for identifier, data in expected.items():
            observed = schema_of(port, identifier)
            if observed.get('encoded') != {'bytes': data}:
                failures.append(f'{port} {identifier}: expected {data}, got {observed.get("encoded")}')
                break
            decoded = observed.get('decoded', {})
            if 'error' in decoded or decoded.get('byteCount') != len(data) // 2:
                failures.append(f'{port} {identifier}: decoding failed: {decoded}')
                break
        first, second = schema_of(port, 'respelled-0'), schema_of(port, 'respelled-1')
        if first != second:
            failures.append(f'{port}: respelling changed the canonical form or hash')
    accepted = sum('value' in schema_of('csharp', f'fuzz-{index}').get('decoded', {}) for index in range(options.count))
    # Negative controls: the comparison notices a changed byte, and a mutated value would not pass as equal.
    if not compare({'a': {'bytes': '00'}}, {'a': {'bytes': '01'}}) or not compare({'a': 1}, {}):
        failures.append('The comparison misses differences.')
    if failures:
        print('\n'.join(failures[:20]), file=sys.stderr)
        return 1
    print(f'Schema bytes verified: {options.count} encodings in both ports, {options.count} fuzzed inputs with equal results ({accepted} accepted), {len(observations[("csharp", "corpus")])} corpus cases.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
