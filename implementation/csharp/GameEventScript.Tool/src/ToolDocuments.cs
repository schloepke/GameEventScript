// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Tool;

// Reads GES documents for the document commands and :show, with the highlighter's explanation of a reading error.
internal static class ToolDocuments
{
    private static readonly GesSyntaxHighlighter Highlighter = new(GesSyntaxLanguage.Gesd, int.MaxValue);

    internal readonly record struct Error(int Line, int Column, string Code, string Message);

    // The document, or null with the error of reading it.
    internal static GesDocument? Read(string text, out Error? error)
    {
        error = null;
        try
        {
            return GesDocument.Parse(text);
        }
        catch (GesDocumentException exception)
        {
            var reading = Highlighter.Highlight(text).Diagnostics;
            var message = reading.Count > 0 && reading[0].Code == exception.Code ? reading[0].Message
                : exception.LimitName is { } limit ? $"The document exceeds {limit} ({exception.Limit})."
                : "This explicit form does not construct a value.";
            error = new Error(exception.Line, exception.Column, exception.Code, message);
            return null;
        }
    }

    internal static string Format(string path, Error error) => $"{path}({error.Line},{error.Column}): error {error.Code}: {error.Message}";
}
