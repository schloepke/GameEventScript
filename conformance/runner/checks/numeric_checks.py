# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Binary64 transport and bit/ULP comparisons, independent of GES implementations."""
import math
import re
import struct
from core.corpus import InvalidDocument, closed
from checks.compiler_checks import exact

FLOAT_TYPES = {':Number.binary64', ':Quantity.binary64', ':Percentage'}
# Each unit is written by its symbol, ASCII spelling, portable name or `:name`; all normalize to `:name`.
UNIT_SPELLINGS = {
    'degree': ('°', 'deg'), 'meter': ('m',), 'second': ('s',), 'meterPerSecond': ('m/s',),
    'meterPerSecondSquared': ('m/s²', 'm/s^2'), 'kilogram': ('kg',), 'newton': ('N',), 'joule': ('J',),
    'watt': ('W',), 'volt': ('V',), 'ampere': ('A',), 'hertz': ('Hz',), 'kelvin': ('K',),
    'degreePerSecond': ('°/s', 'deg/s'),
}
UNITS = {spelling: ':' + name for name, spellings in UNIT_SPELLINGS.items() for spelling in (name, ':' + name, *spellings)}


def number(text):
    if not isinstance(text, str):
        raise InvalidDocument('Binary64 transport requires a string')
    if text in ('NaN', 'Infinity', '-Infinity'):
        return float(text)
    if not re.fullmatch(r'[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?', text):
        raise InvalidDocument('Invalid Binary64 text')
    result = float(text)
    if not math.isfinite(result):
        raise InvalidDocument('Binary64 decimal overflow; use explicit Infinity')
    return result


def options(case):
    comparison = case.metadata.get('comparison', {})
    closed(comparison, {'binary64'}, 'comparison')
    config = comparison.get('binary64', {'mode': 'exact'})
    closed(config, {'mode', 'maxUlps'}, 'binary64 comparison')
    mode = config.get('mode')
    if mode == 'exact':
        if set(config) != {'mode'}:
            raise InvalidDocument('Exact comparison cannot specify maxUlps')
    elif mode == 'ulp':
        count = config.get('maxUlps')
        if type(count) is not int or not 0 <= count < 2**64:
            raise InvalidDocument('Invalid ULP tolerance')
    else:
        raise InvalidDocument('Invalid Binary64 comparison mode')
    return config


def bits(value):
    return struct.unpack('>Q', struct.pack('>d', value))[0]


def ordered(value):
    word = bits(value)
    return (~word & (2**64 - 1)) if word >> 63 else word | (1 << 63)


def floating_equal(left, right, config):
    if math.isnan(left) or math.isnan(right):
        return math.isnan(left) and math.isnan(right)
    if config['mode'] == 'exact':
        return bits(left) == bits(right)
    if left == right:
        return True
    if math.isinf(left) or math.isinf(right):
        return False
    return abs(ordered(left) - ordered(right)) <= config['maxUlps']


def equal(left, right, config):
    if isinstance(left, dict):
        kind = left.get('type')
        fields = ('value',) if kind in FLOAT_TYPES else ('x', 'y', 'z') if kind in (':Vector', ':Point') else ('from', 'to', 'step') if kind == ':Range.binary64' else ()
        if fields:
            numbers = [number(left[field]) for field in fields]
            # The shared corpus uses NaN as the expected spelling of invalid mathematics.
            if any(math.isnan(x) for x in numbers):
                return right == {'type': ':Nothing'}
            if not isinstance(right, dict) or left.keys() != right.keys():
                return False
            return (all(exact(v, right[k]) for k, v in left.items() if k not in fields)
                    and all(floating_equal(x, number(right[field]), config) for field, x in zip(fields, numbers)))
    if isinstance(left, dict):
        return isinstance(right, dict) and left.keys() == right.keys() and all(equal(v, right[k], config) for k, v in left.items())
    if isinstance(left, list):
        return isinstance(right, list) and len(left) == len(right) and all(equal(a, b, config) for a, b in zip(left, right))
    return exact(left, right)


def canonical_text(value):
    """Finite metric/Conformance number spelling, independent of product text formatting."""
    value = float(value)
    if not math.isfinite(value):
        raise InvalidDocument('Non-finite metric')
    if value == 0:
        return '0'
    mantissa, separator, exponent = repr(value).partition('e')
    if mantissa.endswith('.0'):
        mantissa = mantissa[:-2]
    return mantissa + ('e' + str(int(exponent)) if separator else '')


def validate_authored(node):
    """Validate canonical numeric payloads, leaving Text and opaque JSON untouched."""
    if isinstance(node, list):
        for item in node:
            validate_authored(item)
    elif isinstance(node, dict):
        kind = node.get('type')
        fields = (('value',) if kind in FLOAT_TYPES | {':Number.int64', ':Quantity.int64'} else
                  ('x', 'y', 'z') if kind in (':Vector', ':Point') else
                  ('from', 'to', 'step') if kind in (':Range.int64', ':Range.binary64') else ())
        for field in fields:
            text = node.get(field)
            if not isinstance(text, str):
                raise InvalidDocument('Numeric payload must be canonical text')
            if kind in (':Number.int64', ':Quantity.int64', ':Range.int64'):
                valid = re.fullmatch(r'0|-?[1-9][0-9]*', text) and -(2**63) <= int(text) < 2**63
            else:
                valid = text in ('NaN', 'Infinity', '-Infinity') or canonical_text(number(text)) == text
            if not valid:
                raise InvalidDocument('Noncanonical authored numeric payload')
        for item in node.values():
            validate_authored(item)
