#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Publish a verified static build to origin/site without touching the source checkout.

The initial site commit has no parent. Later commits preserve that independent
history and use normal fast-forward pushes. Authentication comes from Git's
existing configuration (GITHUB_TOKEN in Actions), never webhosting credentials.
"""

import argparse
import os
from pathlib import Path
import re
import subprocess
import tempfile
import sys

sys.dont_write_bytecode = True
from website_assets import validate_static_assets

ROOT = Path(__file__).resolve().parent.parent
MARKER = "Website-Source: "


def git(repository, *arguments, environment=None, input_text=None):
    result = subprocess.run(["git", *arguments], cwd=repository, env=environment,
                            input=input_text, text=True, capture_output=True, timeout=120)
    if result.returncode:
        # Git's existing credential configuration is used without printing it.
        raise RuntimeError(f"git {arguments[0]} failed:\n{result.stderr}")
    return result.stdout.strip()


def publish(repository, output, source):
    repository, output = repository.resolve(), output.resolve()
    source = git(repository, "rev-parse", "--verify", f"{source}^{{commit}}")
    for relative in ("index.html", "404.html", "docs/index.html", "pagefind/pagefind.js"):
        if not (output / relative).is_file():
            raise RuntimeError(f"Incomplete website build: missing {relative}")
    if not (output / "_astro").is_dir():
        raise RuntimeError("Incomplete website build: missing _astro assets")
    validate_static_assets(output)

    def is_current():
        git(repository, "fetch", "--no-tags", "origin", "+refs/heads/main:refs/remotes/origin/main")
        return git(repository, "rev-parse", "refs/remotes/origin/main") == source

    if not is_current():
        print("Skipped: a newer main revision must supply the website build.")
        return

    previous = None
    if git(repository, "ls-remote", "--heads", "origin", "refs/heads/site"):
        git(repository, "fetch", "--no-tags", "origin", "+refs/heads/site:refs/remotes/origin/site")
        previous = git(repository, "rev-parse", "refs/remotes/origin/site")
        message = git(repository, "show", "-s", "--format=%B", previous)
        if not re.search(r"^Website-Source: [0-9a-f]{40,64}$", message, re.MULTILINE):
            raise RuntimeError("origin/site is not a generated website branch; refusing to replace it.")

    # A separate index stages only the build output, including removed assets.
    # The source branch, its worktree and its real index remain unchanged.
    with tempfile.TemporaryDirectory(prefix="ges-site-index-") as temporary:
        environment = dict(os.environ,
                           GIT_DIR=git(repository, "rev-parse", "--absolute-git-dir"),
                           GIT_WORK_TREE=str(output), GIT_INDEX_FILE=str(Path(temporary) / "index"),
                           GIT_AUTHOR_NAME="github-actions[bot]", GIT_COMMITTER_NAME="github-actions[bot]",
                           GIT_AUTHOR_EMAIL="41898282+github-actions[bot]@users.noreply.github.com",
                           GIT_COMMITTER_EMAIL="41898282+github-actions[bot]@users.noreply.github.com")
        git(output, "read-tree", "--empty", environment=environment)
        git(output, "add", "--all", "--force", "--", ".", environment=environment)
        tree = git(output, "write-tree", environment=environment)
        if previous and tree == git(repository, "rev-parse", f"{previous}^{{tree}}"):
            print("Website unchanged; no deployment commit needed.")
            return
        parents = ["-p", previous] if previous else []
        commit = git(output, "-c", "commit.gpgsign=false", "commit-tree", tree, *parents,
                     environment=environment,
                     input_text=f"Publish website from {source[:12]}\n\n{MARKER}{source}\n")
    if not is_current():
        print("Skipped: main advanced while preparing publication.")
        return
    # No force: a concurrent update to site must fail rather than be overwritten.
    git(repository, "push", "origin", f"{commit}:refs/heads/site")
    print(f"Published {source[:12]} as site commit {commit[:12]}.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=ROOT)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/website/dist")
    parser.add_argument("--source", required=True, help="Full source revision used to build the artifact")
    args = parser.parse_args()
    publish(args.repository, args.output, args.source)
