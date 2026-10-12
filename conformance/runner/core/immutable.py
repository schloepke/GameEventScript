# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Read-only normalized containers; deepcopy explicitly creates editable working copies."""
from copy import deepcopy


def readonly(*args, **kwargs):
    raise TypeError('Normalized Conformance input is immutable; copy it before editing')


class FrozenDict(dict):
    __setitem__ = __delitem__ = clear = pop = popitem = setdefault = update = __ior__ = readonly

    def __deepcopy__(self, memo):
        return {deepcopy(k, memo): deepcopy(v, memo) for k, v in self.items()}


class FrozenList(list):
    __setitem__ = __delitem__ = append = clear = extend = insert = pop = remove = reverse = sort = __iadd__ = __imul__ = readonly

    def __deepcopy__(self, memo):
        return [deepcopy(v, memo) for v in self]


def freeze(value):
    if isinstance(value, dict):
        return FrozenDict((key, freeze(item)) for key, item in value.items())
    if isinstance(value, list):
        return FrozenList(freeze(item) for item in value)
    return value
