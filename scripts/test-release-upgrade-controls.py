#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Reject stale or partial dependency upgrades before running consumer checks."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('upgrade', Path(__file__).with_name('test-release-upgrade.py'))
upgrade = importlib.util.module_from_spec(spec)
spec.loader.exec_module(upgrade)


class UpgradeControls(unittest.TestCase):
    def test_all_requested_packages_must_resolve_exact_version(self):
        names = ['Runtime', 'Compiler']
        upgrade.require_versions(dict.fromkeys(names, '0.4.0-test'), '0.4.0-test', names)
        for actual in ({}, {'Runtime': '0.4.0-test'}, {'Runtime': '0.3.0', 'Compiler': '0.4.0-test'}):
            with self.assertRaises(AssertionError):
                upgrade.require_versions(actual, '0.4.0-test', names)

    def test_frozen_release_fixture_detects_drift(self):
        for language in ('csharp', 'swift'):
            data = (upgrade.ROOT / 'conformance/fixtures/Release030' / (language + '-consumer.gesb')).read_bytes()
            upgrade.require_fixture(language, data)
            with self.assertRaises(AssertionError):
                upgrade.require_fixture(language, data[:-1])

    def test_consumer_failure_and_timeout_cannot_pass(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.assertRaises(RuntimeError):
                upgrade.run([sys.executable, '-c', 'raise SystemExit(1)'], root, root / 'failure.log')
            with self.assertRaises(subprocess.TimeoutExpired):
                upgrade.run([sys.executable, '-c', 'import time; time.sleep(5)'], root, root / 'timeout.log', timeout=0.1)


if __name__ == '__main__':
    unittest.main()
