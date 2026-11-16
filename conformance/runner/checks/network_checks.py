# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""GES network sessions: scripted steps over two connections and exact traces of events and packets."""
import re
from core.corpus import InvalidDocument, closed

HEX = re.compile(r'(?:[0-9a-f]{2})*')
SIDES = ('client', 'server')
STATES = {'hello', 'awaitHello', 'awaitAccept', 'connected', 'closed'}
OPTIONS = {'transport', 'maxPacketBytes', 'maxPendingReceiveBytes', 'maxFramesPerSecond', 'maxSendQueueBytes',
           'timeoutMicroseconds', 'keepaliveMicroseconds', 'ackDelayMicroseconds', 'sendBudgetBytesPerSecond'}
STEPS = {'advance', 'accept', 'refuse', 'close', 'send', 'receive', 'transmit', 'transportClosed', 'budget'}
SIDE_FIELDS = {'state', 'roundTripMicroseconds', 'queuedSendBytes', 'pendingReceiveBytes'}
MAX_TIME = 2**62


def integer(raw, label, low=0, high=MAX_TIME):
    if type(raw) is not int or not low <= raw <= high:
        raise InvalidDocument('Expected an integer: ' + label)
    return raw


def text(raw, label):
    if not isinstance(raw, str):
        raise InvalidDocument('Expected text: ' + label)
    return raw


def hexadecimal(raw, label):
    if not isinstance(raw, str) or not HEX.fullmatch(raw):
        raise InvalidDocument('Expected lowercase hexadecimal bytes: ' + label)
    return raw


def side(raw):
    if raw not in SIDES:
        raise InvalidDocument('Expected client or server')
    return raw


def sources(raw, label):
    if not isinstance(raw, list) or not raw or any(not isinstance(item, str) for item in raw):
        raise InvalidDocument('Expected schema sources: ' + label)
    return raw


def options(raw):
    closed(raw, OPTIONS, 'network options')
    result = dict(raw)
    if 'transport' in result and result['transport'] not in ('message', 'stream'):
        raise InvalidDocument('A transport is message or stream')
    for key in OPTIONS - {'transport'}:
        if key in result:
            integer(result[key], key, 0, 2**53)
    return result


def payload(raw):
    """A payload is hexadecimal bytes or a generated run whose byte i is (i + seed) mod 251."""
    if isinstance(raw, str):
        return {'hex': hexadecimal(raw, 'payload')}
    closed(raw, {'length', 'seed'}, 'generated payload')
    return {'length': integer(raw.get('length'), 'payload length', 0, 1 << 20), 'seed': integer(raw.get('seed', 0), 'payload seed', 0, 250)}


def step(raw):
    if not isinstance(raw, dict) or len(raw) != 1 or set(raw) - STEPS:
        raise InvalidDocument('A network step has exactly one known action')
    (name, value), = raw.items()
    if name == 'advance':
        return {'action': name, 'now': integer(value, 'advance')}
    if name == 'accept':
        if value is not True:
            raise InvalidDocument('accept: true')
        return {'action': name}
    if name == 'refuse':
        return {'action': name, 'detail': text(value, 'refuse detail')}
    if name in ('transmit', 'transportClosed'):
        return {'action': name, 'side': side(value)}
    closed(value, {'side', 'detail', 'route', 'payload', 'bytes', 'bytesPerSecond'}, name + ' step')
    result = {'action': name, 'side': side(value.get('side'))}
    if name == 'close':
        closed(value, {'side', 'detail'}, 'close step')
        result['detail'] = text(value.get('detail', ''), 'close detail')
    elif name == 'send':
        closed(value, {'side', 'route', 'payload'}, 'send step')
        result['route'] = integer(value.get('route'), 'route', -1, 1 << 20)
        result['payload'] = payload(value.get('payload'))
    elif name == 'receive':
        closed(value, {'side', 'bytes'}, 'receive step')
        result['bytes'] = hexadecimal(value.get('bytes'), 'received bytes')
    else:
        closed(value, {'side', 'bytesPerSecond'}, 'budget step')
        result['bytesPerSecond'] = integer(value.get('bytesPerSecond'), 'budget', -1, 2**53)
    return result


