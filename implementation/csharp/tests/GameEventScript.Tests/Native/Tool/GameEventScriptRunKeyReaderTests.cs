// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies the CLI terminal adapter preserves input while decoding modified Enter sequences.</summary>
[TestClass]
public sealed class GameEventScriptRunKeyReaderTests
{
    /// <summary>Verifies extended terminal encodings produce the editor's existing newline key events.</summary>
    /// <param name="sequence">The terminal input sequence.</param>
    /// <param name="modifiers">The expected Enter modifiers.</param>
    [TestMethod]
    [DataRow("\u001b[27;2;13~", ConsoleModifiers.Shift)]
    [DataRow("\u001b[13;2u", ConsoleModifiers.Shift)]
    [DataRow("\u001b[27;3;13~", ConsoleModifiers.Alt)]
    [DataRow("\u001b[13;3u", ConsoleModifiers.Alt)]
    public void ExtendedEnterPreservesFollowingText(string sequence, ConsoleModifiers modifiers)
    {
        var input = new Queue<ConsoleKeyInfo>(Keys(sequence + "tail\r"));
        var reader = Reader(input);
        var key = reader.ReadKey(true);
        Assert.AreEqual(ConsoleKey.Enter, key.Key);
        Assert.AreEqual('\n', key.KeyChar);
        Assert.AreEqual(modifiers, key.Modifiers);
        CollectionAssert.AreEqual(Keys("tail\r"), ReadAll(reader));
        Assert.IsEmpty(input);
    }

    /// <summary>Verifies unknown or truncated escape sequences are returned unchanged and do not block later input.</summary>
    /// <param name="sequence">The unrecognized or incomplete input.</param>
    [TestMethod]
    [DataRow("\u001b")]
    [DataRow("\u001b[")]
    [DataRow("\u001b[27;2;")]
    [DataRow("\u001b[13;2")]
    [DataRow("\u001b[27;5;13~")]
    [DataRow("\u001b[13;5u")]
    [DataRow("\u001b[27;2;130~")]
    [DataRow("\u001b[27;2;13x")]
    [DataRow("\u001b[A")]
    [DataRow("\u001b]0;title\u0007")]
    [DataRow("\u001bxplain text")]
    public void OtherSequencesRemainUnchanged(string sequence)
    {
        var expected = Keys(sequence);
        var input = new Queue<ConsoleKeyInfo>(expected);
        var reader = Reader(input);
        CollectionAssert.AreEqual(expected, ReadAll(reader));
        Assert.IsEmpty(input);

        foreach (var key in Keys("next input\r")) input.Enqueue(key);
        CollectionAssert.AreEqual(Keys("next input\r"), ReadAll(reader));
    }

    /// <summary>Verifies ordinary editing keys, modifiers, and pasted multiline Unicode text survive unchanged.</summary>
    [TestMethod]
    public void OrdinaryKeysAndPastedTextRemainUnchanged()
    {
        ConsoleKeyInfo[] expected =
        [
            new('\r', ConsoleKey.Enter, false, false, false),
            new('\r', ConsoleKey.Enter, false, true, false),
            new('\r', ConsoleKey.Enter, true, false, false),
            new('\u000e', ConsoleKey.N, false, false, true),
            new('\0', ConsoleKey.UpArrow, false, false, false),
            new('\0', ConsoleKey.LeftArrow, false, false, true),
            new('\u007f', ConsoleKey.Backspace, false, false, false),
            .. Keys("let text be 'Grüße 😀'\n// next line\nemit ConsoleOut(text)\r")
        ];
        CollectionAssert.AreEqual(expected, ReadAll(Reader(new Queue<ConsoleKeyInfo>(expected))));
    }

    /// <summary>Verifies failed lookahead preserves the original key codes and modifier flags.</summary>
    [TestMethod]
    public void FailedLookaheadPreservesKeyMetadata()
    {
        ConsoleKeyInfo[] expected =
        [
            new('\u001b', ConsoleKey.Escape, false, false, false),
            new('[', ConsoleKey.Oem4, false, false, false),
            new('x', ConsoleKey.X, false, true, false),
            new('\0', ConsoleKey.UpArrow, true, false, false)
        ];
        CollectionAssert.AreEqual(expected, ReadAll(Reader(new Queue<ConsoleKeyInfo>(expected))));
    }

    /// <summary>Verifies consecutive encoded newlines are decoded independently within one buffered input.</summary>
    [TestMethod]
    public void ConsecutiveNewlinesRemainSeparateEvents()
    {
        var reader = Reader(new Queue<ConsoleKeyInfo>(Keys("\u001b[27;2;13~\u001b[13;2u")));
        var keys = ReadAll(reader);
        Assert.HasCount(2, keys);
        Assert.IsTrue(keys.All(key => key.Key == ConsoleKey.Enter && key.Modifiers == ConsoleModifiers.Shift && key.KeyChar == '\n'));
    }

    /// <summary>Verifies a short availability gap inside a terminal sequence does not discard the modifier.</summary>
    [TestMethod]
    public void FragmentedSequenceIsDecoded()
    {
        var input = new Queue<ConsoleKeyInfo>(Keys("\u001b[27;2;13~"));
        var gap = true;
        var reader = new RunKeyReader(Available, _ => input.Dequeue());
        var key = reader.ReadKey(true);
        Assert.IsFalse(gap);
        Assert.AreEqual(ConsoleKey.Enter, key.Key);
        Assert.AreEqual(ConsoleModifiers.Shift, key.Modifiers);
        Assert.IsFalse(reader.KeyAvailable);

        bool Available()
        {
            if (input.Count == 6 && gap)
            {
                gap = false;
                return false;
            }
            return input.Count > 0;
        }
    }

    private static RunKeyReader Reader(Queue<ConsoleKeyInfo> input) => new(() => input.Count > 0, _ => input.Dequeue());

    private static ConsoleKeyInfo[] Keys(string text)
        => text.Select(character => new ConsoleKeyInfo(character, character == '\u001b' ? ConsoleKey.Escape : (ConsoleKey)0, false, false, false)).ToArray();

    private static ConsoleKeyInfo[] ReadAll(RunKeyReader reader)
    {
        var keys = new List<ConsoleKeyInfo>();
        while (reader.KeyAvailable) keys.Add(reader.ReadKey(true));
        return keys.ToArray();
    }
}
