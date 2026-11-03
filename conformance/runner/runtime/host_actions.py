# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Lifecycle fixture inputs and independent action-result assertions."""
from core.corpus import InvalidDocument, closed
from checks.compiler_checks import LINK_NULLABLE, diagnostic
from checks.lifecycle_checks import duration

OPERATIONS = {'loadProgram', 'detachProgram', 'subscribeHandler', 'unsubscribeHandler', 'advanceMicroseconds'}


def actions(raw, programs, deferred, handlers):
    if not isinstance(raw, list):
        raise InvalidDocument('Expected host action sequence')
    result = []
    for action in raw:
        closed(action, OPERATIONS | {'expectResult', 'expectError', 'expectErrors'}, 'host action')
        operations = OPERATIONS & action.keys()
        if len(operations) != 1:
            raise InvalidDocument('Exactly one host action required')
        operation = next(iter(operations))
        target = action[operation]
        targets = deferred if operation == 'loadProgram' else programs if operation == 'detachProgram' else handlers
        if operation == 'advanceMicroseconds':
            duration(target)
        elif not isinstance(target, str) or target not in targets:
            raise InvalidDocument('Unknown host action target')
        if 'expectResult' in action and type(action['expectResult']) is not bool:
            raise InvalidDocument('Expected Boolean action result')
        # `expectError` constrains the first diagnostic; `expectErrors` lists every diagnostic in order.
        if 'expectError' in action or 'expectErrors' in action:
            if operation != 'loadProgram' or 'expectResult' in action or ('expectError' in action and 'expectErrors' in action):
                raise InvalidDocument('Only load supports an exclusive error expectation')
            expected = action['expectErrors'] if 'expectErrors' in action else [action['expectError']]
            if not isinstance(expected, list) or not expected:
                raise InvalidDocument('Expected load diagnostics')
            for item in expected:
                diagnostic(item, expectation=True, phases=('link',), nullable=LINK_NULLABLE)
        result.append({'operation': operation, 'target': target})
    return result


def prepare(case, native):
    programs = {s['program'] for s in case.sources}
    deferred = case.metadata.get('deferredPrograms', [])
    if not isinstance(deferred, list) or any(not isinstance(p, str) or p not in programs for p in deferred) or len(set(deferred)) != len(deferred):
        raise InvalidDocument('Invalid deferred Programs')
    handlers = {h['id'] for h in native}
    for definition, authored in zip(native, case.metadata.get('nativeHandlers', [])):
        definition['actions'] = actions(authored.get('actions', []), programs, deferred, handlers)
    steps = case.metadata.get('stepActions', {})
    closed(steps, {s['step'] for s in case.steps}, 'step action IDs')
    return {'deferredPrograms': deferred, 'stepActions': {
        key: actions(value, programs, deferred, handlers) for key, value in steps.items()}}


def compare(case, observed, step, check):
    if not isinstance(observed, list):
        raise InvalidDocument('Expected action invocation sequence')
    definitions = {('native/' + h.get('id', f'native-{i + 1:04}')): h.get('actions', [])
                   for i, h in enumerate(case.metadata.get('nativeHandlers', []))}
    if step is not None:
        definitions['step/' + step] = case.metadata.get('stepActions', {}).get(step, [])
    mandatory = bool(step is not None and definitions['step/' + step])
    check('/actions/stepInvocation', mandatory, bool(observed and observed[0].get('origin') == 'step/' + str(step)))
    seen_step = False
    for call_index, call in enumerate(observed):
        closed(call, {'origin', 'results'}, 'action invocation')
        origin = call.get('origin')
        if not isinstance(origin, str) or origin not in definitions:
            raise InvalidDocument('Unknown action origin')
        if origin.startswith('step/'):
            if seen_step or call_index != 0:
                raise InvalidDocument('Duplicate or misplaced step actions')
            seen_step = True
        expected = definitions[origin]
        results = call.get('results')
        if not isinstance(results, list) or len(results) != len(expected) or not expected:
            raise InvalidDocument('Incomplete action results')
        for index, (action, result) in enumerate(zip(expected, results)):
            closed(result, {'result', 'errors'}, 'action result')
            if set(result) != {'result', 'errors'}:
                raise InvalidDocument('Incomplete action result')
            errors = result['errors']
            if not isinstance(errors, list):
                raise InvalidDocument('Expected action diagnostics')
            path = '/actions/' + str(call_index) + '/' + str(index)
            if not errors:
                if type(result['result']) is not bool:
                    raise InvalidDocument('Action result must be Boolean')
                check(path + '/error', action.get('expectError', action.get('expectErrors')), None)
                if 'expectResult' in action:
                    check(path + '/result', action['expectResult'], result['result'])
            else:
                for error in errors:
                    diagnostic(error, phases=('link',))
                if result['result'] is not None:
                    raise InvalidDocument('Error action has a Boolean result')
                if 'expectErrors' in action:
                    expected_errors = action['expectErrors']
                    check(path + '/errors/count', len(expected_errors), len(errors))
                    for position, (expectation, error) in enumerate(zip(expected_errors, errors)):
                        for key, value in expectation.items():
                            check(path + '/errors/' + str(position) + '/' + key, value, error.get(key))
                elif 'expectError' in action:
                    for key, value in action['expectError'].items():
                        check(path + '/error/' + key, value, errors[0].get(key))
                else:
                    check(path + '/error', None, errors[0])
