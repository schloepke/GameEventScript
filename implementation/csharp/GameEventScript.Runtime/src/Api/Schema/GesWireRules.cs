// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

/// <summary>
/// The value rules of the schema wire format over plain operands: Text and tag validity, the exact quantization of a step
/// <c>p / 10^k</c> and wrapping. Generated schema code calls them with constant operands, and the interpreted writer and
/// reader use the same rules, so both encode identical bytes.
/// </summary>
public static class GesWireRules
{
    // 2^50, the largest step count of a quantized value.
    private const double LargestSteps = 1125899906842624d;

    /// <summary>Gets whether bytes are well-formed UTF-8 without surrogates or overlong forms.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Whether the bytes are valid.</returns>
    public static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length)
        {
            var first = bytes[index];
            int more;
            int least;
            int value;
            if (first < 0x80)
            {
                index++;
                continue;
            }

            if (first is >= 0xC2 and <= 0xDF) (more, least, value) = (1, 0x80, first & 0x1F);
            else if (first is >= 0xE0 and <= 0xEF) (more, least, value) = (2, 0x800, first & 0x0F);
            else if (first is >= 0xF0 and <= 0xF4) (more, least, value) = (3, 0x10000, first & 0x07);
            else return false;
            if (index + more >= bytes.Length) return false;
            for (var offset = 1; offset <= more; offset++)
            {
                var next = bytes[index + offset];
                if ((next & 0xC0) != 0x80) return false;
                value = (value << 6) | (next & 0x3F);
            }

            if (value < least || value > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF) return false;
            index += more + 1;
        }

        return true;
    }

    /// <summary>Gets whether bytes are a tag name without <c>#</c>: a lowercase ASCII letter, then ASCII letters and digits.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Whether the bytes are a tag name.</returns>
    public static bool IsTagName(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes[0] is < 0x61 or > 0x7A) return false;
        for (var index = 1; index < bytes.Length; index++)
            if (bytes[index] is not (>= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x61 and <= 0x7A)) return false;
        return true;
    }

    /// <summary>Gets the index of a value in a closed quantized range.</summary>
    /// <param name="value">The value.</param>
    /// <param name="lowerValue">The decoded value of the lower bound.</param>
    /// <param name="upperValue">The decoded value of the upper bound.</param>
    /// <param name="tenPower">The binary64 value of <c>10^k</c> of the step <c>p / 10^k</c>.</param>
    /// <param name="numerator">The binary64 value of <c>p</c> of the step.</param>
    /// <param name="lower">The lower bound in steps.</param>
    /// <param name="path">The field path for an error.</param>
    /// <returns>The index.</returns>
    /// <exception cref="GesWireException"><c>wire.outOfRange</c> when the value is not finite or lies outside the range.</exception>
    public static ulong QuantizedIndex(double value, double lowerValue, double upperValue, double tenPower, double numerator, long lower, string path)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < lowerValue || value > upperValue) throw new GesWireException(GesWireException.OutOfRange, path);
        return (ulong)(Steps(value, tenPower, numerator, path) - lower);
    }

    /// <summary>Gets the index of a value in a wrapping quantized range, which reduces it into its period.</summary>
    /// <param name="value">The value.</param>
    /// <param name="tenPower">The binary64 value of <c>10^k</c> of the step <c>p / 10^k</c>.</param>
    /// <param name="numerator">The binary64 value of <c>p</c> of the step.</param>
    /// <param name="lower">The lower bound in steps.</param>
    /// <param name="period">The number of steps in one period.</param>
    /// <param name="path">The field path for an error.</param>
    /// <returns>The index.</returns>
    /// <exception cref="GesWireException"><c>wire.outOfRange</c> when the value is not finite or exceeds 2^50 steps.</exception>
    public static ulong WrappedQuantizedIndex(double value, double tenPower, double numerator, long lower, ulong period, string path)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new GesWireException(GesWireException.OutOfRange, path);
        return WrappedIndex(Steps(value, tenPower, numerator, path), lower, period);
    }

    /// <summary>Gets the value of a quantized index: <c>(index + lower) * p / 10^k</c>, exact as the format requires.</summary>
    /// <param name="index">The index.</param>
    /// <param name="lower">The lower bound in steps.</param>
    /// <param name="numerator">The integer <c>p</c> of the step <c>p / 10^k</c>.</param>
    /// <param name="tenPower">The binary64 value of <c>10^k</c>.</param>
    /// <returns>The value.</returns>
    public static double QuantizedValue(ulong index, long lower, long numerator, double tenPower) => (((long)index + lower) * numerator) / tenPower;

    /// <summary>Gets the index of an integer in a wrapping range, reduced into its period.</summary>
    /// <param name="value">The value.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="period">The number of values in one period.</param>
    /// <returns>The index.</returns>
    public static ulong WrappedIndex(long value, long lower, ulong period)
    {
        if (value >= lower) return ((ulong)value - (ulong)lower) % period;
        var remainder = ((ulong)lower - (ulong)value) % period;
        return remainder == 0 ? 0 : period - remainder;
    }

    /// <summary>
    /// Writes a changed index of a field with <c>delta within</c>: its difference from the baseline's index in the range form
    /// when that is at most <paramref name="deltaRange"/>, otherwise the full index after a fallback bit. A wrapping field
    /// takes the shorter way around its period.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="index">The record's index, different from <paramref name="held"/>.</param>
    /// <param name="held">The baseline's index.</param>
    /// <param name="maxIndex">The largest index; a wrapping field's period is one more.</param>
    /// <param name="wrapping">Whether the field wraps.</param>
    /// <param name="deltaRange">The largest difference of the range form.</param>
    /// <param name="deltaWidth">The width of the range form.</param>
    /// <param name="width">The width of a full index.</param>
    /// <exception cref="GesWireException"><c>wire.bufferTooSmall</c> when the buffer is full.</exception>
    public static void WriteDeltaRange(ref GesBitWriter writer, ulong index, ulong held, ulong maxIndex, bool wrapping, ulong deltaRange, int deltaWidth, int width)
    {
        var difference = Difference(index, held, maxIndex, wrapping, out var negative);
        if (difference > deltaRange)
        {
            writer.Write(1, 1);
            writer.Write(index, width);
            return;
        }

        writer.Write(0, 1);
        writer.Write(negative ? deltaRange - difference : deltaRange + difference, deltaWidth);
    }

    /// <summary>
    /// Reads a changed index of a field with <c>delta within</c> against the baseline's index, and rejects every form that is
    /// not the canonical one.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="held">The baseline's index.</param>
    /// <param name="maxIndex">The largest index; a wrapping field's period is one more.</param>
    /// <param name="wrapping">Whether the field wraps.</param>
    /// <param name="deltaRange">The largest difference of the range form.</param>
    /// <param name="deltaWidth">The width of the range form.</param>
    /// <param name="width">The width of a full index.</param>
    /// <param name="path">The field path for an error.</param>
    /// <returns>The record's index.</returns>
    /// <exception cref="GesWireException">The data ends early, a full index is invalid (<c>wire.invalidIndex</c>), or the form
    /// is not canonical or leaves the range (<c>wire.invalidDelta</c>).</exception>
    public static ulong ReadDeltaRange(ref GesBitReader reader, ulong held, ulong maxIndex, bool wrapping, ulong deltaRange, int deltaWidth, int width, string path)
    {
        if (reader.Read(1, path) == 1)
        {
            var value = reader.Read(width, path);
            if (value > maxIndex) throw new GesWireException(GesWireException.InvalidIndex, path);
            if (value == held || Difference(value, held, maxIndex, wrapping, out _) <= deltaRange) throw new GesWireException(GesWireException.InvalidDelta, path);
            return value;
        }

        var range = reader.Read(deltaWidth, path);
        if (range > deltaRange * 2 || range == deltaRange) throw new GesWireException(GesWireException.InvalidDelta, path);
        if (range > deltaRange)
        {
            var forward = range - deltaRange;
            if (wrapping) return forward >= maxIndex + 1 - held ? held - (maxIndex + 1 - forward) : held + forward;
            if (forward > maxIndex - held) throw new GesWireException(GesWireException.InvalidDelta, path);
            return held + forward;
        }

        var backward = deltaRange - range;
        if (backward <= held) return held - backward;
        if (!wrapping) throw new GesWireException(GesWireException.InvalidDelta, path);
        return maxIndex + 1 - (backward - held);
    }

    /// <summary>
    /// Writes Text from a string as its UTF-8 byte count in <paramref name="width"/> bits followed by the bytes; a lone surrogate
    /// becomes U+FFFD, as with <see cref="System.Text.Encoding.UTF8"/>. Generated code writes the text id of a handle with it.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="text">The text.</param>
    /// <param name="width">The width of the count.</param>
    /// <param name="maxCount">The largest byte count.</param>
    /// <param name="path">The field path for an error.</param>
    /// <exception cref="GesWireException"><c>wire.tooLong</c> when the text has more bytes, or <c>wire.bufferTooSmall</c>.</exception>
    public static void WriteText(ref GesBitWriter writer, string text, int width, int maxCount, string path)
    {
        var count = 0L;
        for (var index = 0; index < text.Length; index++)
        {
            var unit = text[index];
            if (unit < 0x80) count += 1;
            else if (unit < 0x800) count += 2;
            else if (char.IsHighSurrogate(unit) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                count += 4;
                index++;
            }
            else count += 3;
        }

        if (count > maxCount) throw new GesWireException(GesWireException.TooLong, path);
        writer.Write((ulong)count, width);
        for (var index = 0; index < text.Length; index++)
        {
            int scalar = text[index];
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                scalar = char.ConvertToUtf32(text[index], text[index + 1]);
                index++;
            }
            else if (char.IsSurrogate(text[index])) scalar = 0xFFFD;

            if (scalar < 0x80) writer.Write((ulong)scalar, 8);
            else if (scalar < 0x800)
            {
                writer.Write((ulong)(0xC0 | (scalar >> 6)), 8);
                writer.Write((ulong)(0x80 | (scalar & 0x3F)), 8);
            }
            else if (scalar < 0x10000)
            {
                writer.Write((ulong)(0xE0 | (scalar >> 12)), 8);
                writer.Write((ulong)(0x80 | ((scalar >> 6) & 0x3F)), 8);
                writer.Write((ulong)(0x80 | (scalar & 0x3F)), 8);
            }
            else
            {
                writer.Write((ulong)(0xF0 | (scalar >> 18)), 8);
                writer.Write((ulong)(0x80 | ((scalar >> 12) & 0x3F)), 8);
                writer.Write((ulong)(0x80 | ((scalar >> 6) & 0x3F)), 8);
                writer.Write((ulong)(0x80 | (scalar & 0x3F)), 8);
            }
        }
    }

    /// <summary>
    /// Reads Text written by <see cref="WriteText"/> into a string. When <paramref name="current"/> holds the same bytes it is
    /// returned unchanged, so reading an unchanged value does not allocate.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="width">The width of the count.</param>
    /// <param name="maxCount">The largest byte count.</param>
    /// <param name="current">The string held so far, or <see langword="null"/>.</param>
    /// <param name="path">The field path for an error.</param>
    /// <returns>The text.</returns>
    /// <exception cref="GesWireException">The data ends early, the count is larger (<c>wire.invalidCount</c>) or the bytes
    /// are not valid UTF-8 (<c>wire.invalidText</c>).</exception>
    public static string ReadText(ref GesBitReader reader, int width, int maxCount, string? current, string path)
    {
        var count = reader.Read(width, path);
        if (count > (ulong)maxCount) throw new GesWireException(GesWireException.InvalidCount, path);
        reader.Require((long)count * 8L, path);
        Span<byte> bytes = count <= 256 ? stackalloc byte[(int)count] : new byte[count];
        for (var index = 0; index < bytes.Length; index++) bytes[index] = (byte)reader.Read(8, path);
        if (!IsValidUtf8(bytes)) throw new GesWireException(GesWireException.InvalidText, path);
        if (current is not null && SameUtf8(current, bytes)) return current;
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    // Whether a string's UTF-8 form equals valid UTF-8 bytes, without allocating.
    private static bool SameUtf8(string text, ReadOnlySpan<byte> bytes)
    {
        var at = 0;
        for (var index = 0; index < text.Length; index++)
        {
            int scalar = text[index];
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                scalar = char.ConvertToUtf32(text[index], text[index + 1]);
                index++;
            }
            else if (char.IsSurrogate(text[index])) return false;

            if (at >= bytes.Length) return false;
            int decoded;
            var first = bytes[at];
            if (first < 0x80)
            {
                decoded = first;
                at += 1;
            }
            else if (first < 0xE0)
            {
                decoded = ((first & 0x1F) << 6) | (bytes[at + 1] & 0x3F);
                at += 2;
            }
            else if (first < 0xF0)
            {
                decoded = ((first & 0x0F) << 12) | ((bytes[at + 1] & 0x3F) << 6) | (bytes[at + 2] & 0x3F);
                at += 3;
            }
            else
            {
                decoded = ((first & 0x07) << 18) | ((bytes[at + 1] & 0x3F) << 12) | ((bytes[at + 2] & 0x3F) << 6) | (bytes[at + 3] & 0x3F);
                at += 4;
            }

            if (decoded != scalar) return false;
        }

        return at == bytes.Length;
    }

    // The difference of two indices without overflow; a wrapping field takes the shorter way, and an exact half period is negative.
    internal static ulong Difference(ulong index, ulong baseline, ulong maxIndex, bool wrapping, out bool negative)
    {
        if (!wrapping)
        {
            negative = index < baseline;
            return negative ? baseline - index : index - baseline;
        }

        var period = maxIndex + 1;
        var forward = index >= baseline ? index - baseline : period - (baseline - index);
        negative = forward >= period - (period / 2);
        return negative ? period - forward : forward;
    }

    private static long Steps(double value, double tenPower, double numerator, string path)
    {
        var quotient = value * tenPower / numerator;
        if (quotient > LargestSteps || quotient < -LargestSteps) throw new GesWireException(GesWireException.OutOfRange, path);
        return (long)Math.Round(quotient, MidpointRounding.ToEven);
    }
}
