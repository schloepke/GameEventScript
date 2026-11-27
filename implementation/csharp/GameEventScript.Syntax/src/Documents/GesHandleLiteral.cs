// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text;

namespace GameEventScript.Syntax;

/// <summary>
/// Recognizes the text form of a handle (specs/Language.md#handle-text-form): <c>@Type:id</c> with an optional
/// <c>~version</c> or ref and an optional <c>/part</c>. Documents and Runtime's <c>GesHandle.Parse</c> share this one
/// scanner; Runtime builds the handle from the recognized parts.
/// </summary>
internal static class GesHandleLiteral
{
    /// <summary>What follows the <c>~</c> of a handle.</summary>
    internal enum VersionForm : byte
    {
        None,
        Revision,
        Hash,
        Tag,
        Text,
    }

    /// <summary>An id or part: an integer or a Text.</summary>
    internal readonly struct Key
    {
        internal Key(long integer)
        {
            IsInteger = true;
            Integer = integer;
            Text = "";
        }

        internal Key(string text)
        {
            IsInteger = false;
            Integer = 0;
            Text = text;
        }

        internal bool IsInteger { get; }

        internal long Integer { get; }

        internal string Text { get; }
    }

    /// <summary>The recognized parts of a handle.</summary>
    internal readonly struct Parts
    {
        internal Parts(string typeName, Key id, VersionForm version, long revision, string versionText, bool hasPart, Key part)
        {
            TypeName = typeName;
            Id = id;
            Version = version;
            Revision = revision;
            VersionText = versionText;
            HasPart = hasPart;
            Part = part;
        }

        internal string TypeName { get; }

        internal Key Id { get; }

        internal VersionForm Version { get; }

        // The decimal revision of a VersionForm.Revision.
        internal long Revision { get; }

        // The lowercase hexadecimal digits of a hash, the tag name or the Text of a ref.
        internal string VersionText { get; }

        internal bool HasPart { get; }

        internal Key Part { get; }
    }

    // Reads a handle at `start`, which holds `@`, without looking at what follows it. Returns the end of the handle, or
    // -1 when the text there is no well-formed handle. A `/` that begins a `//` comment never starts or continues a part.
    internal static int Scan(string text, int start, out Parts parts) => Scan(text, start, false, out parts, out _);

    // Reads a handle like Scan; with `allowPin`, a `~` that no version follows pins the handle of a catalog
    // (specs/Documents.md#references) and `pin` is its offset, otherwise -1.
    internal static int Scan(string text, int start, bool allowPin, out Parts parts, out int pin)
    {
        parts = default;
        pin = -1;
        var position = start + 1;
        if (position >= text.Length || !GesText.IsAsciiUpper(text[position])) return -1;
        while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9' or '_')) position++;
        var typeName = text.Substring(start + 1, position - start - 1);
        if (!GesText.IsTypeName(typeName) || position >= text.Length || text[position] != ':') return -1;
        position++;
        if (!ReadKey(text, ref position, false, out var id)) return -1;
        var version = VersionForm.None;
        var revision = 0L;
        var versionText = "";
        if (position < text.Length && text[position] == '~')
        {
            position++;
            if (allowPin && (position == text.Length || IsDelimiter(text, position)))
            {
                pin = position - 1;
            }
            else if (position < text.Length && text[position] == '#')
            {
                var tagStart = ++position;
                while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9')) position++;
                versionText = text.Substring(tagStart, position - tagStart);
                if (!GesText.IsTagName(versionText)) return -1;
                version = VersionForm.Tag;
            }
            else if (position < text.Length && text[position] == '\'')
            {
                if (ReadQuoted(text, ref position) is not { } quoted) return -1;
                versionText = quoted;
                version = VersionForm.Text;
            }
            else
            {
                var versionStart = position;
                while (position < text.Length && (GesText.IsAsciiLetter(text[position]) || text[position] is >= '0' and <= '9' or '-')) position++;
                var span = text.AsSpan(versionStart, position - versionStart);
                if (span.Length is 40 or 64 && IsLowerHex(span))
                {
                    versionText = span.ToString();
                    version = VersionForm.Hash;
                }
                else if (ReadsAsInteger(span) && long.TryParse(span.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out revision))
                {
                    version = VersionForm.Revision;
                }
                else
                {
                    return -1;
                }
            }
        }
        var hasPart = false;
        Key part = default;
        if (position < text.Length && text[position] == '/' && !IsComment(text, position))
        {
            position++;
            if (!ReadKey(text, ref position, true, out part)) return -1;
            hasPart = true;
        }
        parts = new Parts(typeName, id, version, revision, versionText, hasPart, part);
        return position;
    }

