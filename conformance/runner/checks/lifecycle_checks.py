# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Portable host lifecycle observations, without a host state machine in Python."""
import re
from core.corpus import InvalidDocument, closed

STATES = {'pending', 'ready', 'runtimeError', 'runtimeLimitReached'}


def duration(value, allow_none=False):
    if allow_none and value == 'none':
        return
    if not isinstance(value, str) or not re.fullmatch(r'0|[1-9][0-9]*', value) or int(value) > 2**63-1:
        raise InvalidDocument('Expected canonical nonnegative Int64 duration')


def validate(case, raw, observation=False):
    if 'hostReady' in raw and type(raw['hostReady']) is not bool:
        raise InvalidDocument('hostReady must be Boolean')
    if 'idleDurationMicroseconds' in raw:
        duration(raw['idleDurationMicroseconds'], allow_none=True)
    starts = raw.get('programStarts', {})
    programs = {s['program'] for s in case.sources} if case.kind != 'programBinary' else {'main'}
    closed(starts, programs, 'Program start IDs')
    for state in starts.values():
        if not isinstance(state, str) or state not in STATES:
            raise InvalidDocument('Invalid Program start state')
    if 'pendingTimers' in raw:
        timers(raw['pendingTimers'], programs)
    if observation and not {'hostReady', 'idleDurationMicroseconds', 'programStarts', 'pendingTimers'} <= raw.keys():
        raise InvalidDocument('Incomplete lifecycle observations')


# The host's pending named timers in its own order (deadline, then send order); deadline and period are exact
# microseconds as decimal text, and period 0 marks a one-shot timer.
TIMER_FIELDS = {'name', 'deadline', 'period', 'message', 'program', 'publish'}


def timers(raw, programs):
    if not isinstance(raw, list):
        raise InvalidDocument('Expected pending timer sequence')
    for item in raw:
        closed(item, TIMER_FIELDS, 'pending timer')
        if item.keys() != TIMER_FIELDS:
            raise InvalidDocument('Incomplete pending timer')
        if not isinstance(item['name'], str) or not item['name'] or not isinstance(item['message'], str) or not item['message']:
            raise InvalidDocument('Invalid pending timer name')
        duration(item['deadline'])
        duration(item['period'])
        if item['program'] not in programs:
            raise InvalidDocument('Unknown pending timer Program')
        if type(item['publish']) is not bool:
            raise InvalidDocument('Invalid pending timer publish flag')


def compare(case, expected, actual, check):
    validate(case, actual, observation=True)
    for key in ('hostReady', 'idleDurationMicroseconds'):
        if key in expected:
            check('/' + key, expected[key], actual[key])
    if 'pendingTimers' in expected:
        check('/pendingTimers', expected['pendingTimers'], actual['pendingTimers'])
    for program, state in expected.get('programStarts', {}).items():
        check('/programStarts/' + program, state, actual['programStarts'].get(program))
