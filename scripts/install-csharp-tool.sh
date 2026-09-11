#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

usage() {
    echo "Usage: $0 [--tool-path DIRECTORY]"
    echo "Build the C# solution, then install or update ges for the current user."
    echo "Use --tool-path to install into a directory instead of globally."
}

case "$#" in
    0) set -- --global ;;
    1)
        case "$1" in
            -h|--help) usage; exit 0 ;;
            *) usage >&2; exit 2 ;;
        esac
        ;;
    2)
        if [ "$1" != "--tool-path" ] || [ -z "$2" ]; then
            usage >&2
            exit 2
        fi
        case "$2" in
            /*) ;;
            *) set -- --tool-path "$PWD/$2" ;;
        esac
        ;;
    *) usage >&2; exit 2 ;;
esac

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"
tool_project="implementation/csharp/tools/StepH.GameEventScript.Tool/StepH.GameEventScript.Tool.csproj"

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

# Update also installs a missing tool. Allow replacing a stable or newer build
# with this checkout's explicitly selected local development version.
dotnet tool update StepH.GameEventScript.Tool "$@" \
    --version "$local_version" --source "$build_directory" --allow-downgrade

echo "Installed ges $local_version ($*)."
