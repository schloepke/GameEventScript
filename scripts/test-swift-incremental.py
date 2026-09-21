#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Exercise local-package source discovery through the repository's Swift build script."""

from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts/swift-incremental-tests"


def main():
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    workspace = Path(tempfile.mkdtemp(prefix="source changes ", dir=ARTIFACTS))
    scripts = workspace / "scripts"
    scripts.mkdir()
    shutil.copy2(ROOT / "scripts/build-swift.sh", scripts / "build-swift.sh")
    packages = workspace / "implementation/swift"
    runtime = packages / "GameEventScriptRuntime"
    compiler = packages / "GameEventScriptCompiler"
    runtime_source = runtime / "Sources/GameEventScriptRuntime"
    compiler_source = compiler / "Sources/GameEventScriptCompiler"
    runtime_source.mkdir(parents=True)
    compiler_source.mkdir(parents=True)
    (runtime / "Package.swift").write_text('''// swift-tools-version: 6.0
import PackageDescription
let package = Package(name: "GameEventScriptRuntime",
    products: [.library(name: "GameEventScriptRuntime", targets: ["GameEventScriptRuntime"])],
    targets: [.target(name: "GameEventScriptRuntime")])
''')
    (compiler / "Package.swift").write_text('''// swift-tools-version: 6.0
import PackageDescription
let package = Package(name: "GameEventScriptCompiler",
    products: [.executable(name: "probe", targets: ["GameEventScriptCompiler"])],
    dependencies: [.package(path: "../GameEventScriptRuntime")],
    targets: [.executableTarget(name: "GameEventScriptCompiler", dependencies: [
        .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")])])
''')
    (compiler_source / "main.swift").write_text('import GameEventScriptRuntime\nprint(Host.value())\n')
    host = runtime_source / "Host.swift"
    host.write_text('public enum Host { public static func value() -> Int { 1 } }\n')

    def build(step, expected):
        log = workspace / (step + ".log")
        with log.open("w") as output:
            result = subprocess.run([str(scripts / "build-swift.sh")], cwd=workspace,
                                    stdout=output, stderr=subprocess.STDOUT, timeout=180)
        if result.returncode:
            raise RuntimeError(f"{step} failed; see {log}\n{log.read_text()[-8000:]}")
        binary = workspace / "artifacts/swift/compiler/release/probe"
        actual = subprocess.check_output([str(binary)], text=True, timeout=10).strip()
        if actual != str(expected):
            raise RuntimeError(f"{step}: expected {expected}, got {actual}; see {log}")

    build("01-initial", 1)
    # Native SwiftPM may plan again on the first repeated invocation. Warm it
    # fully before changing dependency sources, as in a long-lived checkout.
    build("02-warm", 1)
    objects = {path: path.stat().st_mtime_ns for path in (workspace / "artifacts").rglob("*.o")}
    if not objects:
        raise RuntimeError("No object files found for incremental reuse check.")
    build("03-unchanged", 1)
    if any(path.stat().st_mtime_ns != stamp for path, stamp in objects.items()):
        raise RuntimeError("Unchanged build recompiled object files.")

    builder = runtime_source / "HostBuilder.swift"
    builder.write_text('public struct GameEventScriptHostBuilder { public init() {} }\n')
    host.write_text('''public enum Host {
    public static func value() -> Int { 2 }
    public static func createBuilder() -> GameEventScriptHostBuilder { .init() }
}
''')
    build("04-added-source", 2)
    builder = builder.rename(runtime_source / "RenamedHostBuilder.swift")
    build("05-renamed-source", 2)
    builder.unlink()
    host.write_text('public enum Host { public static func value() -> Int { 3 } }\n')
    build("06-removed-source", 3)
    print(f"Swift incremental source discovery and object reuse passed. Logs: {workspace}")


if __name__ == "__main__":
    main()
