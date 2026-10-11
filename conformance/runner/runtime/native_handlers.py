# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Closed declarative native fixtures; no executable host code or expectations."""
from core.corpus import InvalidDocument, closed


def prepare(metadata, encode_message):
    definitions = metadata.get('nativeHandlers', [])
    if not isinstance(definitions, list):
        raise InvalidDocument('Expected native handler sequence')
    result, ids = [], set()
    for index, raw in enumerate(definitions):
        closed(raw, {'id', 'message', 'parameters', 'messageName', 'priority', 'initiallySubscribed',
                     'throw', 'faultCode', 'faultContext', 'actions', 'emit'}, 'native handler')
        identifier = raw.get('id', f'native-{index + 1:04}')
        if not isinstance(identifier, str) or not identifier or identifier in ids:
            raise InvalidDocument('Invalid or duplicate native handler ID')
        ids.add(identifier)
        name = raw.get('message')
        if not isinstance(name, str) or not name:
            raise InvalidDocument('Native message name required')
        parameters = raw.get('parameters', [])
        if not isinstance(parameters, list) or any(not isinstance(p, str) for p in parameters):
            raise InvalidDocument('Invalid native parameters')
        flags = {key: raw.get(key, default) for key, default in [('messageName', False), ('initiallySubscribed', True), ('throw', False)]}
        if any(type(v) is not bool for v in flags.values()) or (flags['messageName'] and parameters):
            raise InvalidDocument('Invalid native matching or flags')
        priority = raw.get('priority', 0)
        if type(priority) is not int or not -(2**31) <= priority < 2**31:
            raise InvalidDocument('Invalid native priority')
        code = raw.get('faultCode')
        if 'faultCode' in raw and (not isinstance(code, str) or not code.strip() or code.startswith('runtime.') or flags['throw']):
            raise InvalidDocument('Invalid native fault code')
        context = raw.get('faultContext', {})
        closed(context, {'programName', 'handlerName'}, 'native fault context')
        if ('faultContext' in raw and code is None) or any(v is not None and not isinstance(v, str) for v in context.values()):
            raise InvalidDocument('Invalid native fault context')
        emits = raw.get('emit', [])
        if not isinstance(emits, list):
            raise InvalidDocument('Expected native emit sequence')
        outputs = []
        for emit in emits:
            closed(emit, {'name', 'forwardArguments', 'args'}, 'native emit')
            forward = 'forwardArguments' in emit
            if forward == ('args' in emit) or (forward and emit['forwardArguments'] is not True):
                raise InvalidDocument('Native emit needs arguments or forwarding')
            message = encode_message({'name': emit.get('name'), 'args': emit.get('args', [])})
            outputs.append({'message': message, 'forwardArguments': forward})
        result.append({'id': identifier, 'message': name, 'parameters': parameters, **flags, 'priority': priority,
                       'faultCode': code, 'faultContext': {key: context.get(key) for key in ('programName', 'handlerName')}, 'emit': outputs})
    return result
