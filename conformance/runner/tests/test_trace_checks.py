# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest
from core.corpus import InvalidDocument
from checks.compiler_checks import exact
from checks.runtime_checks import message, limit
from checks.trace_checks import normalize, compare


class TraceTests(unittest.TestCase):
    def test_closed_shapes_and_publish_consistency(self):
        publish = {'event': 'publish', 'message': {'name': 'Ping'}, 'id': '1', 'result': {
            'localAccepted': True, 'outboundAttempted': True, 'outboundAccepted': False, 'anyAccepted': True}}
        self.assertEqual('Ping', normalize([publish], message, limit)[0]['message']['name'])
        bad_results = [{'localAccepted': True}, {**publish['result'], 'anyAccepted': False},
                       {**publish['result'], 'outboundAccepted': True, 'outboundAttempted': False},
                       {**publish['result'], 'localAccepted': 1}]
        for result in bad_results:
            with self.assertRaises(InvalidDocument):
                normalize([{**publish, 'result': result}], message, limit)
        for event in ({'event': 'unknown'}, {'event': 'emit', 'message': {'name': 'Ping'}, 'id': '1', 'accepted': 1},
                      {'event': 'emit', 'message': {'name': 'Ping'}, 'accepted': True},
                      {'event': 'emit', 'message': {'name': 'Ping'}, 'id': 0, 'accepted': False},
                      {'event': 'dispatchStarted', 'message': {'name': 'Ping'}, 'id': '1'},
                      {'event': 'dispatchStarted', 'message': {'name': 'Ping'}, 'signatureId': 'Ping()'},
                      {'event': 'diagnostic', 'diagnostic': {'phase': 'compile', 'code': 'test.error'}}):
            with self.assertRaises(InvalidDocument):
                normalize([event], message, limit)

    def test_scheduling_shapes(self):
        scheduled = {'event': 'scheduled', 'message': {'name': 'Ping'}, 'id': '1', 'deadline': '1000000', 'publish': False}
        discarded = {'event': 'scheduledDiscarded', 'id': '1'}
        self.assertEqual(['scheduled', 'scheduledDiscarded'], [item['event'] for item in normalize([scheduled, discarded], message, limit)])
        for event in ({**scheduled, 'id': 1}, {**scheduled, 'deadline': '-1'}, {**scheduled, 'deadline': '\u00b2'},
                      {**scheduled, 'publish': 0}, {key: value for key, value in scheduled.items() if key != 'deadline'},
                      {**discarded, 'id': ''}, {**discarded, 'message': {'name': 'Ping'}}):
            with self.assertRaises(InvalidDocument):
                normalize([event], message, limit)

    def test_order_empty_trace_and_partial_context(self):
        expected = normalize([{'event': 'runtimeLimit', 'runtimeLimit': {'any': True}},
                              {'event': 'diagnostic', 'diagnostic': {'phase': 'runtime', 'code': 'test.error'}}], message, limit, expectation=True)
        actual = normalize([{'event': 'runtimeLimit', 'runtimeLimit': {'name': 'MaxLoopIterations', 'limit': 10}},
                            {'event': 'diagnostic', 'diagnostic': {'phase': 'runtime', 'code': 'test.error', 'handlerName': 'Main()'}}], message, limit)
        mismatches = []
        def check(path, wanted, observed):
            if not exact(wanted, observed):
                mismatches.append(path)
        compare(expected, actual, check)
        self.assertEqual([], mismatches)
        for altered in (list(reversed(actual)), actual[:1], []):
            mismatches.clear()
            compare(expected, altered, check)
            self.assertTrue(mismatches)
        mismatches.clear()
        compare([], actual, check)
        self.assertEqual(['/trace/count'], mismatches)
        changed = copy.deepcopy(expected)
        changed[1]['diagnostic']['handlerName'] = None
        mismatches.clear()
        compare(changed, actual, check)
        self.assertTrue(mismatches)
