# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Runtime-step protocol and exact typed value oracle."""
import re
from runtime import random_config
from runtime import native_handlers
from runtime import host_actions
from checks import lifecycle_checks
from checks import trace_checks
from core.corpus import InvalidDocument, closed
from checks.compiler_checks import exact, diagnostic
from checks import numeric_checks as numeric

LIMITS = {'maxProcessedEventsPerRun', 'maxQueuedMessagesPerRun', 'maxExecutionSteps', 'maxRegisterValues',
          'maxLoopIterations', 'maxCallDepth', 'maxRandomScopeDepth', 'maxRangeItems',
          'maxGeneratedCollectionItems', 'maxDiceCount', 'maxDiceSides'}


def value(raw):
    if not isinstance(raw, dict):
        raise InvalidDocument('Expected typed value')
    kind = raw.get('type')
    if kind in (':Vector', ':Point'):
        if set(raw) not in ({'type', 'x', 'y', 'z'}, {'type', 'x', 'y', 'z', 'unit'}):
            raise InvalidDocument('Invalid spatial value fields')
        for axis in ('x', 'y', 'z'):
            numeric.number(raw[axis])
        result = dict(raw)
        if 'unit' in raw:
            if not isinstance(raw['unit'], str) or raw['unit'] not in numeric.UNITS:
                raise InvalidDocument('Invalid spatial unit')
            result['unit'] = numeric.UNITS[raw['unit']]
        return result
    if kind in (':Range.int64', ':Range.binary64'):
        if set(raw) != {'type', 'from', 'to', 'step'}:
            raise InvalidDocument('Invalid range fields')
        result = dict(raw)
        for key in ('from', 'to', 'step'):
            if kind == ':Range.int64':
                result[key] = value({'type': ':Number.int64', 'value': raw[key]})['value']
            else:
                numeric.number(raw[key])
        return result
    if kind == ':Message':
        if set(raw) != {'type', 'message'}:
            raise InvalidDocument('Invalid message value fields')
        return {'type': kind, 'message': message(raw['message'])}
    custom = isinstance(kind, str) and re.fullmatch(r':[A-Z][A-Za-z0-9]*', kind) and 'entries' in raw and kind != ':Map'
    if custom:
        if set(raw) != {'type', 'entries'}:
            raise InvalidDocument('Invalid record fields')
        return {'type': kind, 'entries': value({'type': ':Map', 'entries': raw['entries']})['entries']}

    field = {':Nothing': None, ':Text': 'value', ':Boolean': 'value', ':Number.int64': 'value',
             ':List': 'items', ':Map': 'entries', ':Tag': 'value', ':Dice': 'rolls',
             ':Number.binary64': 'value', ':Quantity.int64': 'value', ':Quantity.binary64': 'value', ':Percentage': 'value'}.get(kind, 'unsupported')
    if field == 'unsupported':
        raise InvalidDocument('Unsupported runtime value type: ' + str(kind))
    fields = ({'type', field} if field else {'type'}) | ({'unit'} if kind.startswith(':Quantity.') else set())
    if set(raw) != fields:
        raise InvalidDocument('Invalid typed value fields')
    result = dict(raw)
    if kind.startswith(':Quantity.'):
        if not isinstance(raw['unit'], str) or raw['unit'] not in numeric.UNITS:
            raise InvalidDocument('Invalid quantity unit')
        result['unit'] = numeric.UNITS[raw['unit']]
    if kind in numeric.FLOAT_TYPES:
        numeric.number(raw['value'])
    elif kind in (':Number.int64', ':Quantity.int64'):
        text = raw['value']
        if not isinstance(text, str) or not re.fullmatch(r'[+-]?[0-9]+', text) or not -(2**63) <= int(text) < 2**63:
            raise InvalidDocument('Invalid Int64 transport')
        result['value'] = str(int(text))
    elif kind == ':Boolean' and type(raw['value']) is bool:
        result['value'] = 'true' if raw['value'] else 'false'
    elif kind in (':Text', ':Tag', ':Boolean'):
        if not isinstance(raw['value'], str) or (kind == ':Boolean' and raw['value'] not in ('true', 'false')):
            raise InvalidDocument('Invalid scalar transport')
    elif kind == ':Dice':
        if not isinstance(raw['rolls'], list) or any(type(x) is not int or not 0 < x < 2**31 for x in raw['rolls']):
            raise InvalidDocument('Invalid dice rolls')
    elif kind == ':List':
        if not isinstance(raw['items'], list):
            raise InvalidDocument('Invalid list transport')
        result['items'] = [value(x) for x in raw['items']]
    elif kind == ':Map':
        if not isinstance(raw['entries'], list):
            raise InvalidDocument('Invalid map transport')
        entries = {}
        for item in raw['entries']:
            closed(item, {'key', 'value'}, 'map entry')
            key = item.get('key')
            if not isinstance(key, str):
                raise InvalidDocument('Invalid map key')
            entries[key] = value(item.get('value'))
        result['entries'] = [{'key': key, 'value': entries[key]} for key in sorted(entries)]
    return result


