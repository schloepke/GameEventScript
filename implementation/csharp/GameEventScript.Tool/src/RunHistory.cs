// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Tool;

// One session history is shared by PrettyPrompt and the timer-aware terminal reader.
internal sealed class RunHistory
{
    private readonly List<string> _entries = [];
    private int _index;
    private string _draft = "";

    internal void Begin()
    {
        _index = _entries.Count;
        _draft = "";
    }

    internal void Add(string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && (_entries.Count == 0 || _entries[^1] != text)) _entries.Add(text);
    }

    internal string Move(string text, bool previous)
    {
        if (_index == _entries.Count) _draft = text;
        _index = Math.Clamp(_index + (previous ? -1 : 1), 0, _entries.Count);
        return _index == _entries.Count ? _draft : _entries[_index];
    }
}
