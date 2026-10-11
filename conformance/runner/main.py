#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""CLI for corpus inventory and independent compiler/binary conformance verification."""
import argparse
import json
from pathlib import Path
import sys

from core.corpus import InvalidDocument, read_corpus
from core.runner import read_json, requests, verify
from core.runner import SUPPORTED
from checks.binary_checks import FIXTURES
from core import execution
from reporting import reports
from reporting import received


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['list', 'requests', 'verify', 'run', 'validate', 'received'])
    parser.add_argument('--corpus', type=Path, default=Path(__file__).resolve().parents[1] / 'suites')
    parser.add_argument('--fixtures', type=Path, default=FIXTURES, help='Binary fixture resource root')
    parser.add_argument('--kind', help='Filter by kind; run/list/validate default to the complete corpus')
    parser.add_argument('--case', action='append', default=[], help='Exact full case ID; repeat to select several')
    parser.add_argument('--performance-profile', action='append', default=[], help='Opt-in measurement profile; repeat in preference order')
    parser.add_argument('--report', type=Path, help='V1 report for received output')
    parser.add_argument('--observations', type=Path, help='Required for verify')
    parser.add_argument('--output', type=Path, help='JSON output; defaults to stdout')
    parser.add_argument('--timeout', type=float, default=60, help='Adapter batch deadline in seconds (default: 60)')
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, help='Executable and arguments; must be the last option')
    options = parser.parse_args(argv)
    try:
        paths = sorted(options.corpus.rglob('*.md')) if options.corpus.is_dir() else [options.corpus]
        cases = read_corpus(paths)
        missing = set(options.case) - {case.id for case in cases}
        if missing:
            raise InvalidDocument('Unknown cases: ' + ', '.join(sorted(missing)))
        kind = options.kind or (None if options.command in ('list', 'validate', 'run', 'received') else 'supported')
        selected = [case for case in cases if (kind is None or case.kind == kind or (kind == 'supported' and case.kind in SUPPORTED)) and (not options.case or case.id in options.case)]
        if not selected or (options.case and set(options.case) != {case.id for case in selected}):
            raise InvalidDocument('Empty or conflicting selection')
        if options.command in ('list', 'validate', 'run', 'received'):
            execution.validate(selected, options.fixtures)
        if options.command == 'received':
            if len(paths) != 1 or options.output is None or options.report is None or not options.case:
                raise InvalidDocument('received requires one document, --case, --report and --output')
            if options.output.resolve() in {paths[0].resolve(), options.report.resolve()} or options.output.resolve().is_relative_to(options.fixtures.resolve()):
                raise InvalidDocument('Received output must not overwrite input or fixtures')
            data = received.render(selected[0].document, selected, read_json(options.report), options.case)
            options.output.parent.mkdir(parents=True, exist_ok=True)
            options.output.write_bytes(data)
            return 0
        if options.command == 'validate':
            result = {'validated': len(selected)}
        elif options.command == 'list':
            result = {'total': len(cases), 'selected': len(selected), 'cases': [
                {'id': case.id, 'kind': case.kind, 'path': case.path, 'executionSupported': True, 'optionalMeasurement': case.kind == 'performance'} for case in selected]}
        elif options.command == 'requests':
            result = requests(selected, options.fixtures)
        elif options.command == 'run':
            result = reports.canonical(selected, execution.run(selected, options.adapter, options.fixtures, options.timeout, options.performance_profile))
        else:
            if options.observations is None:
                raise InvalidDocument('--observations is required')
            result = reports.canonical(selected, verify(selected, read_json(options.observations), options.fixtures))
        text = json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + '\n'
        if options.output:
            if options.output.resolve().is_relative_to(options.fixtures.resolve()):
                raise InvalidDocument('Output must not overwrite fixture resources')
            if options.output.resolve() in {path.resolve() for path in paths} or (options.observations and options.output.resolve() == options.observations.resolve()):
                raise InvalidDocument('Output would overwrite input')
            options.output.parent.mkdir(parents=True, exist_ok=True)
            options.output.write_text(text, encoding='utf-8')
        else:
            sys.stdout.write(text)
        print(f"{len(selected)}/{len(cases)} cases selected; supported kinds: {', '.join(sorted(SUPPORTED))}", file=sys.stderr)
        summary = result.get('summary', {})
        if summary.get('error'):
            return 2
        if summary.get('failed'):
            return 1
        if options.command in ('verify', 'run') and summary.get('skipped'):
            return 3
        return 0
    except (OSError, ValueError, TypeError, KeyError, RecursionError) as error:
        print(json.dumps(error.diagnostic(), ensure_ascii=False) if isinstance(error, InvalidDocument) else 'Conformance input error: ' + str(error), file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