def prepare(case):
    if case.steps or case.assembler is not None or case.sources:
        raise InvalidDocument('A network case holds its schema sources in networkApi')
    raw = case.metadata.get('networkApi')
    closed(raw, {'sources', 'clientSources', 'network', 'options', 'clientOptions', 'token', 'link', 'steps'}, 'network input')
    link = raw.get('link', 'loopback')
    if link not in ('loopback', 'none'):
        closed(link, {'seed', 'latencyMicroseconds', 'jitterMicroseconds'}, 'simulated link')
        link = {'seed': integer(link.get('seed', 0), 'seed', -2**63, 2**63 - 1),
                'latencyMicroseconds': integer(link.get('latencyMicroseconds', 0), 'latency'),
                'jitterMicroseconds': integer(link.get('jitterMicroseconds', 0), 'jitter')}
    steps = raw.get('steps')
    if not isinstance(steps, list) or not steps:
        raise InvalidDocument('A network case has steps')
    server_sources = sources(raw.get('sources'), 'sources')
    request = {
        'sources': server_sources,
        'clientSources': sources(raw['clientSources'], 'client sources') if 'clientSources' in raw else server_sources,
        'network': text(raw.get('network'), 'network'),
        'options': options(raw.get('options', {})),
        'token': hexadecimal(raw.get('token', ''), 'token'),
        'link': link,
        'steps': [step(item) for item in steps],
    }
    request['clientOptions'] = options(raw['clientOptions']) if 'clientOptions' in raw else request['options']
    closed(case.expected, {'gesBlock', 'network'}, 'network expectation')
    expected = case.expected.get('network')
    closed(expected, {'error', 'trace', 'client', 'server'}, 'network result')
    if 'error' in expected:
        if set(expected) != {'error'} or not isinstance(expected['error'], dict):
            raise InvalidDocument('A setup error stands alone')
        closed(expected['error'], {'code'}, 'network setup error')
        text(expected['error'].get('code'), 'setup error code')
    elif 'trace' not in expected:
        raise InvalidDocument('A network expectation has a trace or a setup error')
    if 'trace' in expected and (not isinstance(expected['trace'], list) or any(not isinstance(line, str) for line in expected['trace'])):
        raise InvalidDocument('A trace is a list of lines')
    for name in SIDES:
        if name in expected:
            side_result(expected[name])
    return request


def side_result(raw):
    closed(raw, SIDE_FIELDS, 'connection result')
    if 'state' in raw and raw['state'] not in STATES:
        raise InvalidDocument('Unknown connection state')
    for key in SIDE_FIELDS - {'state'}:
        if key in raw:
            integer(raw[key], key, -1)
    return raw


def compare(case, observed):
    closed(observed, {'status', 'detail', 'network'}, 'network observation')
    if observed.get('status') == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid network error')
        return 'error', []
    if set(observed) != {'status', 'network'} or observed['status'] != 'ok':
        raise InvalidDocument('Invalid network outcome')
    actual = observed['network']
    closed(actual, {'error', 'trace', 'client', 'server'}, 'network observation')
    expected = case.expected['network']
    if 'error' in actual:
        if set(actual) != {'error'} or not isinstance(actual['error'], dict) or not isinstance(actual['error'].get('code'), str):
            raise InvalidDocument('Invalid network setup error')
    else:
        if set(actual) != {'trace', 'client', 'server'} or not isinstance(actual['trace'], list) or any(not isinstance(line, str) for line in actual['trace']):
            raise InvalidDocument('Incomplete network observation')
        for name in SIDES:
            side_result(actual[name])
            if set(actual[name]) != SIDE_FIELDS:
                raise InvalidDocument('Incomplete connection observation')
    differences = []
    if 'error' in expected or 'error' in actual:
        if expected.get('error') != actual.get('error'):
            differences.append({'path': '/network/error', 'expected': expected.get('error'), 'actual': actual.get('error')})
        return ('passed', []) if not differences else ('failed', differences)
    if actual['trace'] != expected['trace']:
        differences.append({'path': '/network/trace', 'expected': expected['trace'], 'actual': actual['trace']})
    for name in SIDES:
        for key, wanted in expected.get(name, {}).items():
            if actual[name].get(key) != wanted:
                differences.append({'path': f'/network/{name}/{key}', 'expected': wanted, 'actual': actual[name].get(key)})
    return ('passed', []) if not differences else ('failed', differences)
