// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using PrettyPrompt.Rendering;

namespace GameEventScript.Tool;

// Input and host execution share this event loop. Only redirected input uses a reader worker;
// the worker never touches the host or output streams.
internal static class RunWaitingInput
{
    internal static string? Read(RunSession session, TextReader input, bool terminal, bool color, RunHistory history)
    {
        if (!terminal)
        {
            var read = Task.Run(input.ReadLine);
            while (!read.Wait(10))
                if (session.Host.NextMessageDelay == 0 && !session.Pump()) throw new IOException("Delayed message processing failed.");
            return read.GetAwaiter().GetResult();
        }
        var capture = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;
        try { return ReadTerminal(session, color, history); }
        finally { Console.TreatControlCAsInput = capture; }
    }

    private static string? ReadTerminal(RunSession session, bool color, RunHistory history)
    {
        history.Begin();
        var text = new StringBuilder();
        var cursor = 0;
        var previousRow = 0;
        var keys = new RunKeyReader(() => Console.KeyAvailable, Console.ReadKey);
        void Clear()
        {
            Console.Error.Write("\r" + (previousRow > 0 ? $"\u001b[{previousRow}A" : "") + "\u001b[J");
            previousRow = 0;
        }
        (int Row, int Column) Position(string value)
        {
            var row = 0;
            var column = 5;
            var lineColumn = 0;
            var width = Math.Max(10, Console.WindowWidth);
            foreach (var rune in value.EnumerateRunes())
            {
                if (rune.Value == '\n') { row++; column = 5; lineColumn = 0; continue; }
                var size = rune.Value == '\t' ? 4 - lineColumn % 4 : Math.Max(0, UnicodeWidth.GetWidth(rune.ToString()));
                if (column + size > width) { row++; column = 0; }
                column += size;
                lineColumn += size;
            }
            return column >= width ? (row + 1, 0) : (row, column);
        }
        void Draw()
        {
            Clear();
            var value = text.ToString();
            var visible = ToolTextDisplay.ExpandTabs(value);
            Console.Error.Write("ges> " + (color ? RunHighlighting.Render(visible) : visible).Replace("\n", "\n ... "));
            var end = Position(value);
            var caret = Position(value[..cursor]);
            if (end.Column == 0) Console.Error.Write(" \r");
            if (end.Row > caret.Row) Console.Error.Write($"\u001b[{end.Row - caret.Row}A");
            Console.Error.Write("\r" + (caret.Column > 0 ? $"\u001b[{caret.Column}C" : ""));
            previousRow = caret.Row;
        }
        Draw();
        while (true)
        {
            if (session.Host.NextMessageDelay == 0)
            {
                Clear();
                if (!session.Pump()) throw new IOException("Delayed message processing failed.");
                Draw();
            }
            if (!keys.KeyAvailable) { Thread.Sleep(10); continue; }
            var key = keys.ReadKey(true);
            var control = (key.Modifiers & ConsoleModifiers.Control) != 0;
            if (control && key.Key == ConsoleKey.D && text.Length == 0) { Console.Error.WriteLine(); return null; }
            if (control && key.Key == ConsoleKey.C) { text.Clear(); cursor = 0; history.Begin(); Draw(); continue; }
            if (key.Key == ConsoleKey.Enter && key.Modifiers == 0)
            {
                cursor = text.Length;
                Draw();
                Console.Error.WriteLine();
                return text.ToString();
            }
            if (key.Key == ConsoleKey.Enter || control && key.Key == ConsoleKey.N) { text.Insert(cursor++, '\n'); }
            else if (key.Key == ConsoleKey.LeftArrow && cursor > 0) { cursor--; if (cursor > 0 && char.IsLowSurrogate(text[cursor])) cursor--; }
            else if (key.Key == ConsoleKey.RightArrow && cursor < text.Length) { if (char.IsHighSurrogate(text[cursor])) cursor++; cursor++; }
            else if (key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow && key.Modifiers == 0)
            {
                var recalled = history.Move(text.ToString(), key.Key == ConsoleKey.UpArrow);
                text.Clear().Append(recalled);
                cursor = text.Length;
            }
            else if (key.Key == ConsoleKey.Home) cursor = 0;
            else if (key.Key == ConsoleKey.End) cursor = text.Length;
            else if (key.Key == ConsoleKey.Backspace && cursor > 0)
            {
                var count = cursor > 1 && char.IsLowSurrogate(text[cursor - 1]) ? 2 : 1;
                cursor -= count;
                text.Remove(cursor, count);
            }
            else if (key.Key == ConsoleKey.Delete && cursor < text.Length) text.Remove(cursor, char.IsHighSurrogate(text[cursor]) ? 2 : 1);
            else if (!control && (key.KeyChar >= ' ' || key.KeyChar == '\t')) text.Insert(cursor++, key.KeyChar);
            Draw();
        }
    }
}
