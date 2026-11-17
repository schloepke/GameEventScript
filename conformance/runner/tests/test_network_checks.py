# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest
from pathlib import Path
from core.corpus import InvalidDocument, read_corpus
from checks.network_checks import compare, prepare


class NetworkInputTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cases = read_corpus(sorted((Path(__file__).resolve().parents[3] / 'conformance/suites').rglob('*.md')))
        cls.cases = {c.id.rsplit('/', 1)[-1]: c for c in cases if c.kind == 'networkApi'}

    def case(self, name, **changes):
        case = copy.deepcopy(self.cases[name])
        case.metadata['networkApi'].update(changes)
        return case

    def test_defaults_reach_the_adapter(self):
        request = prepare(self.cases['handshake-loopback'])
        self.assertEqual('loopback', request['link'])
        self.assertEqual(request['sources'], request['clientSources'])
        self.assertEqual(request['options'], request['clientOptions'])
        self.assertEqual({'action': 'accept'}, request['steps'][1])
        self.assertEqual({'action': 'send', 'side': 'client', 'route': 0, 'payload': {'hex': '05'}}, request['steps'][3])
        link = prepare(self.cases['simulated-link'])['link']
        self.assertEqual({'seed': 7, 'latencyMicroseconds': 20000, 'jitterMicroseconds': 10000, 'lossPerMille': 0,
                          'duplicatePerMille': 0, 'reorder': False}, link)

    def test_datagram_steps_reach_the_adapter(self):
        key = '00' * 32
        steps = prepare(self.case('handshake-loopback', steps=[
            {'deliver': {'side': 'server', 'packets': [1, 0, 0]}},
            {'cookie': {'key': key, 'windowMicroseconds': 10, 'now': -5, 'address': '7f000001', 'packet': 0}},
            {'cookie': {'key': key, 'windowMicroseconds': 10, 'now': 0, 'address': '', 'bytes': '01'}}]))['steps']
        self.assertEqual({'action': 'deliver', 'side': 'server', 'packets': [1, 0, 0]}, steps[0])
        self.assertEqual({'action': 'cookie', 'key': key, 'windowMicroseconds': 10, 'now': -5, 'address': '7f000001', 'packet': 0}, steps[1])
        self.assertEqual('01', steps[2]['bytes'])
        for step in ({'deliver': {'side': 'server'}}, {'deliver': {'side': 'server', 'packets': [-1]}},
                     {'cookie': {'key': key, 'windowMicroseconds': 10, 'now': 0, 'address': ''}},
                     {'cookie': {'key': key, 'windowMicroseconds': 10, 'now': 0, 'address': '', 'bytes': '', 'packet': 0}}):
            with self.assertRaises(InvalidDocument, msg=step):
                prepare(self.case('handshake-loopback', steps=[step]))
        with self.assertRaises(InvalidDocument):
            prepare(self.case('handshake-loopback', link={'seed': 1, 'reorder': 1}))

    def test_topic_steps_reach_the_adapter(self):
        steps = prepare(self.case('handshake-loopback', steps=[
            {'subscribe': {'side': 'server', 'route': 2}},
            {'publish': {'side': 'server', 'route': 2, 'payload': '0500'}},
            {'unsubscribe': {'side': 'server', 'route': 2}}]))['steps']
        self.assertEqual([{'action': 'subscribe', 'side': 'server', 'route': 2},
                          {'action': 'publish', 'side': 'server', 'route': 2, 'payload': {'hex': '0500'}},
                          {'action': 'unsubscribe', 'side': 'server', 'route': 2}], steps)

    def test_transfer_steps_and_options_reach_the_adapter(self):
        request = prepare(self.case('handshake-loopback', options={'maxTransferBytes': 4, 'deflateTransfers': False}, steps=[
            {'cancel': {'side': 'server', 'route': 3, 'id': 0}},
            {'cancel': {'side': 'server', 'route': 3, 'id': 1, 'code': 'game.stale'}}]))
        self.assertEqual({'maxTransferBytes': 4, 'deflateTransfers': False}, request['options'])
        self.assertEqual([{'action': 'cancel', 'side': 'server', 'route': 3, 'id': 0},
                          {'action': 'cancel', 'side': 'server', 'route': 3, 'id': 1, 'code': 'game.stale'}], request['steps'])
        for step in ({'cancel': {'side': 'server', 'route': 3}}, {'cancel': {'side': 'server', 'route': 3, 'id': '0'}},
                     {'cancel': {'side': 'server', 'route': 3, 'id': 0, 'code': 1}},
                     {'cancel': {'side': 'server', 'route': 3, 'id': 0, 'payload': '00'}}):
            with self.assertRaises(InvalidDocument, msg=step):
                prepare(self.case('handshake-loopback', steps=[step]))
        for options in ({'deflateTransfers': 1}, {'maxTransferBytes': True}, {'maxTransferBytes': -1}):
            with self.assertRaises(InvalidDocument, msg=options):
                prepare(self.case('handshake-loopback', options=options))

    def test_malformed_steps_are_document_errors(self):
        for step in ({}, {'advance': 0, 'accept': True}, {'jump': 1}, {'advance': -1}, {'advance': '1'},
                     {'accept': False}, {'transmit': 'both'}, {'refuse': 1},
                     {'send': {'side': 'client', 'route': 0, 'payload': 'ABC'}},
                     {'send': {'side': 'client', 'route': 0, 'payload': {'length': 2, 'seed': 251}}},
                     {'send': {'side': 'client', 'route': 0, 'payload': '00', 'detail': ''}},
                     {'receive': {'side': 'server', 'bytes': '0g'}},
                     {'budget': {'side': 'server'}},
                     {'publish': {'side': 'server', 'route': 2}},
                     {'subscribe': {'side': 'server', 'route': 2, 'payload': '00'}},
                     {'unsubscribe': {'side': 'server'}}):
            with self.assertRaises(InvalidDocument, msg=step):
                prepare(self.case('handshake-loopback', steps=[step]))
        with self.assertRaises(InvalidDocument):
            prepare(self.case('handshake-loopback', steps=[]))

    def test_malformed_inputs_are_document_errors(self):
        for changes in ({'sources': []}, {'sources': [1]}, {'network': 3}, {'token': 'AB'},
                        {'options': {'transport': 'pipe'}}, {'options': {'unknown': 1}},
                        {'options': {'maxPacketBytes': -1}}, {'link': 'wire'}, {'link': {'seed': 1, 'drop': 0}},
                        {'extra': 1}):
            with self.assertRaises(InvalidDocument, msg=changes):
                prepare(self.case('handshake-loopback', **changes))

    def test_a_setup_error_stands_alone(self):
        case = copy.deepcopy(self.cases['schema-mismatch'])
        prepare(case)
        case = copy.deepcopy(self.cases['invalid-options'])
        case.expected['network']['trace'] = []
        with self.assertRaises(InvalidDocument):
            prepare(case)

    def test_observations_are_compared_exactly(self):
        case = self.cases['handshake-loopback']
        expected = case.expected['network']
        sides = {name: {'state': 'connected', 'roundTripMicroseconds': 0, 'queuedSendBytes': 0, 'pendingReceiveBytes': 0}
                 for name in ('client', 'server')}
        observed = {'status': 'ok', 'network': {'trace': list(expected['trace']), **sides}}
        self.assertEqual('passed', compare(case, observed)[0])
        observed['network']['trace'] = expected['trace'][1:]
        verdict, differences = compare(case, observed)
        self.assertEqual('failed', verdict)
        self.assertEqual('/network/trace', differences[0]['path'])
        with self.assertRaises(InvalidDocument):
            compare(case, {'status': 'ok', 'network': {'trace': []}})
        self.assertEqual('error', compare(case, {'status': 'error', 'detail': 'x'})[0])


if __name__ == '__main__':
    unittest.main()
