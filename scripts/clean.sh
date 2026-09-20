#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -eu

ges_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
exec python3 "$ges_root/scripts/clean.py" "$@"
