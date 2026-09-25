#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify a CLI archive on its native platform, after relocation, without an SDK."""

import argparse
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
from cli_distribution import ROOT, archive_name, extract, identity, sha256


def native_target():
    system = {"Darwin": "osx", "Linux": "linux", "Windows": "win"}[platform.system()]
    arch = {"arm64": "arm64", "aarch64": "arm64", "x86_64": "x64", "amd64": "x64"}[platform.machine().lower()]
    return f"{system}-{arch}"


def verify(archive, implementation, release, target):
    if native_target() != target:
        raise ValueError(f"Native verification requires {target}; this machine is {native_target()}.")
    base = ROOT / "artifacts/cli/verification"
    base.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="relocated CLI with spaces ", dir=base) as temporary:
        workspace = Path(temporary)
        root = extract(archive, workspace / "unpacked", identity(implementation, release, target))
        executable = root / "bin" / ("dotnet-ges" if implementation == "csharp" else "ges")
        if target.startswith("win-"):
            executable = executable.with_suffix(".exe")
        info = json.loads((root / "build-info.json").read_text())
        if info["version"] != release or info["target"] != target or info["implementation"] != implementation:
            raise ValueError("Archive identity does not match the requested build.")
        if not (root / "LICENSE").is_file() or not (root / "README.md").is_file():
            raise ValueError("Archive is missing its license or installation instructions.")
        environment = {**os.environ, "DOTNET_ROOT": str(workspace / "no-dotnet"), "DOTNET_MULTILEVEL_LOOKUP": "0", "NO_COLOR": "1"}
        # Tool subprocesses must not discover compilers or a Swift toolchain.
        environment["PATH"] = str(workspace / "no-tools")

        def run(arguments, expected=0, input=None):
            result = subprocess.run([str(executable), *arguments], cwd=workspace, env=environment,
                                    input=input, capture_output=True, text=True, encoding="utf-8", timeout=30)
            if result.returncode != expected:
                raise RuntimeError(f"CLI {arguments} exited {result.returncode}: {result.stdout}\n{result.stderr}")
            return result.stdout.replace("\r\n", "\n")

        expected_version = f"dotnet ges {release}" if implementation == "csharp" else f"ges {release} (Swift)"
        if run(["--version"]).strip() != expected_version:
            raise ValueError("Executable version does not match archive version.")
        run(["--help"])
        source = workspace / "main.ges"
        source.write_text('on Main(args) { for arg in args { emit ConsoleOut(arg) } }\n', encoding="utf-8")
        run(["check", str(source), "-q"])
        run(["compile", str(source), "-q"])
        if ".segment code" not in run(["dump", str(source.with_suffix(".gesb"))]):
            raise ValueError("Missing GESA output.")
        if run(["run", str(source.with_suffix(".gesb")), "-q", "--", "Grüße", "--color"]) != "Grüße\n--color\n":
            raise ValueError("Argument or UTF-8 output mismatch.")
        if run(["run", "--interactive", "-q"], input='emit ConsoleOut("REPL")\n:quit\n') != "REPL\n":
            raise ValueError("Redirected REPL did not execute.")
        source.write_text('on Main(args) { emit after 0.01s Tick() }\non Tick { emit ConsoleOut("delayed"); emit ErrorCode(7) }', encoding="utf-8")
        if run(["run", str(source), "-q"], expected=7) != "delayed\n":
            raise ValueError("Delayed delivery/exit-code mismatch.")
        if os.name != "nt":
            subprocess.run([sys.executable, str(ROOT / "scripts/test-cli-delayed.py"), str(executable)],
                           cwd=workspace, env=environment, check=True, timeout=90)
        if implementation == "swift" and target.startswith("linux-"):
            headers = subprocess.check_output(["readelf", "-l", str(executable)], text=True, stderr=subprocess.PIPE)
            if "INTERP" in headers:
                raise ValueError("Linux Swift download must not require a dynamic loader.")
        return info


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("implementation", choices=["csharp", "swift"])
    parser.add_argument("version")
    parser.add_argument("target")
    parser.add_argument("--directory", type=Path, required=True)
    args = parser.parse_args()
    name = archive_name(args.implementation, args.version, args.target)
    archive = args.directory.resolve() / name
    (archive.parent / (name + ".json")).unlink(missing_ok=True)
    info = verify(archive, args.implementation, args.version, args.target)
    receipt = {"archive": name, "version": args.version, "revision": info["revision"], "implementation": args.implementation,
               "target": args.target, "sha256": sha256(archive), "verified": True, "sourceClean": info["sourceClean"]}
    (archive.parent / (name + ".json")).write_text(json.dumps(receipt, indent=2) + "\n")
    print(f"Verified native CLI archive: {name}")


if __name__ == "__main__":
    main()
