# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import copy
import unittest
from pathlib import Path
from core.corpus import InvalidDocument, read_corpus
from checks.api_checks import compare, prepare
from core.runner import requests


class ApiChecksTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = [c for c in read_corpus(sorted((Path(__file__).resolve().parents[3] / 'conformance/suites').rglob('*.md')))
                     if c.kind in ('loadError', 'externalTypeApi')]

    def test_load_results_require_link_phase_and_matching_context(self):
        case = copy.deepcopy(next(c for c in self.cases if c.kind == 'loadError'))
        case.expected['error']['programName'] = None
        actual = {'status': 'loadError', 'diagnostics': [dict(case.expected['error'])]}
        self.assertEqual(('passed', []), compare(case, actual))
        actual['diagnostics'][0]['programName'] = 'wrong'
        self.assertEqual('failed', compare(case, actual)[0])
        self.assertEqual('failed', compare(case, {'status': 'loaded', 'diagnostics': []})[0])
        actual['diagnostics'][0]['phase'] = 'compile'
        with self.assertRaises(InvalidDocument):
            compare(case, actual)
        before = requests([case])
        case.expected['error']['code'] = 'link.different'
        self.assertEqual(before, requests([case]))

    def test_closed_api_schemas(self):
        catalog = copy.deepcopy(next(c for c in self.cases if c.kind == 'externalTypeApi'))
        for expected in ({}, {'typeCount': True}, {'error': 'unknown'}, {'typeCount': 1, 'error': 'duplicateTypeName'}):
            catalog.expected['externalType'] = expected
            with self.assertRaises(InvalidDocument):
                prepare(catalog)
        load = copy.deepcopy(next(c for c in self.cases if c.kind == 'loadError'))
        load.metadata['externalTypeRegistry'] = 'unknown'
        with self.assertRaises(InvalidDocument):
            prepare(load)
