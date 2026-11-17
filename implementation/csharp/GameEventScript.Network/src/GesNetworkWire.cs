// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;

namespace GameEventScript.Network;

// Constants and value codecs of the network wire format.
internal static class GesNetworkWire
{
    internal const int MinimumPacketBytes = 1200;
    internal const int MaximumPacketBytes = 65535;
    internal const int MaximumTokenBytes = 512;
    internal const int MaximumDetailBytes = 512;
    internal const int MaximumCodeBytes = 64;
    internal const int MaximumRoutes = 64;
    internal const int MaximumBaselineBytes = 1 << 30;
    internal const int Version = 1;

    internal const byte Hello = 1;
    internal const byte Welcome = 3;
    internal const byte Close = 4;
    internal const byte Data = 5;

    // The magic of a hello: "GESN".
    internal const uint Magic = 0x4e534547;

    internal static int VarintSize(uint value)
    {
        var size = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            size++;
        }

        return size;
    }

    internal static int WriteVarint(Span<byte> target, int position, uint value)
    {
        while (value >= 0x80)
        {
            target[position++] = (byte)(value | 0x80);
            value >>= 7;
        }

        target[position++] = (byte)value;
        return position;
    }

    // Reads a canonical varint. Returns 1 on success, 0 when the bytes end first, -1 when it is not canonical or too long.
    internal static int ReadVarint(ReadOnlySpan<byte> source, ref int position, out uint value)
    {
        value = 0;
        var start = position;
        for (var index = 0; index < 5; index++)
        {
            if (start + index >= source.Length) return 0;
            var current = source[start + index];
            if (index == 4 && current > 0x0f) return -1;
            value |= (uint)(current & 0x7f) << (7 * index);
            if ((current & 0x80) == 0)
            {
                if (current == 0 && index > 0) return -1;
                position = start + index + 1;
                return 1;
            }
        }

        return -1;
    }

    internal static void WriteUInt16(Span<byte> target, int position, ushort value)
    {
        target[position] = (byte)value;
        target[position + 1] = (byte)(value >> 8);
    }

    internal static ushort ReadUInt16(ReadOnlySpan<byte> source, int position) => (ushort)(source[position] | (source[position + 1] << 8));

    internal static void WriteUInt32(Span<byte> target, int position, uint value)
    {
        target[position] = (byte)value;
        target[position + 1] = (byte)(value >> 8);
        target[position + 2] = (byte)(value >> 16);
        target[position + 3] = (byte)(value >> 24);
    }

    internal static uint ReadUInt32(ReadOnlySpan<byte> source, int position) =>
        (uint)(source[position] | (source[position + 1] << 8) | (source[position + 2] << 16) | (source[position + 3] << 24));

    internal static void WriteUInt64(Span<byte> target, int position, ulong value)
    {
        WriteUInt32(target, position, (uint)value);
        WriteUInt32(target, position + 4, (uint)(value >> 32));
    }

    internal static ulong ReadUInt64(ReadOnlySpan<byte> source, int position) =>
        ReadUInt32(source, position) | ((ulong)ReadUInt32(source, position + 4) << 32);

    // The UTF-8 bytes of a detail, shortened to whole scalars of at most 512 bytes.
    internal static byte[] DetailBytes(string detail)
    {
        var bytes = new UTF8Encoding(false, false).GetBytes(detail);
        if (bytes.Length <= MaximumDetailBytes) return bytes;
        var length = MaximumDetailBytes;
        while ((bytes[length] & 0xc0) == 0x80) length--;
        return bytes.AsSpan(0, length).ToArray();
    }

    // Whether the bytes are well-formed UTF-8: no overlong forms, surrogates or scalars above U+10FFFF.
    internal static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length)
        {
            var lead = bytes[index];
            if (lead < 0x80)
            {
                index++;
                continue;
            }

            int count;
            byte low = 0x80, high = 0xbf;
            if (lead is >= 0xc2 and <= 0xdf) count = 1;
            else if (lead is >= 0xe0 and <= 0xef)
            {
                count = 2;
                if (lead == 0xe0) low = 0xa0;
                else if (lead == 0xed) high = 0x9f;
            }
            else if (lead is >= 0xf0 and <= 0xf4)
            {
                count = 3;
                if (lead == 0xf0) low = 0x90;
                else if (lead == 0xf4) high = 0x8f;
            }
            else return false;

            if (index + count >= bytes.Length) return false;
            var second = bytes[index + 1];
            if (second < low || second > high) return false;
            for (var next = 2; next <= count; next++)
            {
                if ((bytes[index + next] & 0xc0) != 0x80) return false;
            }

            index += count + 1;
        }

        return true;
    }
}
