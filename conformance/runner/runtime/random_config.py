# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Random configuration transport; randomness itself belongs to the implementation."""
import re
import math
from decimal import Decimal
from core.corpus import InvalidDocument, closed
from checks.numeric_checks import number


def prepare(metadata):
    if 'random' not in metadata:
        return {'seed': '0'}
    raw = metadata['random']
    closed(raw, {'seed', 'sequence', 'entropy'}, 'random configuration')
    if len(raw) != 1:
        raise InvalidDocument('Exactly one random configuration required')
    if 'seed' in raw:
        seed = raw['seed']
        if type(seed) is not int or not -(2**63) <= seed < 2**63:
            raise InvalidDocument('Random seed must be signed Int64')
        return {'seed': str(seed)}
    if 'entropy' in raw:
        if not isinstance(raw['entropy'], str) or not re.fullmatch(r'(?:[0-9A-F]{2})+', raw['entropy']):
            raise InvalidDocument('Entropy requires uppercase hexadecimal byte pairs')
    else:
        if not isinstance(raw['sequence'], list) or not raw['sequence']:
            raise InvalidDocument('Random sequence must be nonempty')
        for item in raw['sequence']:
            value = number(item)
            if not math.isfinite(value):
                raise InvalidDocument('Random sequence requires finite Binary64 values')
            decimal = Decimal(repr(value))
            exponent = decimal.adjusted()
            if value == 0:
                canonical = '0'
            elif -4 <= exponent < 17:
                canonical = format(decimal, 'f')
                if '.' in canonical:
                    canonical = canonical.rstrip('0').rstrip('.')
            else:
                mantissa = format(decimal.scaleb(-exponent), 'f').rstrip('0').rstrip('.')
                canonical = mantissa + 'e' + str(exponent)
            if item != canonical:
                raise InvalidDocument('Random sequence requires canonical shortest-roundtrip spelling')
    return raw
