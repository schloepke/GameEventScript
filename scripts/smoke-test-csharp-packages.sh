#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
package_output=${2:-"$repository_root/artifacts/csharp/packages/$release_version"}
# A fresh build tree prevents stale reproducible DLLs surviving CopyLocal.
consumer_root="$repository_root/artifacts/csharp/distribution-consumers"
mkdir -p "$consumer_root"
consumer_workspace=$(mktemp -d "$consumer_root/packages.XXXXXX")
echo "Consumer workspace (retained on failure): $consumer_workspace"
consumer_project="$repository_root/implementation/csharp/verification/GameEventScript.PackageConsumer/GameEventScript.PackageConsumer.csproj"
consumer_packages="$consumer_workspace/nuget"
runtime_project="$repository_root/implementation/csharp/verification/GameEventScript.RuntimeConsumer/GameEventScript.RuntimeConsumer.csproj"
fixture="$consumer_workspace/program.gesb"

mkdir -p "$consumer_packages"

dotnet restore "$consumer_project" \
    -p:BaseOutputPath="$consumer_workspace/full/bin/" \
    -p:BaseIntermediateOutputPath="$consumer_workspace/full/obj/" --force --no-cache \
    -p:GesPackageVersion="$release_version" \
    -p:GesPackageSource="$package_output" \
    -p:RestorePackagesPath="$consumer_packages"
dotnet run --project "$consumer_project" \
    -p:BaseOutputPath="$consumer_workspace/full/bin/" \
    -p:BaseIntermediateOutputPath="$consumer_workspace/full/obj/" \
    --configuration Release --no-restore \
    -p:GesPackageVersion="$release_version" \
    -p:GesPackageSource="$package_output" \
    -p:RestorePackagesPath="$consumer_packages" -- "$fixture"

dotnet restore "$runtime_project" \
    -p:BaseOutputPath="$consumer_workspace/runtime/bin/" \
    -p:BaseIntermediateOutputPath="$consumer_workspace/runtime/obj/" --force --no-cache \
    -p:GesUsePackages=true \
    -p:GesPackageVersion="$release_version" \
    -p:GesPackageSource="$package_output" \
    -p:RestorePackagesPath="$consumer_packages"
dotnet run --project "$runtime_project" \
    -p:BaseOutputPath="$consumer_workspace/runtime/bin/" \
    -p:BaseIntermediateOutputPath="$consumer_workspace/runtime/obj/" \
    --configuration Release --no-restore \
    -p:GesUsePackages=true \
    -p:GesPackageVersion="$release_version" \
    -p:GesPackageSource="$package_output" \
    -p:RestorePackagesPath="$consumer_packages" -- "$fixture"

# Only remove this successful invocation; failed builds remain for diagnosis.
rm -rf "$consumer_workspace"
