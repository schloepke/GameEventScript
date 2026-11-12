#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Compare real schema bytes, hashes and decoding between the C# and Swift adapters.

Random values, values at each bound and half-step ties encode to the bytes of an independent oracle in both ports. Random
and mutated bytes decode to the same result in both ports, and every schema of the corpus has the same canonical form and
hash. Deltas between random records match the oracle too, also element by element in keyed lists, and random and mutated deltas
decode alike. Code that both CLIs generate for these schemas joins as a third and fourth party: in each port it gives the
oracle's bytes and the results of the interpreted path, values included. Negative controls qualify
the comparison.
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
import schema_generated_parties  # noqa: E402

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
  named: :Tag max 3
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
    named = rng.choice(('a', 'aB', 'x1Y'))
    bits.put(len(named), 2)
    for byte in named.encode():
        bits.put(byte, 8)
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
        'pick': pick, 'boxed': boxed, 'maybe': maybe, 'named': {'type': ':Tag', 'value': named}, 'tail': {'type': ':Boolean', 'value': tail},
    }
    entries = [{'key': key, 'value': fields[key]} for key in sorted(fields)]
    return {'type': ':Sample', 'entries': entries}, bits.hex()


DELTA_SCHEMA = """schema :Part as {
  v: :Number integer between 0 and 3
  on: :Boolean
}

schema :Moving as {
  id: :Number integer between 0 and 255 key
  x: :Quantity(m) between -100m and 100m step 1cm delta within 1.27m
  bearing: :Quantity(°) wrapping between 0° and 360° step 1° delta within 15°
  count: :Number integer between -1000 and 1000 delta within 7
  wide: :Number integer
  raw: :Number
  flag: :Boolean
  state: :Tag(#a, #b, #c)
  label: :Text max 3 optional
  items: :List(:Number integer between 0 and 7) max 2
  part: :Part
  at: :Point(m) {
    x between 0m and 100m step 1m delta within 3m
    y between 0m and 100m step 1m
  }
  pick: :Number integer between 0 and 7 | :Part | :Nothing
}
"""


KEYED_SCHEMA = """schema :Leaf as {
  id: :Number integer key
  v: :Number integer between 0 and 3
}

schema :Unit as {
  id: :Number integer between 0 and 255 key
  hp: :Number integer between 0 and 15
  on: :Boolean
  leaves: :List(:Leaf) max 2
  name: :Text max 3 optional
  mark: :Boolean | :Tag(#a, #b)
  raw: :Number
}

schema :Fleet as {
  tick: :Number integer between 0 and 255
  units: :List(:Unit) max 5
  spare: :List(:Unit) max 1 optional
}
"""


def unit(rng, key, base=None):
    """A Unit by wire values; a base makes it often stay or change a little. Its last fields need skipping, which an
    element only holding fixed widths would never exercise: an optional Text, a union and a 64-bit number."""
    if base is not None and rng.random() < 0.4:
        return base

    def chosen(position, make):
        return base[position] if base is not None and rng.random() < 0.5 else make()

    leaves = sorted(rng.sample((-(2**63), -3, -1, 0, 2, 2**63 - 1), rng.randrange(3)))
    if rng.random() < 0.1:
        leaves.reverse()
    return (
        key, rng.randrange(16), rng.random() < 0.5, [(leaf, rng.randrange(4)) for leaf in leaves],
        chosen(4, lambda: rng.choice((None, '', 'ab', 'äb'))),
        chosen(5, lambda: ('flag', rng.random() < 0.5) if rng.random() < 0.5 else ('tag', rng.randrange(2))),
        chosen(6, lambda: rng.choice((1.5, -2.25, 0.125, -0.375))),
    )


def fleet_units(rng, base=None):
    """Sorted by key most of the time, with some keys of the base and some new ones."""
    held = {item[0]: item for item in base or []}
    keys = rng.sample(range(12), rng.randrange(6))
    keys.sort()
    if rng.random() < 0.15:
        rng.shuffle(keys)
    if len(keys) > 1 and rng.random() < 0.05:
        keys[1] = keys[0]
    return [unit(rng, key, held.get(key)) for key in keys]


