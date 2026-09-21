#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Consume the root distribution through a tagged Git URL, never sibling packages.

Copy current sources (including uncommitted edits) into an isolated repository
under artifacts. No tag, commit or remote in the working repository is changed.
"""

import json
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
MODULES = ("GameEventScriptRuntime", "GameEventScriptCompiler", "GameEventScriptSwiftBridge")


def run(arguments, cwd, log):
    with log.open("a") as output:
        result = subprocess.run(arguments, cwd=cwd, stdout=output, stderr=subprocess.STDOUT, timeout=300)
    if result.returncode:
        raise RuntimeError(f"Command failed: {arguments!r}\nSee {log}\n{log.read_text()[-8000:]}")


def main():
    artifacts = ROOT / "artifacts/swift-package"
    artifacts.mkdir(parents=True, exist_ok=True)
    workspace = Path(tempfile.mkdtemp(prefix="release consumer ", dir=artifacts))
    repository = workspace / "GameEventScript"
    repository.mkdir()
    for name in ("Package.swift", "LICENSE", "README.md"):
        shutil.copy2(ROOT / name, repository / name)
    for module in MODULES:
        relative = Path("implementation/swift") / module / "Sources" / module
        shutil.copytree(ROOT / relative, repository / relative)

    description = json.loads(subprocess.check_output([
        "swift", "package", "--package-path", str(repository),
        "--scratch-path", str(workspace / "describe"), "describe", "--type", "json",
    ], text=True))
    products = {item["name"]: item for item in description["products"]}
    targets = {item["name"]: item for item in description["targets"]}
    if set(products) != set(MODULES) or set(targets) != set(MODULES) or description.get("dependencies"):
        raise RuntimeError("Distribution must contain exactly the three libraries and no package dependencies.")
    for module in MODULES:
        if products[module]["type"] != {"library": ["automatic"]} or products[module]["targets"] != [module]:
            raise RuntimeError(f"Unexpected product definition for {module}")
        expected = [] if module == "GameEventScriptRuntime" else ["GameEventScriptRuntime"]
        if targets[module].get("target_dependencies", []) != expected:
            raise RuntimeError(f"Unexpected dependency graph for {module}")
        # Explicit source lists in one manifest must not silently omit future sources.
        authored = {str(path.relative_to(repository / targets[module]["path"]))
                    for path in (repository / targets[module]["path"]).rglob("*.swift")}
        if set(targets[module]["sources"]) != authored:
            raise RuntimeError(f"Distribution omits authored sources from {module}")

    log = workspace / "git.log"
    for args in (["init"], ["add", "."],
                 ["-c", "user.name=Package verification", "-c", "user.email=package-test@example.invalid",
                  "-c", "commit.gpgsign=false", "commit", "-m", "Distribution fixture"],
                 ["-c", "tag.gpgsign=false", "tag", "0.1.0-package-test"]):
        run(["git", *args], repository, log)

    fixture = workspace / "program.gesb"
    # Runtime is built first in its own clean scratch directory. Resolution may
    # fetch the whole repository; compiling Runtime must not compile other GES modules.
    binaries = {}
    for consumer, modules in (("RuntimeConsumer", MODULES[:1]), ("SwiftPMConsumer", MODULES)):
        directory = workspace / consumer
        source = directory / "Sources" / consumer
        source.mkdir(parents=True)
        shutil.copy2(ROOT / "implementation/swift/verification" / consumer / "main.swift", source / "main.swift")
        dependencies = ", ".join(f'.product(name: "{name}", package: "GameEventScript")' for name in modules)
        (directory / "Package.swift").write_text(f'''// swift-tools-version: 6.0
import PackageDescription
let package = Package(name: "{consumer}",
    dependencies: [.package(url: {json.dumps(str(repository))}, exact: "0.1.0-package-test")],
    targets: [.executableTarget(name: "{consumer}", dependencies: [{dependencies}])])
''')
        scratch = workspace / (consumer + "-build")
        args = ["swift", "build", "--package-path", str(directory), "--scratch-path", str(scratch),
                "--build-system", "native", "--disable-build-manifest-caching", "--configuration", "release"]
        run(args, directory, workspace / (consumer + ".log"))
        if consumer == "RuntimeConsumer":
            for module in MODULES[1:]:
                # SwiftPM creates planning directories even for unused products;
                # only emitted modules or compiled objects indicate a build.
                objects = [path for directory in scratch.rglob(module + ".build") for path in directory.rglob("*.o")]
                if list(scratch.rglob(module + ".swiftmodule")) or objects:
                    raise RuntimeError(f"Runtime-only consumption built {module}")
        binaries[consumer] = scratch / "release" / consumer
        resolved = json.loads((directory / "Package.resolved").read_text())
        if len(resolved["pins"]) != 1 or resolved["pins"][0]["state"].get("version") != "0.1.0-package-test":
            raise RuntimeError("Consumer did not resolve the tagged distribution.")
    for consumer in ("SwiftPMConsumer", "RuntimeConsumer"):
        run([str(binaries[consumer]), str(fixture)], workspace, workspace / (consumer + ".log"))
    print(f"SwiftPM tagged distribution and Runtime-only consumption passed. Logs: {workspace}")


if __name__ == "__main__":
    main()
