#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Skip native Swift verification only for known documentation or browser-only inputs."""

import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent.parent
DOC_FILES = {"README.md", "CHANGELOG.md", "BACKLOG.md", "AGENTS.md", "LICENSING.md", "LICENSE", "NOTICE", ".spi.yml"}

# These assets are exercised by the always-selected Website workflow's Card Lab
# native/WASM build and browser tests. New files require an explicit scope review.
CARD_WEB_FILES = {"examples/card-game/web/" + name for name in (
    "app.mjs", "card-actions.mjs", "diagnostics.mjs", "dump.mjs",
    "editor.mjs", "editor-folding.mjs", "engine.mjs", "games.mjs",
    "loading.mjs", "search.mjs", "worker.mjs", "worker-client.mjs",
    "examples.json", "index.html", "style.css",
)}


def requires_swift(paths):
    # This is deliberately an allowlist of irrelevant inputs. New directories,
    # specs, Markdown conformance cases, scripts and workflow edits run all gates.
    return any(path not in DOC_FILES and path not in CARD_WEB_FILES and not path.startswith(("docs/", "website/")) for path in paths)


def changed_paths(event_name, event, repository=ROOT):
    if event_name == "pull_request":
        pr = event["pull_request"]
        # Compare the complete PR from its merge base, not just its last commit.
        base, head = pr["base"]["sha"], pr["head"]["sha"]
        base = subprocess.check_output(["git", "merge-base", base, head], cwd=repository, text=True).strip()
    elif event_name == "push" and event.get("before", "").strip("0"):
        base, head = event["before"], event["after"]
    else:
        # Manual runs and new branches always get complete verification.
        return None
    # Treat renames as a removal plus addition so moving code into a docs folder
    # cannot hide the removal. NUL delimiters preserve arbitrary Git filenames.
    output = subprocess.check_output(["git", "diff", "--no-renames", "--name-only", "-z", base, head], cwd=repository, stderr=subprocess.PIPE)
    return [path.decode("utf-8", errors="surrogateescape") for path in output.split(b"\0") if path]


def main():
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text())
    paths = changed_paths(os.environ["GITHUB_EVENT_NAME"], event)
    full = paths is None or requires_swift(paths)
    with open(os.environ["GITHUB_OUTPUT"], "a") as output:
        output.write(f"swift={'true' if full else 'false'}\n")
    print("Full Swift verification required." if full else "Only known website/guide assets changed; native Swift verification is unchanged.")


if __name__ == "__main__":
    main()
