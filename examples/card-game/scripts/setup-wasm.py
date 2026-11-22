#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Prepare the Wasm build: the browser dependencies, and the pinned Swift tools when no matching ones are installed.

An installed Swift.org toolchain with its matching Wasm SDK (swiftly and `swift sdk install`) is used as it is. Without
one, or with --download, the pinned toolchain and SDK are downloaded below artifacts on macOS. System Swift, Xcode
selection and global SDK installations are never changed.
"""
import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import sys

EXAMPLE = Path(__file__).resolve().parents[1]
ROOT = EXAMPLE.parents[1]
sys.path.insert(0, str(ROOT / 'scripts'))
from swift_toolchains import installed_sdk  # noqa: E402
ARTIFACTS = ROOT / 'artifacts/card-game'
SDK = ARTIFACTS / 'sdk'
VERSION = '6.4.0'
SDK_NAME = f'swift-{VERSION}-RELEASE_wasm.artifactbundle'
SDK_HASH = 'f07b7be3c586d92d7a07051fc6d303b87ebea67eadc40640ba59d5a8b79aa86d'
# Recorded after verifying Apple's notarization and the Swift Open Source installer signature.
TOOLCHAIN_HASH = '8fd03185b98fe27f54a54631c2449decf75d5b466ce8e34abbd414141063c6aa'


def acquire(url, filename, expected):
    destination = SDK / filename
    if not destination.exists():
        temporary = destination.with_suffix(destination.suffix + '.partial')
        subprocess.run(['curl', '-fL', '--retry', '2', url, '-o', str(temporary)], check=True)
        temporary.replace(destination)
    with destination.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    if digest != expected:
        raise RuntimeError(f'Checksum mismatch: {destination}; remove the incomplete download and retry')
    return destination


def download():
    if sys.platform != 'darwin':
        raise RuntimeError('This local tool bootstrap targets macOS. Install a Swift.org toolchain with its matching Wasm SDK on other hosts.')
    SDK.mkdir(parents=True, exist_ok=True)
    bundle = acquire(f'https://download.swift.org/swift-{VERSION}-release/wasm-sdk/swift-{VERSION}-RELEASE/{SDK_NAME}.tar.gz', 'swift-wasm.tar.gz', SDK_HASH)
    if not (SDK / SDK_NAME).exists():
        subprocess.run(['tar', '-xzf', str(bundle), '-C', str(SDK)], check=True)
    installer = acquire(f'https://download.swift.org/swift-{VERSION}-release/xcode/swift-{VERSION}-RELEASE/swift-{VERSION}-RELEASE-osx.pkg', 'swift-toolchain.pkg', TOOLCHAIN_HASH)
    if not (ARTIFACTS / 'toolchain').exists():
        subprocess.run(['pkgutil', '--check-signature', str(installer)], check=True)
        subprocess.run(['pkgutil', '--expand-full', str(installer), str(ARTIFACTS / 'toolchain')], check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--download', action='store_true', help='Download the pinned Swift toolchain and Wasm SDK even if matching ones are installed')
    args = parser.parse_args()
    system = None if args.download else installed_sdk('wasm')
    if system:
        print(f'Using the installed Swift {system.version} toolchain and Wasm SDK; nothing downloaded.')
    else:
        download()
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    dependencies = ARTIFACTS / 'web-deps'
    dependencies.mkdir(exist_ok=True)
    for name in ['package.json', 'package-lock.json']:
        shutil.copy2(EXAMPLE / 'browser-dependencies' / name, dependencies / name)
    subprocess.run(['npm', 'ci', '--prefix', str(dependencies), '--cache', str(ARTIFACTS / 'npm-cache'), '--ignore-scripts', '--no-audit', '--no-fund'], check=True)
    print('Swift/Wasm tools are ready. System Swift and Xcode settings were not changed.')


if __name__ == '__main__':
    main()
