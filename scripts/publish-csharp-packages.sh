#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -eu

if [ "$#" -ne 2 ] && [ "$#" -ne 3 ]; then
    echo "Usage: $0 <verified-package-directory> <version> [verified-tool-directory]" >&2
    exit 2
fi
package_directory=$1
release_version=$2
repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
if [ "$#" -eq 3 ]; then
    python3 "$repository_root/scripts/publish-csharp-tools.py" "$3" "$release_version"
fi
case "$release_version" in
    ''|.*|*[!0-9A-Za-z.-]*) echo "Invalid package version: $release_version" >&2; exit 2 ;;
esac
: "${NUGET_API_KEY:?A short-lived NuGet API key is required}"

# Check the entire release before the first upload. Never glob a directory that
# may contain internal Conformance packages, tools or a different release.
for package_id in GameEventScript.Syntax GameEventScript.Runtime GameEventScript.Compiler GameEventScript.CSharpBridge GameEventScript.Generator; do
    for extension in nupkg snupkg; do
        if [ ! -f "$package_directory/$package_id.$release_version.$extension" ]; then
            echo "Missing release artifact: $package_id.$release_version.$extension" >&2
            exit 1
        fi
    done
done
for package_id in GameEventScript.Syntax GameEventScript.Runtime GameEventScript.Compiler GameEventScript.CSharpBridge GameEventScript.Generator; do
    # dotnet also submits the matching .snupkg to NuGet's symbol server. A package that an earlier, interrupted run
    # already published with identical contents is skipped, so the release can be run again.
    python3 "$repository_root/scripts/nuget_publication.py" "$package_directory/$package_id.$release_version.nupkg" "$package_id" "$release_version"
done

if [ "$#" -eq 3 ]; then
    python3 "$repository_root/scripts/publish-csharp-tools.py" "$3" "$release_version" --publish
fi
