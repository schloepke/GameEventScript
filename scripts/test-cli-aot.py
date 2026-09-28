#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Exercise an already published native CLI without a discoverable .NET runtime."""

import argparse
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--managed", action="store_true", help="Keep the runtime available for portable-package checks")
    args = parser.parse_args()
    binary = args.binary.resolve(strict=True)
    root = Path(__file__).resolve().parent.parent / "artifacts/csharp/aot-checks"
    root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=root) as temporary:
        work = Path(temporary)
        env = dict(os.environ, PATH=str(work / "no-runtime"),
                   DOTNET_ROOT=str(work / "no-runtime"), DOTNET_MULTILEVEL_LOOKUP="0", NO_COLOR="1")
        if args.managed:
            env = dict(os.environ, NO_COLOR="1")
        source = work / "main.ges"
        source.write_text('module aot.probe\non Main(args) { for arg in args { emit ConsoleOut(arg) } }\n', encoding="utf-8")

        def check(name, arguments, stdin=None, stdout=None, stderr=None, exit_code=0):
            result = subprocess.run([str(binary), *arguments], cwd=work, env=env,
                                    input=stdin, capture_output=True, text=True, encoding="utf-8", timeout=30)
            if (result.returncode != exit_code
                    or (stdout is not None and stdout not in result.stdout)
                    or (stderr is not None and stderr not in result.stderr)):
                raise AssertionError(f"{name}: exit {result.returncode}\n{result.stdout}\n{result.stderr}")
            print(f"PASS {name}")

        check("version", ["--version"])
        check("check", ["check", str(source), "-q"])
        check("compile", ["compile", str(source), "-q"])
        check("dump", ["dump", str(source.with_suffix(".gesb"))], stdout=".segment code")
        check("run binary", ["run", str(source.with_suffix(".gesb")), "-q", "--", "Grüße", "42"], stdout="Grüße\n42\n")
        check("REPL", ["run", "--interactive", "-q"], 'emit ConsoleOut("REPL")\n:quit\n', stdout="REPL\n")
        check("list", ["run", str(source), "--interactive", "-q"], ":list\n:quit\n", stderr='main.ges"')
        check("source", ["run", str(source), "--interactive", "-q"], ":source aot.probe\n:quit\n", stderr="on Main(args)")
        check("verbose text escaping", ["run", str(source), "--verbose", "--", 'Hello\n"World"'],
              stdout='Hello\n"World"\n', stderr='Hello\\n\\u0022World\\u0022')
        source.write_text('on Main(args) { emit after 0.01s Tick() }\non Tick { emit ConsoleOut("delayed"); emit ErrorCode(7) }', encoding="utf-8")
        check("delayed delivery and exit code", ["run", str(source), "-q"], stdout="delayed\n", exit_code=7)


if __name__ == "__main__":
    main()
