#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check delayed REPL output and history across reader changes on real terminals, and the console and output
behaviour through pipes that both CLIs must share.

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
import tempfile
import termios
import time
from pathlib import Path


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
        if not color:
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


def pipe_tests():
    # These processes run in a temporary directory, so a relative executable or DLL path must name the same file there.
    command = [str(Path(value).resolve()) if Path(value).exists() else value for value in sys.argv[1:]]
    with tempfile.TemporaryDirectory() as directory:
        work = Path(directory)

        # A driver that keeps stdin open gets each console line answered without closing it.
        process = subprocess.Popen(command + ['run', '--interactive', '-q'], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE, cwd=work)
        try:
            for word in (b'first', b'second'):
                process.stdin.write(b'emit ConsoleOut("' + word + b'")\n')
                process.stdin.flush()
                assert select.select([process.stdout], [], [], 10)[0], ('no reply while stdin stays open', word)
                line = process.stdout.readline()
                assert line == word + b'\n', (word, line, process.poll())
            process.stdin.close()
            assert process.wait(timeout=10) == 0
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()

        # The end of console input delivers delayed messages, including a delayed exit code, as a batch run does.
        result = subprocess.run(command + ['run', '--interactive', '-q'], input=b'emit after 0.2s ConsoleOut("LATE")\nemit after 0.3s ErrorCode(5)\n',
                                capture_output=True, cwd=work, timeout=30)
        assert (result.stdout, result.returncode) == (b'LATE\n', 5), result

        # A closed stdout is failure status 1, never the script's own code.
        (work / 'output.ges').write_text('on Main(args) { emit ConsoleOut(42); emit ErrorCode(7) }', encoding='utf-8')
        read_fd, write_fd = os.pipe()
        os.close(read_fd)
        process = subprocess.Popen(command + ['run', 'output.ges', '-q'], cwd=work, stdout=write_fd, stderr=subprocess.PIPE)
        os.close(write_fd)
        process.communicate(timeout=30)
        assert process.returncode == 1, process.returncode

        # Verbose traces and file lists escape only quotes, backslashes and control characters.
        (work / 'grüße.ges').write_text('on Main(args) { emit Say(text: "Grüße <b> it\'s 😀\\") }', encoding='utf-8')
        result = subprocess.run(command + ['run', 'grüße.ges', '--verbose'], capture_output=True, cwd=work, timeout=30)
        assert 'Say(text: "Grüße <b> it\'s 😀\\\\")'.encode() in result.stderr, result.stderr
        result = subprocess.run(command + ['run', '--interactive', '-q', 'grüße.ges'], input=b':list\n', capture_output=True, cwd=work, timeout=30)
        assert 'files: "grüße.ges"'.encode() in result.stderr, result.stderr

        # Both CLIs explain a decode failure and a reached runtime limit, not only their codes.
        (work / 'nope.gesb').write_bytes(b'NOPE' + bytes(12))
        result = subprocess.run(command + ['dump', 'nope.gesb'], capture_output=True, cwd=work, timeout=30)
        assert result.returncode == 1 and b"error decode.invalidMagic: The file does not start with ASCII 'GESB'." in result.stderr, result.stderr
        (work / 'loop.ges').write_text('on Main(args) { emit Loop() }\non Loop { emit Loop() }', encoding='utf-8')
        result = subprocess.run(command + ['run', 'loop.ges', '-q'], capture_output=True, cwd=work, timeout=60)
        assert result.returncode == 1 and b'Message processing limit reached before the host became idle.' in result.stderr, result.stderr

        # A source without a module declaration names no empty program in its diagnostics.
        (work / 'broken.ges').write_text('on Main(args) { emit }', encoding='utf-8')
        result = subprocess.run(command + ['run', 'broken.ges', '-q'], capture_output=True, cwd=work, timeout=30)
        assert result.returncode == 1 and b'error parse.syntax' in result.stderr and b'[program=]' not in result.stderr, result


def continuation_test():
    # While a delayed message is pending, continuation lines are indented without `...` markers.
    master, slave = pty.openpty()
    fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack('HHHH', 40, 100, 0, 0))
    environment = {k: v for k, v in os.environ.items() if k != 'NO_COLOR'}
    environment['TERM'] = 'xterm-256color'
    process = subprocess.Popen(sys.argv[1:] + ['run', '--interactive', '--quiet'], stdin=slave, stdout=slave, stderr=slave,
                               start_new_session=True, env=environment)
    transcript = bytearray()

    def pump(seconds):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if select.select([master], [], [], 0.05)[0]:
                chunk = os.read(master, 65536)
                transcript.extend(chunk)
                for _ in range(chunk.count(b'\x1b[6n')):
                    os.write(master, b'\x1b[1;1R')

    try:
        pump(1)
        os.write(master, b'emit after 5s ConsoleOut("LATER")')
        pump(0.3)
        os.write(master, b'\r')
        pump(1)
        start = len(transcript)
        os.write(master, b'emit ConsoleOut(\x0e"NEXT")')
        pump(1)
        visible = re.sub(rb'\x1b\[[0-?]*[ -/]*[@-~]', b'', bytes(transcript[start:]))
        assert b' ... ' not in visible, visible
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        os.close(master)
        os.close(slave)


terminal_test(False)
terminal_test(True)
continuation_test()
pipe_tests()
print('Delayed terminal output preserves unfinished input and shared command history; console pipes, closed output and display quoting agree.')
