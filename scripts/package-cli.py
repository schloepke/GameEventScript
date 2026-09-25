#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Build a standalone CLI archive below artifacts; never publish or install it."""

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
import zipfile

sys.dont_write_bytecode = True
from cli_distribution import ROOT, TARGETS, archive_name, identity, sha256, version


def run(arguments, **kwargs):
    subprocess.run([str(arg) for arg in arguments], cwd=ROOT, check=True, **kwargs)


def csharp(work, output, release, target):
    project = ROOT / "implementation/csharp/GameEventScript.Tool/src/GameEventScript.Tool.csproj"
    run(["dotnet", "publish", project, "--configuration", "Release", "--runtime", target,
         "--self-contained", "true", "--artifacts-path", work / "build", "--output", output / "bin",
         "-p:PackAsTool=false", "-p:PublishTrimmed=false", "-p:PublishSingleFile=false",
         f"-p:Version={release}", "-p:IncludeSourceRevisionInInformationalVersion=false"])
    suffix = ".exe" if target.startswith("win-") else ""
    (output / "bin" / ("GameEventScript.Tool" + suffix)).rename(output / "bin" / ("dotnet-ges" + suffix))
    shutil.copy2(ROOT / "implementation/csharp/GameEventScript.Tool/THIRD-PARTY-NOTICES.md", output / "THIRD-PARTY-NOTICES.md")
    # The exact runtime pack selected by the SDK supplies its own license and
    # third-party notices; do not substitute this repository's Apache license.
    config = json.loads((output / "bin/GameEventScript.Tool.runtimeconfig.json").read_text())
    runtime_version = next(f["version"] for f in config["runtimeOptions"]["includedFrameworks"] if f["name"] == "Microsoft.NETCore.App")
    environment = {**os.environ, "DOTNET_CLI_UI_LANGUAGE": "en"}
    packages = subprocess.check_output(["dotnet", "nuget", "locals", "global-packages", "--list"], env=environment, text=True).strip().split(": ", 1)[1]
    runtime = Path(packages) / f"microsoft.netcore.app.runtime.{target}" / runtime_version
    notices = [p for p in runtime.iterdir() if p.is_file() and ("license" in p.name.lower() or "notice" in p.name.lower())]
    if len(notices) < 2:
        raise RuntimeError(f"Missing .NET runtime license/notices in {runtime}")
    destination = output / "licenses/dotnet-runtime"
    destination.mkdir(parents=True)
    for notice in notices:
        shutil.copy2(notice, destination / notice.name)


