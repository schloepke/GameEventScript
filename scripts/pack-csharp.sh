#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
package_output=${2:-"$repository_root/artifacts/csharp/packages/$release_version"}

case "$release_version" in
    ''|.*|*[!0-9A-Za-z.-]*)
        echo "Invalid package version: $release_version" >&2
        exit 1
        ;;
esac

mkdir -p "$package_output"
cd "$repository_root"
dotnet restore GameEventScript.sln -p:NuGetAudit=false

for package_id in GameEventScript.Runtime GameEventScript.Compiler GameEventScript.CSharpBridge GameEventScript.Syntax GameEventScript.Generator; do
    rm -f "$package_output/$package_id.$release_version.nupkg" "$package_output/$package_id.$release_version.snupkg"
done

dotnet pack implementation/csharp/GameEventScript.Runtime/src/GameEventScript.Runtime.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"
dotnet pack implementation/csharp/GameEventScript.Compiler/src/GameEventScript.Compiler.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"
dotnet pack implementation/csharp/GameEventScript.CSharpBridge/src/GameEventScript.CSharpBridge.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"

dotnet pack implementation/csharp/GameEventScript.Syntax/src/GameEventScript.Syntax.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"
dotnet pack implementation/csharp/GameEventScript.Generator/src/GameEventScript.Generator.csproj --configuration Release --no-restore --output "$package_output" -p:Version="$release_version" -p:PackageVersion="$release_version"

# The packaged files take the commit time, so a newer build's DLLs are newer and a copy that compares date and size replaces
# them; packing one commit stays byte-identical. Uncommitted changes take the current time instead, since the commit time
# would not change with them. Untracked files count only below implementation/csharp, where a new source file joins a
# package by its project's glob. SOURCE_DATE_EPOCH overrides both.
if [ -n "${SOURCE_DATE_EPOCH:-}" ]; then
    source_date=$SOURCE_DATE_EPOCH
elif [ -n "$(git -C "$repository_root" status --porcelain --untracked-files=no 2>/dev/null)" ] \
    || [ -n "$(git -C "$repository_root" ls-files --others --exclude-standard -- implementation/csharp 2>/dev/null)" ]; then
    source_date=$(date +%s)
else
    source_date=$(git -C "$repository_root" log -1 --format=%ct 2>/dev/null || echo 946684800)
fi
dotnet run --project implementation/csharp/verification/GameEventScript.PackageTool/GameEventScript.PackageTool.csproj --configuration Release --no-restore -- prepare "$package_output" "$release_version" "$repository_root" "$source_date"
