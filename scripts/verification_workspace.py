# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Disposable verification builds with retained diagnostics."""

from contextlib import contextmanager
from pathlib import Path
import shutil
import sys
import tempfile


@contextmanager
def verification_workspace(parent: Path, prefix: str):
    """Keep failed workspaces; retain only top-level logs after successful checks.

    Each invocation builds in isolation. Successful logs overwrite the same names
    in latest-logs, so normal repeated verification does not retain build trees.
    Existing workspaces (including another running check) are never removed.
    """
    parent.mkdir(parents=True, exist_ok=True)
    workspace = Path(tempfile.mkdtemp(prefix=prefix, dir=parent))
    try:
        yield workspace
        logs = parent / "latest-logs"
        if logs.is_symlink():
            raise RuntimeError(f"Refusing symlinked log directory: {logs}")
        logs.mkdir(exist_ok=True)
        for source in workspace.glob("*.log"):
            destination = logs / source.name
            if source.is_symlink() or destination.is_symlink():
                raise RuntimeError(f"Refusing symlinked verification log: {source.name}")
            shutil.copy2(source, destination)
        shutil.rmtree(workspace)
    except BaseException:
        print(f"Verification workspace retained for diagnosis: {workspace}", file=sys.stderr)
        raise
    print(f"Verification build files removed. Latest logs: {logs}")
