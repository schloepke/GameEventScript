#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
package_output=${2:-"$repository_root/artifacts/csharp/packages"}
consumer_project="$repository_root/implementation/csharp/tests/StepH.GameEventScript.PackageConsumer/StepH.GameEventScript.PackageConsumer.csproj"
consumer_packages="$repository_root/artifacts/csharp/package-consumer/nuget"
runtime_project="$repository_root/implementation/csharp/tests/StepH.GameEventScript.RuntimeConsumer/StepH.GameEventScript.RuntimeConsumer.csproj"
fixture="$repository_root/artifacts/csharp/package-consumer/program.gesb"

rm -rf "$consumer_packages"
mkdir -p "$consumer_packages"

dotnet restore "$consumer_project" --force --no-cache -p:GesPackageVersion="$release_version" -p:GesPackageSource="$package_output" -p:RestorePackagesPath="$consumer_packages"
dotnet run --project "$consumer_project" --configuration Release --no-restore -p:GesPackageVersion="$release_version" -p:GesPackageSource="$package_output" -p:RestorePackagesPath="$consumer_packages" -- "$fixture"

dotnet restore "$runtime_project" --force --no-cache -p:GesUsePackages=true -p:GesPackageVersion="$release_version" -p:GesPackageSource="$package_output" -p:RestorePackagesPath="$consumer_packages"
dotnet run --project "$runtime_project" --configuration Release --no-restore -p:GesUsePackages=true -p:GesPackageVersion="$release_version" -p:GesPackageSource="$package_output" -p:RestorePackagesPath="$consumer_packages" -- "$fixture"