def message(raw):
    closed(raw, {'name', 'args', 'tags'}, 'message')
    if not isinstance(raw.get('name'), str) or not raw['name']:
        raise InvalidDocument('Missing message name')
    args, tags = raw.get('args', []), raw.get('tags', [])
    if not isinstance(args, list) or not isinstance(tags, list) or any(not isinstance(x, str) for x in tags):
        raise InvalidDocument('Invalid message arguments/tags')
    result = []
    for arg in args:
        closed(arg, {'name', 'value'}, 'argument')
        if not isinstance(arg.get('name'), str):
            raise InvalidDocument('Invalid argument name')
        result.append({'name': arg['name'], 'value': value(arg.get('value'))})
    return {'name': raw['name'], 'tags': tags, 'args': result}


def limit(raw, expectation=False):
    if expectation and isinstance(raw, dict) and set(raw) == {'any'} and raw['any'] is True:
        return raw
    closed(raw, {'name', 'limit'}, 'runtime limit')
    if not raw or (not expectation and set(raw) != {'name', 'limit'}):
        raise InvalidDocument('Incomplete runtime limit')
    if 'name' in raw and (not isinstance(raw['name'], str) or not raw['name']):
        raise InvalidDocument('Invalid runtime limit name')
    if 'limit' in raw and (type(raw['limit']) is not int or not -(2**31) <= raw['limit'] < 2**31):
        raise InvalidDocument('Invalid runtime limit value')
    return raw


def host_count(case):
    count = case.metadata.get('hostCount', 1)
    if type(count) is not int or not 1 <= count <= 256:
        raise InvalidDocument('hostCount must be between 1 and 256')
    return count


def phase_expectation(case, exp, initialization=False):
    allowed = {'local', 'outbound', 'runtimeLimits', 'diagnostics', 'hostReady', 'programStarts', 'idleDurationMicroseconds', 'trace'}
    allowed |= {'pump'} if initialization else {'input', 'accepted', 'paused', 'waiting'}
    closed(exp, allowed, 'supported runtime expectation')
    lifecycle_checks.validate(case, exp)
    if 'trace' in exp:
        trace_checks.normalize(exp['trace'], message, limit, expectation=True)
    diagnostics = exp.get('diagnostics', [])
    if not isinstance(diagnostics, list):
        raise InvalidDocument('Expected diagnostic sequence')
    for item in diagnostics:
        diagnostic(item, expectation=True, phases=('runtime',))
    input_value = exp.get('input', {})
    closed(input_value, {'args', 'tags'}, 'step input')
    for key in ('accepted', 'paused', 'waiting'):
        if key in exp and type(exp[key]) is not bool:
            raise InvalidDocument('Expected Boolean step constraint')
    for key in ('local', 'outbound'):
        if not isinstance(exp.get(key, []), list):
            raise InvalidDocument('Expected message sequence')
        for item in exp.get(key, []):
            message(item)
    constraints = exp.get('runtimeLimits', {})
    closed(constraints, {'include', 'exclude'}, 'limit constraints')
    for items in constraints.values():
        if not isinstance(items, list):
            raise InvalidDocument('Expected limit sequence')
        for item in items:
            limit(item, expectation=True)

    return input_value


def prepare(case):
    numeric.options(case)
    publish_sink = case.metadata.get('publishSink', 'accept')
    if publish_sink not in ('accept', 'absent', 'reject', 'throw'):
        raise InvalidDocument('Unknown publish sink mode')
    initialization = case.expected.get('initialization', {})
    phase_expectation(case, initialization, initialization=True)
    initialization_pump = initialization.get('pump', 'completion')
    if initialization_pump not in ('start', 'completion'):
        raise InvalidDocument('Invalid initialization pump')
    limits = case.metadata.get('runtimeLimits', {})
    closed(limits, LIMITS, 'supported runtime limits')
    for number in limits.values():
        if type(number) is not int or not 0 < number < 2**31:
            raise InvalidDocument('Invalid runtime limit')
    if limits.get('maxRandomScopeDepth', 0) > 65535:
        raise InvalidDocument('Random scope depth exceeds portable bound')
    expected = case.expected.get('steps', {})
    if set(expected) - {s['step'] for s in case.steps}:
        raise InvalidDocument('Unknown runtime step')
    steps = []
    for step in case.steps:
        if step['pump'] not in ('completion', 'enqueue', 'frame', 'frames'):
            raise InvalidDocument('Unknown pump mode')
        try:
            budget = int(step['budget']) if step['pump'] in ('frame', 'frames') else None
        except (ValueError, TypeError, KeyError) as error:
            raise InvalidDocument('Invalid frame budget') from error
        if budget is not None and not 0 < budget < 2**31:
            raise InvalidDocument('Invalid frame budget')
        exp = expected.get(step['step'], {})
        input_value = phase_expectation(case, exp)
        steps.append({'id': step['step'], 'input': message({'name': step['receive'], **input_value}),
                      'pump': step['pump'], 'budget': budget})
    native = native_handlers.prepare(case.metadata, message)
    lifecycle = host_actions.prepare(case, native)
    return {**lifecycle, 'publishSink': publish_sink, 'initializationPump': initialization_pump, 'limits': limits, 'steps': steps, 'random': random_config.prepare(case.metadata), 'hostCount': host_count(case),
            'nativeHandlers': native}


