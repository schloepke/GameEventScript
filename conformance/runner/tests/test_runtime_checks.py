# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
from pathlib import Path
import unittest

from core.corpus import InvalidDocument, read_corpus
from checks.runtime_checks import LIMITS, compare_host as compare, compare as compare_hosts, limit, message, prepare, value

ROOT = Path(__file__).resolve().parents[3]


class RuntimeChecksTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
                     if c.kind == 'programBinary' and c.steps]

    def observation(self, case):
        empty = {'local': [], 'outbound': [], 'diagnostics': [], 'runtimeLimits': [], 'actions': [], 'trace': [], 'hostReady': True, 'programStarts': {'main': 'ready'}, 'idleDurationMicroseconds': 'none'}
        result = {'initialization': {**empty}, 'steps': {}}
        for step in case.steps:
            expected = case.expected['steps'][step['step']]
            result['steps'][step['step']] = {**empty, 'accepted': True, 'paused': False, 'waiting': False,
                                           'local': [message(x) for x in expected.get('local', [])],
                                           'runtimeLimits': expected.get('runtimeLimits', {}).get('include', [])}
        return copy.deepcopy(result)

    def test_requests_only_contain_execution_inputs(self):
        for case in self.cases:
            request = prepare(case)
            self.assertEqual({'limits', 'steps', 'random', 'hostCount', 'nativeHandlers', 'stepActions', 'deferredPrograms', 'initializationPump', 'publishSink'}, set(request))
            for step in request['steps']:
                self.assertEqual({'id', 'input', 'pump', 'budget'}, set(step))
            self.assertEqual([], compare(case, self.observation(case)))

    def test_exact_numeric_storage_and_unicode(self):
        maximum = {'type': ':Number.int64', 'value': '9223372036854775807'}
        self.assertEqual(maximum, value(maximum))
        for item in [{'type': ':Number.int64', 'value': True}, {'type': ':Number.int64', 'value': '9223372036854775808'},
                     {'type': ':Number.int64', 'value': 1}, {'type': ':List', 'items': {}},
                     {'type': ':Boolean', 'value': 1}, {'type': ':Nothing', 'value': ''}]:
            with self.assertRaises(InvalidDocument):
                value(item)
        self.assertNotEqual(value({'type': ':Text', 'value': 'é'}), value({'type': ':Text', 'value': 'e\u0301'}))
        self.assertNotEqual(value({'type': ':Boolean', 'value': True}), value({'type': ':Number.int64', 'value': '1'}))

    def test_map_order_is_key_based_but_lists_are_ordered(self):
        one, two = {'type': ':Number.int64', 'value': '1'}, {'type': ':Number.int64', 'value': '2'}
        entries = [{'key': 'z', 'value': one}, {'key': 'a', 'value': two}]
        self.assertEqual(value({'type': ':Map', 'entries': entries}), value({'type': ':Map', 'entries': entries[::-1]}))
        self.assertNotEqual(value({'type': ':List', 'items': [one, two]}), value({'type': ':List', 'items': [two, one]}))
        self.assertEqual(value({'type': ':Map', 'entries': [entries[0]]}), value({'type': ':Map', 'entries': [entries[0], entries[0]]}))

    def test_channels_order_diagnostics_and_initialization(self):
        case = next(c for c in self.cases if len(c.expected['steps'][c.steps[0]['step']].get('local', [])) > 1)
        key = case.steps[0]['step']
        for mutate in [lambda o: o['steps'][key]['local'].reverse(),
                       lambda o: o['steps'][key]['local'].pop(),
                       lambda o: o['steps'][key].update(accepted=False),
                       lambda o: o['steps'][key]['diagnostics'].append({'phase': 'runtime', 'code': 'runtime.test'}),
                       lambda o: o['initialization']['local'].append({'name': 'Unexpected'})]:
            observation = self.observation(case)
            mutate(observation)
            self.assertTrue(compare(case, observation))
        observation = self.observation(case)
        del observation['steps'][key]
        with self.assertRaises(InvalidDocument):
            compare(case, observation)

    def test_limit_include_exclude_and_type(self):
        for mode in ('include', 'exclude'):
            case = next(c for c in self.cases if mode in c.expected['steps'][c.steps[0]['step']].get('runtimeLimits', {}))
            key = case.steps[0]['step']
            observed = self.observation(case)
            observed['steps'][key]['runtimeLimits'] = [] if mode == 'include' else case.expected['steps'][key]['runtimeLimits']['exclude']
            self.assertTrue(compare(case, observed))
        observed['steps'][key]['runtimeLimits'] = [{'name': 'MaxLoopIterations', 'limit': True}]
        with self.assertRaises(InvalidDocument):
            compare(case, observed)

    def test_limit_wildcards_and_order(self):
        case = copy.deepcopy(self.cases[0])
        key = case.steps[0]['step']
        case.expected['steps'][key]['runtimeLimits'] = {'include': [{'any': True}, {'limit': 2}]}
        observed = self.observation(case)
        observed['steps'][key]['runtimeLimits'] = [{'name': 'MaxLoopIterations', 'limit': 2}]
        self.assertTrue(compare(case, observed))
        observed['steps'][key]['runtimeLimits'].insert(0, {'name': 'MaxExecutionSteps', 'limit': 1})
        self.assertEqual([], compare(case, observed))
        case.expected['steps'][key]['runtimeLimits'] = {'exclude': [{'any': True}]}
        self.assertTrue(compare(case, observed))
        observed['steps'][key]['runtimeLimits'] = []
        self.assertEqual([], compare(case, observed))
        for item in ({'any': False}, {'any': True, 'name': 'X'}, {}, {'limit': True}):
            with self.assertRaises(InvalidDocument):
                limit(item, expectation=True)

    def test_all_limit_overrides_and_bounds(self):
        case = copy.deepcopy(self.cases[0])
        case.metadata['runtimeLimits'] = dict.fromkeys(LIMITS, 10)
        self.assertEqual(case.metadata['runtimeLimits'], prepare(case)['limits'])
        for invalid in (0, -1, True, 2**31):
            case.metadata['runtimeLimits'] = {'maxExecutionSteps': invalid}
            with self.assertRaises(InvalidDocument):
                prepare(case)
        case.metadata['runtimeLimits'] = {'maxRandomScopeDepth': 65536}
        with self.assertRaises(InvalidDocument):
            prepare(case)

    def test_frame_budgets(self):
        case = copy.deepcopy(self.cases[0])
        for pump in ('completion', 'enqueue', 'frame', 'frames'):
            case.steps[0].update(pump=pump, budget='1')
            result = prepare(case)['steps'][0]
            self.assertEqual(pump, result['pump'])
            self.assertEqual(1 if pump in ('frame', 'frames') else None, result['budget'])
        for budget in ('', 'invalid', '0', '-1', str(2**31)):
            case.steps[0]['budget'] = budget
            with self.assertRaises(InvalidDocument):
                prepare(case)

    def test_host_count_bounds_and_independent_comparison(self):
        case = copy.deepcopy(self.cases[0])
        case.metadata['hostCount'] = 2
        case.expected['initialization'] = {'hostReady': True}
        actual = {'hosts': [self.observation(case), self.observation(case)]}
        self.assertEqual([], compare_hosts(case, actual))
        actual['hosts'][1]['initialization']['hostReady'] = False
        differences = compare_hosts(case, actual)
        self.assertEqual('/hosts/1/initialization/hostReady', differences[0]['path'])
        for hosts in ([], actual['hosts'][:1], actual['hosts'] * 2, {}):
            with self.assertRaises(InvalidDocument):
                compare_hosts(case, {'hosts': hosts})
        for count in (0, -1, 257, True, '2'):
            case.metadata['hostCount'] = count
            with self.assertRaises(InvalidDocument):
                prepare(case)

    def test_runtime_diagnostics_are_ordered_structured_constraints(self):
        case = copy.deepcopy(self.cases[0])
        key = case.steps[0]['step']
        expected = [{'phase': 'runtime', 'code': 'test.first', 'programName': None},
                    {'phase': 'runtime', 'code': 'test.second', 'handlerName': 'Main()'}]
        case.expected['steps'][key]['diagnostics'] = expected
        prepare(case)
        actual = self.observation(case)
        actual['steps'][key]['diagnostics'] = copy.deepcopy(expected)
        self.assertEqual([], compare(case, actual))
        actual['steps'][key]['diagnostics'][0]['programName'] = 'unexpected'
        self.assertTrue(compare(case, actual))
        actual['steps'][key]['diagnostics'] = list(reversed(expected))
        self.assertTrue(compare(case, actual))
        actual['steps'][key]['diagnostics'] = expected[:1]
        self.assertTrue(compare(case, actual))
        case.expected['steps'][key]['diagnostics'] = [{'phase': 'compile', 'code': 'test.error'}]
        with self.assertRaises(InvalidDocument):
            prepare(case)

    def test_unsupported_features_are_not_silently_ignored(self):
        case = copy.deepcopy(self.cases[0])
        case.steps[0]['pump'] = 'unknown'
        with self.assertRaises(InvalidDocument):
            prepare(case)
        case.steps[0]['pump'] = 'completion'
        case.expected['steps'][case.steps[0]['step']]['unknownExpectation'] = []
        with self.assertRaises(InvalidDocument):
            prepare(case)


if __name__ == '__main__':
    unittest.main()
