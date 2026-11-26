// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;

namespace GameEventScript.Syntax;

/// <summary>
/// Recognizes the text form of a handle in a document (specs/Documents.md#handles): <c>@Type:id</c> with an optional
/// <c>~version</c> or ref and an optional <c>/part</c>. Runtime builds the handle from the recognized text.
/// </summary>
internal static class GesHandleLiteral
{
    // Reads a handle at `start`, which holds `@`. Returns the end of the handle, or -1 when the text there is no
    // well-formed handle that a document delimiter follows. `identity` is the type, id and part, which compare keys;
    // `versioned` is whether a version or a ref follows the id.
    internal static int Read(string text, int start, out string identity, out bool versioned)
    {
        identity = "";
        versioned = false;
        var position = start + 1;
        if (position >= text.Length || !GesText.IsAsciiUpper(text[position])) return -1;
        while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9' or '_')) position++;
        var typeName = text.Substring(start + 1, position - start - 1);
        if (!GesText.IsTypeName(typeName) || position >= text.Length || text[position] != ':') return -1;
        position++;
        var builder = new StringBuilder(typeName).Append(':');
        if (!ReadKey(text, ref position, false, builder)) return -1;
        if (position < text.Length && text[position] == '~')
        {
            versioned = true;
            position++;
            if (position < text.Length && text[position] == '#')
            {
                var tagStart = ++position;
                while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9')) position++;
                if (!GesText.IsTagName(text.Substring(tagStart, position - tagStart))) return -1;
            }
            else if (position < text.Length && text[position] == '\'')
            {
                if (GesTextLiteralReader.Read(text, ref position) is not { } quoted || GesText.GetInvalidUtf16Offset(quoted) >= 0) return -1;
            }
            else
            {
                var versionStart = position;
                while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9' or '-')) position++;
                if (!IsVersion(text.AsSpan(versionStart, position - versionStart))) return -1;
            }
        }
        if (position < text.Length && text[position] == '/' && !IsComment(text, position))
        {
            position++;
            builder.Append('/');
            if (!ReadKey(text, ref position, true, builder)) return -1;
        }
        if (position < text.Length && !IsDelimiter(text, position)) return -1;
        identity = builder.ToString();
        return position;
    }

    // A document delimiter ends a handle: whitespace, a separator, a closing bracket, a key colon or a comment.
    private static bool IsDelimiter(string text, int position)
        => text[position] is ' ' or '\t' or '\r' or '\n' or ',' or ']' or ')' or ':' || IsComment(text, position);

    private static bool IsComment(string text, int position) => text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/';

    // An id or part, quoted or bare, appended to the identity as `i<integer>` or `t<text>`.
    private static bool ReadKey(string text, ref int position, bool isPart, StringBuilder identity)
    {
        if (position >= text.Length) return false;
        if (text[position] == '\'')
        {
            if (GesTextLiteralReader.Read(text, ref position) is not { } quoted || GesText.GetInvalidUtf16Offset(quoted) >= 0) return false;
            identity.Append('t').Append(quoted.Length).Append(':').Append(quoted);
            return true;
        }
        var start = position;
        while (position < text.Length && IsBareCharacter(text[position], isPart) && !(isPart && IsComment(text, position))) position++;
        if (position == start) return false;
        var bare = text.AsSpan(start, position - start);
        if (ReadsAsInteger(bare))
        {
            if (!long.TryParse(bare.ToString(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var integer)) return false;
            identity.Append('i').Append(integer.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            return true;
        }
        identity.Append('t').Append(bare.Length).Append(':').Append(bare.ToString());
        return true;
    }

    // A version is 40 or 64 lowercase hexadecimal digits or a decimal revision within Int64.
    private static bool IsVersion(ReadOnlySpan<char> text)
    {
        if (text.Length is 40 or 64)
        {
            var hex = true;
            foreach (var character in text) hex &= character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (hex) return true;
        }
        return ReadsAsInteger(text) && long.TryParse(text.ToString(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    // An integer is a decimal with an optional '-' and no leading zero.
    internal static bool ReadsAsInteger(ReadOnlySpan<char> text)
    {
        var start = text.Length > 0 && text[0] == '-' ? 1 : 0;
        if (start == text.Length) return false;
        if (text[start] == '0' && text.Length - start > 1) return false;
        for (var index = start; index < text.Length; index++)
            if (text[index] is < '0' or > '9') return false;
        return true;
    }

    private static bool IsBareCharacter(char character, bool isPart)
        => GesText.IsAsciiLetter(character) || character is >= '0' and <= '9' or '.' or '_' or '-' || (isPart && character == '/');
}
