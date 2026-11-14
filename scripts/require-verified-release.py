#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Refuse publication unless the release commit is on main and C# CI and Swift CI succeeded for exactly that commit.

The release workflow runs a subset of the gates itself; this check binds publication to the complete verification that
already ran on main, instead of a documented manual checklist. It reads the workflow runs of the commit through the
GitHub API (`gh api`), so it needs `GH_TOKEN` with `actions: read`.
"""
import argparse
import json
import os
import subprocess
import sys

REQUIRED_WORKFLOWS = ("C# CI", "Swift CI")
# Jobs that must have run and succeeded, not only been skipped: Swift CI skips its native jobs for documentation-only
# changes and still concludes with success, which does not verify the Swift port.
REQUIRED_JOBS = {"Swift CI": ("native", "distribution", "linux", "linux-minimum")}


def missing_workflows(runs, required=REQUIRED_WORKFLOWS, jobs=lambda run: []):
    """The required workflows without a successful completed run on main for the commit whose required jobs all succeeded."""
    succeeded = set()
    for run in runs:
        if run.get("status") != "completed" or run.get("conclusion") != "success" or run.get("head_branch") != "main":
            continue
        needed = REQUIRED_JOBS.get(run.get("name"), ())
        if needed:
            concluded = {job.get("name"): job.get("conclusion") for job in jobs(run)}
            if any(concluded.get(name) != "success" for name in needed):
                continue
        succeeded.add(run.get("name"))
    return [name for name in required if name not in succeeded]


def on_main(commit):
    return subprocess.run(["git", "merge-base", "--is-ancestor", commit, "origin/main"], check=False).returncode == 0


def workflow_runs(repository, commit):
    output = subprocess.check_output(["gh", "api", "--paginate", f"repos/{repository}/actions/runs?head_sha={commit}&per_page=100",
                                      "--jq", ".workflow_runs[]"], text=True)
    return [json.loads(line) for line in output.splitlines() if line.strip()]


def run_jobs(repository, run):
    output = subprocess.check_output(["gh", "api", "--paginate", f"repos/{repository}/actions/runs/{run['id']}/jobs?per_page=100",
                                      "--jq", ".jobs[]"], text=True)
    return [json.loads(line) for line in output.splitlines() if line.strip()]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--commit", default=os.environ.get("GITHUB_SHA"))
    parser.add_argument("--repository", default=os.environ.get("GITHUB_REPOSITORY"))
    args = parser.parse_args()
    if not args.commit or not args.repository:
        raise SystemExit("The commit and repository are required.")
    if not on_main(args.commit):
        raise SystemExit(f"Release commit {args.commit} is not on main; publish only a verified main commit.")
    missing = missing_workflows(workflow_runs(args.repository, args.commit), jobs=lambda run: run_jobs(args.repository, run))
    if missing:
        raise SystemExit(f"Release commit {args.commit} has no successful run on main, with every required job run, of: {', '.join(missing)}.")
    print(f"Release commit {args.commit} is on main and passed {', '.join(REQUIRED_WORKFLOWS)}.")


if __name__ == "__main__":
    main()