def fleet(rng, base=None):
    spare = None
    if rng.random() < 0.7:
        spare = fleet_units(rng, base['spare'] if base else None)[:1]
    return {'tick': base['tick'] if base and rng.random() < 0.5 else rng.randrange(256), 'units': fleet_units(rng, base['units'] if base else None), 'spare': spare}


def unit_value(item):
    leaves = [{'type': ':Leaf', 'entries': [{'key': 'id', 'value': number(leaf)}, {'key': 'v', 'value': number(v)}]} for leaf, v in item[3]]
    name = {'type': ':Nothing'} if item[4] is None else {'type': ':Text', 'value': item[4]}
    mark = {'type': ':Boolean', 'value': item[5][1]} if item[5][0] == 'flag' else {'type': ':Tag', 'value': 'ab'[item[5][1]]}
    return {'type': ':Unit', 'entries': [
        {'key': 'hp', 'value': number(item[1])}, {'key': 'id', 'value': number(item[0])},
        {'key': 'leaves', 'value': {'type': ':List', 'items': leaves}}, {'key': 'mark', 'value': mark}, {'key': 'name', 'value': name},
        {'key': 'on', 'value': {'type': ':Boolean', 'value': item[2]}}, {'key': 'raw', 'value': number(item[6])},
    ]}


def fleet_value(record):
    spare = {'type': ':Nothing'} if record['spare'] is None else {'type': ':List', 'items': [unit_value(item) for item in record['spare']]}
    return {'type': ':Fleet', 'entries': [
        {'key': 'spare', 'value': spare}, {'key': 'tick', 'value': number(record['tick'])},
        {'key': 'units', 'value': {'type': ':List', 'items': [unit_value(item) for item in record['units']]}},
    ]}


def put_leaf(bits, leaf):
    bits.put(leaf[0] & (2**64 - 1), 64)
    bits.put(leaf[1], 2)


def put_unit(bits, item):
    bits.put(item[0], 8)
    bits.put(item[1], 4)
    bits.put(1 if item[2] else 0, 1)
    bits.put(len(item[3]), 2)
    for leaf in item[3]:
        put_leaf(bits, leaf)
    bits.put(0 if item[4] is None else 1, 1)
    if item[4] is not None:
        put_label(bits, item[4])
    put_mark(bits, item[5])
    bits.put(raw_bits(item[6]), 64)


def put_mark(bits, mark):
    bits.put(0 if mark[0] == 'flag' else 1, 1)
    bits.put(1 if mark[1] else 0, 1)


def put_units(bits, items, count_width):
    bits.put(len(items), count_width)
    for item in items:
        put_unit(bits, item)


def fleet_full(record):
    bits = Bits()
    bits.put(record['tick'], 8)
    put_units(bits, record['units'], 3)
    if record['spare'] is None:
        bits.put(0, 1)
    else:
        bits.put(1, 1)
        put_units(bits, record['spare'], 1)
    return bits.hex()


def strictly_ascending(keys):
    return all(first < second for first, second in zip(keys, keys[1:]))


def put_keyed(bits, items, held, count_width, position_width, put_full, key_of, put_changed):
    """A changed keyed list, written from the rules of the specification."""
    if not strictly_ascending([key_of(item) for item in items]) or not strictly_ascending([key_of(item) for item in held]):
        bits.put(1, 1)
        bits.put(len(items), count_width)
        for item in items:
            put_full(bits, item)
        return
    bits.put(0, 1)
    positions = {key_of(item): position for position, item in enumerate(held)}
    kept = {key_of(item) for item in items}
    removed = [position for position, item in enumerate(held) if key_of(item) not in kept]
    bits.put(len(removed), count_width)
    for position in removed:
        bits.put(position, position_width)
    added = [item for item in items if key_of(item) not in positions]
    bits.put(len(added), count_width)
    for item in added:
        put_full(bits, item)
    changed = [item for item in items if key_of(item) in positions and held[positions[key_of(item)]] != item]
    bits.put(len(changed), count_width)
    for item in changed:
        bits.put(positions[key_of(item)], position_width)
        put_changed(bits, item, held[positions[key_of(item)]])


