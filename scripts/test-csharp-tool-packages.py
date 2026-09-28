#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Validate tool release completeness and upload ordering using fake packages/SDK."""
import json
import os
import runpy
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from zipfile import ZipFile

sys.dont_write_bytecode = True
from csharp_tool_packages import AOT, MANAGED, PACKAGE_IDS, RIDS, release_packages

ROOT = Path(__file__).resolve().parent.parent
VERSION = "0.1.0-test.1"


def fixture(directory, identity, version=VERSION, targets=RIDS):
    package = ET.Element("package", xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd")
    meta = ET.SubElement(package, "metadata")
    ET.SubElement(meta, "id").text = identity
    ET.SubElement(meta, "version").text = version
    kinds = ET.SubElement(meta, "packageTypes")
    ET.SubElement(kinds, "packageType", name="DotnetToolRidPackage" if identity.startswith(AOT + ".") else "DotnetTool")
    settings = ET.Element("DotNetCliTool", Version="2")
    commands = ET.SubElement(settings, "Commands")
    attrs = {"Name": "dotnet-ges"}
    if identity != AOT:
        attrs.update(EntryPoint="app", Runner="dotnet" if identity == MANAGED else "executable")
    ET.SubElement(commands, "Command", **attrs)
    if identity == AOT:
        rids = ET.SubElement(settings, "RuntimeIdentifierPackages")
        for rid in targets:
            ET.SubElement(rids, "RuntimeIdentifierPackage", RuntimeIdentifier=rid, Id=AOT + "." + rid)
    path = directory / f"{identity}.{VERSION}.nupkg"
    with ZipFile(path, "w") as archive:
        archive.writestr("tool.nuspec", ET.tostring(package))
        archive.writestr("tools/net8.0/any/DotnetToolSettings.xml", ET.tostring(settings))
        archive.writestr("tools/net8.0/any/app", b"fake")
        for name in ("LICENSE", "README.md", "THIRD-PARTY-NOTICES.md", "icon.png"):
            archive.writestr(name, b"fake")
    return path


class ToolReleaseTests(unittest.TestCase):
    def setUp(self):
        root = ROOT / "artifacts/csharp/tool-package-controls"
        root.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=root)
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        for identity in PACKAGE_IDS:
            fixture(self.directory, identity)

    def test_complete_set_and_dependency_order(self):
        packages = release_packages(self.directory, VERSION)
        self.assertEqual([f"{name}.{VERSION}.nupkg" for name in PACKAGE_IDS], [path.name for path in packages])
        self.assertEqual(AOT + "." + VERSION + ".nupkg", packages[-1].name)

    def test_every_missing_package_prevents_publication(self):
        for identity in PACKAGE_IDS:
            with self.subTest(identity=identity):
                (self.directory / f"{identity}.{VERSION}.nupkg").unlink()
                with self.assertRaises(FileNotFoundError):
                    release_packages(self.directory, VERSION)
                fixture(self.directory, identity)

    def test_wrong_version_and_incomplete_pointer(self):
        fixture(self.directory, MANAGED, version="9.9.9")
        with self.assertRaises(ValueError):
            release_packages(self.directory, VERSION)
        fixture(self.directory, MANAGED)
        fixture(self.directory, AOT, targets=RIDS[:-1])
        with self.assertRaises(ValueError):
            release_packages(self.directory, VERSION)

    def test_explicit_publication_allowlist_and_preflight(self):
        fake = self.directory / "dotnet"
        log = self.directory / "calls"
        fake.write_text('#!/usr/bin/env python3\nimport json, os, sys\nwith open(os.environ["TEST_LOG"], "a") as f: f.write(json.dumps(sys.argv[1:]) + "\\n")\n')
        fake.chmod(0o755)
        env = dict(os.environ, PATH=str(self.directory) + os.pathsep + os.environ["PATH"], TEST_LOG=str(log), NUGET_API_KEY="test-only")
        command = [sys.executable, str(ROOT / "scripts/publish-csharp-tools.py"), str(self.directory), VERSION]
        (self.directory / "GameEventScript.Conformance.0.1.0-test.1.nupkg").touch()
        subprocess.run(command, env=env, check=True, capture_output=True)
        self.assertFalse(log.exists())
        subprocess.run([*command, "--publish"], env=env, check=True, capture_output=True)
        calls = [json.loads(line) for line in log.read_text().splitlines()]
        self.assertEqual([str(path) for path in release_packages(self.directory, VERSION)], [call[2] for call in calls])
        log.unlink()
        (self.directory / f"{AOT}.{VERSION}.nupkg").unlink()
        result = subprocess.run([*command, "--publish"], env=env, capture_output=True)
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(log.exists())


class InstalledCommandTests(unittest.TestCase):
    def test_sdk_shim_names(self):
        name = runpy.run_path(str(ROOT / "scripts/pack-csharp-tool.py"))["installed_command_name"]
        self.assertEqual(name(True, True), "dotnet-ges.cmd")
        self.assertEqual(name(False, True), "dotnet-ges.exe")
        self.assertEqual(name(True, False), "dotnet-ges")
        self.assertEqual(name(False, False), "dotnet-ges")


if __name__ == "__main__":
    unittest.main()
