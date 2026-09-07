#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
package_output=${2:-"$repository_root/artifacts/csharp/packages"}

case "$release_version" in
    ''|.*|*[!0-9A-Za-z.-]*)
        echo "Invalid package version: $release_version" >&2
        exit 1
        ;;
esac

mkdir -p "$package_output"
cd "$repository_root"
dotnet restore GameEventScript.sln -p:NuGetAudit=false

for package_id in StepH.GameEventScript StepH.GameEventScript.CSharpBridge StepH.GameEventScript.Conformance; do
    rm -f "$package_output/$package_id.$release_version.nupkg" "$package_output/$package_id.$release_version.snupkg"
done

dotnet pack implementation/csharp/src/StepH.GameEventScript/StepH.GameEventScript.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"
dotnet pack implementation/csharp/src/StepH.GameEventScript.CSharpBridge/StepH.GameEventScript.CSharpBridge.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"
dotnet pack implementation/csharp/src/StepH.GameEventScript.Conformance/StepH.GameEventScript.Conformance.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"

dotnet run --project implementation/csharp/tools/StepH.GameEventScript.PackageTool/StepH.GameEventScript.PackageTool.csproj --configuration Release --no-restore -- prepare "$package_output" "$release_version" "$repository_root"
