# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Link failures and external catalogs: input transport and independent assertions."""
from core.corpus import InvalidDocument, closed
from checks.compiler_checks import diagnostic, exact
from checks.runtime_checks import LIMITS
from checks import value_api_checks
from checks import message_api_checks

KINDS = {'loadError', 'externalTypeApi', 'valueApi', 'messageApi'}


def prepare(case):
    if case.kind == 'messageApi':
        return message_api_checks.prepare(case)
    if case.kind == 'valueApi':
        return value_api_checks.prepare(case)
    if case.steps or case.assembler is not None:
        raise InvalidDocument('API case cannot contain Steps or assembler')
    if case.kind == 'loadError':
        if not case.sources or len({s['program'] for s in case.sources}) != 1:
            raise InvalidDocument('Load error requires one Program')
        closed(case.expected, {'gesBlock', 'error'}, 'load error expectation')
        diagnostic(case.expected.get('error'), expectation=True, phases=('link',))
        limits = case.metadata.get('runtimeLimits', {})
        closed(limits, LIMITS, 'runtime limits')
        if any(type(v) is not int or not 0 < v < 2**31 for v in limits.values()) or limits.get('maxRandomScopeDepth', 0) > 65535:
            raise InvalidDocument('Invalid runtime limits')
        registry = case.metadata.get('externalTypeRegistry', 'environment')
        if registry not in ('environment', 'absent', 'mismatch'):
            raise InvalidDocument('Invalid external type registry')
        return {'limits': limits, 'externalTypeRegistry': registry}
    if case.sources:
        raise InvalidDocument('External catalog test cannot contain sources')
    raw = case.metadata.get('externalTypeApi')
    closed(raw, {'typeNames'}, 'external type input')
    names = raw.get('typeNames')
    if not isinstance(names, list) or any(not isinstance(n, str) or not n.strip() for n in names):
        raise InvalidDocument('Expected external type names')
    closed(case.expected, {'gesBlock', 'externalType'}, 'external type expectation')
    expected = case.expected.get('externalType')
    closed(expected, {'error', 'typeCount'}, 'external type result')
    if len(expected) != 1 or ('error' in expected and expected['error'] != 'duplicateTypeName'):
        raise InvalidDocument('Invalid external type outcome')
    if 'typeCount' in expected and (type(expected['typeCount']) is not int or not 0 <= expected['typeCount'] < 2**32):
        raise InvalidDocument('Invalid type count')
    return raw


def compare(case, observed):
    if case.kind == 'messageApi':
        return message_api_checks.compare(case, observed)
    if case.kind == 'valueApi':
        return value_api_checks.compare(case, observed)
    closed(observed, {'status', 'detail', 'diagnostics', 'externalType'}, 'API observation')
    status = observed.get('status')
    if status == 'error':
        if set(observed) - {'status', 'detail'} or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid API error observation')
        return 'error', []
    if case.kind == 'loadError':
        if set(observed) != {'status', 'diagnostics'} or status not in ('loaded', 'loadError'):
            raise InvalidDocument('Invalid load outcome')
        diagnostics = observed['diagnostics']
        if not isinstance(diagnostics, list) or len(diagnostics) != (1 if status == 'loadError' else 0):
            raise InvalidDocument('Invalid load diagnostics')
        for item in diagnostics:
            diagnostic(item, phases=('link',))
        expected = case.expected['error']
        if diagnostics and all(exact(v, diagnostics[0].get(k)) for k, v in expected.items()):
            return 'passed', []
        return 'failed', [{'path': '/error', 'expected': expected, 'actual': diagnostics}]
    if set(observed) != {'status', 'externalType'} or status != 'ok':
        raise InvalidDocument('Invalid catalog outcome')
    actual = observed['externalType']
    closed(actual, {'error', 'typeCount'}, 'catalog observation')
    if set(actual) == {'error'}:
        if actual['error'] != 'duplicateTypeName':
            raise InvalidDocument('Unknown catalog error')
    elif set(actual) == {'typeCount'}:
        if type(actual['typeCount']) is not int or not 0 <= actual['typeCount'] < 2**32:
            raise InvalidDocument('Invalid catalog count')
    else:
        raise InvalidDocument('Incomplete catalog outcome')
    expected = case.expected['externalType']
    return ('passed', []) if exact(expected, actual) else ('failed', [{'path': '/externalType', 'expected': expected, 'actual': actual}])
