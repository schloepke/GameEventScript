#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"

dotnet build implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj --configuration Release
python3 conformance/runner/main.py run --kind supported \
    --output artifacts/conformance/python/csharp-results.json \
    --adapter dotnet artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll
python3 conformance/runner/main.py run --corpus conformance/suites/performance/low-allocation.md \
    --performance-profile csharp-dotnet-release-managed \
    --output artifacts/conformance/python/csharp-allocation.json \
    --adapter dotnet artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll
