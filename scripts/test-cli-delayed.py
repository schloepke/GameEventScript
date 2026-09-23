#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check delayed REPL output and history across reader changes on real terminals.

Usage: python3 scripts/test-cli-delayed.py ges
       python3 scripts/test-cli-delayed.py dotnet /absolute/path/dotnet-ges.dll
"""
import errno
import fcntl
import os
import pty
import re
import select
import struct
import subprocess
import sys
import termios
import time


def terminal_test(color):
    master, slave = pty.openpty()
    fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack('HHHH', 40, 100, 0, 0))
    environment = {k: v for k, v in os.environ.items() if k != 'NO_COLOR'}
    environment['TERM'] = 'xterm-256color'
    process = subprocess.Popen(sys.argv[1:] + ['run', '--interactive', '--quiet'] + (['--color'] if color else []),
                               stdin=slave, stdout=slave, stderr=slave, start_new_session=True, env=environment)
    transcript = bytearray()
    position = 0
    queries = 0

    def read(timeout):
        nonlocal queries
        if select.select([master], [], [], timeout)[0]:
            try:
                chunk = os.read(master, 65536)
            except OSError as error:
                if error.errno == errno.EIO:
                    raise AssertionError(('terminal exited', process.poll(), bytes(transcript))) from error
                raise
            assert chunk, (process.poll(), bytes(transcript))
            transcript.extend(chunk)
            # PrettyPrompt queries cursor position when starting a new input.
            count = transcript.count(b'\x1b[6n')
            for _ in range(count - queries):
                os.write(master, b'\x1b[1;1R')
            queries = count

    def visible():
        return re.sub(rb'\x1b\[[0-?]*[ -/]*[@-~]', b'', bytes(transcript))

    def until(marker):
        nonlocal position
        deadline = time.monotonic() + 10
        while True:
            found = visible().find(marker, position)
            if found >= 0:
                position = found + len(marker)
                return
            assert time.monotonic() < deadline, (marker, bytes(transcript[-3000:]))
            read(0.1)

    def submit(text):
        os.write(master, text)
        # Distinguish typed input from PrettyPrompt's multi-key paste detection.
        deadline = time.monotonic() + 0.2
        while time.monotonic() < deadline:
            read(0.02)
        os.write(master, b'\r')

    try:
        until(b'ges> ')
        if color:
            submit(b'emit ConsoleOut("BEFORE")')
            until(b'BEFORE\r\n')
            until(b'ges> ')
            submit(b'emit after 2s ConsoleOut("WAKE")')
            until(b'ges> ')
            # Navigate to an entry from before the timer and back to the draft.
            os.write(master, b'emit ConsoleOut("DRAFT")\x1b[A\x1b[A')
            submit(b'')
            until(b'BEFORE\r\n')
            until(b'ges> ')
            submit(b'emit ConsoleOut("DRAFT")\x1b[A\x1b[B')
            until(b'DRAFT\r\n')
            until(b'ges> ')
            submit(b'emit ConsoleOut("DURING")')
            until(b'DURING\r\n')
            until(b'ges> ')
            until(b'WAKE\r\n')
            # First recall finishes the waiting reader; second uses the ordinary reader.
            for _ in range(2):
                submit(b'\x1b[A')
                until(b'DURING\r\n')
                until(b'ges> ')
        else:
            submit(b'emit after 0.2s ConsoleOut("WAKE")')
            until(b'ges> ')
            os.write(master, b'emit ConsoleOut("PRESERVED")')
            until(b'WAKE\r\n')
            os.write(master, b'\r')
            until(b'PRESERVED\r\n')
            until(b'ges> ')
        submit(b':quit')
        assert process.wait(timeout=10) == 0
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        os.close(master)
        os.close(slave)


terminal_test(False)
terminal_test(True)
print('Delayed terminal output preserves unfinished input and shared command history.')