def swift(work, output, release, target, sdks):
    # Inject version only in a disposable source tree. Never rewrite the checkout
    # or mix generated production-version constants with development builds.
    for name in ("GameEventScriptRuntime", "GameEventScriptCompiler", "GameEventScriptSyntaxHighlighter", "GameEventScriptTool"):
        relative = Path("implementation/swift") / name
        shutil.copytree(ROOT / relative, work / "source" / relative,
                        ignore=shutil.ignore_patterns(".build", ".swiftpm", ".DS_Store", "Package.resolved"))
    package = work / "source/implementation/swift/GameEventScriptTool"
    info = package / "Sources/GameEventScriptTool/ToolBuildInfo.swift"
    info.write_text(info.read_text().replace('"development"', json.dumps(release)))
    options = ["--package-path", package, "--scratch-path", work / "build", "--build-system", "native",
               "--disable-build-manifest-caching", "--configuration", "release", "--product", "ges"]
    if target.startswith("linux-"):
        if sdks is None:
            raise ValueError("Linux Swift downloads require --swift-sdks-path with the matching Static Linux SDK.")
        architecture = "aarch64" if target.endswith("arm64") else "x86_64"
        options += ["--swift-sdks-path", sdks, "--swift-sdk", f"{architecture}-swift-linux-musl"]
        sboms = list(sdks.rglob("sbom.spdx.json"))
        if len(sboms) != 1 or not any(p.get("name") == "swift" and p.get("versionInfo") == "6.4.0-RELEASE"
                                      for p in json.loads(sboms[0].read_text())["packages"]):
            raise RuntimeError("Use only the pinned Swift 6.4.0 Static Linux SDK; update dependency notices with SDK upgrades.")
        license_source = ROOT / "tools/distribution/licenses"
        sources = json.loads((license_source / "sources.json").read_text())
        if any(not (license_source / name).is_file() for name in sources):
            raise RuntimeError("Missing Static Linux SDK dependency notices.")
        shutil.copytree(license_source, output / "licenses/components")
        # Preserve all license/notice files and SBOMs shipped with the SDK.
        notices = [p for p in sdks.rglob("*") if p.is_file() and
                   (any(word in p.name.lower() for word in ("license", "copyright", "notice")) or p.name.endswith(".spdx.json"))]
        if not notices or not any(p.name.endswith(".spdx.json") for p in notices):
            raise RuntimeError("The Static Linux SDK must provide license files and its SPDX bill of materials.")
        for notice in notices:
            destination = output / "licenses/swift-sdk" / notice.relative_to(sdks)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(notice, destination)
    else:
        architecture = "arm64" if target.endswith("arm64") else "x86_64"
        # A stated release baseline avoids accidental dependencies on the build
        # machine's newer OS. Local source builds retain their existing minimum.
        options += ["--triple", f"{architecture}-apple-macosx15.0"]
    run(["swift", "build", *options])
    binary_path = subprocess.check_output(["swift", "build", *map(str, options), "--show-bin-path"], cwd=ROOT, text=True).strip()
    (output / "bin").mkdir()
    shutil.copy2(Path(binary_path) / "ges", output / "bin/ges")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("implementation", choices=TARGETS)
    parser.add_argument("version", type=version)
    parser.add_argument("target")
    parser.add_argument("--swift-sdks-path", type=Path)
    args = parser.parse_args()
    name = identity(args.implementation, args.version, args.target)
    directory = ROOT / "artifacts/cli" / args.version
    directory.mkdir(parents=True, exist_ok=True)
    # Invalidate any earlier verification before beginning a replacement build.
    archive = directory / archive_name(args.implementation, args.version, args.target)
    for old in (archive, Path(str(archive) + ".json"), Path(str(archive) + ".sha256")):
        old.unlink(missing_ok=True)
    with tempfile.TemporaryDirectory(prefix=name + "-", dir=directory) as temporary:
        work = Path(temporary)
        output = work / name
        output.mkdir()
        if args.implementation == "csharp":
            csharp(work, output, args.version, args.target)
        else:
            swift(work, output, args.version, args.target, args.swift_sdks_path.resolve() if args.swift_sdks_path else None)
        shutil.copy2(ROOT / "LICENSE", output / "LICENSE")
        shutil.copy2(ROOT / "docs/guide/distribution/Tools.md", output / "README.md")
        revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        (output / "build-info.json").write_text(json.dumps({"implementation": args.implementation, "version": args.version,
                                                         "target": args.target, "revision": revision,
                                                         "sourceClean": not subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT).strip()}, indent=2) + "\n")
        for path in output.rglob("*"):
            if path.is_symlink() or path.name == ".DS_Store":
                raise ValueError(f"Unexpected distribution file: {path}")
        if args.target.startswith("win-"):
            with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as package:
                for path in sorted(output.rglob("*")):
                    if path.is_file():
                        package.write(path, path.relative_to(work))
        else:
            with tarfile.open(archive, "w:gz") as package:
                package.add(output, arcname=name)
        Path(str(archive) + ".sha256").write_text(f"{sha256(archive)}  {archive.name}\n")
    print(f"Prepared {archive}; run verify-cli-archive.py on {args.target} before publication.")


if __name__ == "__main__":
    main()
