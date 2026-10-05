#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Serve only the generated browser prototype on localhost without browser caching."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class DevelopmentHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def send_head(self):
        # Rebuilds can preserve mtimes or replace files within the same second.
        # Always send current bytes instead of validating an old browser copy.
        if 'If-Modified-Since' in self.headers:
            del self.headers['If-Modified-Since']
        return super().send_head()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=8766)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    web = root / 'artifacts/card-game/web'
    if not (web / 'card-game.wasm').exists():
        parser.error('Build Wasm first: python3 examples/card-game/scripts/build.py wasm')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(DevelopmentHandler, directory=str(web)))
    print(f'Card Lab: http://127.0.0.1:{args.port}/ — Ctrl+C stops the server.', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
