// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace GameEventScript.Tool;

internal sealed class RunKeyReader(Func<bool> keyAvailable, Func<bool, ConsoleKeyInfo> readKey)
{
    // Ghostty's default modifyOtherKeys encoding and the equivalent CSI-u forms.
    // Decode before PrettyPrompt batches (and discards unrecognized) escape sequences.
    private static readonly string[] Sequences = ["\u001b[27;2;13~", "\u001b[13;2u", "\u001b[27;3;13~", "\u001b[13;3u"];
    private readonly Queue<ConsoleKeyInfo> _pending = new();
    private readonly ConsoleKeyInfo[] _candidate = new ConsoleKeyInfo[10];

    internal bool KeyAvailable => _pending.Count > 0 || keyAvailable();

    internal ConsoleKeyInfo ReadKey(bool intercept)
    {
        if (_pending.Count > 0) return _pending.Dequeue();
        var first = readKey(intercept);
        if (first.KeyChar != '\u001b' || first.Modifiers != 0) return first;

        _candidate[0] = first;
        Span<char> characters = stackalloc char[_candidate.Length];
        characters[0] = first.KeyChar;
        var count = 1;
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            var prefix = false;
            for (var i = 0; i < Sequences.Length; i++)
            {
                if (!Sequences[i].AsSpan().StartsWith(characters[..count], StringComparison.Ordinal)) continue;
                if (Sequences[i].Length == count) return new ConsoleKeyInfo('\n', ConsoleKey.Enter, shift: i < 2, alt: i >= 2, control: false);
                prefix = true;
            }
            if (!prefix || count == _candidate.Length) break;

            // A terminal write can arrive in fragments. Bound the wait so a lone
            // Escape or a truncated sequence cannot leave the editor blocked.
            while (!keyAvailable() && Stopwatch.GetElapsedTime(started).TotalMilliseconds < 50) Thread.Sleep(1);
            if (!keyAvailable()) break;
            var next = readKey(intercept);
            _candidate[count] = next;
            characters[count++] = next.KeyChar;
        }

        // Preserve the complete original events, including modifiers, on failure.
        for (var i = 1; i < count; i++) _pending.Enqueue(_candidate[i]);
        return first;
    }
}
