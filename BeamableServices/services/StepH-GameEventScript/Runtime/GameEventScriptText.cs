// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Runtime;

/// <summary>
/// Implements the language-level text rules without depending on platform Unicode
/// classification or native string indexing semantics.
/// </summary>
internal static class GameEventScriptText
{
    internal static IComparer<string> ScalarComparer { get; } = new ScalarOrdinalComparer();

    internal static bool IsAsciiLower(char value) => value is >= 'a' and <= 'z';
    internal static bool IsAsciiUpper(char value) => value is >= 'A' and <= 'Z';
    internal static bool IsAsciiLetter(char value) => IsAsciiLower(value) || IsAsciiUpper(value);
    internal static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
    internal static bool IsInlineWhitespace(char value) => value is ' ' or '\t';
    internal static bool IsLineBreak(char value) => value is '\r' or '\n';
    internal static bool IsTokenWhitespace(char value) => IsInlineWhitespace(value) || IsLineBreak(value);

    internal static string PrepareSource(string value, string parameterName)
    {
        RequireValidUnicode(value, parameterName);
        return value.Length > 0 && value[0] == '\uFEFF' ? value[1..] : value;
    }

    internal static void RequireValidUnicode(string value, string parameterName)
    {
        var invalidOffset = GetInvalidUtf16Offset(value);
        if (invalidOffset >= 0)
        {
            throw new ArgumentException(
                $"Text must contain only valid Unicode scalar values; invalid UTF-16 at offset {invalidOffset}.",
                parameterName);
        }
    }

    internal static int GetInvalidUtf16Offset(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])) return index;
                index++;
                continue;
            }

            if (char.IsLowSurrogate(current)) return index;
        }

        return -1;
    }

    internal static string TrimAsciiWhitespace(string value)
    {
        var start = 0;
        while (start < value.Length && IsInlineWhitespace(value[start])) start++;
        var end = value.Length;
        while (end > start && IsInlineWhitespace(value[end - 1])) end--;
        return start == 0 && end == value.Length ? value : value[start..end];
    }

    internal static bool IsMessageName(string value)
    {
        if (value.Length == 0 || !IsAsciiLetter(value[0])) return false;
        for (var index = 1; index < value.Length; index++)
            if (!IsAsciiLetter(value[index])) return false;
        return true;
    }

    internal static bool IsIdentifier(string value)
        => IsName(value, requireLowerFirst: true, allowNumericSuffix: true);

    internal static bool IsTagName(string value)
        => IsName(value, requireLowerFirst: true, allowNumericSuffix: true);

    internal static bool IsTagValue(string value)
        => IsName(value, requireLowerFirst: false, allowNumericSuffix: true);

    internal static bool IsTypeName(string value)
        => IsName(value, requireLowerFirst: true, allowNumericSuffix: false);

    internal static bool EqualsAsciiIgnoreCase(string left, string right)
    {
        if (left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            var leftValue = left[index];
            var rightValue = right[index];
            if (leftValue == rightValue) continue;
            if (leftValue is >= 'A' and <= 'Z') leftValue = (char)(leftValue + ('a' - 'A'));
            if (rightValue is >= 'A' and <= 'Z') rightValue = (char)(rightValue + ('a' - 'A'));
            if (leftValue != rightValue) return false;
        }

        return true;
    }

    private static bool IsName(string value, bool requireLowerFirst, bool allowNumericSuffix)
    {
        if (value.Length == 0 || (requireLowerFirst ? !IsAsciiLower(value[0]) : !IsAsciiLetter(value[0]))) return false;
        var index = 1;
        while (index < value.Length && IsAsciiLetter(value[index])) index++;
        if (index == value.Length) return true;
        if (!allowNumericSuffix || value[index++] != '_' || index == value.Length) return false;
        if (value[index] == '0') return index + 1 == value.Length;
        if (value[index] is < '1' or > '9') return false;
        for (index++; index < value.Length; index++)
            if (!IsAsciiDigit(value[index])) return false;
        return true;
    }

    internal static int CountScalars(string value)
    {
        var count = value.Length;
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsHighSurrogate(value[index])) continue;
            count--;
            index++;
        }

        return count;
    }

    internal static int ScalarUtf16LengthAt(string value, int utf16Offset)
        => char.IsHighSurrogate(value[utf16Offset]) ? 2 : 1;

    internal static string ScalarAtUtf16Offset(string value, int utf16Offset)
        => value.Substring(utf16Offset, ScalarUtf16LengthAt(value, utf16Offset));

    internal static string? ScalarAt(string value, long zeroBasedScalarIndex)
    {
        if (zeroBasedScalarIndex < 0) return null;
        long scalarIndex = 0;
        for (var utf16Offset = 0; utf16Offset < value.Length;)
        {
            if (scalarIndex == zeroBasedScalarIndex) return ScalarAtUtf16Offset(value, utf16Offset);
            scalarIndex++;
            utf16Offset += ScalarUtf16LengthAt(value, utf16Offset);
        }

        return null;
    }

    internal static int Utf16OffsetForScalarOffset(string value, int start, int end, int scalarOffset)
    {
        var utf16Offset = start;
        for (var index = 0; index < scalarOffset && utf16Offset < end; index++)
            utf16Offset += ScalarUtf16LengthAt(value, utf16Offset);
        return utf16Offset > end ? end : utf16Offset;
    }

    internal static int CompareScalarOrdinal(string? left, string? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        var leftOffset = 0;
        var rightOffset = 0;
        while (leftOffset < left.Length && rightOffset < right.Length)
        {
            var leftScalar = ScalarValueAt(left, leftOffset);
            var rightScalar = ScalarValueAt(right, rightOffset);
            if (leftScalar != rightScalar) return leftScalar < rightScalar ? -1 : 1;
            leftOffset += ScalarUtf16LengthAt(left, leftOffset);
            rightOffset += ScalarUtf16LengthAt(right, rightOffset);
        }

        if (leftOffset == left.Length) return rightOffset == right.Length ? 0 : -1;
        return 1;
    }

    private static int ScalarValueAt(string value, int utf16Offset)
    {
        var high = value[utf16Offset];
        if (!char.IsHighSurrogate(high)) return high;
        var low = value[utf16Offset + 1];
        return 0x10000 + ((high - 0xD800) << 10) + low - 0xDC00;
    }

    private sealed class ScalarOrdinalComparer : IComparer<string>
    {
        public int Compare(string? left, string? right) => CompareScalarOrdinal(left, right);
    }
}
