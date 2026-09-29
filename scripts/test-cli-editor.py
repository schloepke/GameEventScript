#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Exercise CLI editor/save sessions with a real, deterministic external editor."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parent.parent



def terminal_editor(command, work, color):
    """Use a controlling terminal, not just pipes: editors must own its foreground."""
    import errno
    import fcntl
    import struct
    import termios
    import pty
    import select
    import signal
    import time

    editor = work / "terminal-editor.py"
    editor.write_text("""import os, sys, termios
assert os.isatty(0) and os.isatty(1) and os.isatty(2)
assert os.tcgetpgrp(0) == os.getpgrp(), "editor is a background process"
assert termios.tcgetattr(0)[3] & termios.ICANON, "CLI raw mode leaked"
print("EDITOR_READY", flush=True)
assert input() == "editor input"
print("EDITOR_INPUT_OK", flush=True)
""")
    pid, fd = pty.fork()
    if pid == 0:
        os.chdir(work)
        env = dict(os.environ, GES_EDITOR="", VISUAL="", EDITOR=f'"{sys.executable}" "{editor}"', TERM="xterm-256color")
        env.pop("NO_COLOR", None)
        os.execvpe(command[0], [*command, "run", "--interactive", "-q", *(["--color"] if color else [])], env)
    fcntl.ioctl(fd, termios.TIOCSWINSZ, struct.pack("HHHH", 40, 100, 0, 0))
    transcript = bytearray()
    cursor_replies = 0
    position = 0

    def read(timeout):
        nonlocal cursor_replies
        if not select.select([fd], [], [], timeout)[0]:
            return
        try:
            data = os.read(fd, 65536)
        except OSError as error:
            if error.errno != errno.EIO:
                raise
            return
        transcript.extend(data)
        requests = transcript.count(b"\x1b[6n")
        while cursor_replies < requests:
            os.write(fd, b"\x1b[1;1R")
            cursor_replies += 1

    def expect(value):
        nonlocal position
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            found = transcript.find(value, position)
            if found >= 0:
                position = found + len(value)
                return
            read(0.1)
        raise AssertionError((value, bytes(transcript)))

    def submit(text):
        os.write(fd, text)
        # Wait for input to be rendered, answering cursor-position requests even
        # on slow CI runners. Enter must not join PrettyPrompt's paste burst.
        expect(text)
        deadline = time.monotonic() + 0.25
        while time.monotonic() < deadline:
            read(0.02)
        os.write(fd, b"\r")

    try:
        expect(b"ges> ")
        submit(b":edit")
        expect(b"EDITOR_READY")
        os.write(fd, b"editor input\n")
        expect(b"ges> ")
        assert b"EDITOR_INPUT_OK" in transcript, bytes(transcript)
        submit(b":help")
        expect(b"Discard unsaved changes")
        expect(b"ges> ")
        assert b":edit [ID]" in transcript and b":save [ID]" in transcript
        submit(b":quit")
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            done, status = os.waitpid(pid, os.WNOHANG)
            if done:
                assert os.waitstatus_to_exitcode(status) == 0, bytes(transcript)
                pid = 0
                return
            if select.select([fd], [], [], 0.05)[0]:
                try:
                    transcript.extend(os.read(fd, 65536))
                    requests = transcript.count(b"\x1b[6n")
                    while cursor_replies < requests:
                        os.write(fd, b"\x1b[1;1R")
                        cursor_replies += 1
                except OSError as error:
                    if error.errno != errno.EIO:
                        raise
        raise AssertionError(("CLI did not exit after editor", bytes(transcript)))
    finally:
        os.close(fd)
        if pid:
            try:
                os.kill(pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            os.waitpid(pid, 0)


def main():
    command = sys.argv[1:]
    if not command:
        raise SystemExit("Usage: test-cli-editor.py <CLI executable> [prefix arguments ...]")
    command = [str(Path(value).resolve()) if Path(value).exists() else value for value in command]
    root = ROOT / "artifacts/cli-editor-tests"
    root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=root) as temporary:
        work = Path(temporary)
        editor = work / "fake editor.py"
        plan = work / "edits.json"
        editor.write_text('''import json, os, sys
from pathlib import Path
plan = Path(os.environ["GES_EDITOR_PLAN"])
items = json.loads(plan.read_text(encoding="utf-8"))
item = items.pop(0)
plan.write_text(json.dumps(items), encoding="utf-8")
if "check" in item:
    assert Path(item["check"]).read_text(encoding="utf-8") == item["expected"]
if "external" in item:
    Path(item["external"]).write_text(item.get("external_text", "// external change\\n"), encoding="utf-8")
Path(sys.argv[-1]).write_text(item["text"], encoding="utf-8")
sys.exit(item.get("exit", 0))
''', encoding="utf-8")
        env = dict(os.environ, GES_EDITOR="", VISUAL=f'"{sys.executable}" "{editor}"', GES_EDITOR_PLAN=str(plan))
        env.pop("NO_COLOR", None)

        def run(lines, edits=(), inputs=(), color=False, expected=0, extra=None):
            plan.write_text(json.dumps(list(edits)), encoding="utf-8")
            args = [*command, "run", "--interactive", "-q", *( ["--color"] if color else []), *map(str, inputs)]
            result = subprocess.run(args, input="\n".join(lines) + "\n", cwd=work, env=dict(env, **(extra or {})),
                                    capture_output=True, text=True, encoding="utf-8", timeout=60)
            assert result.returncode == expected, (result.returncode, result.stdout, result.stderr)
            assert json.loads(plan.read_text()) == [], result.stderr
            return result

        def contains(result, *parts):
            for part in parts:
                assert part in result.stderr, (part, result.stderr)

        configured = f'"{sys.executable}" "{editor}"'
        for variables in [
            {"GES_EDITOR": configured, "VISUAL": "missing-visual", "EDITOR": "missing-editor"},
            {"GES_EDITOR": "  ", "VISUAL": configured, "EDITOR": "missing-editor"},
            {"GES_EDITOR": "", "VISUAL": "  ", "EDITOR": configured},
        ]:
            run([":edit", ":quit"], [{"text": ""}], extra=variables)
        # Test the default through PATH without opening a real desktop editor.
        if os.name == "posix":
            default_editor = work / "nano"
            default_editor.write_text(f'#!{sys.executable}\n' + editor.read_text())
            default_editor.chmod(0o755)
            run([":edit", ":quit"], [{"text": ""}], extra={
                "GES_EDITOR": "", "VISUAL": "", "EDITOR": "",
                "PATH": str(work) + os.pathsep + os.environ.get("PATH", ""),
            })

        first = "module scratch\nrecord :Data as { input: :List, sum: :Number computed by input[:sum] }\non initialization { emit ConsoleOut(:Data(input: [1, 2, 3]).sum) }\non Ping { emit ConsoleOut('pong') }\n"
        saved = work / "saved scratch.ges"
        result = run([":edit", ":list", ":source", ":dump", ":quit", f':save 0 "{saved}"', ":list", ":source scratch", ":edit", ":source 0", ":quit!"],
                     [{"text": first}, {"text": "// new scratch\n", "check": str(saved), "expected": first}])
        contains(result, "scratch [no file]", " *", "Unsaved changes in programs: 0", "Saved scratch as program 1", "  1  scratch", ".segment code", "// new scratch")
        assert saved.read_text() == first
        assert "6\n" in result.stdout

        original = work / "program.ges"
        old = "module demo\non Ping { emit ConsoleOut('old') }\n"
        new = "module demo\non Ping { emit ConsoleOut('new') }\n"
        result = run([":edit", ":edit", "emit Ping", ":quit"], [{"text": old}, {"text": ""}])
        assert result.stdout == "", result.stdout

        original.write_text(old)
        result = run([":edit 1", ":reload", "emit Ping", ":source 1", ":edit 1", ":save 1", ":quit"],
                     [{"text": old, "external": str(original), "external_text": new}, {"text": old}], [original])
        assert result.stdout == "new\n", result.stdout
        contains(result, new.strip())
        assert original.read_text() == old

        original.write_text(old)
        result = run([":edit 1", ":reload", "emit Ping", ":source 1", ":quit!"],
                     [{"text": new, "external": str(original), "external_text": "broken @"}], [original])
        assert result.stdout == "new\n", result.stdout
        contains(result, new.strip())

        original.write_text(old)
        result = run([":edit 1", ":reload", "emit Ping", ":source 1", ":quit"],
                     [{"text": old, "external": str(original), "external_text": "broken @"}], [original], expected=1)
        assert result.stdout == "old\n", result.stdout
        contains(result, old.strip())

        original.write_text(old)
        result = run([":edit 1", "emit Ping", ":source 1", ":unload 1", ":quit", ":quit!"], [{"text": new}], [original], expected=1)
        assert result.stdout == "new\n", result.stdout
        assert original.read_text() == old
        contains(result, "Unsaved changes", new.strip())

        result = run([":edit 1", ":edit 1", "emit Ping", ":source 1", ":dump 1", ":save 1", ":quit"],
                     [{"text": new}, {"text": "broken @", "check": str(original), "expected": old}], [original], expected=1)
        assert result.stdout == "new\n"
        assert original.read_text() == "broken @"
        contains(result, "broken @", "last successful compile")

        original.write_text(old)
        result = run([":edit 1", ":save", ":quit!"], [{"text": new, "external": str(original)}], [original], expected=1)
        assert original.read_text() == "// external change\n"
        contains(result, "Save conflict")

        result = run([":edit", f':save 0 "{saved}"', ":quit!"], [{"text": new}], expected=1)
        assert saved.read_text() == first
        contains(result, "Save conflict")

        result = run([":edit", ":source", ":save 0 invalid.ges", ":list", ":quit"], [{"text": "broken @"}], expected=1)
        contains(result, "draft not applied", "Saved scratch as program 1")
        assert (work / "invalid.ges").read_text() == "broken @"

        result = run([":edit"], [{"text": new}], expected=1)
        contains(result, "Unsaved changes")
        result = run([":source", ":dump", ":quit"], color=True, expected=1)
        contains(result, "No scratch found", "\x1b[31m")
        result = run([":source", ":quit"], color=True, expected=1, extra={"NO_COLOR": "1"})
        assert "\x1b[" not in result.stderr

        original.write_text(old)
        second = work / "second.ges"
        second.write_text("module demo\nfunction value() be 1\n")
        result = run([":edit 1", ":edit 1 2", ":save", ":quit"], [{"text": "module demo\nfunction value() be 2\n"}], [original, second], expected=1)
        contains(result, "Select a source")
        assert original.read_text() == old
        assert "be 2" in second.read_text()
        if os.name == "posix":
            terminal_editor(command, work, False)
            terminal_editor(command, work, True)
        print("Editor drafts, scratch promotion, save conflicts, reload, quit guards and error colors passed.")


if __name__ == "__main__":
    main()