def put_changed_leaf(bits, leaf, held):
    bits.put(1, 1)
    bits.put(leaf[1], 2)


def put_changed_unit(bits, item, held):
    raw_changed = raw_bits(item[6]) != raw_bits(held[6])
    for changed in (item[1] != held[1], item[2] != held[2], item[3] != held[3], item[4] != held[4], item[5] != held[5], raw_changed):
        bits.put(1 if changed else 0, 1)
    if item[1] != held[1]:
        bits.put(item[1], 4)
    if item[3] != held[3]:
        put_keyed(bits, item[3], held[3], 2, 1, put_leaf, lambda leaf: leaf[0], put_changed_leaf)
    if item[4] != held[4]:
        # Present: the presence bit, then the Text in full, whether or not the baseline held one.
        bits.put(0 if item[4] is None else 1, 1)
        if item[4] is not None:
            put_label(bits, item[4])
    if item[5] != held[5]:
        # The index; the same alternative takes its payload (none for a Boolean), another one its full encoding.
        bits.put(0 if item[5][0] == 'flag' else 1, 1)
        if item[5][0] != held[5][0] or item[5][0] == 'tag':
            bits.put(1 if item[5][1] else 0, 1)
    if raw_changed:
        bits.put(raw_bits(item[6]), 64)


def fleet_delta(record, base):
    bits = Bits()
    bits.put(1 if record['tick'] != base['tick'] else 0, 1)
    bits.put(1 if record['units'] != base['units'] else 0, 1)
    bits.put(1 if record['spare'] != base['spare'] else 0, 1)
    if record['tick'] != base['tick']:
        bits.put(record['tick'], 8)
    if record['units'] != base['units']:
        put_keyed(bits, record['units'], base['units'], 3, 3, put_unit, lambda item: item[0], put_changed_unit)
    if record['spare'] != base['spare']:
        if record['spare'] is None:
            bits.put(0, 1)
        else:
            bits.put(1, 1)
            if base['spare'] is None:
                put_units(bits, record['spare'], 1)
            else:
                put_keyed(bits, record['spare'], base['spare'], 1, 0, put_unit, lambda item: item[0], put_changed_unit)
    return bits.hex()


def moving(rng, base=None):
    """Wire values of a record: unchanged, near or far from its base."""
    def near(name, low, high, reach):
        if base is None or rng.random() < 0.2:
            return rng.randrange(low, high + 1)
        if rng.random() < 0.4:
            return base[name]
        return min(high, max(low, base[name] + rng.randrange(-reach, reach + 1)))

    def pick():
        kind = rng.randrange(3)
        return None if kind == 2 else rng.randrange(8) if kind == 0 else (rng.randrange(4), rng.random() < 0.5)

    def chosen(name, make):
        return base[name] if base is not None and rng.random() < 0.5 else make()

    bearing = (base['bearing'] + rng.randrange(-20, 21)) % 360 if base is not None and rng.random() < 0.7 else rng.randrange(360)
    return {
        'id': base['id'] if base is not None else rng.randrange(256),
        'x': near('x', 0, 20000, 200), 'bearing': bearing, 'count': near('count', 0, 2000, 12),
        'wide': chosen('wide', lambda: rng.choice((-(2**63), 2**63 - 1, rng.randrange(-(2**40), 2**40)))),
        'raw': chosen('raw', lambda: rng.choice((1.5, -2.25, rng.uniform(-1e6, 1e6)))),
        'flag': chosen('flag', lambda: rng.random() < 0.5), 'state': chosen('state', lambda: rng.randrange(3)),
        'label': chosen('label', lambda: rng.choice((None, '', 'äb', 'abc'))),
        'items': chosen('items', lambda: [rng.randrange(8) for _ in range(rng.randrange(3))]),
        'part': chosen('part', lambda: (rng.randrange(4), rng.random() < 0.5)),
        'at': chosen('at', lambda: (rng.randrange(101), rng.randrange(101))), 'pick': chosen('pick', pick),
    }


