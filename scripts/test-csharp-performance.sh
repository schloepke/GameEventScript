#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"

GES_RUN_PERFORMANCE_TESTS=1 dotnet test implementation/csharp/tests/GameEventScript.Tests/GameEventScript.Tests.csproj --configuration Release --filter "TestCategory=Performance"
