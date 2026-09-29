#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

usage() {
    echo "Usage: $0 [--aot] [--tool-path DIRECTORY]"
    echo "Build the C# solution, then install or update dotnet ges for the current user."
    echo "Use --aot for a native executable on macOS/Linux; default remains a .NET tool."
    echo "Use --tool-path to install into a directory instead of globally."
}

ges_aot=false
ges_tool_path=""
while [ "$#" -gt 0 ]; do
    case "$1" in
        --aot) ges_aot=true; shift ;;
        --tool-path)
            if [ "$#" -lt 2 ] || [ -z "$2" ]; then usage >&2; exit 2; fi
            ges_tool_path="$2"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; exit 2 ;;
    esac
done
if [ -n "$ges_tool_path" ]; then
    case "$ges_tool_path" in /*) ;; *) ges_tool_path="$PWD/$ges_tool_path" ;; esac
    set -- --tool-path "$ges_tool_path"
else
    set -- --global
    ges_tool_path="$HOME/.dotnet/tools"
fi

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"
if [ "$ges_aot" = true ]; then
    exec "$repository_root/scripts/csharp-aot-install.sh" install "$ges_tool_path" "$@"
fi
ges_aot_marker="$ges_tool_path/.ges-csharp-aot.sha256"
if [ -e "$ges_aot_marker" ] || [ -L "$ges_aot_marker" ]; then
    "$repository_root/scripts/csharp-aot-install.sh" check "$ges_tool_path"
fi
tool_project="implementation/csharp/GameEventScript.Tool/src/GameEventScript.Tool.csproj"

mkdir -p artifacts/csharp/tool
build_directory=$(mktemp -d "$repository_root/artifacts/csharp/tool/install-build.XXXXXX")
trap 'rm -rf "$build_directory"' 0
trap 'exit 1' 1 2 15

base_version=$(dotnet msbuild "$tool_project" -nologo -getProperty:Version)
base_version=${base_version%%+*}
case "$base_version" in
    *-*) local_version="$base_version.local" ;;
    *) local_version="$base_version-local" ;;
esac
# A distinct package identity prevents cached same-version builds being reused.
# The random suffix also distinguishes calls within the same second.
local_version="$local_version.$(date -u +%Y%m%d%H%M%S).r${build_directory##*.}"

"$repository_root/scripts/build-csharp.sh"
dotnet pack "$tool_project" --configuration Release --no-restore \
    --output "$build_directory" -p:Version="$local_version" -p:PackageVersion="$local_version"

# Validate the replacement before removing a native installation.
if [ -e "$ges_aot_marker" ]; then
    dotnet tool install GameEventScript.Tool --tool-path "$build_directory/aot-migration-check" \
        --version "$local_version" --source "$build_directory"
    "$build_directory/aot-migration-check/dotnet-ges" --version >/dev/null
    "$repository_root/scripts/csharp-aot-install.sh" remove "$ges_tool_path"
fi

# Verify the replacement before removing another GES package owning the command.
mkdir -p "$ges_tool_path"
installed_tools=$(dotnet tool list "$@")
legacy_tools=$(printf '%s\n' "$installed_tools" | awk 'tolower($1) == "steph.gameeventscript.tool" || tolower($1) == "gameeventscript.tool.aot" { print $1 }')
if [ -n "$legacy_tools" ]; then
    dotnet tool install GameEventScript.Tool --tool-path "$build_directory/migration-check" \
        --version "$local_version" --source "$build_directory"
    "$build_directory/migration-check/dotnet-ges" --version >/dev/null
    for package in $legacy_tools; do dotnet tool uninstall "$package" "$@"; done
fi

# Update also installs a missing tool. Allow replacing a stable or newer build
# with this checkout's explicitly selected local development version.
dotnet tool update GameEventScript.Tool "$@" \
    --version "$local_version" --source "$build_directory" --allow-downgrade

echo "Installed dotnet ges $local_version ($*)."
