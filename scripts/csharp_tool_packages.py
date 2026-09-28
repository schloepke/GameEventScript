# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Explicit NuGet tool release identities and validation (no publication on import)."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from zipfile import ZipFile

RIDS = ("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
MANAGED = "GameEventScript.Tool"
AOT = MANAGED + ".Aot"
# Pointer last: every dependency must be pushed before it becomes installable.
PACKAGE_IDS = (MANAGED, *(AOT + "." + rid for rid in RIDS), AOT)


def version_checked(version):
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?", version):
        raise ValueError("Use a three-part NuGet version with an optional prerelease suffix")
    return version


def validate_package(path, package_id, version):
    with ZipFile(path) as archive:
        specs = [name for name in archive.namelist() if name.endswith('.nuspec')]
        if len(specs) != 1:
            raise ValueError(f"Invalid nuspec set: {path}")
        spec = ET.fromstring(archive.read(specs[0]))
        ns = {"n": spec.tag.split("}")[0].lstrip("{")}
        meta = spec.find("n:metadata", ns)
        if meta is None or meta.findtext("n:id", namespaces=ns) != package_id or meta.findtext("n:version", namespaces=ns) != version:
            raise ValueError(f"Package identity mismatch: {path}")
        expected_type = "DotnetToolRidPackage" if package_id.startswith(AOT + ".") else "DotnetTool"
        kind = meta.find("n:packageTypes/n:packageType", ns)
        if kind is None or kind.get("name") != expected_type:
            raise ValueError(f"Wrong package type: {path}")
        for name in ("LICENSE", "README.md", "THIRD-PARTY-NOTICES.md", "icon.png"):
            if name not in archive.namelist():
                raise ValueError(f"Missing {name}: {path}")
        settings_names = [name for name in archive.namelist() if name.endswith('/DotnetToolSettings.xml')]
        if len(settings_names) != 1:
            raise ValueError(f"Invalid tool settings set: {path}")
        settings = ET.fromstring(archive.read(settings_names[0]))
        commands = settings.findall("Commands/Command")
        if len(commands) != 1 or commands[0].get("Name") != "dotnet-ges":
            raise ValueError(f"Wrong command: {path}")
        if package_id == AOT:
            targets = [(item.get("RuntimeIdentifier"), item.get("Id")) for item in settings.findall("RuntimeIdentifierPackages/RuntimeIdentifierPackage")]
            if sorted(targets) != sorted((rid, AOT + "." + rid) for rid in RIDS):
                raise ValueError(f"Incomplete or unexpected AOT platform references: {path}")
        else:
            entry = commands[0].get("EntryPoint", "")
            entry_path = str(Path(settings_names[0]).parent / entry).replace("\\", "/")
            if not entry or entry_path not in archive.namelist():
                raise ValueError(f"Missing tool entry point: {path}")
            if package_id != MANAGED and commands[0].get("Runner") != "executable":
                raise ValueError(f"AOT package does not use a native executable: {path}")


def release_packages(directory, version):
    version_checked(version)
    packages = [Path(directory) / f"{name}.{version}.nupkg" for name in PACKAGE_IDS]
    for path, name in zip(packages, PACKAGE_IDS):
        validate_package(path, name, version)
    return packages