def part_value(part):
    return {'type': ':Part', 'entries': [{'key': 'on', 'value': {'type': ':Boolean', 'value': part[1]}}, {'key': 'v', 'value': number(part[0])}]}


def moving_value(record):
    """A record in the value transport, from its wire values."""
    fields = {
        'id': number(record['id']), 'x': quantity((record['x'] - 10000) / 100, 'meter'), 'bearing': quantity(record['bearing'], 'degree'),
        'count': number(record['count'] - 1000), 'wide': number(record['wide']), 'raw': number(record['raw']),
        'flag': {'type': ':Boolean', 'value': record['flag']}, 'state': {'type': ':Tag', 'value': 'abc'[record['state']]},
        'label': {'type': ':Nothing'} if record['label'] is None else {'type': ':Text', 'value': record['label']},
        'items': {'type': ':List', 'items': [number(item) for item in record['items']]}, 'part': part_value(record['part']),
        'at': {'type': ':Point', 'x': str(record['at'][0]), 'y': str(record['at'][1]), 'z': '0', 'unit': ':meter'},
        'pick': {'type': ':Nothing'} if record['pick'] is None else number(record['pick']) if isinstance(record['pick'], int) else part_value(record['pick']),
    }
    return {'type': ':Moving', 'entries': [{'key': key, 'value': fields[key]} for key in sorted(fields)]}


def raw_bits(value):
    return struct.unpack('<Q', struct.pack('<d', value))[0]


def moving_full(record):
    """The full encoding of a record."""
    bits = Bits()
    bits.put(record['id'], 8)
    bits.put(record['x'], 15)
    bits.put(record['bearing'], 9)
    bits.put(record['count'], 11)
    bits.put(record['wide'] & (2**64 - 1), 64)
    bits.put(raw_bits(record['raw']), 64)
    bits.put(1 if record['flag'] else 0, 1)
    bits.put(record['state'], 2)
    bits.put(0 if record['label'] is None else 1, 1)
    if record['label'] is not None:
        put_label(bits, record['label'])
    put_items(bits, record['items'])
    bits.put(record['part'][0], 2)
    bits.put(1 if record['part'][1] else 0, 1)
    bits.put(record['at'][0], 7)
    bits.put(record['at'][1], 7)
    put_pick(bits, record['pick'])
    return bits.hex()


def put_label(bits, label):
    encoded = label.encode()
    bits.put(len(encoded), 2)
    for byte in encoded:
        bits.put(byte, 8)


def put_items(bits, items):
    bits.put(len(items), 2)
    for item in items:
        bits.put(item, 3)


def put_pick(bits, pick):
    if pick is None:
        bits.put(2, 2)
    elif isinstance(pick, int):
        bits.put(0, 2)
        bits.put(pick, 3)
    else:
        bits.put(1, 2)
        bits.put(pick[0], 2)
        bits.put(1 if pick[1] else 0, 1)


