# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Single-Program source execution using the shared runtime observation oracle."""
from core.corpus import InvalidDocument, closed
from checks import runtime_checks


def prepare(case):
    closed(case.metadata, {'gesBlock', 'id', 'kind', 'level', 'categories', 'tags', 'requires',
                           'sources', 'compile', 'runtimeLimits', 'comparison', 'random', 'hostCount', 'nativeHandlers', 'stepActions', 'deferredPrograms', 'publishSink'}, 'supported script metadata')
    closed(case.expected, {'gesBlock', 'steps', 'initialization'}, 'supported script expectation')
    if (not case.sources and not case.metadata.get('nativeHandlers')):
        raise InvalidDocument('Script execution requires source Programs or native handlers')
    return runtime_checks.prepare(case)


def compare(case, observed):
    closed(observed, {'status', 'detail', 'runtime'}, 'script observation')
    if observed.get('status') == 'error':
        if 'runtime' in observed or not isinstance(observed.get('detail', ''), str):
            raise InvalidDocument('Invalid script execution error')
        return 'error', []
    if observed.get('status') != 'ok':
        raise InvalidDocument('Invalid script observation status')
    differences = runtime_checks.compare(case, observed.get('runtime'))
    return ('failed' if differences else 'passed'), differences
