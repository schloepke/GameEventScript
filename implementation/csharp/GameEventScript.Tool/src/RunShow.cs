// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using GameEventScript.Syntax;
using PrettyPrompt.Rendering;

namespace GameEventScript.Tool;

// `:show <file>` displays a GES source, GESA or GES document file and marks its errors below the text, without loading it.
internal static class RunShow
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly record struct Mark(int Line, int Column, int Length, string Code, string Message);

    internal static void Write(string path, bool color)
    {
        var text = StrictUtf8.GetString(File.ReadAllBytes(path));
        var language = path.EndsWith(".gesd", StringComparison.OrdinalIgnoreCase) ? GesSyntaxLanguage.Gesd
            : path.EndsWith(".gesa", StringComparison.OrdinalIgnoreCase) ? GesSyntaxLanguage.Gesa : GesSyntaxLanguage.Ges;
        var highlighter = new GesSyntaxHighlighter(language, int.MaxValue);
        var result = highlighter.Highlight(text);
        // Columns do not count a leading byte order mark.
        var lines = (text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text).Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var marks = new List<Mark>();
        foreach (var diagnostic in result.Diagnostics)
        {
            var (line, column) = Location(text, diagnostic.Start);
            marks.Add(new Mark(line, column, Math.Max(1, ScalarCount(text, diagnostic.Start, diagnostic.Length)), diagnostic.Code, diagnostic.Message));
        }
        // A document read without error can still fail to construct an explicit form.
        if (language == GesSyntaxLanguage.Gesd && marks.Count == 0)
        {
            ToolDocuments.Read(text, out var error);
            if (error is { } found) marks.Add(new Mark(found.Line, found.Column, FormLength(lines, found.Line, found.Column), found.Code, found.Message));
        }

        Console.Error.WriteLine();
        var header = "// File: " + JsonSerializer.Serialize(path, ToolJsonContext.Default.String);
        Console.Error.WriteLine(color ? RunHighlighting.Paint(header, 90) : header);
        var display = Console.IsErrorRedirected ? text : ToolTextDisplay.ExpandTabs(text);
        // An expanded display is highlighted again with the same highlighter, so tabs never change its colors.
        Console.Error.Write(!color ? display
            : GesAnsiRenderer.Render(display, ReferenceEquals(display, text) ? result.Spans : highlighter.Highlight(display).Spans));
        if (text.Length > 0 && !text.EndsWith('\n') && !text.EndsWith('\r')) Console.Error.WriteLine();
        foreach (var mark in marks)
        {
            var report = $"{path}({mark.Line},{mark.Column}): error {mark.Code}: {mark.Message}";
            Console.Error.WriteLine(color ? RunHighlighting.Paint(report, 31) : report);
            var source = mark.Line <= lines.Length ? lines[mark.Line - 1] : "";
            Console.Error.WriteLine("  " + ToolTextDisplay.ExpandTabs(source));
            // A range that continues on later lines is marked to the end of its first line, in display columns.
            var start = DisplayColumn(source, mark.Column - 1);
            var caret = new string('^', Math.Max(1, DisplayColumn(source, mark.Column - 1 + mark.Length) - start));
            Console.Error.WriteLine("  " + new string(' ', start) + (color ? RunHighlighting.Paint(caret, 31) : caret));
        }
        Console.Error.WriteLine();
    }

    // The 1-based line and column of a UTF-16 offset, in Unicode scalars; CRLF is one line break and a leading byte order
    // mark is not counted, as in document errors.
    private static (int Line, int Column) Location(string text, int offset)
    {
        var line = 1;
        var column = 1;
        for (var index = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0; index < offset && index < text.Length; index++)
        {
            var c = text[index];
            if (c is '\r' or '\n')
            {
                if (c == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                line++;
                column = 1;
            }
            else if (!char.IsLowSurrogate(c)) column++;
        }
        return (line, column);
    }

    // A construction error points at the `:` of its form; the mark covers `:Name`.
    private static int FormLength(string[] lines, int line, int column)
    {
        if (line > lines.Length) return 1;
        var source = lines[line - 1];
        var start = Prefix(source, column - 1).Length;
        if (start >= source.Length || source[start] != ':') return 1;
        var end = start + 1;
        while (end < source.Length && (char.IsAsciiLetterOrDigit(source[end]) || source[end] == '_')) end++;
        return end - start;
    }

    private static int ScalarCount(string text, int start, int length)
    {
        var count = 0;
        for (var index = start; index < start + length && index < text.Length; index++)
            if (!char.IsLowSurrogate(text[index]) && text[index] is not ('\r' or '\n')) count++;
        return count;
    }

    // The first `scalars` Unicode scalars of a line.
    // The display column after the first `scalars` scalars of a line, with tabs expanded as by ToolTextDisplay.ExpandTabs.
    private static int DisplayColumn(string line, int scalars)
    {
        var end = Prefix(line, scalars).Length;
        var column = 0;
        var start = 0;
        for (var index = 0; index < end; index++)
        {
            if (line[index] != '\t') continue;
            column += Math.Max(0, UnicodeWidth.GetWidth(line.AsSpan(start, index - start)));
            column += ToolTextDisplay.TabSize - column % ToolTextDisplay.TabSize;
            start = index + 1;
        }
        return column + Math.Max(0, UnicodeWidth.GetWidth(line.AsSpan(start, end - start)));
    }

    private static string Prefix(string line, int scalars)
    {
        var index = 0;
        for (var count = 0; count < scalars && index < line.Length; count++)
            index += char.IsHighSurrogate(line[index]) && index + 1 < line.Length ? 2 : 1;
        return line.Substring(0, index);
    }
}
