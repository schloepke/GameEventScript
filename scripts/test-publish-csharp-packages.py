#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Qualify publication selection with a fake dotnet; never access NuGet."""

import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent
PACKAGES = ("GameEventScript.Runtime", "GameEventScript.Compiler", "GameEventScript.CSharpBridge")
VERSION = "0.1.0-test.1"


class PublicationTests(unittest.TestCase):
    def setUp(self):
        artifacts = ROOT / "artifacts/publish-selection-tests"
        artifacts.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="candidate ", dir=artifacts)
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.log = self.directory / "calls.jsonl"
        fake = self.directory / "dotnet"
        fake.write_text('''#!/usr/bin/env python3
import json, os, sys
with open(os.environ["GES_PUBLISH_TEST_LOG"], "a") as output:
    output.write(json.dumps(sys.argv[1:]) + "\\n")
''')
        fake.chmod(0o755)
        self.environment = dict(os.environ, PATH=str(self.directory) + os.pathsep + os.environ["PATH"],
                                NUGET_API_KEY="test-only", GES_PUBLISH_TEST_LOG=str(self.log))
        for package in (*PACKAGES, "GameEventScript.Conformance", "GameEventScript.Tool"):
            for extension in ("nupkg", "snupkg"):
                (self.directory / f"{package}.{VERSION}.{extension}").touch()
                (self.directory / f"{package}.9.9.9.{extension}").touch()

    def publish(self, version=VERSION):
        return subprocess.run(["sh", str(ROOT / "scripts/publish-csharp-packages.sh"), str(self.directory), version],
                              env=self.environment, capture_output=True, text=True, timeout=10)

    def test_only_public_libraries_of_selected_version_are_uploaded(self):
        result = self.publish()
        self.assertEqual(0, result.returncode, result.stderr)
        calls = [json.loads(line) for line in self.log.read_text().splitlines()]
        self.assertEqual([
            ["nuget", "push", str(self.directory / f"{package}.{VERSION}.nupkg"),
             "--api-key", "test-only", "--source", "https://api.nuget.org/v3/index.json"]
            for package in PACKAGES
        ], calls)

    def test_missing_artifact_prevents_any_upload(self):
        for extension in ("nupkg", "snupkg"):
            with self.subTest(extension=extension):
                path = self.directory / f"{PACKAGES[-1]}.{VERSION}.{extension}"
                path.unlink()
                self.assertNotEqual(0, self.publish().returncode)
                self.assertFalse(self.log.exists())
                path.touch()

    def test_missing_credentials_prevent_any_upload(self):
        del self.environment["NUGET_API_KEY"]
        self.assertNotEqual(0, self.publish().returncode)
        self.assertFalse(self.log.exists())

    def test_invalid_version_prevents_any_upload(self):
        for version in ("", "../other", "0.1.0;echo nope", "0.1.0 extra"):
            with self.subTest(version=version):
                self.assertNotEqual(0, self.publish(version).returncode)
                self.assertFalse(self.log.exists())


if __name__ == "__main__":
    unittest.main()
