#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Process and POSIX-terminal checks for the native Swift CLI, without .NET."""
import argparse
import errno
import fcntl
import os
from pathlib import Path
import pty
import select
import shutil
import signal
import struct
import subprocess
import tempfile
import termios
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('executable', type=Path)
parser.add_argument('--reference', type=Path, help='Optionally verify CLI interoperability with this C# tool DLL')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
base = root / 'artifacts/swift/tool-process-tests'
base.mkdir(parents=True, exist_ok=True)
workspace = Path(tempfile.mkdtemp(prefix='process with spaces ', dir=base))
executable = workspace / 'ges'
shutil.copy2(args.executable.resolve(), executable)
environment = dict(os.environ)
environment.pop('NO_COLOR', None)

def run(arguments, *, input=None, expected=0, command=None, env=environment):
    result = subprocess.run((command or [str(executable)]) + arguments, input=input, capture_output=True,
                            cwd=workspace, env=env, timeout=30)
    assert result.returncode == expected, (arguments, result.returncode, result.stdout, result.stderr)
    return result

assert b'(Swift)' in run(['--version']).stdout
(workspace / 'main.ges').write_text('on Main(args) { for arg in args { emit ConsoleOut(arg) } }\n')
run(['compile', 'main.ges', '-q'])
assert run(['run', 'main.gesb', '-q', '--', 'Grüße', '--color']).stdout == 'Grüße\n--color\n'.encode()
assert run(['check', 'main.ges', '-q']).stdout == b''
assert b'.segment code' in run(['dump', 'main.gesb']).stdout
assert run(['run', '--interactive', '-q'], input=b'emit ConsoleOut(1)\r\n:quit\r\n').stdout == b'1\n'
assert b'cli.invalidEncoding' in run(['run', '--interactive', '-q'], input=b'\xff\n', expected=1).stderr
run(['run', 'main.ges', '-q'], env=dict(environment, NO_COLOR='1'))

# A closed stdout must become failure status 1, never SIGPIPE or a successful script code.
(workspace / 'output.ges').write_text('on Main(args) { emit ConsoleOut(42); emit ErrorCode(7) }')
read_fd, write_fd = os.pipe()
os.close(read_fd)
process = subprocess.Popen([str(executable), 'run', 'output.ges', '-q'], cwd=workspace,
                           stdout=write_fd, stderr=subprocess.PIPE, env=environment)
os.close(write_fd)
assert process.communicate(timeout=10)[0] is None
assert process.returncode == 1, process.returncode

def terminal_test(interrupt=False):
    master, slave = pty.openpty()
    # Exercise wrapping as well as ordinary multiline input.
    fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack('HHHH', 40, 48, 0, 0))
    original = termios.tcgetattr(slave)
    process = subprocess.Popen([str(executable), 'run', '--interactive', '--color', '-q'],
                               stdin=slave, stdout=slave, stderr=slave, env=dict(environment, LC_ALL='C'),
                               cwd=workspace, start_new_session=True)
    transcript = bytearray()
    position = 0
    def until(marker, timeout=10):
        nonlocal position
        deadline = time.monotonic() + timeout
        while True:
            found = transcript.find(marker, position)
            if found >= 0:
                position = found + len(marker)
                return
            remaining = deadline - time.monotonic()
            assert remaining > 0, (marker, bytes(transcript[-3000:]))
            readable, _, _ = select.select([master], [], [], min(0.2, remaining))
            if readable:
                try:
                    chunk = os.read(master, 65536)
                except OSError as error:
                    if error.errno == errno.EIO:
                        raise AssertionError(('terminal exited', process.poll(), bytes(transcript))) from error
                    raise
                assert chunk, (marker, process.poll(), bytes(transcript))
                transcript.extend(chunk)
    try:
        until(b'ges> ')
        if interrupt:
            process.send_signal(signal.SIGTERM)
            assert process.wait(timeout=10) == 128 + signal.SIGTERM
        else:
            for newline in [b'\x0e', b'\x1b[27;2;13~', b'\x1b[13;3u', b'\x1b\r']:
                os.write(master, b'let x be 21' + newline + b'emit ConsoleOut("RESULT:", x+x)\r')
                until(b'RESULT:\x1b[34m42\x1b[0m\r\n')
                until(b'ges> ')
            # A bracketed paste includes newlines without submitting until Enter arrives.
            payload = 'let y be "Grüße 👩‍💻"\nemit ConsoleOut(y)'.encode()
            os.write(master, b'\x1b[200~' + payload + b'\x1b[201~\r')
            until('Grüße 👩‍💻\r\n'.encode())
            until(b'ges> ')
            os.write(master, b'emit ConsoleOut("EDIT:", 12)\x1b[D\x7f3\r')
            until(b'EDIT:\x1b[34m13\x1b[0m\r\n')
            until(b'ges> ')
            os.write(master, b'\x1b[A\r')
            until(b'EDIT:\x1b[34m13\x1b[0m\r\n')
            until(b'ges> ')
            os.write(master, b'emit ConsoleOut("CANCELLED")\x03')
            until(b'^C\r\n')
            until(b'ges> ')
            os.write(master, b'\x04')
            assert process.wait(timeout=10) == 0
        actual = termios.tcgetattr(slave)
        # Darwin may set its transient pending-input bit when ICANON is restored.
        # Compare caller-configurable settings, excluding that kernel-owned state.
        transient = getattr(termios, 'PENDIN', 0)
        actual[3] &= ~transient
        original[3] &= ~transient
        assert actual == original, ('Terminal settings were not restored', original, actual)
        (workspace / ('terminal-signal.log' if interrupt else 'terminal.log')).write_bytes(transcript)
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        os.close(master)
        os.close(slave)

terminal_test()
terminal_test(interrupt=True)

if args.reference:
    reference = ['dotnet', str(args.reference.resolve())]
    run(['compile', 'main.ges', '-q', '-o', 'csharp.gesb'], command=reference)
    assert run(['run', 'csharp.gesb', '-q', '--', 'C# to Swift']).stdout == b'C# to Swift\n'
    assert run(['run', 'main.gesb', '-q', '--', 'Swift to C#'], command=reference).stdout == b'Swift to C#\n'
    assert run(['dump', 'csharp.gesb']).stdout == run(['dump', 'csharp.gesb'], command=reference).stdout
    assert run(['dump', 'main.gesb']).stdout == run(['dump', 'main.gesb'], command=reference).stdout
    print('C#/Swift CLI binary execution and byte-identical GESA presentation passed.')
print(f'Swift CLI relocation, process I/O, UTF-8, exit status, terminal editing and restoration passed. Logs: {workspace}')
