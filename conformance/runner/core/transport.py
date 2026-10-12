# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Bounded one-batch process transport. No shell interpretation or GES execution here."""
import math
import os
import signal
import subprocess
import tempfile
import time
from pathlib import Path

from core.corpus import InvalidDocument
from core.runner import encode, read_json

MAX_BYTES = 32 * 1024 * 1024


def execute(command, request, timeout=60):
    if not command or not math.isfinite(timeout) or timeout <= 0:
        raise InvalidDocument('Adapter command and positive finite timeout required')
    payload = encode(request)
    if len(payload) > MAX_BYTES:
        raise InvalidDocument('Adapter request exceeds 32 MiB')
    with tempfile.TemporaryDirectory(prefix='ges-conformance-') as directory:
        root = Path(directory)
        incoming, outgoing, errors = (root / name for name in ('input.json', 'output.json', 'stderr.txt'))
        incoming.write_bytes(payload)
        with incoming.open('rb') as stdin, outgoing.open('wb') as stdout, errors.open('wb') as stderr:
            process = subprocess.Popen(command, stdin=stdin, stdout=stdout, stderr=stderr, start_new_session=os.name == 'posix')
            deadline = time.monotonic() + timeout
            try:
                while True:
                    if outgoing.stat().st_size > MAX_BYTES or errors.stat().st_size > MAX_BYTES:
                        raise InvalidDocument('Adapter output exceeds 32 MiB')
                    if process.poll() is not None:
                        break
                    if time.monotonic() >= deadline:
                        raise InvalidDocument('Adapter timed out')
                    time.sleep(0.02)
            finally:
                if process.poll() is None:
                    if os.name == 'posix':
                        os.killpg(process.pid, signal.SIGKILL)
                    else:
                        process.kill()
                process.wait()
        if outgoing.stat().st_size > MAX_BYTES or errors.stat().st_size > MAX_BYTES:
            raise InvalidDocument('Adapter output exceeds 32 MiB')
        if process.returncode:
            detail = errors.read_bytes()[:4096].decode('utf-8', errors='replace')
            raise InvalidDocument(f'Adapter exited with {process.returncode}: {detail}')
        return read_json(outgoing, MAX_BYTES)
