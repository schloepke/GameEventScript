#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"

# Keep separate test assemblies from competing with elapsed-time measurements.
GES_RUN_PERFORMANCE_TESTS=1 dotnet test GameEventScript.sln --configuration Release -m:1 --filter "TestCategory=Performance"
