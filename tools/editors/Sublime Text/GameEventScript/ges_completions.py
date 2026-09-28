# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Small syntax-only completion provider for Sublime Text."""
import json
import re
import sublime
import sublime_plugin

DATA = json.loads(sublime.load_resource("Packages/GameEventScript/completions.json"))


def candidates(before):
    for context in DATA["contexts"]:
        match = re.search(context["pattern"], before)
        if match:
            prefix = ":" if context.get("typePrefix") and not match.group(1) else ""
            return [prefix + word for word in DATA[context["group"]]]
    return DATA["keywords"]


class GesCompletions(sublime_plugin.EventListener):
    def on_query_completions(self, view, prefix, locations):
        if not locations:
            return None
        point = locations[0]
        # Inspect the preceding character as well: at EOF the caret can be just
        # beyond a comment/string scope even though the typed prefix belongs to it.
        selector = "source.gameeventscript - source.gameeventscript.assembler - comment - string"
        if not view.match_selector(point, selector) or (point and not view.match_selector(point - 1, selector)):
            return None
        before = view.substr(sublime.Region(view.line(point).begin(), point))
        return ([(word + "\tGES", word) for word in candidates(before)
                 if word.lstrip(":").lower().startswith(prefix.lower())],
                sublime.INHIBIT_WORD_COMPLETIONS)
