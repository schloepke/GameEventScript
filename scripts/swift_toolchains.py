# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Find an installed Swift.org toolchain with its matching Swift SDK.

Swift SDKs installed with `swift sdk install` live in SwiftPM's per-user directory; Swift.org toolchains come from
swiftly or the Swift.org installer. An SDK only works with the toolchain of exactly its version, so an SDK counts
only together with that toolchain. Xcode's compiler supports none of the Wasm, Android and Static Linux SDKs.
"""

from dataclasses import dataclass
import os
from pathlib import Path
import re
import sys

_BUNDLE = re.compile(r"^swift-(\d+\.\d+(?:\.\d+)?)-RELEASE_(wasm|android|static-linux-[0-9.]+)\.artifactbundle$")


@dataclass(frozen=True)
class SwiftSdk:
    """An installed Swift SDK bundle and the swift executable of its toolchain."""

    version: str
    swift: Path
    directory: Path
    bundle: Path

    @property
    def wasm_id(self):
        return f"swift-{self.version}-RELEASE_wasm"

    @property
    def android_id(self):
        return f"swift-{self.version}-RELEASE_android"


def _version_key(version):
    return tuple(int(part) for part in version.split("."))


def sdk_directories():
    """The per-user Swift SDK directories SwiftPM uses by default.

    SwiftPM takes the home directory from the user database, not from `HOME`; GitHub container jobs set `HOME` to
    another directory, so both count.
    """
    homes = [Path.home()]
    if sys.platform != "win32":
        import pwd
        user_home = Path(pwd.getpwuid(os.getuid()).pw_dir)
        if user_home not in homes:
            homes.append(user_home)
    if sys.platform == "darwin":
        candidates = [home / "Library/org.swift.swiftpm/swift-sdks" for home in homes]
    else:
        candidates = [home / ".swiftpm/swift-sdks" for home in homes]
        if os.environ.get("XDG_CONFIG_HOME"):
            candidates.append(Path(os.environ["XDG_CONFIG_HOME"]) / "swiftpm/swift-sdks")
    return [directory for directory in candidates if directory.is_dir()]


def toolchain_swift(version):
    """The swift executable of the installed Swift.org toolchain of exactly this version, or None."""
    home = Path.home()
    candidates = [root / f"swift-{version}-RELEASE.xctoolchain/usr/bin/swift"
                  for root in (home / "Library/Developer/Toolchains", Path("/Library/Developer/Toolchains"))]
    swiftly = os.environ.get("SWIFTLY_TOOLCHAINS_DIR")
    for root in ([Path(swiftly)] if swiftly else []) + [home / ".local/share/swiftly/toolchains"]:
        candidates.append(root / version / "usr/bin/swift")
    return next((path for path in candidates if path.is_file() and os.access(path, os.X_OK)), None)


def installed_sdk(kind):
    """The newest installed SDK of `kind` ("wasm", "android" or "static-linux") whose toolchain is installed, or None."""
    found = []
    for directory in sdk_directories():
        for bundle in directory.iterdir():
            match = _BUNDLE.match(bundle.name)
            if not match or not bundle.is_dir() or not match.group(2).startswith(kind):
                continue
            swift = toolchain_swift(match.group(1))
            if swift:
                found.append(SwiftSdk(match.group(1), swift, directory, bundle))
    return max(found, key=lambda sdk: _version_key(sdk.version), default=None)
