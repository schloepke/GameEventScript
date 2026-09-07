#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
release_version=${1:-0.1.0}
consumer_project="$repository_root/implementation/csharp/tests/StepH.GameEventScript.PackageConsumer/StepH.GameEventScript.PackageConsumer.csproj"
unity_dlls="$repository_root/artifacts/csharp/dll/$release_version/unity"

dotnet run --project "$consumer_project" --configuration Release -p:GesUseUnityDlls=true -p:GesUnityDllDirectory="$unity_dlls"
