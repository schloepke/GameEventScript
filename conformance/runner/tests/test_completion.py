# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Completion gates: source identity, surgical updates and full selection."""
import copy
import json
import subprocess
import sys
import tempfile
from pathlib import Path
import unittest
from unittest.mock import patch
from core.corpus import InvalidDocument, parse_document, read_corpus, read_document
from core.source_document import SourceDocument
from core.yaml_subset import parse
from core import execution
from core.runner import requests, verify
from reporting import received
from reporting import reports

ROOT = Path(__file__).resolve().parents[2]


class CompletionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = read_corpus(sorted((ROOT / 'suites').rglob('*.md')))

    def report(self, case, **fields):
        raw = {'implementation': {'id': 'fixture', 'version': '1'}, 'results': [
            {'id': case.id, 'status': 'passed', 'code': 'conformance.passed', **fields}]}
        return reports.canonical([case], raw)

    def test_adapter_error_detail_survives_canonical_json_report(self):
        case = next(case for case in self.cases if case.kind == 'bytecode')
        request = requests([case])
        detail = 'Adapterfehler: Größe unbekannt.\nAdditional context.'
        observed = {'formatVersion': 1, 'requestDigest': request['requestDigest'],
                    'implementation': {'id': 'fixture', 'version': '1'},
                    'capabilities': request['cases'][0]['requires']['core'] + request['cases'][0]['requires']['optional'],
                    'observations': {case.id: {'status': 'error', 'detail': detail}}}
        result = json.loads(json.dumps(reports.canonical([case], verify([case], observed))))
        self.assertEqual(result['status'], 'error')
        self.assertEqual(result['cases'][0]['code'], 'python.observation.executionError')
        self.assertEqual(result['cases'][0]['technicalDetails'], detail)
        self.assertEqual(list(result['cases'][0])[-1], 'technicalDetails')
        for fields in ({}, {'detail': None}):
            self.assertNotIn('technicalDetails', self.report(case, **fields)['cases'][0])

    def test_all_cases_validate_no_adapter(self):
        execution.validate(self.cases, ROOT / 'fixtures')
        self.assertEqual(2016, len(self.cases))

    def test_normalized_models_are_immutable_with_explicit_editable_copies(self):
        case = next(c for c in self.cases if c.sources)
        with self.assertRaises(TypeError):
            case.title = 'Changed'
        with self.assertRaises(TypeError):
            case.sources[0]['text'] = 'Changed'
        with self.assertRaises(TypeError):
            case.metadata['tags'].append('Changed')
        edited = copy.deepcopy(case)
        edited.title = 'Changed'
        edited.sources[0]['text'] = 'Changed'
        self.assertNotEqual(case.title, edited.title)
        self.assertNotEqual(case.sources[0]['text'], edited.sources[0]['text'])

    def test_readonly_containers_compare_by_content_without_coercing_numbers(self):
        from checks.compiler_checks import exact
        from checks.numeric_checks import equal
        from core.immutable import freeze
        for compare in (exact, lambda a, b: equal(a, b, {'mode': 'exact'})):
            self.assertTrue(compare(freeze({'a': [1, {'b': True}]}), {'a': [1, {'b': True}]}))
            self.assertFalse(compare(freeze({'a': [1]}), {'a': [True]}))
            self.assertFalse(compare(freeze({'a': [1]}), {'a': [1.0]}))

    def test_authored_numbers_require_canonical_spelling_but_text_is_opaque(self):
        from checks.numeric_checks import validate_authored
        for kind, bad in [(':Number.int64', '+1'), (':Number.int64', '01'),
                          (':Number.binary64', '1.0'), (':Number.binary64', '1E+20'),
                          (':Quantity.binary64', '-0')]:
            with self.assertRaises(InvalidDocument):
                validate_authored({'type': kind, 'value': bad})
            validate_authored({'type': ':Text', 'value': bad})

    def test_yaml_scalar_ranges_ignore_comments_and_spaces(self):
        text = 'profiles:\n  p:\n    metrics: {m: {reference: 1.20  , unit: ms}} # é\n'
        spans = {}
        parse(text, spans)
        start, end = spans[('profiles', 'p', 'metrics', 'm', 'reference')]
        self.assertEqual('1.20', text[start:end])
        text = 'items:\n  - reference: 12\n    unit: ms'
        spans = {}
        parse(text, spans)
        self.assertEqual('12', text[slice(*spans[('items', 0, 'reference')])])

    def test_structured_invalid_utf8_and_yaml_diagnostics(self):
        for data, code in [(b'\xff', 'conformance.markdown.invalidUtf8'),
                           (b'wrong', 'conformance.markdown.missingFrontmatter'),
                           (b'---\n', 'conformance.markdown.unterminatedFrontmatter'),
                           (b'---\na: 1\na: 2\n---', 'conformance.yaml.duplicateKey')]:
            with self.assertRaises(InvalidDocument) as caught:
                parse_document(data)
            diagnostic = caught.exception.diagnostic()
            self.assertEqual(code, diagnostic['code'])
            self.assertGreaterEqual(diagnostic['line'], 1)
            self.assertGreaterEqual(diagnostic['column'], 1)
        document = SourceDocument('u', '\ufeffé\r\nα\rZ'.encode())
        self.assertEqual({'sourceName': 'u', 'byteOffset': 4, 'byteLength': 0, 'line': 2, 'column': 1}, document.location(4))

    def test_received_snapshot_preserves_every_other_byte(self):
        case = next(c for c in self.cases if c.assembler is not None)
        # Reparse BOM/CRLF input to ensure byte ranges are never normalized offsets.
        data = b'\xef\xbb\xbf' + case.document.content.replace(b'\n', b'\r\n')
        _, cases = parse_document(data, 'windows.md')
        case = next(c for c in cases if c.id == case.id and c.assembler is not None)
        actual = case.assembler.replace('RegisterLocals', 'ChangedOpcode')
        report = self.report(case, actualAssembler=actual)
        output = received.render(case.document, cases, report, [case.id])
        a, b = case.ranges[('assembler',)]
        self.assertEqual(data[:a + 3], output[:a + 3])
        replacement = (actual + '\n').encode().replace(b'\n', b'\r\n')
        self.assertEqual(data[:a + 3] + replacement + data[b + 3:], output)
        reparsed = parse_document(output, 'windows.md')[1]
        self.assertEqual(actual, next(c for c in reparsed if c.id == case.id).assembler)

    def test_received_cli_accepts_relative_input_and_never_overwrites_source(self):
        original = next(c for c in self.cases if c.assembler is not None)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, report_path, output = root / 'suite.md', root / 'report.json', root / 'received.md'
            source.write_bytes(original.document.data)
            case = next(c for c in read_document(source)[1] if c.id == original.id)
            report_path.write_text(json.dumps(self.report(case, actualAssembler=case.assembler)))
            command = [sys.executable, str(Path(__file__).resolve().parents[1] / 'main.py'), 'received',
                       '--corpus', 'suite.md', '--case', case.id, '--report', str(report_path), '--output']
            result = subprocess.run([*command, str(output)], cwd=root, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual(source.read_bytes(), output.read_bytes())
            result = subprocess.run([*command, str(source)], cwd=root, capture_output=True, text=True)
            self.assertEqual(2, result.returncode)
            self.assertEqual(original.document.data, source.read_bytes())

    def test_received_performance_changes_only_reference_tokens(self):
        case = next(c for c in self.cases if c.kind == 'performance')
        profile = next(iter(case.expected['performance']['profiles']))
        metrics = case.expected['performance']['profiles'][profile]['metrics']
        measurement = {'profile': profile, 'metrics': [
            {'id': name, 'unit': m['unit'], 'value': '0'} for name, m in metrics.items()]}
        report = self.report(case, measurement=measurement)
        output = received.render(case.document, [case], report, [case.id])
        expected = case.document.content
        ranges = [case.ranges[('performance', 'profiles', profile, 'metrics', name, 'reference')] for name in metrics]
        for a, b in sorted(ranges, reverse=True):
            expected = expected[:a] + b'0' + expected[b:]
        self.assertEqual(expected, output)

    def test_source_documents_are_keyed_by_portable_suite_id(self):
        case = next(case for case in self.cases if case.document)
        suite = case.id.split('/')[0]
        self.assertEqual({suite: case.document.digest}, self.report(case)['sourceDocuments'])
        self.assertNotIn(str(ROOT), json.dumps(self.report(case)['sourceDocuments']))

    def test_received_reports_a_document_without_cases_as_missing(self):
        case = next(c for c in self.cases if c.assembler is not None)
        with self.assertRaises(InvalidDocument) as caught:
            received.render(case.document, [], self.report(case, actualAssembler=case.assembler), [case.id])
        self.assertEqual('conformance.received.missingCase', caught.exception.code)

    def test_received_rejects_stale_identity_missing_ranges_and_overlap(self):
        original = next(c for c in self.cases if c.assembler is not None)
        for mode, code in [('source', 'staleRange'), ('title', 'invalidReport'), ('range', 'missingRange'), ('duplicate', 'overlappingRange')]:
            case = copy.deepcopy(original)
            report = self.report(case, actualAssembler=case.assembler)
            ids = [case.id]
            if mode == 'source': report['sourceDocuments'][case.id.split('/')[0]] = 'stale'
            if mode == 'title': report['cases'][0]['title'] = 'stale'
            if mode == 'range': case.ranges = {}
            if mode == 'duplicate': ids *= 2
            with self.assertRaises(InvalidDocument) as caught:
                received.render(case.document, [case], report, ids)
            self.assertEqual('conformance.received.' + code, caught.exception.code)

    def test_failed_correctness_cannot_pass_unmeasured_cases_in_same_batch(self):
        cases = copy.deepcopy([c for c in self.cases if c.kind == 'performance'][:2])
        for case in cases:
            profiles = case.expected['performance']['profiles']
            case.expected['performance']['profiles'] = {'sample-profile': next(iter(profiles.values()))}
        identity = {'id': 'fixture', 'version': '1'}
        description = {'formatVersion': 1, 'implementation': identity, 'capabilities': ['compiler'],
                       'performanceProfiles': ['sample-profile']}
        correctness = {'implementation': identity, 'results': [
            {'id': cases[0].id, 'status': 'failed', 'code': 'conformance.assertion.mismatch'},
            {'id': cases[1].id, 'status': 'passed', 'code': 'conformance.passed'}]}
        with patch('core.execution.execute', return_value=description), patch('measurements.performance.run',
                return_value={'correctness': correctness, 'measured': False}):
            result = execution.run(cases, ['fixture'], ROOT / 'fixtures', profiles=['sample-profile'])
        self.assertEqual(0, result['summary']['passed'])
        self.assertEqual(1, result['summary']['failed'])
        self.assertEqual(1, result['summary']['error'])
        self.assertEqual('conformance.runner.measurementNotPerformed', result['results'][1]['code'])

    def test_complete_run_reports_optional_performance_skips(self):
        regular = next(c for c in self.cases if c.kind == 'bytecodeSnapshot')
        measured = next(c for c in self.cases if c.kind == 'performance')
        def adapter(command, request, timeout):
            return {'formatVersion': 1, 'requestDigest': request['requestDigest'],
                    'implementation': {'id': 'test', 'version': '1'}, 'capabilities': ['compiler', 'program-binary', 'bytecode-snapshot'],
                    'observations': {regular.id: {'status': 'ok', 'dump': regular.assembler}}}
        with patch('core.execution.execute', side_effect=adapter):
            result = execution.run([measured, regular], ['test'], ROOT / 'fixtures')
        self.assertEqual([measured.id, regular.id], [r['id'] for r in result['results']])
        self.assertEqual(1, result['summary']['skipped'])
        self.assertEqual(1, result['summary']['passed'])
        self.assertEqual(regular.assembler, result['results'][1]['actualAssembler'])
