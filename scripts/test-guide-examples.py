#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Compile and run the authored introductory guide examples against this checkout."""

import argparse
import re
import subprocess
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "artifacts/website/examples"


def blocks(name, language):
    source = (ROOT / "docs/guide" / name).read_text()
    return re.findall(r"^```" + language + r"\n(.*?)^```", source, re.MULTILINE | re.DOTALL)


def run(arguments, directory=ROOT):
    result = subprocess.run(arguments, cwd=directory, text=True, capture_output=True, timeout=900)
    if result.returncode:
        raise RuntimeError(f"{arguments!r}\n{result.stdout}\n{result.stderr}")
    return result.stdout.strip()


def csharp():
    directory = OUTPUT / "csharp"
    directory.mkdir(parents=True, exist_ok=True)
    references = "\n".join(
        f'<ProjectReference Include="{escape(str(ROOT / "implementation/csharp" / module / "src" / (module + ".csproj")))}" />'
        for module in ("GameEventScript.Runtime", "GameEventScript.Compiler", "GameEventScript.CSharpBridge")
    )
    (directory / "GuideExample.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>12</LangVersion></PropertyGroup>
<ItemGroup>{references}</ItemGroup></Project>
''')
    (directory / "Program.cs").write_text(blocks("CSharp.md", "csharp")[0])
    run(["dotnet", "build", str(directory / "GuideExample.csproj"), "--configuration", "Release", "--nologo"])
    if run(["dotnet", str(directory / "bin/Release/net8.0/GuideExample.dll")]) != "42":
        raise RuntimeError("C# guide did not print 42.")

    bridge_examples = blocks(ROOT / "implementation/csharp/GameEventScript.CSharpBridge/README.md", "csharp")
    bridge_source = bridge_examples[0].replace(
        ".WithExternalTypeRegistry(registry)",
        ".WithExternalTypeRegistry(registry)\n    .WithRegistry(GameEventScriptCSharpExtensions.CreateRegistry(typeof(CombatFunctions)))",
    )
    (directory / "Program.cs").write_text(bridge_source + "\n" + bridge_examples[1])
    run(["dotnet", "build", str(directory / "GuideExample.csproj"), "--configuration", "Release", "--nologo"])
    if run(["dotnet", str(directory / "bin/Release/net8.0/GuideExample.dll")]) != "Energy: 100, near wall: True":
        raise RuntimeError("C# bridge attributes did not produce the documented output.")

    tool = ROOT / "implementation/csharp/GameEventScript.Tool/src/GameEventScript.Tool.csproj"
    run(["dotnet", "build", str(tool), "--configuration", "Release", "--nologo"])
    expectations = ["Damage: 42", "42\n42", "Damage: 120", "Hello, player", "No player supplied", None, "160, 60", "Ready"]
    examples = blocks("Language.md", "ges")
    if len(examples) != len(expectations):
        raise RuntimeError("Update the expected outputs when adding language examples.")
    for index, (example, expected) in enumerate(zip(examples, expectations)):
        source = OUTPUT / f"language-{index + 1}.ges"
        source.write_text(example)
        actual = run(["dotnet", "run", "--project", str(tool), "--configuration", "Release", "--no-build", "--", "run", str(source), "--quiet"])
        if expected is None:
            expected = "[20, 60, 40]\nTotal: 60"
        if actual != expected:
            raise RuntimeError(f"Language example {index + 1}: expected {expected!r}, got {actual!r}")
    print(f"C# embedding, bridge annotations and {len(examples)} language examples passed.")


def swift():
    directory = OUTPUT / "swift"
    source = directory / "Sources/GesExample"
    source.mkdir(parents=True, exist_ok=True)
    examples = blocks("Swift.md", "swift")
    # Consume the current root distribution rather than an unrelated published tag.
    manifest = examples[0].replace(
        '.package(url: "https://github.com/schloepke/GameEventScript.git", exact: "<release-version>")',
        '.package(path: "' + str(ROOT).replace('\\', '\\\\').replace('"', '\\"') + '")',
    )
    (directory / "Package.swift").write_text(manifest)
    (source / "main.swift").write_text(examples[1] + "\n" + examples[2])
    scratch = OUTPUT / "swift-build"
    run(["swift", "build", "--package-path", str(directory), "--scratch-path", str(scratch),
         "--disable-build-manifest-caching", "--configuration", "release"])
    if run([str(scratch / "release/GesExample")]) != "42":
        raise RuntimeError("Swift guide did not print 42.")
    print("Swift embedding and annotation examples passed.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--language", choices=("csharp", "swift", "all"), default="all")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if args.language in ("csharp", "all"):
        csharp()
    if args.language in ("swift", "all"):
        swift()
