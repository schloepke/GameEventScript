#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Build every library product of the root Swift package for WebAssembly (WASI).

Without options it uses an installed Swift.org toolchain with its matching Wasm SDK. Swift CI passes the SDK it
installed into its container and the container's `swift`. Xcode's compiler cannot build for Wasm.
"""

import argparse
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from swift_toolchains import installed_sdk  # noqa: E402


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--swift", help="swift executable of the toolchain that matches the SDK")
    parser.add_argument("--sdk", help="Swift SDK ID, for example swift-6.4.0-RELEASE_wasm")
    parser.add_argument("--configuration", choices=["debug", "release"], default="debug")
    args = parser.parse_args()
    swift, sdk = args.swift, args.sdk
    if not swift or not sdk:
        system = installed_sdk("wasm")
        if not system:
            parser.error("no Swift.org toolchain with its matching Wasm SDK is installed; pass --swift and --sdk")
        swift, sdk = swift or str(system.swift), sdk or system.wasm_id
    command = [swift, "build", "--package-path", str(ROOT), "--swift-sdk", sdk, "--configuration", args.configuration,
               "--scratch-path", str(ROOT / "artifacts/swift/wasm"), "--disable-build-manifest-caching"]
    print(" ".join(command), flush=True)
    return subprocess.run(command, check=False).returncode


if __name__ == "__main__":
    sys.exit(main())
