# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
import sys
import unittest
from unittest.mock import patch
from core.corpus import InvalidDocument
from core.transport import execute


class TransportTests(unittest.TestCase):
    def test_output_and_request_bounds(self):
        with patch('core.transport.MAX_BYTES', 128):
            with self.assertRaises(InvalidDocument):
                execute([sys.executable, '-c', 'print("x" * 129)'], {})
            with self.assertRaises(InvalidDocument):
                execute([sys.executable], {'large': 'x' * 129})

    def test_timeout_nonzero_and_invalid_json(self):
        for program in ['import time; time.sleep(10)', 'raise SystemExit(7)', 'print("not JSON")']:
            with self.assertRaises((InvalidDocument, ValueError)):
                execute([sys.executable, '-c', program], {}, timeout=0.2)

    def test_timeout_validation(self):
        for timeout in [0, -1, float('inf'), float('nan')]:
            with self.assertRaises(InvalidDocument):
                execute([sys.executable], {}, timeout)
