#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
mkdir -p "$repository_root/artifacts/csharp"
comparison_root=$(mktemp -d "$repository_root/artifacts/csharp/reproducibility.XXXXXX")
first="$comparison_root/first"
second="$comparison_root/second"

cleanup() {
    rm -rf "$comparison_root"
}
trap cleanup EXIT HUP INT TERM

mkdir -p "$first" "$second"

clean_product_outputs() {
    dotnet clean "$repository_root/implementation/csharp/src/GameEventScript.CSharpBridge/GameEventScript.CSharpBridge.csproj" --configuration Release >/dev/null
    dotnet clean "$repository_root/implementation/csharp/src/GameEventScript.Conformance/GameEventScript.Conformance.csproj" --configuration Release >/dev/null
    dotnet clean "$repository_root/implementation/csharp/src/GameEventScript.Compiler/GameEventScript.Compiler.csproj" --configuration Release >/dev/null
    dotnet clean "$repository_root/implementation/csharp/src/GameEventScript.Runtime/GameEventScript.Runtime.csproj" --configuration Release >/dev/null
}

clean_product_outputs
"$repository_root/scripts/pack-csharp.sh" "$release_version" "$first"
clean_product_outputs
"$repository_root/scripts/pack-csharp.sh" "$release_version" "$second"

for package_id in GameEventScript.Runtime GameEventScript.Compiler GameEventScript.CSharpBridge GameEventScript.Conformance; do
    cmp "$first/$package_id.$release_version.nupkg" "$second/$package_id.$release_version.nupkg"
    cmp "$first/$package_id.$release_version.snupkg" "$second/$package_id.$release_version.snupkg"
done

echo "Two independent canonical package runs are byte-identical."
