#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}

"$repository_root/scripts/pack-csharp.sh" "$release_version"
"$repository_root/scripts/stage-csharp-dlls.sh" "$release_version" --no-build
"$repository_root/scripts/smoke-test-csharp-packages.sh" "$release_version"
"$repository_root/scripts/smoke-test-csharp-unity-dlls.sh" "$release_version"

echo "C# release dry run $release_version completed without publishing."
