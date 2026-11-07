// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Tool;

// Reads GES documents for the document commands and :show, with the Syntax module's explanation of an error.
internal static class ToolDocuments
{
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
            var message = GesDocumentDiagnostic.Explain(exception.Code, exception.LimitName, exception.Limit ?? 0);
            error = new Error(exception.Line, exception.Column, exception.Code, message);
            return null;
        }
    }

    internal static string Format(string path, Error error) => $"{path}({error.Line},{error.Column}): error {error.Code}: {error.Message}";
}
