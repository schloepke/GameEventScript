#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
build_mode=${2:-build}
artifact_root="$repository_root/artifacts/csharp/dll/$release_version"
staging_root="$repository_root/artifacts/csharp/dll/.staging-$release_version"
core_output="$repository_root/implementation/csharp/src/StepH.GameEventScript/bin/Release/netstandard2.1"
bridge_output="$repository_root/implementation/csharp/src/StepH.GameEventScript.CSharpBridge/bin/Release/netstandard2.1"
conformance_output="$repository_root/implementation/csharp/src/StepH.GameEventScript.Conformance/bin/Release/netstandard2.1"

case "$release_version" in
    ''|.*|*[!0-9A-Za-z.-]*)
        echo "Invalid package version: $release_version" >&2
        exit 1
        ;;
esac

cd "$repository_root"
if [ "$build_mode" = "build" ]; then
    dotnet restore implementation/csharp/src/StepH.GameEventScript.CSharpBridge/StepH.GameEventScript.CSharpBridge.csproj -p:NuGetAudit=false
    dotnet restore implementation/csharp/src/StepH.GameEventScript.Conformance/StepH.GameEventScript.Conformance.csproj -p:NuGetAudit=false
    dotnet build implementation/csharp/src/StepH.GameEventScript.CSharpBridge/StepH.GameEventScript.CSharpBridge.csproj --configuration Release --no-restore -p:Version="$release_version"
    dotnet build implementation/csharp/src/StepH.GameEventScript.Conformance/StepH.GameEventScript.Conformance.csproj --configuration Release --no-restore -p:Version="$release_version"
elif [ "$build_mode" != "--no-build" ]; then
    echo "Unknown staging mode: $build_mode" >&2
    exit 1
fi

rm -rf "$staging_root"
mkdir -p "$staging_root/core" "$staging_root/unity" "$staging_root/conformance"

for extension in dll pdb xml; do
    cp "$core_output/StepH.GameEventScript.$extension" "$staging_root/core/"
    cp "$core_output/StepH.GameEventScript.$extension" "$staging_root/unity/"
    cp "$core_output/StepH.GameEventScript.$extension" "$staging_root/conformance/"
    cp "$bridge_output/StepH.GameEventScript.CSharpBridge.$extension" "$staging_root/unity/"
    cp "$conformance_output/StepH.GameEventScript.Conformance.$extension" "$staging_root/conformance/"
done

rm -rf "$artifact_root"
mv "$staging_root" "$artifact_root"
echo "Staged C# DLL sets under $artifact_root"
