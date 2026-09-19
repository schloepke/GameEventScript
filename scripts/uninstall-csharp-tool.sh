#!/usr/bin/env sh
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -eu

usage() {
    echo "Usage: $0 [--tool-path DIRECTORY]"
    echo "Uninstall the C# dotnet ges tool for the current user."
    echo "Use --tool-path to select a directory instead of the global installation."
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

# Do not mask lookup failures as an absent installation. Match package IDs,
# never the command name, so unrelated tools in the same scope remain installed.
ges_installed_tools=$(dotnet tool list "$@")
ges_removed=false
for ges_package in GameEventScript.Tool StepH.GameEventScript.Tool; do
    if printf '%s\n' "$ges_installed_tools" | awk -v package="$ges_package" \
        'tolower($1) == tolower(package) { found = 1 } END { exit !found }'; then
        dotnet tool uninstall "$ges_package" "$@"
        ges_removed=true
    fi
done

if [ "$ges_removed" = true ]; then
    echo "Uninstalled C# dotnet ges ($*)."
else
    echo "C# dotnet ges is not installed ($*); nothing to uninstall."
fi