    // Reads a handle in a document at `start`, which holds `@`. Returns the end of the handle, or -1 when the text there
    // is no well-formed handle that a document delimiter follows. `identity` is the type, id and part, which compare
    // keys; `versioned` is whether a version or a ref follows the id.
    internal static int Read(string text, int start, out string identity, out bool versioned) => Read(text, start, false, out identity, out versioned, out _);

    // Reads like Read; with `allowPin`, a catalog handle may end with a pinning `~` at offset `pin`, otherwise -1.
    internal static int Read(string text, int start, bool allowPin, out string identity, out bool versioned, out int pin)
    {
        identity = "";
        versioned = false;
        var end = Scan(text, start, allowPin, out var parts, out pin);
        if (end < 0 || (end < text.Length && !IsDelimiter(text, end))) return -1;
        var builder = new StringBuilder(parts.TypeName).Append(':');
        AppendIdentity(parts.Id, builder);
        if (parts.HasPart)
        {
            builder.Append('/');
            AppendIdentity(parts.Part, builder);
        }
        identity = builder.ToString();
        versioned = parts.Version != VersionForm.None;
        return end;
    }

    // Whether a Text id or part may be written without quotes: a nonempty bare run that does not read as an integer
    // and, for a part, neither starts with `/` nor contains `//`.
    internal static bool IsBare(string text, bool isPart)
    {
        if (text.Length == 0 || ReadsAsInteger(text.AsSpan())) return false;
        foreach (var character in text)
            if (!IsBareCharacter(character, isPart)) return false;
        return !isPart || (text[0] != '/' && text.IndexOf("//", StringComparison.Ordinal) < 0);
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

    // A document delimiter ends a handle: whitespace, a separator, a closing bracket, a key colon or a comment.
    internal static bool IsDelimiter(string text, int position)
        => text[position] is ' ' or '\t' or '\r' or '\n' or ',' or ']' or ')' or ':' || IsComment(text, position);

    private static bool IsComment(string text, int position) => text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/';

    // An identity key: `i<integer>;` or `t<length>:<text>`.
    private static void AppendIdentity(Key key, StringBuilder identity)
    {
        if (key.IsInteger) identity.Append('i').Append(key.Integer.ToString(CultureInfo.InvariantCulture)).Append(';');
        else identity.Append('t').Append(key.Text.Length).Append(':').Append(key.Text);
    }

    // An id or part, quoted or bare; a bare one is an integer when it reads as one.
    private static bool ReadKey(string text, ref int position, bool isPart, out Key key)
    {
        key = default;
        if (position >= text.Length) return false;
        if (text[position] == '\'')
        {
            if (ReadQuoted(text, ref position) is not { } quoted) return false;
            key = new Key(quoted);
            return true;
        }
        var start = position;
        while (position < text.Length && IsBareCharacter(text[position], isPart) && !(isPart && IsComment(text, position))) position++;
        if (position == start) return false;
        var bare = text.AsSpan(start, position - start);
        if (ReadsAsInteger(bare))
        {
            if (!long.TryParse(bare.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer)) return false;
            key = new Key(integer);
            return true;
        }
        key = new Key(bare.ToString());
        return true;
    }

    // A `'`-quoted Text with doubled quotes, from the opening quote at `position`; null when unterminated or not valid
    // UTF-16.
    private static string? ReadQuoted(string text, ref int position)
        => GesTextLiteralReader.Read(text, ref position) is { } quoted && GesText.GetInvalidUtf16Offset(quoted) < 0 ? quoted : null;

    private static bool IsLowerHex(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        return true;
    }

    // Whether the character may start a bare or quoted id.
    internal static bool IsIdStart(char character) => character == '\'' || IsBareCharacter(character, false);

    private static bool IsBareCharacter(char character, bool isPart)
        => GesText.IsAsciiLetter(character) || character is >= '0' and <= '9' or '.' or '_' or '-' || (isPart && character == '/');
}
