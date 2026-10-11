# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Independent performance schema and bound oracle; no timing or reference echo."""
from decimal import Decimal
import math
from core.corpus import ID, InvalidDocument, closed
from checks.numeric_checks import number

UNITS = {'ns', 'us', 'ms', 's', 'B', 'KiB', 'count'}
UNITS |= {unit + '/iteration' for unit in tuple(UNITS)}
BOUNDS = {'maximum', 'toleranceRelative', 'toleranceAbsolute'}


def nonnegative(raw):
    if type(raw) not in (int, float, Decimal):
        raise InvalidDocument('Performance bound must be a number')
    try:
        value = float(raw)
    except (OverflowError, ValueError) as error:
        raise InvalidDocument('Invalid performance number') from error
    if not math.isfinite(value) or value < 0:
        raise InvalidDocument('Performance number must be finite and nonnegative')
    return value


def identifier(raw):
    if not isinstance(raw, str) or not ID.fullmatch(raw):
        raise InvalidDocument('Invalid performance identifier')


def profiles(case):
    raw = case.expected.get('performance')
    closed(raw, {'profiles'}, 'performance expectation')
    items = raw.get('profiles')
    if not isinstance(items, dict) or not items:
        raise InvalidDocument('Performance profiles required')
    for profile, data in items.items():
        identifier(profile)
        closed(data, {'metrics'}, 'performance profile')
        metrics = data.get('metrics')
        if not isinstance(metrics, dict) or not metrics:
            raise InvalidDocument('Performance metrics required')
        for name, metric in metrics.items():
            identifier(name)
            closed(metric, {'reference', 'unit'} | BOUNDS, 'performance metric')
            if not isinstance(metric.get('unit'), str) or metric['unit'] not in UNITS or 'reference' not in metric or not (metric.keys() & BOUNDS):
                raise InvalidDocument('Performance metric requires reference, unit and bound')
            for key in metric.keys() - {'unit'}:
                nonnegative(metric[key])
            if not math.isfinite(upper_bound(metric)):
                raise InvalidDocument('Performance bound overflows Binary64')
    return items


def workload(case):
    raw = case.metadata.get('performance')
    closed(raw, {'iterations', 'warmupIterations', 'compileWarmupIterations', 'observeRuntime'}, 'performance workload')
    result = {'iterations': raw.get('iterations'), 'warmupIterations': raw.get('warmupIterations', 0),
              'compileWarmupIterations': raw.get('compileWarmupIterations', 0), 'observeRuntime': raw.get('observeRuntime', True)}
    for key in ('iterations', 'warmupIterations', 'compileWarmupIterations'):
        if type(result[key]) is not int or not 0 <= result[key] < 2**32:
            raise InvalidDocument('Performance iteration count must be UInt32')
    if not result['iterations'] or type(result['observeRuntime']) is not bool:
        raise InvalidDocument('Positive iterations and Boolean observer flag required')
    return result


def prepare(case, profile):
    """Return measurement directives only; baselines never cross this boundary."""
    result = workload(case)
    expected = profiles(case)
    identifier(profile)
    if profile not in expected:
        raise InvalidDocument('conformance.runner.missingPerformanceProfile')
    return {**result, 'profile': profile, 'metrics': [{'id': name, 'unit': metric['unit']} for name, metric in expected[profile]['metrics'].items()]}


def upper_bound(metric):
    reference = nonnegative(metric['reference'])
    bounds = []
    if 'maximum' in metric:
        bounds.append(nonnegative(metric['maximum']))
    if 'toleranceRelative' in metric:
        bounds.append(reference * (1.0 + nonnegative(metric['toleranceRelative'])))
    if 'toleranceAbsolute' in metric:
        bounds.append(reference + nonnegative(metric['toleranceAbsolute']))
    return min(bounds)


def compare(case, profile, observed):
    """Check raw metrics; callers must first verify correctness and measurement provenance."""
    request = prepare(case, profile)
    closed(observed, {'profile', 'metrics'}, 'performance observation')
    if observed.get('profile') != profile or not isinstance(observed.get('metrics'), list):
        raise InvalidDocument('Performance observation profile mismatch')
    actual = {}
    for metric in observed['metrics']:
        closed(metric, {'id', 'unit', 'value'}, 'measured metric')
        name = metric.get('id')
        identifier(name)
        if name in actual:
            raise InvalidDocument('Duplicate measured metric')
        measured = number(metric.get('value'))
        if not math.isfinite(measured) or measured < 0:
            raise InvalidDocument('Measurement must be finite and nonnegative')
        actual[name] = (metric.get('unit'), measured)
    if set(actual) != {metric['id'] for metric in request['metrics']}:
        raise InvalidDocument('Missing or unexpected measured metric')
    differences = []
    for name, metric in profiles(case)[profile]['metrics'].items():
        unit, measured = actual[name]
        if unit != metric['unit']:
            raise InvalidDocument('Measured unit mismatch')
        allowed = upper_bound(metric)
        if measured > allowed:
            differences.append({'path': '/performance/' + name, 'maximum': allowed, 'actual': measured, 'unit': unit})
    return ('failed' if differences else 'passed'), differences
