#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Verify local rebuilds cannot validate stale browser assets."""
from functools import partial
from http.client import HTTPConnection
from http.server import ThreadingHTTPServer
import os
from pathlib import Path
import tempfile
from threading import Thread
import unittest
from serve import DevelopmentHandler


class DevelopmentCacheTests(unittest.TestCase):
    def test_assets_are_not_cached_or_validated_by_mtime(self):
        with tempfile.TemporaryDirectory() as directory:
            handler = partial(DevelopmentHandler, directory=directory)
            with ThreadingHTTPServer(('127.0.0.1', 0), handler) as server:
                thread = Thread(target=server.serve_forever, daemon=True)
                thread.start()
                connection = HTTPConnection(*server.server_address, timeout=5)
                try:
                    for name in ['index.html', 'app.mjs', 'worker.mjs', 'card-game.wasm', 'examples.json', 'rules.ges']:
                        with self.subTest(asset=name):
                            file = Path(directory) / name
                            file.write_bytes(b'old')
                            stamp = file.stat().st_mtime_ns
                            connection.request('GET', '/' + name)
                            response = connection.getresponse()
                            self.assertEqual(response.status, 200)
                            self.assertEqual(response.getheader('Cache-Control'), 'no-store')
                            modified = response.getheader('Last-Modified')
                            self.assertEqual(response.read(), b'old')
                            file.write_bytes(b'new')
                            os.utime(file, ns=(stamp, stamp))
                            connection.request('GET', '/' + name, headers={'If-Modified-Since': modified})
                            response = connection.getresponse()
                            self.assertEqual(response.status, 200)
                            self.assertEqual(response.read(), b'new')
                            connection.request('HEAD', '/' + name)
                            response = connection.getresponse()
                            self.assertEqual(response.getheader('Cache-Control'), 'no-store')
                            self.assertEqual(response.read(), b'')
                finally:
                    connection.close()
                    server.shutdown()
                    thread.join()


if __name__ == '__main__':
    unittest.main()
