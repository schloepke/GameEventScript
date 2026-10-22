# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Ordered observer callback assertions independent of the host implementation."""
from core.corpus import InvalidDocument, closed
from checks.compiler_checks import diagnostic


def normalize(events, message, limit, expectation=False):
    if not isinstance(events, list):
        raise InvalidDocument('Expected observer trace sequence')
    result = []
    for item in events:
        if not isinstance(item, dict):
            raise InvalidDocument('Expected observer event')
        event = item.get('event')
        fields = {'emit': {'message', 'accepted'}, 'publish': {'message', 'result'},
                  'dispatchStarted': {'message', 'signatureId'}, 'dispatchCompleted': {'message', 'signatureId'},
                  'runtimeLimit': {'runtimeLimit'}, 'diagnostic': {'diagnostic'},
                  'scheduled': {'message', 'id', 'deadline', 'publish'}, 'scheduledDiscarded': {'id'}}
        if not isinstance(event, str) or event not in fields:
            raise InvalidDocument('Unknown observer event')
        allowed = fields[event] | {'event'}
        closed(item, allowed, 'observer event')
        if item.keys() != allowed:
            raise InvalidDocument('Incomplete observer event')
        normalized = dict(item)
        if 'message' in item:
            normalized['message'] = message(item['message'])
        if event == 'emit' and type(item['accepted']) is not bool:
            raise InvalidDocument('Invalid emit acceptance')
        if event == 'publish':
            flags = item['result']
            keys = {'localAccepted', 'outboundAttempted', 'outboundAccepted', 'anyAccepted'}
            closed(flags, keys, 'publish result')
            if flags.keys() != keys or any(type(flag) is not bool for flag in flags.values()):
                raise InvalidDocument('Incomplete publish result')
            if (flags['outboundAccepted'] and not flags['outboundAttempted']) or flags['anyAccepted'] != (flags['localAccepted'] or flags['outboundAccepted']):
                raise InvalidDocument('Inconsistent publish result')
        # Scheduling IDs and absolute deadlines are exact whole numbers, transported as decimal text.
        for key in ('id', 'deadline'):
            if key in item and (not isinstance(item[key], str) or not (item[key].isascii() and item[key].isdigit())):
                raise InvalidDocument('Invalid scheduling ' + key)
        if event == 'scheduled' and type(item['publish']) is not bool:
            raise InvalidDocument('Invalid scheduling publish flag')
        if event.startswith('dispatch') and (not isinstance(item['signatureId'], str) or not item['signatureId']):
            raise InvalidDocument('Dispatch signature required')
        if event == 'diagnostic':
            diagnostic(item['diagnostic'], expectation=expectation, phases=('runtime',))
        if event == 'runtimeLimit':
            limit(item['runtimeLimit'], expectation=expectation)
        result.append(normalized)
    return result


def compare(expected, observed, check):
    check('/trace/count', len(expected), len(observed))
    for index, (wanted, actual) in enumerate(zip(expected, observed)):
        path = '/trace/' + str(index)
        check(path + '/event', wanted['event'], actual['event'])
        if wanted['event'] != actual['event']:
            continue
        if wanted['event'] in ('diagnostic', 'runtimeLimit'):
            field = wanted['event']
            if wanted[field].get('any') is True:
                continue
            for key, value in wanted[field].items():
                check(path + '/' + field + '/' + key, value, actual[field].get(key))
        else:
            check(path, wanted, actual)
