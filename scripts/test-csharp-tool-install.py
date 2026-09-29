#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check native installation lifecycle with a fake SDK and disposable repository."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent


class InstallationTests(unittest.TestCase):
    def test_native_and_managed_lifecycle(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            scripts = root / "scripts"
            scripts.mkdir()
            for name in ("install-csharp-tool.sh", "uninstall-csharp-tool.sh", "csharp-aot-install.sh", "build-csharp.sh"):
                shutil.copy2(ROOT / "scripts" / name, scripts / name)
            (root / "LICENSE").write_text("license")
            notices = root / "implementation/csharp/GameEventScript.Tool/THIRD-PARTY-NOTICES.md"
            notices.parent.mkdir(parents=True)
            notices.write_text("notices")
            sdk = root / "sdk"
            sdk.mkdir()
            fake = sdk / "dotnet"
            fake.write_text('''#!/usr/bin/env python3
import os, sys
from pathlib import Path
a = sys.argv[1:]
def arg(n): return a[a.index(n)+1]
def binary(p):
 p.parent.mkdir(parents=True, exist_ok=True)
 p.write_text('#!/bin/sh\\necho fake-cli\\n'); p.chmod(0o755)
if a[0] == 'msbuild': print('0.1.0')
elif a[0] == 'publish':
 if os.environ.get('FAIL_PUBLISH'): sys.exit(1)
 binary(Path(arg('--output')) / 'GameEventScript.Tool')
elif a[0] == 'tool':
 p = Path(arg('--tool-path')) if '--tool-path' in a else Path(os.environ['HOME'])/'.dotnet/tools'
 marker = p/'.managed'
 if a[1] == 'list':
  if marker.exists(): print('GameEventScript.Tool 0.1.0 dotnet-ges')
 elif a[1] == 'uninstall':
  (p/'dotnet-ges').unlink(); marker.unlink()
 elif a[1] in ('install', 'update'):
  binary(p/'dotnet-ges'); marker.write_text('owned')
''')
            fake.chmod(0o755)
            env = dict(os.environ, PATH=str(sdk) + os.pathsep + os.environ['PATH'])
            destination = root / "tools with spaces"

            def run(script, *args, success=True, extra=None):
                result = subprocess.run([str(scripts / script), *args, "--tool-path", str(destination)],
                                        env=dict(env, **(extra or {})), capture_output=True, text=True)
                self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)

            install = "install-csharp-tool.sh"
            uninstall = "uninstall-csharp-tool.sh"
            run(install, "--aot")
            original = (destination / "dotnet-ges").read_bytes()
            run(install, "--aot")
            run(install, "--aot", success=False, extra={"FAIL_PUBLISH": "1"})
            self.assertEqual(original, (destination / "dotnet-ges").read_bytes())
            run(install)  # Native -> managed.
            self.assertTrue((destination / ".managed").exists())
            self.assertFalse((destination / ".ges-csharp-aot.sha256").exists())
            run(install, "--aot")  # Managed -> native.
            self.assertFalse((destination / ".managed").exists())
            (destination / "dotnet-ges").write_text("modified")
            run(install, "--aot", success=False)
            run(install, success=False)
            run(uninstall, success=False)
            (destination / "dotnet-ges").write_bytes(original)
            run(uninstall)
            self.assertFalse((destination / "dotnet-ges").exists())
            (destination / "dotnet-ges").write_text("unrelated")
            run(install, "--aot", success=False)
            self.assertEqual("unrelated", (destination / "dotnet-ges").read_text())
            (destination / "dotnet-ges").unlink()
            (destination / "dotnet-ges").symlink_to(root / "missing")
            run(install, "--aot", success=False)


if __name__ == "__main__":
    unittest.main()