def put_range(bits, value, base, reach, width, full_width, period=None):
    """The range form of an index, or the full index outside the range."""
    difference = value - base
    if period is not None:
        difference = (value - base + period // 2) % period - period // 2
    if abs(difference) <= reach:
        bits.put(0, 1)
        bits.put(difference + reach, width)
    else:
        bits.put(1, 1)
        bits.put(value, full_width)


def moving_delta(record, base):
    """The delta of a record against its base, written from the rules of the specification."""
    bits = Bits()

    def changes(pairs):
        for changed, _ in pairs:
            bits.put(1 if changed else 0, 1)
        for changed, payload in pairs:
            if changed:
                payload()

    def part_payload(part, held):
        changes(((part[0] != held[0], lambda: bits.put(part[0], 2)), (part[1] != held[1], lambda: None)))

    def label_payload():
        if record['label'] is None:
            bits.put(0, 1)
        else:
            bits.put(1, 1)
            put_label(bits, record['label'])

    def pick_payload():
        pick, held = record['pick'], base['pick']
        if isinstance(pick, tuple) and isinstance(held, tuple):
            bits.put(1, 2)
            part_payload(pick, held)
        else:
            put_pick(bits, pick)

    def at_payload():
        changes((
            (record['at'][0] != base['at'][0], lambda: put_range(bits, record['at'][0], base['at'][0], 3, 3, 7)),
            (record['at'][1] != base['at'][1], lambda: bits.put(record['at'][1], 7)),
        ))

    bits.put(record['id'], 8)
    changes((
        (record['x'] != base['x'], lambda: put_range(bits, record['x'], base['x'], 127, 8, 15)),
        (record['bearing'] != base['bearing'], lambda: put_range(bits, record['bearing'], base['bearing'], 15, 5, 9, 360)),
        (record['count'] != base['count'], lambda: put_range(bits, record['count'], base['count'], 7, 4, 11)),
        (record['wide'] != base['wide'], lambda: bits.put(record['wide'] & (2**64 - 1), 64)),
        (raw_bits(record['raw']) != raw_bits(base['raw']), lambda: bits.put(raw_bits(record['raw']), 64)),
        (record['flag'] != base['flag'], lambda: None),
        (record['state'] != base['state'], lambda: bits.put(record['state'], 2)),
        (record['label'] != base['label'], label_payload),
        (record['items'] != base['items'], lambda: put_items(bits, record['items'])),
        (record['part'] != base['part'], lambda: part_payload(record['part'], base['part'])),
        (record['at'] != base['at'], at_payload),
        (record['pick'] != base['pick'], pick_payload),
    ))
    return bits.hex()


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
            # Infinities have one spelling already; the canonical metric text covers finite numbers only.
            finite = numeric and key in ('value', 'x', 'y', 'z') and isinstance(item, str) and math.isfinite(float(parsed_number(item)))
            result[key] = canonical_text(parsed_number(item)) if finite else normalized(item)
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
    # Requests to the generated code: (root, operation, first, second, limit) and the expected line, or None for any result
    # on which the generated and the interpreted path agree in both ports.
    generated = []
    for index in range(options.count):
        value, data = sample(rng)
        encoded.append(bytes.fromhex(data))
        identifier = f'encode-{index}'
        expected[identifier] = data
        cases.append((identifier, {'sources': [SCHEMA], 'root': 'Sample', 'encode': value, 'decode': data}))
        generated.append((('Sample', 'decode', data, '', None), f'ok {len(data) // 2} {data}'))
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
        generated.append((('Sample', 'decode', data.hex(), '', None), None))
    deltas = {}
    bases = []
    for index in range(options.count):
        base = moving(rng)
        record = moving(rng, base)
        data = moving_delta(record, base)
        identifier = f'delta-{index}'
        full, held = moving_full(record), moving_full(base)
        deltas[identifier] = (data, full)
        bases.append((data, base))
        cases.append((identifier, {
            'sources': [DELTA_SCHEMA], 'root': 'Moving', 'encode': moving_value(record), 'baseline': moving_value(base), 'decode': data,
            'deltaEncode': {'record': full, 'baseline': held}, 'deltaDecode': {'delta': data, 'baseline': held},
        }))
        cases.append((f'{identifier}-full', {'sources': [DELTA_SCHEMA], 'root': 'Moving', 'encode': moving_value(record)}))
        generated.append((('Moving', 'deltaEncode', full, held, None), f'ok {data}'))
        generated.append((('Moving', 'delta', data, held, None), f'ok {len(data) // 2} {full}'))
    # A delta from untrusted data must produce the same record or the same error in both ports.
    for index in range(options.count):
        data, base = rng.choice(bases)
        mutated = bytearray.fromhex(data)
        if index % 4 == 0:
            mutated = bytearray(rng.randrange(256) for _ in range(rng.randrange(40)))
        else:
            for _ in range(rng.randrange(1, 4)):
                mutated[rng.randrange(len(mutated))] ^= 1 << rng.randrange(8)
        cases.append((f'delta-fuzz-{index}', {'sources': [DELTA_SCHEMA], 'root': 'Moving', 'baseline': moving_value(base), 'decode': bytes(mutated).hex()}))
        generated.append((('Moving', 'delta', bytes(mutated).hex(), moving_full(base), None), None))
    # Keyed lists: bytes from the rules, and untrusted deltas that must end alike in both ports.
    keyed = {}
    keyed_bases = []
    keyed_cases = []
    for index in range(options.count):
        base = fleet(rng)
        record = fleet(rng, base)
        data = fleet_delta(record, base)
        identifier = f'keyed-{index}'
        full, held = fleet_full(record), fleet_full(base)
        keyed[identifier] = (data, full)
        keyed_bases.append((data, base))
        keyed_cases.append((identifier, {
            'sources': [KEYED_SCHEMA], 'root': 'Fleet', 'encode': fleet_value(record), 'baseline': fleet_value(base), 'decode': data,
            'deltaEncode': {'record': full, 'baseline': held}, 'deltaDecode': {'delta': data, 'baseline': held},
        }))
        generated.append((('Fleet', 'deltaEncode', full, held, None), f'ok {data}'))
        generated.append((('Fleet', 'delta', data, held, None), f'ok {len(data) // 2} {full}'))
    for index in range(options.count):
        data, base = rng.choice(keyed_bases)
        mutated = bytearray.fromhex(data)
        if not mutated:
            mutated = bytearray(1)
        for _ in range(rng.randrange(1, 4)):
            mutated[rng.randrange(len(mutated))] ^= 1 << rng.randrange(8)
        keyed_cases.append((f'keyed-fuzz-{index}', {'sources': [KEYED_SCHEMA], 'root': 'Fleet', 'baseline': fleet_value(base), 'decode': bytes(mutated).hex()}))
        generated.append((('Fleet', 'delta', bytes(mutated).hex(), fleet_full(base), None), None))
    respelled = ('schema :Force as { force: :Quantity(N) between 0kN and 50kN step 0.01kN }\n', '// other\n\nschema :Force as {\n  force: :Quantity(N) between 0N and 50000N step 10N // comment\n}\n')
    for index, source in enumerate(respelled):
        cases.append((f'respelled-{index}', {'sources': [source], 'root': 'Force', 'bind': False}))
    with tempfile.TemporaryDirectory(dir=REPORTS) as directory:
        temporary = Path(directory)
        corpus = temporary / 'exchange.md'
        corpus.write_text(suite(cases))
        keyed_corpus = temporary / 'keyed.md'
        keyed_corpus.write_text(suite(keyed_cases))
        observations = {}
        for source, name in ((corpus, 'exchange'), (keyed_corpus, 'keyed'), (ROOT / 'conformance/suites/api/schemas.md', 'corpus')):
            requests = temporary / f'{name}-requests.json'
            subprocess.run([sys.executable, 'conformance/runner/main.py', 'requests', '--corpus', str(source), '--output', str(requests)], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
            for port, adapter in adapters.items():
                observations[(port, name)] = observe(adapter, requests, temporary / f'{port}-{name}.json')
    failures = []
    commands = schema_generated_parties.build(REPORTS / 'generated', [('Sample', SCHEMA), ('Moving', DELTA_SCHEMA), ('Fleet', KEYED_SCHEMA)])
    lines = schema_generated_parties.run(commands, [request for request, _ in generated])
    for position, (request, wanted) in enumerate(generated):
        for port, output in lines.items():
            line = output[position]
            if line.startswith('mismatch') or (wanted is not None and line != wanted):
                failures.append(f'generated {port} {request[0]} {request[1]} {request[2]}: expected {wanted or "agreement"}, got {line}')
        if lines['C#'][position] != lines['Swift'][position]:
            failures.append(f'generated {request[0]} {request[1]} {request[2]}: C# {lines["C#"][position]}, Swift {lines["Swift"][position]}')
    generated_accepted = sum(line.startswith('ok') for (_, wanted), line in zip(generated, lines['C#']) if wanted is None)
    for name in ('exchange', 'keyed', 'corpus'):
        different = compare(observations[('csharp', name)], observations[('swift', name)])
        if different:
            failures.append(f'{name}: {len(different)} observations differ, first {different[0]}: ' + difference(normalized(observations[("csharp", name)][different[0]]), normalized(observations[("swift", name)][different[0]])))
    def schema_of(port, identifier):
        for key, value in observations[(port, 'keyed' if identifier.startswith('keyed-') else 'exchange')].items():
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
        for identifier, (data, full) in deltas.items():
            observed = schema_of(port, identifier)
            if observed.get('encoded') != {'bytes': data} or observed.get('deltaEncoded') != {'bytes': data}:
                failures.append(f'{port} {identifier}: expected {data}, got {observed.get("encoded")} and {observed.get("deltaEncoded")}')
                break
            decoded = observed.get('decoded', {})
            if 'error' in decoded or decoded.get('byteCount') != len(data) // 2:
                failures.append(f'{port} {identifier}: decoding failed: {decoded}')
                break
            if observed.get('deltaDecoded') != {'record': full, 'deltaByteCount': len(data) // 2}:
                failures.append(f'{port} {identifier}: expected the record {full}, got {observed.get("deltaDecoded")}')
                break
            if schema_of(port, f'{identifier}-full').get('encoded') != {'bytes': full}:
                failures.append(f'{port} {identifier}: the oracle and the full encoding differ')
                break
        for identifier, (data, full) in keyed.items():
            observed = schema_of(port, identifier)
            if observed.get('encoded') != {'bytes': data} or observed.get('deltaEncoded') != {'bytes': data}:
                failures.append(f'{port} {identifier}: expected {data}, got {observed.get("encoded")} and {observed.get("deltaEncoded")}')
                break
            if observed.get('deltaDecoded') != {'record': full, 'deltaByteCount': len(data) // 2} or 'error' in observed.get('decoded', {}):
                failures.append(f'{port} {identifier}: expected the record {full}, got {observed.get("deltaDecoded")}')
                break
        first, second = schema_of(port, 'respelled-0'), schema_of(port, 'respelled-1')
        if first != second:
            failures.append(f'{port}: respelling changed the canonical form or hash')
    accepted = sum('value' in schema_of('csharp', f'fuzz-{index}').get('decoded', {}) for index in range(options.count))
    accepted_keyed = sum('value' in schema_of('csharp', f'keyed-fuzz-{index}').get('decoded', {}) for index in range(options.count))
    accepted_deltas = sum('value' in schema_of('csharp', f'delta-fuzz-{index}').get('decoded', {}) for index in range(options.count))
    # Negative controls: the comparison notices a changed byte, and a mutated value would not pass as equal.
    if not compare({'a': {'bytes': '00'}}, {'a': {'bytes': '01'}}) or not compare({'a': 1}, {}):
        failures.append('The comparison misses differences.')
    if failures:
        print('\n'.join(failures[:20]), file=sys.stderr)
        return 1
    print(f'Schema bytes verified: {options.count} encodings in both ports, {options.count} fuzzed inputs with equal results ({accepted} accepted), {options.count} deltas, {options.count} fuzzed deltas ({accepted_deltas} accepted), {len(observations[("csharp", "corpus")])} corpus cases.')
    print(f'Keyed lists verified: {options.count} deltas in both ports, {options.count} fuzzed deltas with equal results ({accepted_keyed} accepted).')
    print(f'Generated code verified: {len(generated)} requests in C# and Swift, {sum(wanted is None for _, wanted in generated)} of them untrusted ({generated_accepted} accepted).')
    return 0


if __name__ == '__main__':
    sys.exit(main())
