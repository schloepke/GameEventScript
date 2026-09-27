#!/bin/bash
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
set -uo pipefail
# HTML output supports streaming, cancellation and clickable source diagnostics.
# Perl and its core modules are supplied by macOS; no third-party runtime needed.
run_ges() {
    if ! command -v ges >/dev/null 2>&1; then
        echo 'GES CLI not found. Install ges and add its directory to TextMate’s PATH.'
        return 127
    fi
    if [[ -z "${TM_FILEPATH:-}" || ! -f "$TM_FILEPATH" ]]; then
        echo 'Save the current document before invoking GES.'
        return 1
    fi
    cd -- "$(dirname -- "$TM_FILEPATH")" || return
    case "$1" in
        check|compile|run) ges "$1" "$TM_FILEPATH" ;;
        dump)
            local binary="$TM_FILEPATH"
            [[ "$binary" == *.gesb ]] || binary="${binary%.*}.gesb"
            if [[ ! -f "$binary" ]]; then
                echo 'Compile the source first; no sibling .gesb file exists.'
                return 1
            fi
            ges dump "$binary"
            ;;
        *) echo 'Unknown GES action.'; return 2 ;;
    esac
}
export NO_COLOR=1
run_ges "$@" 2>&1 | /usr/bin/perl "$TM_BUNDLE_SUPPORT/output.pl"
exit "${PIPESTATUS[0]}"
