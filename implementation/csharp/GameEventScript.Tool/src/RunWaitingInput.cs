// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0


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
        var text = "";
        var cursor = 0;
        var previousRow = 0;
        var keys = new RunKeyReader(() => Console.KeyAvailable, Console.ReadKey);
        void Clear()
        {
            Console.Error.Write("\r" + (previousRow > 0 ? $"\u001b[{previousRow}A" : "") + "\u001b[J");
            previousRow = 0;
        }
        // Re-segment after insertion so combining marks and emoji stay one editable cluster, like the Swift CLI.
        void Insert(string value)
        {
            var prefix = text[..cursor] + value;
            text = prefix + text[cursor..];
            cursor = ToolTextDisplay.ClusterIndex(text, ToolTextDisplay.ClusterCount(prefix));
        }
        void Draw()
        {
            Clear();
            var visible = ToolTextDisplay.ExpandTabs(text);
            Console.Error.Write("ges> " + (color ? RunHighlighting.Render(visible) : visible).Replace("\n", "\n     "));
            var (end, caret) = Position(text, cursor, Math.Max(10, Console.WindowWidth));
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
            if (control && key.Key == ConsoleKey.C) { text = ""; cursor = 0; history.Begin(); Draw(); continue; }
            if (key.Key == ConsoleKey.Enter && key.Modifiers == 0)
            {
                cursor = text.Length;
                Draw();
                Console.Error.WriteLine();
                return text;
            }
            if (key.Key == ConsoleKey.Enter || control && key.Key == ConsoleKey.N) Insert("\n");
            else if (key.Key == ConsoleKey.LeftArrow && cursor > 0) cursor = ToolTextDisplay.PreviousCluster(text, cursor);
            else if (key.Key == ConsoleKey.RightArrow && cursor < text.Length) cursor = ToolTextDisplay.NextCluster(text, cursor);
            else if (key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow && key.Modifiers == 0)
            {
                text = history.Move(text, key.Key == ConsoleKey.UpArrow);
                cursor = text.Length;
            }
            else if (key.Key == ConsoleKey.Home) cursor = 0;
            else if (key.Key == ConsoleKey.End) cursor = text.Length;
            else if (key.Key == ConsoleKey.Backspace && cursor > 0)
            {
                var start = ToolTextDisplay.PreviousCluster(text, cursor);
                text = text.Remove(start, cursor - start);
                cursor = start;
            }
            else if (key.Key == ConsoleKey.Delete && cursor < text.Length) text = text.Remove(cursor, ToolTextDisplay.NextCluster(text, cursor) - cursor);
            else if (!control && (key.KeyChar >= ' ' || key.KeyChar == '\t')) Insert(key.KeyChar.ToString());
            Draw();
        }
    }

    // The end and caret positions of value after the five-column prompt, in one pass over its clusters, measured
    // like the expanded text Draw writes. Wide clusters move whole to the next row; a tab's spaces wrap individually.
    internal static ((int Row, int Column) End, (int Row, int Column) Caret) Position(string value, int cursor, int width)
    {
        var row = 0;
        var column = 5;
        var lineColumn = 0;
        var caret = (Row: 0, Column: 5);
        var index = 0;
        while (index < value.Length)
        {
            var length = ToolTextDisplay.ClusterLength(value.AsSpan(index));
            var element = value.AsSpan(index, length);
            index += length;
            if (element is "\n" or "\r\n") { row++; column = 5; lineColumn = 0; }
            else if (element is "\t")
            {
                var size = ToolTextDisplay.TabSize - lineColumn % ToolTextDisplay.TabSize;
                column += size;
                lineColumn += size;
                if (column > width) { row++; column -= width; }
            }
            else
            {
                var size = ToolTextDisplay.Width(element);
                if (size > 0 && column + size > width) { row++; column = 0; }
                column += size;
                lineColumn += size;
            }
            if (index <= cursor) caret = (row, column);
        }
        static (int, int) Wrap(int row, int column, int width) => column >= width ? (row + 1, 0) : (row, column);
        return (Wrap(row, column, width), Wrap(caret.Row, caret.Column, width));
    }
}
