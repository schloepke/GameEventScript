#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Fail closed when any required Swift verification job did not succeed."""
import os
import sys


def accepted(scope_result, run_swift, results):
    if scope_result != "success" or run_swift not in ("true", "false"):
        return False
    expected = "success" if run_swift == "true" else "skipped"
    return len(results) == 3 and all(result == expected for result in results)


if __name__ == "__main__":
    results = [os.environ.get(name) for name in
               ("NATIVE_RESULT", "DISTRIBUTION_RESULT", "HIGHLIGHTER_RESULT")]
    success = accepted(os.environ.get("SCOPE_RESULT"), os.environ.get("RUN_SWIFT"), results)
    print("Swift verification passed." if success else f"Swift verification failed: {results}")
    sys.exit(0 if success else 1)
