#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"

# Keep separate test assemblies from competing with elapsed-time measurements.
GES_RUN_PERFORMANCE_TESTS=1 dotnet test GameEventScript.sln --configuration Release -m:1 --filter "TestCategory=Performance" "$@"

# The independent runner owns corpus assertions and measurement budgets.
dotnet build implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj --configuration Release
python3 conformance/runner/main.py run --corpus conformance/suites/performance --kind performance \
    --performance-profile csharp-dotnet-release-managed --performance-profile csharp-dotnet-release-macos-arm64 \
    --output artifacts/conformance/python/performance-results.json \
    --adapter dotnet artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll
