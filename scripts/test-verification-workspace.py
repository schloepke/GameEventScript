#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Check verification cleanup without compilers or repository build products."""

from pathlib import Path
import tempfile
import unittest

import sys

# Keep generated Python bytecode out of the source tree.
sys.dont_write_bytecode = True
from verification_workspace import verification_workspace


class WorkspaceTests(unittest.TestCase):
    def test_success_retains_logs_and_preserves_other_workspaces(self):
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            previous = parent / 'previous failure'
            previous.mkdir()
            for value in ('first', 'second'):
                with verification_workspace(parent, 'build ') as workspace:
                    (workspace / 'build').mkdir()
                    (workspace / 'build/object.o').write_bytes(b'large build')
                    (workspace / 'check.log').write_text(value)
                self.assertFalse(workspace.exists())
                self.assertEqual((parent / 'latest-logs/check.log').read_text(), value)
            self.assertEqual(set(parent.iterdir()), {previous, parent / 'latest-logs'})

    def test_failure_and_interruption_preserve_diagnostics(self):
        with tempfile.TemporaryDirectory() as temporary:
            for error in (RuntimeError, KeyboardInterrupt):
                with self.assertRaises(error):
                    with verification_workspace(Path(temporary), 'failed ') as workspace:
                        (workspace / 'diagnostic.log').write_text('failure')
                        raise error()
                self.assertEqual((workspace / 'diagnostic.log').read_text(), 'failure')

    def test_log_symlinks_cannot_overwrite_unrelated_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            target = parent / 'unrelated.txt'
            target.write_text('keep')
            logs = parent / 'latest-logs'
            logs.mkdir()
            (logs / 'check.log').symlink_to(target)
            with self.assertRaises(RuntimeError):
                with verification_workspace(parent, 'build ') as workspace:
                    (workspace / 'check.log').write_text('replace')
            self.assertEqual(target.read_text(), 'keep')
            self.assertTrue(workspace.exists())


if __name__ == '__main__':
    unittest.main()
