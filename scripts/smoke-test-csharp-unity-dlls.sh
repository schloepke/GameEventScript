#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
# A fresh build tree prevents stale reproducible DLLs surviving CopyLocal.
consumer_root="$repository_root/artifacts/csharp/distribution-consumers"
mkdir -p "$consumer_root"
consumer_workspace=$(mktemp -d "$consumer_root/dlls.XXXXXX")
echo "Consumer workspace (retained on failure): $consumer_workspace"
consumer_project="$repository_root/implementation/csharp/verification/GameEventScript.PackageConsumer/GameEventScript.PackageConsumer.csproj"
unity_dlls="$repository_root/artifacts/csharp/dll/$release_version/unity"
compiler_dlls="$repository_root/artifacts/csharp/dll/$release_version/compiler"
runtime_project="$repository_root/implementation/csharp/verification/GameEventScript.RuntimeConsumer/GameEventScript.RuntimeConsumer.csproj"
fixture="$consumer_workspace/program.gesb"

mkdir -p "$(dirname -- "$fixture")"
dotnet run --project "$consumer_project" \
    -p:BaseOutputPath="$consumer_workspace/full/bin/" \
    -p:BaseIntermediateOutputPath="$consumer_workspace/full/obj/" \
    --configuration Release \
    -p:GesUseUnityDlls=true \
    -p:GesUnityDllDirectory="$unity_dlls" \
    -p:GesCompilerDllDirectory="$compiler_dlls" -- "$fixture"
dotnet run --project "$runtime_project" \
    -p:BaseOutputPath="$consumer_workspace/runtime/bin/" \
    -p:BaseIntermediateOutputPath="$consumer_workspace/runtime/obj/" \
    --configuration Release \
    -p:GesUseUnityDlls=true \
    -p:GesUnityDllDirectory="$unity_dlls" -- "$fixture"

# Only remove this successful invocation; failed builds remain for diagnosis.
rm -rf "$consumer_workspace"
