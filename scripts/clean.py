#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Remove only known repository build outputs, without following symlinks."""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys


def tracked_paths(root):
    """Protect versioned content; source archives without Git metadata are supported."""
    if not (root / ".git").exists():
        return set()
    result = subprocess.run(
        ["git", "-C", str(root), "ls-files", "--cached", "-z"],
        check=True, capture_output=True,
    )
    return {os.fsdecode(path) for path in result.stdout.split(b"\0") if path}


def targets(root, artifacts_only, report):
    candidates = []

    def add(path):
        if path.is_symlink():
            report(f"Skip symbolic link: {path.relative_to(root)}")
        elif path.is_dir():
            candidates.append(path)

    add(root / "artifacts")
    if not artifacts_only:
        for name in ("bin", "obj", "TestResults", ".build"):
            add(root / name)
        implementation = root / "implementation"
        if implementation.is_symlink():
            report("Skip symbolic link: implementation")
        else:
            for language, names in (
                ("csharp", {"bin", "obj", "TestResults"}),
                ("swift", {".build"}),
            ):
                base = implementation / language
                if base.is_symlink():
                    report(f"Skip symbolic link: implementation/{language}")
                    continue
                for directory, children, _ in os.walk(base, followlinks=False):
                    for name in list(children):
                        path = Path(directory) / name
                        if name in names:
                            add(path)
                            children.remove(name)
                        elif path.is_symlink() or name == ".git":
                            children.remove(name)
    return sorted(candidates)


def logical_size(path):
    """Count file bytes without traversing any directory or file symlink."""
    size = 0
    for directory, children, files in os.walk(path, followlinks=False):
        children[:] = [name for name in children if not (Path(directory) / name).is_symlink()]
        for name in files:
            item = Path(directory) / name
            if not item.is_symlink():
                size += item.stat().st_size
    return size


def clean(root, *, dry_run=False, artifacts_only=False, tracked=None, report=print):
    if not (root / "AGENTS.md").is_file() or not (root / "implementation").is_dir():
        raise RuntimeError("Not a GameEventScript workspace (AGENTS.md/implementation missing).")
    paths = targets(root, artifacts_only, report)
    tracked = tracked_paths(root) if tracked is None else tracked
    for path in paths:
        relative = path.relative_to(root).as_posix()
        if any(name == relative or name.startswith(relative + "/") for name in tracked):
            raise RuntimeError(f"Refusing to delete tracked content below {relative}; no directories removed.")
    sizes = [logical_size(path) for path in paths]
    for path, size in zip(paths, sizes):
        report(f"{'Would remove' if dry_run else 'Remove'}: {path.relative_to(root)} ({size / 1048576:.1f} MiB)")
        if not dry_run:
            shutil.rmtree(path)
    action = "Would remove" if dry_run else "Removed"
    report(f"{action} {len(paths)} directories, {sum(sizes) / 1048576:.1f} MiB of file data.")
    return paths


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="List directories and sizes without deleting anything.")
    parser.add_argument("--artifacts-only", action="store_true", help="Remove only the repository's artifacts directory.")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    try:
        clean(root, dry_run=args.dry_run, artifacts_only=args.artifacts_only)
    except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"Clean failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