def compare(case, actual):
    closed(actual, {'hosts'}, 'runtime observations')
    hosts = actual.get('hosts')
    if not isinstance(hosts, list) or len(hosts) != host_count(case):
        raise InvalidDocument('Missing or extra host observations')
    differences = []
    for index, host in enumerate(hosts):
        for difference in compare_host(case, host):
            differences.append({**difference, 'path': '/hosts/' + str(index) + difference['path']})
    return differences


def compare_host(case, actual):
    closed(actual, {'initialization', 'steps'}, 'runtime observations')
    steps = actual.get('steps')
    if not isinstance(steps, dict) or set(steps) != {s['step'] for s in case.steps}:
        raise InvalidDocument('Missing or extra runtime steps')
    differences = []
    comparison = numeric.options(case)

    def check(path, expected, observed):
        if not numeric.equal(expected, observed, comparison):
            differences.append({'path': path, 'expected': expected, 'actual': observed})

    def channels(path, exp, obs, initialization=False, step=None):
        fields = {'local', 'outbound', 'diagnostics', 'runtimeLimits'} if initialization else {
            'local', 'outbound', 'diagnostics', 'runtimeLimits', 'accepted', 'paused', 'waiting'}
        fields.update({'trace', 'actions', 'hostReady', 'programStarts', 'idleDurationMicroseconds'})
        closed(obs, fields, 'runtime phase')
        if set(obs) != fields:
            raise InvalidDocument('Incomplete runtime observations')
        for key in fields - {'local', 'outbound', 'diagnostics', 'runtimeLimits', 'actions', 'programStarts', 'idleDurationMicroseconds', 'trace'}:
            if type(obs[key]) is not bool:
                raise InvalidDocument('Invalid runtime state')
        observed_trace = trace_checks.normalize(obs['trace'], message, limit)
        if 'trace' in exp:
            trace_checks.compare(trace_checks.normalize(exp['trace'], message, limit, expectation=True), observed_trace,
                                 lambda suffix, expected, actual: check(path + suffix, expected, actual))
        lifecycle_checks.compare(case, exp, obs, lambda suffix, expected, actual: check(path + suffix, expected, actual))
        host_actions.compare(case, obs['actions'], step, lambda suffix, expected, actual: check(path + suffix, expected, actual))
        for key in ('local', 'outbound'):
            if not isinstance(obs[key], list):
                raise InvalidDocument('Invalid observed channel')
            check(path + '/' + key, [message(m) for m in exp.get(key, [])], [message(m) for m in obs[key]])
        if not isinstance(obs['diagnostics'], list):
            raise InvalidDocument('Invalid runtime diagnostics')
        for item in obs['diagnostics']:
            diagnostic(item, phases=('runtime',))
        expected_diagnostics = exp.get('diagnostics', [])
        check(path + '/diagnostics/count', len(expected_diagnostics), len(obs['diagnostics']))
        for index, (expected, observed) in enumerate(zip(expected_diagnostics, obs['diagnostics'])):
            for key, val in expected.items():
                check(path + '/diagnostics/' + str(index) + '/' + key, val, observed.get(key))
        if not isinstance(obs['runtimeLimits'], list):
            raise InvalidDocument('Invalid runtime limit observations')
        limits = [limit(x) for x in obs['runtimeLimits']]
        constraints = exp.get('runtimeLimits', {})
        cursor = 0
        for item in constraints.get('include', []):
            match = next((i for i in range(cursor, len(limits)) if (item.get('any') is True or all(exact(limits[i].get(k), v) for k, v in item.items()))), None)
            check(path + '/runtimeLimits/include', True, match is not None)
            if match is not None:
                cursor = match + 1
        for item in constraints.get('exclude', []):
            check(path + '/runtimeLimits/exclude', False, any((item.get('any') is True or all(exact(x.get(k), v) for k, v in item.items())) for x in limits))
        if not initialization:
            check(path + '/accepted', exp.get('accepted', True), obs['accepted'])
            for key in ('paused', 'waiting'):
                if key in exp:
                    check(path + '/' + key, exp[key], obs[key])

    channels('/initialization', case.expected.get('initialization', {}), actual.get('initialization'), True)
    for step in case.steps:
        key = step['step']
        channels('/steps/' + key, case.expected.get('steps', {}).get(key, {}), steps[key], step=key)
    return differences
