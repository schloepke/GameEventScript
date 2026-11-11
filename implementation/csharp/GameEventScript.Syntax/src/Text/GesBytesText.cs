// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Syntax;

/// <summary>
/// The hexadecimal text of Bytes values, as in <c>:Bytes("a0b7 f3")</c>: pairs of hexadecimal digits in either case,
/// optionally separated by single ASCII spaces. The canonical form writes lowercase pairs without spaces.
/// </summary>
internal static class GesBytesText
{
    private const string Digits = "0123456789abcdef";

    // The canonical source and literal text of a Bytes value.
    internal static string Format(byte[] bytes)
    {
        var text = new char[bytes.Length * 2 + 10];
        ":Bytes(\"".CopyTo(0, text, 0, 8);
        for (var index = 0; index < bytes.Length; index++)
        {
            text[8 + index * 2] = Digits[bytes[index] >> 4];
            text[9 + index * 2] = Digits[bytes[index] & 0xF];
        }

        text[text.Length - 2] = '"';
        text[text.Length - 1] = ')';
        return new string(text);
    }

    // The bytes of hexadecimal text, or null when a pair is incomplete, a digit is invalid or a space does not
    // separate two pairs.
    internal static byte[]? Parse(string text)
    {
        var count = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == ' ')
            {
                if (index == 0 || index + 1 == text.Length || text[index + 1] == ' ') return null;
                index++;
                continue;
            }

            if (index + 1 >= text.Length || GesNumberLiteral.HexDigit(text[index]) < 0 || GesNumberLiteral.HexDigit(text[index + 1]) < 0) return null;
            count++;
            index += 2;
        }

        if (count == 0) return [];
        var bytes = new byte[count];
        count = 0;
        for (index = 0; index < text.Length; index++)
        {
            if (text[index] == ' ') continue;
            bytes[count++] = (byte)(GesNumberLiteral.HexDigit(text[index]) << 4 | GesNumberLiteral.HexDigit(text[index + 1]));
            index++;
        }

        return bytes;
    }
}
