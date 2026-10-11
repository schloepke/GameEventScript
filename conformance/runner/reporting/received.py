# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Surgical Received-Markdown updates; never write to the authored document."""
from core.corpus import InvalidDocument, logical_lines
from checks.performance_checks import profiles, upper_bound
from reporting.reports import numeric_text
from core.yaml_subset import inline


def fail(suffix, message):
    raise InvalidDocument(message, 'conformance.received.' + suffix)


def render(document, cases, report, selected):
    """Return bytes, changing only selected GESA payloads and performance references."""
    if report.get('schemaVersion') != 1 or not isinstance(report.get('cases'), list):
        fail('invalidReport', 'Expected V1 result report')
    if report.get('sourceDocuments', {}).get(document.name) != document.digest:
        fail('staleRange', 'Report was not produced from these exact source bytes')
    authored = {case.id: case for case in cases if case.document == document}
    results = {}
    for item in report['cases']:
        if not isinstance(item, dict) or not isinstance(item.get('id'), str) or item['id'] in results:
            fail('invalidReport', 'Duplicate or malformed case result')
        results[item['id']] = item
    edits = []
    for identifier in selected:
        if identifier not in authored or identifier not in results:
            fail('missingCase', 'Selected case missing from document or report: ' + identifier)
        case, result = authored[identifier], results[identifier]
        suite, local = identifier.split('/')
        identity = {'suiteId': suite, 'caseId': local, 'title': case.title, 'kind': case.kind, 'level': case.metadata['level']}
        if any(result.get(key) != value for key, value in identity.items()) or result.get('status') not in ('passed', 'failed'):
            fail('invalidReport', 'Case identity or execution status mismatch')
        changes = []
        if case.assembler is not None:
            actual = result.get('actualAssembler')
            if not isinstance(actual, str) or '\0' in actual:
                fail('invalidReport', 'Actual assembler is required for snapshot update')
            # Embedded closing fences would change Markdown semantics rather than payload.
            if any(line == '```' for line in logical_lines(actual).split('\n')):
                fail('invalidReport', 'Assembler contains a closing Markdown fence')
            payload = (logical_lines(actual) + '\n').encode('utf-8').replace(b'\n', document.line_ending)
            changes.append((('assembler',), payload))
        if case.kind == 'performance':
            measurement = result.get('performance')
            if not isinstance(measurement, dict) or measurement.get('profile') not in profiles(case):
                fail('invalidReport', 'Missing or unknown performance profile')
            profile = measurement['profile']
            metrics = measurement.get('metrics')
            expected = profiles(case)[profile]['metrics']
            if not isinstance(metrics, dict) or set(metrics) != set(expected):
                fail('invalidReport', 'Performance metric set mismatch')
            for name, baseline in expected.items():
                item = metrics[name]
                if (not isinstance(item, dict) or item.get('unit') != baseline['unit']
                        or item.get('reference') != numeric_text(baseline['reference'])
                        or item.get('allowed') != numeric_text(upper_bound(baseline))):
                    fail('invalidReport', 'Metric baseline or unit mismatch')
                measured = item.get('measured')
                try:
                    valid = isinstance(measured, str) and numeric_text(measured) == measured and float(measured) >= 0
                except (ValueError, TypeError, OverflowError):
                    valid = False
                if not valid:
                    fail('invalidReport', 'Invalid measured metric')
                changes.append((('performance', 'profiles', profile, 'metrics', name, 'reference'), measured.encode('ascii')))
        if not changes:
            fail('invalidReport', 'Case has no updateable snapshot or measurements')
        for path, replacement in changes:
            span = (case.ranges or {}).get(path)
            if span is None:
                fail('missingRange', 'Missing authored token range')
            start, end = span
            if not 0 <= start <= end <= len(document.content):
                fail('staleRange', 'Source range outside original document')
            original = document.content[start:end].decode('utf-8')
            if path == ('assembler',):
                if logical_lines(original) != case.assembler + '\n':
                    fail('staleRange', 'Assembler range does not match the parsed payload')
            else:
                try:
                    token = inline(original)
                    agrees = numeric_text(token) == numeric_text(expected[path[-2]]['reference'])
                except (ValueError, TypeError, OverflowError):
                    agrees = False
                if not agrees:
                    fail('staleRange', 'Reference range does not match the parsed value')
            edits.append((start, end, replacement))
    if not edits:
        fail('missingCase', 'No update cases selected')
    edits.sort()
    if any(right[0] < left[1] or right[0] == left[0] for left, right in zip(edits, edits[1:])):
        fail('overlappingRange', 'Selected update ranges overlap')
    data = document.content
    for start, end, replacement in reversed(edits):
        data = data[:start] + replacement + data[end:]
    return (b'\xef\xbb\xbf' if document.data.startswith(b'\xef\xbb\xbf') else b'') + data
