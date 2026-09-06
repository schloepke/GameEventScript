#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
package_output="$repository_root/artifacts/csharp/packages"
mkdir -p "$package_output"
cd "$repository_root"

dotnet pack implementation/csharp/src/StepH.GameEventScript/StepH.GameEventScript.csproj --configuration Release --output "$package_output"
dotnet pack implementation/csharp/src/StepH.GameEventScript.CSharpBridge/StepH.GameEventScript.CSharpBridge.csproj --configuration Release --output "$package_output"
dotnet pack implementation/csharp/src/StepH.GameEventScript.Conformance/StepH.GameEventScript.Conformance.csproj --configuration Release --output "$package_output"
