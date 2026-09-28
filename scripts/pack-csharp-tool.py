#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Prepare and locally consume NuGet CLI packages; never publish."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
from csharp_tool_packages import AOT, MANAGED, RIDS, validate_package, version_checked

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "implementation/csharp/GameEventScript.Tool/src/GameEventScript.Tool.csproj"


def run(args, **kwargs):
    subprocess.run([str(a) for a in args], cwd=ROOT, check=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version", type=version_checked)
    parser.add_argument("target", choices=("portable", *RIDS))
    args = parser.parse_args()
    output = ROOT / "artifacts/csharp/tool-packages" / args.version / args.target
    output.mkdir(parents=True, exist_ok=True)
    base = ["dotnet", "pack", PROJECT, "-c", "Release", "-p:Version=" + args.version, "-p:PackageVersion=" + args.version, "-o", output]
    native = args.target != "portable"
    package_id = AOT if native else MANAGED
    if native:
        run([*base, "-p:GesPackAotTool=true", "-r", args.target])
        validate_package(output / f"{AOT}.{args.target}.{args.version}.nupkg", AOT + "." + args.target, args.version)
        # Installation must exercise the pointer's RID selection as well.
    else:
        run(base)
        validate_package(output / f"{MANAGED}.{args.version}.nupkg", MANAGED, args.version)
    run([*base, "-p:GesPackAotTool=true"])
    validate_package(output / f"{AOT}.{args.version}.nupkg", AOT, args.version)
    with tempfile.TemporaryDirectory(prefix="consumer-", dir=output) as temporary:
        work = Path(temporary)
        config = ET.Element("configuration")
        sources = ET.SubElement(config, "packageSources")
        ET.SubElement(sources, "clear")
        ET.SubElement(sources, "add", key="local", value=str(output))
        ET.SubElement(sources, "add", key="nuget", value="https://api.nuget.org/v3/index.json")
        mapping = ET.SubElement(config, "packageSourceMapping")
        local = ET.SubElement(mapping, "packageSource", key="local")
        ET.SubElement(local, "package", pattern="GameEventScript.*")
        sdk = ET.SubElement(mapping, "packageSource", key="nuget")
        ET.SubElement(sdk, "package", pattern="Microsoft.*")
        config_path = work / "NuGet.Config"
        ET.ElementTree(config).write(config_path, encoding="utf-8", xml_declaration=True)
        env = dict(os.environ, NUGET_PACKAGES=str(work / "packages"))
        run(["dotnet", "tool", "install", package_id, "--version", args.version, "--tool-path", work / "tool", "--configfile", config_path, "--no-cache"], env=env)
        binary = work / "tool" / ("dotnet-ges.exe" if os.name == "nt" else "dotnet-ges")
        run([sys.executable, ROOT / "scripts/test-cli-aot.py", binary, *([] if native else ["--managed"])], env=env)
        run([sys.executable, ROOT / "scripts/test-cli-editor.py", binary], env=env)
        if native and os.name != "nt":
            run([sys.executable, ROOT / "scripts/test-cli-delayed.py", binary], env=env)
    # Keep one canonical pointer from the portable job, avoiding artifact collisions.
    if native:
        (output / f"{AOT}.{args.version}.nupkg").unlink()
    print(f"Verified {package_id} {args.version} ({args.target}) in {output}")


if __name__ == "__main__":
    main()
