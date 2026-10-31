// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Syntax;

// Lexes every source of a request losslessly. Offsets are Unicode scalar offsets, so both ports report the same values.
internal static class SyntaxTokens
{
    internal static object Run(JsonElement cases)
    {
        var sources = new Dictionary<string, object[]>(StringComparer.Ordinal);
        foreach (var test in cases.EnumerateArray())
        {
            var index = 0;
            foreach (var source in test.GetProperty("sources").EnumerateArray())
            {
                var text = source.GetProperty("text").GetString()!;
                var scalarOffsets = ScalarOffsets(text);
                sources[test.GetProperty("id").GetString()! + "#" + index++] = GesLexer.Tokenize(text)
                    .Select(token => (object)new object[] { token.Kind.ToString(), scalarOffsets[token.FullStart], scalarOffsets[token.Start], scalarOffsets[token.End], token.Text })
                    .ToArray();
            }
        }

        return sources;
    }

    // The scalar offset of every UTF-16 offset, including the end of the text.
    private static int[] ScalarOffsets(string text)
    {
        var offsets = new int[text.Length + 1];
        var scalar = 0;
        for (var index = 0; index < text.Length; index++)
        {
            offsets[index] = scalar;
            if (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[index + 1])) scalar++;
        }

        offsets[text.Length] = scalar;
        return offsets;
    }
}
