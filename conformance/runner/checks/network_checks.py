# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""GES network sessions: scripted steps over two connections and exact traces of events and packets."""
import re
from core.corpus import InvalidDocument, closed

HEX = re.compile(r'(?:[0-9a-f]{2})*')
SIDES = ('client', 'server')
STATES = {'hello', 'awaitHello', 'awaitAccept', 'connected', 'closed'}
OPTIONS = {'transport', 'maxPacketBytes', 'maxPendingReceiveBytes', 'maxFramesPerSecond', 'maxSendQueueBytes',
           'timeoutMicroseconds', 'keepaliveMicroseconds', 'ackDelayMicroseconds', 'sendBudgetBytesPerSecond', 'maxBaselineBytes',
           'maxTransferBytes', 'deflateTransfers', 'clientSecurity', 'serverSecurity', 'key', 'serverPublicKey', 'testKeySeed'}
# The corpus default: a case states the security it tests, while the API requires one on each side.
DEFAULT_SECURITY = 'none'
SECURITY = ('none', 'encrypted', 'verified')
CLIENT_SECURITY = ('none', 'development', 'pinned')
SERVER_SECURITY = ('none', 'key')
STEPS = {'advance', 'accept', 'refuse', 'close', 'send', 'receive', 'transmit', 'transportClosed', 'budget',
         'publish', 'subscribe', 'unsubscribe', 'deliver', 'cookie', 'cancel', 'beginTick', 'include', 'endTick'}
TRANSPORTS = ('message', 'stream', 'datagram')
LINK_FIELDS = {'seed', 'latencyMicroseconds', 'jitterMicroseconds', 'lossPerMille', 'duplicatePerMille', 'reorder'}
SIDE_FIELDS = {'state', 'security', 'roundTripMicroseconds', 'queuedSendBytes', 'pendingReceiveBytes'}
TEXT_SIDE_FIELDS = {'state', 'security'}
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
    if 'transport' in result and result['transport'] not in TRANSPORTS:
        raise InvalidDocument('A transport is message, stream or datagram')
    if 'deflateTransfers' in result and type(result['deflateTransfers']) is not bool:
        raise InvalidDocument('deflateTransfers is true or false')
    result.setdefault('clientSecurity', DEFAULT_SECURITY)
    result.setdefault('serverSecurity', DEFAULT_SECURITY)
    if result['clientSecurity'] not in CLIENT_SECURITY:
        raise InvalidDocument('A client security is none, development or pinned')
    if result['serverSecurity'] not in SERVER_SECURITY:
        raise InvalidDocument('A server security is none or key')
    if ('serverPublicKey' in result) != (result['clientSecurity'] == 'pinned'):
        raise InvalidDocument('A pinned client security and only it has a serverPublicKey')
    if ('key' in result) != (result['serverSecurity'] == 'key'):
        raise InvalidDocument('A key server security and only it has a key')
    for key in ('key', 'serverPublicKey'):
        if key in result and len(hexadecimal(result[key], key)) != 64:
            raise InvalidDocument('A key holds 32 bytes: ' + key)
    if 'testKeySeed' in result:
        integer(result['testKeySeed'], 'testKeySeed', 0, 2**64 - 1)
    for key in OPTIONS - {'transport', 'deflateTransfers', 'clientSecurity', 'serverSecurity', 'key', 'serverPublicKey', 'testKeySeed'}:
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
    if name in ('transmit', 'transportClosed', 'beginTick', 'endTick'):
        return {'action': name, 'side': side(value)}
    if name == 'deliver':
        # Passes packets of the other side's last transmit step, by index, in the listed order.
        closed(value, {'side', 'packets'}, 'deliver step')
        packets = value.get('packets')
        if not isinstance(packets, list) or any(type(index) is not int or not 0 <= index < 1024 for index in packets):
            raise InvalidDocument('A deliver step lists packet indexes')
        return {'action': name, 'side': side(value.get('side')), 'packets': packets}
    if name == 'cookie':
        # Checks a datagram at an address validation gate: hexadecimal bytes or a packet of the client's last transmit.
        closed(value, {'key', 'windowMicroseconds', 'now', 'address', 'bytes', 'packet'}, 'cookie step')
        result = {'action': name, 'key': hexadecimal(value.get('key'), 'cookie key'),
                  'windowMicroseconds': integer(value.get('windowMicroseconds'), 'cookie window', 0),
                  'now': integer(value.get('now'), 'cookie time', -MAX_TIME),
                  'address': hexadecimal(value.get('address'), 'cookie address')}
        if ('bytes' in value) == ('packet' in value):
            raise InvalidDocument('A cookie step has bytes or a packet')
        if 'bytes' in value:
            result['bytes'] = hexadecimal(value['bytes'], 'cookie bytes')
        else:
            result['packet'] = integer(value['packet'], 'cookie packet', 0, 1023)
        return result
    if name == 'cancel':
        # Cancels a transfer by its id; code is the one the peer receives.
        closed(value, {'side', 'route', 'id', 'code'}, 'cancel step')
        result = {'action': name, 'side': side(value.get('side')), 'route': integer(value.get('route'), 'route', -1, 1 << 20),
                  'id': integer(value.get('id'), 'transfer id', -1, 1 << 20)}
        if 'code' in value:
            result['code'] = text(value['code'], 'cancel code')
        return result
    closed(value, {'side', 'detail', 'route', 'payload', 'bytes', 'bytesPerSecond'}, name + ' step')
    result = {'action': name, 'side': side(value.get('side'))}
    if name == 'close':
        closed(value, {'side', 'detail'}, 'close step')
        result['detail'] = text(value.get('detail', ''), 'close detail')
    elif name in ('send', 'publish'):
        closed(value, {'side', 'route', 'payload'}, name + ' step')
        result['route'] = integer(value.get('route'), 'route', -1, 1 << 20)
        result['payload'] = payload(value.get('payload'))
    elif name in ('subscribe', 'unsubscribe', 'include'):
        closed(value, {'side', 'route'}, name + ' step')
        result['route'] = integer(value.get('route'), 'route', -1, 1 << 20)
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
        closed(link, LINK_FIELDS, 'simulated link')
        reorder = link.get('reorder', False)
        if type(reorder) is not bool:
            raise InvalidDocument('reorder is true or false')
        link = {'seed': integer(link.get('seed', 0), 'seed', -2**63, 2**63 - 1),
                'latencyMicroseconds': integer(link.get('latencyMicroseconds', 0), 'latency'),
                'jitterMicroseconds': integer(link.get('jitterMicroseconds', 0), 'jitter'),
                'lossPerMille': integer(link.get('lossPerMille', 0), 'loss', -1, 1001),
                'duplicatePerMille': integer(link.get('duplicatePerMille', 0), 'duplicates', -1, 1001),
                'reorder': reorder}
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
    if 'security' in raw and raw['security'] not in SECURITY:
        raise InvalidDocument('Unknown security level')
    for key in SIDE_FIELDS - TEXT_SIDE_FIELDS:
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
