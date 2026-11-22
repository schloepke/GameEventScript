#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Build every library product of the root Swift package for Android.

Without options it uses an installed Swift.org toolchain with its matching Swift SDK for Android. Swift CI passes the
SDK it installed into its container and the container's `swift`. The SDK needs an Android NDK (r27 or newer): when
its `ndk-sysroot` is not set up yet, this script runs the SDK's `setup-android-sdk.sh` with `ANDROID_NDK_HOME` or the
newest NDK below `ANDROID_HOME`, `ANDROID_SDK_ROOT` or Android Studio's default SDK directory. Xcode's compiler
cannot build for Android.
"""

import argparse
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from swift_toolchains import installed_sdk, sdk_directories  # noqa: E402

# The oldest API level the Swift SDK supports, for each architecture it ships.
TRIPLES = ("aarch64-unknown-linux-android23", "x86_64-unknown-linux-android23", "armv7-unknown-linux-android23")


def find_ndk():
    """ANDROID_NDK_HOME, else the newest NDK of the Android SDK, or None."""
    if os.environ.get("ANDROID_NDK_HOME"):
        return Path(os.environ["ANDROID_NDK_HOME"])
    roots = [Path(os.environ[name]) for name in ("ANDROID_HOME", "ANDROID_SDK_ROOT") if os.environ.get(name)]
    roots.append(Path.home() / ("Library/Android/sdk" if sys.platform == "darwin" else "Android/Sdk"))
    for root in roots:
        versions = [path for path in (root / "ndk").glob("*") if (path / "source.properties").is_file()]
        if versions:
            return max(versions, key=lambda path: tuple(int(part) for part in path.name.split(".") if part.isdigit()))
    return None


def prepare_ndk(sdk):
    """Run the SDK's NDK setup unless its sysroot already links an NDK; returns an error message or None."""
    bundle = next((directory / f"{sdk}.artifactbundle" for directory in sdk_directories()
                   if (directory / f"{sdk}.artifactbundle").is_dir()), None)
    if bundle is None:
        return f"Swift SDK bundle {sdk} is not installed"
    variant = bundle / "swift-android"
    if (variant / "ndk-sysroot/usr/include").exists():
        return None
    ndk = find_ndk()
    if ndk is None:
        return "no Android NDK found; install one (r27 or newer) and set ANDROID_NDK_HOME"
    print(f"Setting up {sdk} with the NDK in {ndk}", flush=True)
    environment = dict(os.environ, ANDROID_NDK_HOME=str(ndk))
    setup = subprocess.run(["bash", str(variant / "scripts/setup-android-sdk.sh")], env=environment, check=False)
    return None if setup.returncode == 0 else "setup-android-sdk.sh failed"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--swift", help="swift executable of the toolchain that matches the SDK")
    parser.add_argument("--sdk", help="Swift SDK ID, for example swift-6.4.0-RELEASE_android")
    parser.add_argument("--triple", action="append", help=f"target triple, repeatable; default: {', '.join(TRIPLES)}")
    parser.add_argument("--configuration", choices=["debug", "release"], default="debug")
    args = parser.parse_args()
    swift, sdk = args.swift, args.sdk
    if not swift or not sdk:
        system = installed_sdk("android")
        if not system:
            parser.error("no Swift.org toolchain with its matching Swift SDK for Android is installed; pass --swift and --sdk")
        swift, sdk = swift or str(system.swift), sdk or system.android_id
    error = prepare_ndk(sdk)
    if error:
        parser.error(error)
    for triple in args.triple or TRIPLES:
        command = [swift, "build", "--package-path", str(ROOT), "--swift-sdk", sdk, "--triple", triple,
                   "--configuration", args.configuration, "--scratch-path", str(ROOT / "artifacts/swift/android" / triple),
                   "--disable-build-manifest-caching"]
        print(" ".join(command), flush=True)
        status = subprocess.run(command, check=False).returncode
        if status:
            return status
    return 0


if __name__ == "__main__":
    sys.exit(main())
