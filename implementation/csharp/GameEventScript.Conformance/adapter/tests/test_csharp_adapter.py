# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Integration tests against the explicitly selected, built internal C# adapter."""
import support
import copy
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

from core.runner import SUPPORTED, supported
from core.corpus import InvalidDocument, read_corpus
from core.runner import requests, verify
from core.transport import execute

ADAPTER = os.environ.get('GES_CSHARP_ADAPTER')
ROOT = support.ROOT


@unittest.skipUnless(ADAPTER, 'Set GES_CSHARP_ADAPTER to the built adapter DLL')
class AdapterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.command = ['dotnet', str(Path(ADAPTER).resolve())]
        cls.cases = [case for case in read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
                     if supported(case)]

    def test_describe(self):
        result = subprocess.run([*self.command, '--describe'], capture_output=True, check=True, text=True, timeout=10)
        data = json.loads(result.stdout)
        self.assertEqual(SUPPORTED, set(data['supportedKinds']))
        self.assertEqual('csharp', data['implementation']['id'])
        self.assertTrue(data['supportedBinarySteps'])

    def test_real_corpus(self):
        report = verify(self.cases, execute(self.command, requests(self.cases)))
        self.assertEqual({'passed': len(self.cases), 'failed': 0, 'error': 0, 'skipped': 0}, report['summary'])

    def test_script_failures_and_fresh_hosts(self):
        template = copy.deepcopy(next(c for c in self.cases if c.kind == 'scriptApi'))
        template.metadata = {'kind': 'scriptApi'}
        template.steps = [{'step': 'run', 'receive': 'Main', 'pump': 'completion', 'budget': ''}]
        template.expected = {'gesBlock': 'expect', 'steps': {'run': {'local': [
            {'name': 'Done', 'args': [{'name': 'value', 'value': {'type': ':Number.int64', 'value': '1'}}]}]}}}
        sources = ['on Main() { emit Done(value: 1) }', 'on {',
                   'on Main() { emit Done(value: :test.fail()) }', 'on Main() { emit Done(value: 1) }']
        cases = []
        for index, source in enumerate(sources):
            case = copy.deepcopy(template)
            case.id = 'adapter.script/control-' + str(index)
            case.sources = [{'name': 'control.ges', 'program': 'main', 'text': source}]
            cases.append(case)
        report = verify(cases, execute(self.command, requests(cases)))
        self.assertEqual(['passed', 'error', 'failed', 'passed'], [item['status'] for item in report['results']])

    def test_random_configuration_is_reproducible_and_expectations_remain_independent(self):
        cases = []
        corpus = read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
        for kind in ('seed', 'sequence', 'entropy'):
            case = copy.deepcopy(next(c for c in corpus if c.kind == 'scriptApi' and kind in c.metadata.get('random', {})
                                      and supported(c)))
            cases.append(case)
        request = requests(cases)
        first = execute(self.command, request)
        second = execute(self.command, request)
        self.assertEqual(first, second)
        self.assertEqual(3, verify(cases, first)['summary']['passed'])
        # Change only an expectation: execution inputs and their digest must be unchanged.
        case = cases[1]
        step = next(key for key, expected in case.expected['steps'].items() if expected.get('local'))
        case.expected['steps'][step]['local'][0]['name'] = 'WrongExpectedMessage'
        self.assertEqual(request, requests(cases))
        self.assertEqual(1, verify(cases, first)['summary']['failed'])

    def test_native_only_dispatch_and_faults(self):
        ids = {'runtime.host-lifecycle/native-only', 'runtime.host-lifecycle/native-message-name-subscription',
               'runtime.host-dispatch/case-0004', 'runtime.host-dispatch/case-0005',
               'runtime.host-dispatch/native-supplied-fault-context'}
        cases = [copy.deepcopy(c) for c in self.cases if c.id in ids]
        self.assertEqual(5, len(cases))
        report = verify(cases, execute(self.command, requests(cases)))
        self.assertEqual(5, report['summary']['passed'])
        case = next(c for c in cases if c.id.endswith('native-only'))
        case.metadata['nativeHandlers'][0]['initiallySubscribed'] = False
        self.assertEqual(1, verify([case], execute(self.command, requests([case])))['summary']['failed'])
        for modify in (lambda r: r['nativeHandlers'][0].update(priority=True),
                       lambda r: r['nativeHandlers'][0].update(faultCode='runtime.bad'),
                       lambda r: r['nativeHandlers'][0].update(actions=[{'operation': 'subscribeHandler', 'target': 'missing'}]),
                       lambda r: r.update(nativeHandlers=[])):
            request = requests([case])
            modify(request['cases'][0]['runtime'])
            with self.assertRaises(InvalidDocument):
                execute(self.command, request)

    def test_lifecycle_action_results_are_checked_only_in_python(self):
        ids = {'runtime.host-lifecycle/detach-unsubscribe-snapshot',
               'runtime.host-lifecycle/load-subscribe-after-dispatch',
               'runtime.host-load-limits/native-callback-handles-rejection'}
        cases = [copy.deepcopy(c) for c in self.cases if c.id in ids]
        self.assertEqual(3, len(cases))
        request = requests(cases)
        observed = execute(self.command, request)
        self.assertEqual(3, verify(cases, observed)['summary']['passed'])
        case = next(c for c in cases if c.id.endswith('native-callback-handles-rejection'))
        actions = case.metadata['nativeHandlers'][1]['actions']
        actions[0]['expectError']['code'] = 'link.wrongError'
        self.assertEqual(request, requests(cases))
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        for mutate in (lambda r: r['stepActions'].update(unknown=[]),
                       lambda r: r.update(deferredPrograms=['missing']),
                       lambda r: r['nativeHandlers'][1]['actions'][0].update(target='missing')):
            corrupted = copy.deepcopy(request)
            runtime = next(c['runtime'] for c in corrupted['cases'] if c['id'] == case.id)
            mutate(runtime)
            with self.assertRaises(InvalidDocument):
                execute(self.command, corrupted)

    def test_start_only_and_virtual_time_observations(self):
        ids = {'runtime.host-startup/group-order-start-only', 'runtime.host-startup/late-pending-and-ready',
               'runtime.host-lifecycle/idle-duration-native', 'runtime.host-startup/delayed-detach-preserves-capture'}
        cases = [copy.deepcopy(c) for c in self.cases if c.id in ids]
        self.assertEqual(4, len(cases))
        request = requests(cases)
        observed = execute(self.command, request)
        self.assertEqual(4, verify(cases, observed)['summary']['passed'])
        case = next(c for c in cases if c.id.endswith('group-order-start-only'))
        runtime = next(c['runtime'] for c in request['cases'] if c['id'] == case.id)
        self.assertEqual('start', runtime['initializationPump'])
        case.expected['initialization']['hostReady'] = False
        self.assertEqual(request, requests(cases))
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        for mutate in (lambda r: r.update(initializationPump='frame'),
                       lambda r: r['stepActions'].update({r['steps'][0]['id']: [{'operation': 'advanceMicroseconds', 'target': '-1'}]})):
            corrupted = requests([cases[-1]])
            mutate(corrupted['cases'][0]['runtime'])
            with self.assertRaises(InvalidDocument):
                execute(self.command, corrupted)

    def test_virtual_time_overflow_keeps_the_clock_unchanged(self):
        case = copy.deepcopy(next(c for c in self.cases if c.kind == 'scriptApi'))
        case.sources = [{'name': 'clock.ges', 'program': 'main', 'text': 'on Main() {}'}]
        case.metadata = {'kind': 'scriptApi', 'stepActions': {
            'maximum': [{'advanceMicroseconds': str(2**63 - 1), 'expectResult': True}],
            'overflow': [{'advanceMicroseconds': '1', 'expectResult': False}]}}
        case.steps = [{'step': name, 'receive': 'Unmatched', 'pump': 'enqueue', 'budget': ''}
                      for name in ('maximum', 'overflow')]
        case.expected = {'gesBlock': 'expect', 'steps': {
            name: {'accepted': False, 'idleDurationMicroseconds': str(2**63 - 1)} for name in ('maximum', 'overflow')}}
        report = verify([case], execute(self.command, requests([case])))
        self.assertEqual(1, report['summary']['passed'], report)

    def test_publish_sink_modes_and_trace_order(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.id.startswith('runtime.host-observer/publish-')]
        self.assertEqual(4, len(cases))
        request = requests(cases)
        observed = execute(self.command, request)
        self.assertEqual(4, verify(cases, observed)['summary']['passed'])
        case = next(c for c in cases if c.metadata['publishSink'] == 'throw')
        trace = observed['observations'][case.id]['runtime']['hosts'][0]['steps']['run']['trace']
        trace[0], trace[1] = trace[1], trace[0]
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        case.expected['steps']['run'].pop('trace')
        self.assertEqual(request, requests(cases))
        self.assertEqual(4, verify(cases, observed)['summary']['passed'])
        for mode in ('unknown', None, 1):
            invalid = requests(cases[:1])
            invalid['cases'][0]['runtime']['publishSink'] = mode
            with self.assertRaises(InvalidDocument):
                execute(self.command, invalid)

    def test_external_fixtures_and_queue_drop_assertions(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.kind == 'scriptApi'
                 and ('external-types' in c.metadata.get('requires', {}).get('core', [])
                      or c.id == 'runtime.control-flow/case-0006')]
        self.assertEqual(10, len(cases))
        request = requests(cases)
        observed = execute(self.command, request)
        self.assertEqual(10, verify(cases, observed)['summary']['passed'])
        queue = next(c for c in cases if c.id == 'runtime.control-flow/case-0006')
        events = observed['observations'][queue.id]['runtime']['hosts'][0]['steps']['step-0001']['trace']
        rejected = next(e for e in events if e['event'] == 'emit' and e['message']['name'] == 'C')
        rejected['accepted'] = True
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        rejected['accepted'] = False
        external = next(c for c in cases if c.id == 'runtime.external-callback-failures/field-declared-context')
        diagnostic = observed['observations'][external.id]['runtime']['hosts'][0]['steps']['both']['diagnostics'][0]
        diagnostic['programName'] = 'wrong.program'
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        self.assertEqual(request, requests(cases))

    def test_load_and_catalog_api_controls(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.kind in ('loadError', 'externalTypeApi')]
        self.assertEqual(5, len(cases))
        report = verify(cases, execute(self.command, requests(cases)))
        self.assertEqual(5, report['summary']['passed'])
        load = next(c for c in cases if c.kind == 'loadError' and 'runtimeLimits' in c.metadata)
        load.metadata['runtimeLimits'] = {}
        self.assertEqual(1, verify([load], execute(self.command, requests([load])))['summary']['failed'])
        load.sources[0]['text'] = 'on {'
        self.assertEqual(1, verify([load], execute(self.command, requests([load])))['summary']['error'])
        catalog = next(c for c in cases if c.kind == 'externalTypeApi')
        for names in ([], ['Sample', 'Other']):
            catalog.metadata['externalTypeApi']['typeNames'] = names
            catalog.expected['externalType'] = {'typeCount': len(names)}
            self.assertEqual(1, verify([catalog], execute(self.command, requests([catalog])))['summary']['passed'])
        catalog.metadata['externalTypeApi']['typeNames'] = ['sample', 'sample']
        catalog.expected['externalType'] = {'error': 'duplicateTypeName'}
        self.assertEqual(1, verify([catalog], execute(self.command, requests([catalog])))['summary']['error'])

    def test_conformance_message_comparisons_use_independent_oracle(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.kind == 'messageApi' and 'compareConformanceMessage' in c.metadata['messageApi'] and supported(c)]
        self.assertEqual(48, len(cases))
        request = requests(cases)
        for item in request['cases']:
            self.assertNotIn('compareConformanceMessage', item['api'])
            self.assertNotIn('comparison', item)
        observed = execute(self.command, request)
        self.assertEqual(48, verify(cases, observed)['summary']['passed'])
        for case in cases:
            case.expected['message']['conformanceEquals'] = not case.expected['message']['conformanceEquals']
        self.assertEqual(request, requests(cases))
        self.assertEqual(48, verify(cases, observed)['summary']['failed'])
        case = cases[0]
        observed['observations'][case.id]['message']['normalized']['args'][0]['value'] = {'type': ':Number.int64', 'value': True}
        with self.assertRaises(InvalidDocument):
            verify(cases, observed)

    def test_product_json_codec_and_exact_oracle(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.kind == 'messageApi' and ('json' in c.metadata['messageApi'] or 'roundTripJson' in c.metadata['messageApi'])]
        self.assertEqual(41, len(cases))
        request = requests(cases)
        observed = execute(self.command, request)
        self.assertEqual(41, verify(cases, observed)['summary']['passed'])
        for case in cases:
            expected = case.expected['message']
            if 'json' in expected:
                expected['json'] += ' '
            else:
                expected['error'] = 'message.resourceLimit' if expected['error'] != 'message.resourceLimit' else 'message.invalidJson'
        self.assertEqual(request, requests(cases))
        self.assertEqual(41, verify(cases, observed)['summary']['failed'])
        for mutate in (lambda api: api.update(json={}), lambda api: api.update(roundTripJson=1)):
            broken = requests([cases[0]])
            mutate(broken['cases'][0]['api'])
            with self.assertRaises(InvalidDocument):
                execute(self.command, broken)

    def test_message_api_construction_and_identity_controls(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.kind == 'messageApi' and supported(c) and 'compareConformanceMessage' not in c.metadata['messageApi'] and 'json' not in c.metadata['messageApi'] and 'roundTripJson' not in c.metadata['messageApi']]
        self.assertEqual(21, len(cases))
        observed = execute(self.command, requests(cases))
        self.assertEqual(21, verify(cases, observed)['summary']['passed'])
        case = next(c for c in cases if c.id.endswith('message-equality'))
        observed['observations'][case.id]['message']['messageHashEquals'] = False
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        before = requests([case])
        case.expected['message']['messageEquals'] = False
        self.assertEqual(before, requests([case]))
        for mutate in (lambda api: api.update(expected=True),
                       lambda api: api['message'].update(tags=[42])):
            request = requests([case])
            mutate(request['cases'][0]['api'])
            with self.assertRaises(InvalidDocument):
                execute(self.command, request)

    def test_value_api_order_equality_and_defensive_storage_controls(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.kind == 'valueApi']
        self.assertEqual(15, len(cases))
        observed = execute(self.command, requests(cases))
        self.assertEqual(15, verify(cases, observed)['summary']['passed'])
        mapping = next(c for c in cases if c.id.endswith('map-ordering-and-copy'))
        actual = observed['observations'][mapping.id]['value']
        actual['normalized']['entries'].reverse()
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        actual['normalized']['entries'].reverse()
        primitive = next(c for c in cases if c.id.endswith('primitives'))
        observed['observations'][primitive.id]['value']['equalHash'] = False
        self.assertEqual(1, verify(cases, observed)['summary']['failed'])
        for mutate in (lambda api: api.update(mutateSourceAfterCreate=1),
                       lambda api: api.update(expected='must not reach adapter'),
                       lambda api: api.update(value={'type': ':Number.int64', 'value': True})):
            request = requests([primitive])
            mutate(request['cases'][0]['api'])
            with self.assertRaises(InvalidDocument):
                execute(self.command, request)

    def test_all_hosts_are_observed_and_compared(self):
        cases = [copy.deepcopy(c) for c in self.cases if c.metadata.get('hostCount', 1) > 1]
        for case in cases:
            case.expected.setdefault('initialization', {})['hostReady'] = True
        self.assertEqual(5, len(cases))
        observations = execute(self.command, requests(cases))
        self.assertEqual(5, verify(cases, observations)['summary']['passed'])
        for case in cases:
            hosts = observations['observations'][case.id]['runtime']['hosts']
            self.assertEqual(case.metadata['hostCount'], len(hosts))
            self.assertEqual(hosts[0], hosts[1])
        case = cases[0]
        observations['observations'][case.id]['runtime']['hosts'][1]['initialization']['hostReady'] = False
        self.assertEqual(1, verify(cases, observations)['summary']['failed'])
        for count in (0, 257, True, '2'):
            request = requests([case])
            request['cases'][0]['runtime']['hostCount'] = count
            with self.assertRaises(InvalidDocument):
                execute(self.command, request)

    def test_adapter_rejects_invalid_random_transport(self):
        case = next(c for c in self.cases if c.kind == 'scriptApi')
        for config in ({}, {'seed': 1}, {'seed': '9223372036854775808'}, {'seed': '01'},
                       {'seed': '0', 'entropy': '00'}, {'entropy': 'ff'}, {'entropy': '0'},
                       {'sequence': []}, {'sequence': [1]}, {'sequence': ['NaN']}, {'sequence': ['Infinity']}):
            request = requests([case])
            request['cases'][0]['runtime']['random'] = config
            with self.subTest(config=config), self.assertRaises(InvalidDocument):
                execute(self.command, request)

    def test_enqueue_and_frame_pumping_preserve_pending_work(self):
        case = copy.deepcopy(next(c for c in self.cases if c.kind == 'scriptApi'))
        case.metadata = {'kind': 'scriptApi'}
        case.sources = [{'name': 'pump.ges', 'program': 'main', 'text': 'on Main() { emit Done() }'}]
        case.steps = [{'step': pump, 'receive': 'Main', 'pump': pump, 'budget': '1'}
                      for pump in ('enqueue', 'frame', 'frames')]
        case.expected = {'gesBlock': 'expect', 'steps': {
            'enqueue': {'paused': False, 'local': []},
            'frame': {'paused': True, 'local': []},
            'frames': {'paused': True, 'local': [{'name': 'Done'}] * 3}}}
        report = verify([case], execute(self.command, requests([case])))
        self.assertEqual({'passed': 1, 'failed': 0, 'error': 0, 'skipped': 0}, report['summary'], report)

    def test_binary_provenance_is_only_compiled_when_requested(self):
        binary_cases = [case for case in self.cases if case.kind == 'programBinary']
        plain = copy.deepcopy(next(case for case in binary_cases if not case.metadata['binaryFixture'].get('compareCompiledRuntime')))
        compiled = copy.deepcopy(next(case for case in binary_cases if case.metadata['binaryFixture'].get('compareCompiledRuntime')))
        for case in (plain, compiled):
            case.sources = [{'name': 'bad.ges', 'program': 'main', 'text': 'on {'}]
        observed = execute(self.command, requests([plain, compiled]))
        self.assertEqual('ok', observed['observations'][plain.id]['status'])
        self.assertEqual('error', observed['observations'][compiled.id]['status'])
        self.assertEqual({'passed': 1, 'failed': 0, 'error': 1, 'skipped': 0}, verify([plain, compiled], observed)['summary'])

    def test_compile_failure_does_not_contaminate_next_case(self):
        cases = copy.deepcopy([case for case in self.cases if case.kind == 'bytecodeSnapshot'][:2])
        cases[0].sources[0]['text'] = 'on {'
        observed = execute(self.command, requests(cases))
        self.assertEqual('compileError', observed['observations'][cases[0].id]['status'])
        self.assertTrue(observed['observations'][cases[0].id]['diagnostics'])
        self.assertEqual('ok', observed['observations'][cases[1].id]['status'])
        self.assertEqual({'passed': 1, 'failed': 0, 'error': 1, 'skipped': 0}, verify(cases, observed)['summary'])

    def test_unsupported_kind_unknown_fields_and_duplicates(self):
        for modify in [lambda x: x['cases'][0].update(kind='scriptApi'),
                       lambda x: x['cases'][0].update(expected='must never reach adapter'),
                       lambda x: x['cases'].append(x['cases'][0]),
                       lambda x: x['cases'][0]['compile'].update(debugInfo=['unknown'])]:
            request = requests(self.cases[:1])
            modify(request)
            with self.assertRaises(InvalidDocument):
                execute(self.command, request)
        result = subprocess.run(self.command, input='{"formatVersion":1,"formatVersion":1}',
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(2, result.returncode)
        self.assertEqual('', result.stdout)

    def test_json_roundtrip_can_be_compared_as_typed_message(self):
        case = copy.deepcopy(next(c for c in self.cases if c.kind == 'messageApi'
                                  and c.expected['message'].get('conformanceEquals') is True))
        case.metadata['messageApi']['roundTripJson'] = True
        request = requests([case])
        self.assertTrue(request['cases'][0]['api']['includeNormalized'])
        self.assertNotIn('compareConformanceMessage', request['cases'][0]['api'])
        observed = execute(self.command, request)
        self.assertEqual(1, verify([case], observed)['summary']['passed'])
        case.expected['message']['conformanceEquals'] = False
        self.assertEqual(1, verify([case], observed)['summary']['failed'])

    def test_runner_command(self):
        result = subprocess.run([sys.executable, str(ROOT / 'conformance/runner/main.py'), 'run',
                                 '--adapter', *self.command], capture_output=True, text=True, timeout=30)
        self.assertEqual(3, result.returncode, result.stderr)
        report = json.loads(result.stdout)
        # Every supported case passes; the measured performance workloads are skipped without a profile. The corpus
        # counts themselves are pinned by the runner's own tests.
        corpus = read_corpus(sorted((ROOT / 'conformance/suites').rglob('*.md')))
        self.assertEqual(len(self.cases), report['summary']['passed'])
        self.assertEqual(sum(case.kind == 'performance' for case in corpus), report['summary']['skipped'])
        self.assertEqual(len(corpus), report['summary']['total'])


if __name__ == '__main__':
    unittest.main()
