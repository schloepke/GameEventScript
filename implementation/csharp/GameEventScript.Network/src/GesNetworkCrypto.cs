// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace GameEventScript.Network;

// The cipher suite of GesNetwork (specs/Network.md#cipher-suite): X25519 (RFC 7748), ChaCha20-Poly1305 (RFC 8439) and
// HKDF-SHA256 (RFC 5869). Secret data never selects a branch or a memory address: the ladder swaps with masks, the
// field arithmetic has no data-dependent carries and the tag comparison accumulates every byte.

// X25519 over field elements of ten signed limbs of alternately 26 and 25 bits (radix 2^25.5).
internal static class GesNetworkX25519
{
    public const int KeyBytes = 32;
    private const int Limbs = 10;
    private const long A24 = 121665;

    // Writes the public key of a private key: its product with the base point 9.
    public static void PublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        Span<byte> basePoint = stackalloc byte[KeyBytes];
        basePoint.Clear();
        basePoint[0] = 9;
        ScalarMultiply(privateKey, basePoint, publicKey);
    }

    // Writes the shared secret of a private key and a peer's public key; false when it is all zero (a low-order key).
    public static bool SharedSecret(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> publicKey, Span<byte> secret)
    {
        ScalarMultiply(privateKey, publicKey, secret);
        var bits = 0;
        for (var index = 0; index < KeyBytes; index++) bits |= secret[index];
        return bits != 0;
    }

    // The function X25519 of RFC 7748 section 5: the clamped scalar times the u-coordinate, high bit masked.
    public static void ScalarMultiply(ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> u, Span<byte> result)
    {
        Span<byte> k = stackalloc byte[KeyBytes];
        scalar.Slice(0, KeyBytes).CopyTo(k);
        k[0] &= 248;
        k[31] &= 127;
        k[31] |= 64;

        Span<long> memory = stackalloc long[Limbs * 14];
        memory.Clear();
        var x1 = memory.Slice(0, Limbs);
        var x2 = memory.Slice(Limbs, Limbs);
        var z2 = memory.Slice(2 * Limbs, Limbs);
        var x3 = memory.Slice(3 * Limbs, Limbs);
        var z3 = memory.Slice(4 * Limbs, Limbs);
        var a = memory.Slice(5 * Limbs, Limbs);
        var aa = memory.Slice(6 * Limbs, Limbs);
        var b = memory.Slice(7 * Limbs, Limbs);
        var bb = memory.Slice(8 * Limbs, Limbs);
        var e = memory.Slice(9 * Limbs, Limbs);
        var c = memory.Slice(10 * Limbs, Limbs);
        var d = memory.Slice(11 * Limbs, Limbs);
        var da = memory.Slice(12 * Limbs, Limbs);
        var cb = memory.Slice(13 * Limbs, Limbs);

        FromBytes(x1, u);
        x2[0] = 1;
        x1.CopyTo(x3);
        z3[0] = 1;
        long swap = 0;
        for (var t = 254; t >= 0; t--)
        {
            long bit = (k[t >> 3] >> (t & 7)) & 1;
            swap ^= bit;
            ConditionalSwap(x2, x3, swap);
            ConditionalSwap(z2, z3, swap);
            swap = bit;

            Add(a, x2, z2);
            Multiply(aa, a, a);
            Subtract(b, x2, z2);
            Multiply(bb, b, b);
            Subtract(e, aa, bb);
            Add(c, x3, z3);
            Subtract(d, x3, z3);
            Multiply(da, d, a);
            Multiply(cb, c, b);
            Add(x3, da, cb);
            Multiply(x3, x3, x3);
            Subtract(z3, da, cb);
            Multiply(z3, z3, z3);
            Multiply(z3, z3, x1);
            Multiply(x2, aa, bb);
            MultiplySmall(z2, e, A24);
            Add(z2, z2, aa);
            Multiply(z2, z2, e);
        }

        ConditionalSwap(x2, x3, swap);
        ConditionalSwap(z2, z3, swap);
        Invert(a, z2);
        Multiply(x2, x2, a);
        ToBytes(result, x2);
        memory.Clear();
        k.Clear();
    }

    // Bit offsets of the limbs; limb i holds bits Offset(i) up to Offset(i + 1).
    private static int Offset(int limb) => (51 * limb + 1) >> 1;

    private static void FromBytes(Span<long> h, ReadOnlySpan<byte> bytes)
    {
        for (var limb = 0; limb < Limbs; limb++)
        {
            var offset = Offset(limb);
            var width = Offset(limb + 1) - offset;
            var index = offset >> 3;
            var word = bytes[index] | (uint)bytes[index + 1] << 8 | (uint)bytes[index + 2] << 16 | (uint)bytes[index + 3] << 24;
            // The last limb ends at bit 255 and so drops the high bit.
            h[limb] = (word >> (offset & 7)) & ((1u << width) - 1);
        }
    }

    // Writes the canonical 32 bytes: the element is reduced below 2^255 - 19 without a branch.
    private static void ToBytes(Span<byte> bytes, Span<long> h)
    {
        var q = (19 * h[9] + (1L << 24)) >> 25;
        for (var limb = 0; limb < Limbs; limb++) q = (h[limb] + q) >> ((limb & 1) == 0 ? 26 : 25);
        h[0] += 19 * q;
        for (var limb = 0; limb < Limbs - 1; limb++)
        {
            var shift = (limb & 1) == 0 ? 26 : 25;
            var carry = h[limb] >> shift;
            h[limb + 1] += carry;
            h[limb] -= carry << shift;
        }

        h[9] &= (1L << 25) - 1;
        ulong accumulator = 0;
        var bits = 0;
        var position = 0;
        for (var limb = 0; limb < Limbs; limb++)
        {
            accumulator |= (ulong)h[limb] << bits;
            bits += (limb & 1) == 0 ? 26 : 25;
            while (bits >= 8)
            {
                bytes[position++] = (byte)accumulator;
                accumulator >>= 8;
                bits -= 8;
            }
        }

        bytes[position] = (byte)accumulator;
    }

    private static void Add(Span<long> h, Span<long> f, Span<long> g)
    {
        for (var limb = 0; limb < Limbs; limb++) h[limb] = f[limb] + g[limb];
    }

    private static void Subtract(Span<long> h, Span<long> f, Span<long> g)
    {
        for (var limb = 0; limb < Limbs; limb++) h[limb] = f[limb] - g[limb];
    }

    // Swaps f and g when swap is 1 and leaves them when it is 0, with the same memory accesses either way.
    private static void ConditionalSwap(Span<long> f, Span<long> g, long swap)
    {
        var mask = -swap;
        for (var limb = 0; limb < Limbs; limb++)
        {
            var x = (f[limb] ^ g[limb]) & mask;
            f[limb] ^= x;
            g[limb] ^= x;
        }
    }

    // h = f * g; h may alias f or g. Products of two odd limbs count twice; those past limb 9 wrap with 19.
    private static void Multiply(Span<long> h, Span<long> f, Span<long> g)
    {
        Span<long> t = stackalloc long[Limbs];
        t.Clear();
        for (var i = 0; i < Limbs; i++)
        {
            for (var j = 0; j < Limbs; j++)
            {
                var product = f[i] * g[j];
                if ((i & j & 1) != 0) product *= 2;
                var k = i + j;
                if (k >= Limbs)
                {
                    product *= 19;
                    k -= Limbs;
                }

                t[k] += product;
            }
        }

        Carry(t);
        t.CopyTo(h);
    }

    private static void MultiplySmall(Span<long> h, Span<long> f, long factor)
    {
        for (var limb = 0; limb < Limbs; limb++) h[limb] = f[limb] * factor;
        Carry(h);
    }

    // Brings every limb back into its signed width; the order bounds the intermediate values as ref10 does.
    private static void Carry(Span<long> h)
    {
        CarryLimb(h, 0);
        CarryLimb(h, 4);
        CarryLimb(h, 1);
        CarryLimb(h, 5);
        CarryLimb(h, 2);
        CarryLimb(h, 6);
        CarryLimb(h, 3);
        CarryLimb(h, 7);
        CarryLimb(h, 4);
        CarryLimb(h, 8);
        CarryLimb(h, 9);
        CarryLimb(h, 0);
    }

    private static void CarryLimb(Span<long> h, int limb)
    {
        var shift = (limb & 1) == 0 ? 26 : 25;
        var carry = (h[limb] + (1L << (shift - 1))) >> shift;
        h[limb] -= carry << shift;
        if (limb == Limbs - 1) h[0] += 19 * carry;
        else h[limb + 1] += carry;
    }

    private static void Square(Span<long> h, Span<long> f, int times)
    {
        Multiply(h, f, f);
        for (var index = 1; index < times; index++) Multiply(h, h, h);
    }

    // out = z^(2^255 - 21), the inverse of z, by the fixed addition chain of ref10.
    private static void Invert(Span<long> result, Span<long> z)
    {
        Span<long> memory = stackalloc long[4 * Limbs];
        var t0 = memory.Slice(0, Limbs);
        var t1 = memory.Slice(Limbs, Limbs);
        var t2 = memory.Slice(2 * Limbs, Limbs);
        var t3 = memory.Slice(3 * Limbs, Limbs);
        Square(t0, z, 1);
        Square(t1, t0, 2);
        Multiply(t1, z, t1);
        Multiply(t0, t0, t1);
        Square(t2, t0, 1);
        Multiply(t1, t1, t2);
        Square(t2, t1, 5);
        Multiply(t1, t2, t1);
        Square(t2, t1, 10);
        Multiply(t2, t2, t1);
        Square(t3, t2, 20);
        Multiply(t2, t3, t2);
        Square(t2, t2, 10);
        Multiply(t1, t2, t1);
        Square(t2, t1, 50);
        Multiply(t2, t2, t1);
        Square(t3, t2, 100);
        Multiply(t2, t3, t2);
        Square(t2, t2, 50);
        Multiply(t1, t2, t1);
        Square(t1, t1, 5);
        Multiply(result, t1, t0);
        memory.Clear();
    }
}

// Poly1305 (RFC 8439 section 2.5) over five 26-bit limbs. Owns its state; reusable after Start.
internal sealed class GesNetworkPoly1305
{
    public const int TagBytes = 16;
    private const uint Mask = 0x3ffffff;

    private readonly byte[] _block = new byte[16];
    private uint _r0, _r1, _r2, _r3, _r4;
    private uint _h0, _h1, _h2, _h3, _h4;
    private uint _pad0, _pad1, _pad2, _pad3;
    private int _buffered;

    // Starts a tag with a one-time 32-byte key.
    public void Start(ReadOnlySpan<byte> key)
    {
        _r0 = Load(key, 0) & 0x3ffffff;
        _r1 = (Load(key, 3) >> 2) & 0x3ffff03;
        _r2 = (Load(key, 6) >> 4) & 0x3ffc0ff;
        _r3 = (Load(key, 9) >> 6) & 0x3f03fff;
        _r4 = (Load(key, 12) >> 8) & 0x00fffff;
        _pad0 = Load(key, 16);
        _pad1 = Load(key, 20);
        _pad2 = Load(key, 24);
        _pad3 = Load(key, 28);
        _h0 = _h1 = _h2 = _h3 = _h4 = 0;
        _buffered = 0;
    }

    public void Update(ReadOnlySpan<byte> data)
    {
        if (_buffered > 0)
        {
            var take = Math.Min(16 - _buffered, data.Length);
            data.Slice(0, take).CopyTo(_block.AsSpan(_buffered));
            _buffered += take;
            data = data.Slice(take);
            if (_buffered < 16) return;
            Block(_block, 1u << 24);
            _buffered = 0;
        }

        var whole = data.Length & ~15;
        if (whole > 0)
        {
            Block(data.Slice(0, whole), 1u << 24);
            data = data.Slice(whole);
        }

        data.CopyTo(_block);
        _buffered = data.Length;
    }

    // Completes a partial block with zero bytes as a full block: the padding of the AEAD construction.
    public void PadToBlock()
    {
        if (_buffered == 0) return;
        _block.AsSpan(_buffered).Clear();
        Block(_block, 1u << 24);
        _buffered = 0;
    }

    public void Finish(Span<byte> tag)
    {
        if (_buffered > 0)
        {
            _block[_buffered] = 1;
            _block.AsSpan(_buffered + 1).Clear();
            Block(_block, 0);
            _buffered = 0;
        }

        uint h0 = _h0, h1 = _h1, h2 = _h2, h3 = _h3, h4 = _h4;
        var c = h1 >> 26;
        h1 &= Mask;
        h2 += c;
        c = h2 >> 26;
        h2 &= Mask;
        h3 += c;
        c = h3 >> 26;
        h3 &= Mask;
        h4 += c;
        c = h4 >> 26;
        h4 &= Mask;
        h0 += c * 5;
        c = h0 >> 26;
        h0 &= Mask;
        h1 += c;

        // g = h + 5 - 2^130; h is replaced by g when g is not negative, selected with a mask.
        var g0 = h0 + 5;
        c = g0 >> 26;
        g0 &= Mask;
        var g1 = h1 + c;
        c = g1 >> 26;
        g1 &= Mask;
        var g2 = h2 + c;
        c = g2 >> 26;
        g2 &= Mask;
        var g3 = h3 + c;
        c = g3 >> 26;
        g3 &= Mask;
        var g4 = h4 + c - (1u << 26);
        var select = (g4 >> 31) - 1;
        var keep = ~select;
        h0 = (h0 & keep) | (g0 & select);
        h1 = (h1 & keep) | (g1 & select);
        h2 = (h2 & keep) | (g2 & select);
        h3 = (h3 & keep) | (g3 & select);
        h4 = (h4 & keep) | (g4 & select);

        h0 |= h1 << 26;
        h1 = (h1 >> 6) | (h2 << 20);
        h2 = (h2 >> 12) | (h3 << 14);
        h3 = (h3 >> 18) | (h4 << 8);
        ulong f = (ulong)h0 + _pad0;
        BinaryPrimitives.WriteUInt32LittleEndian(tag, (uint)f);
        f = (ulong)h1 + _pad1 + (f >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(4), (uint)f);
        f = (ulong)h2 + _pad2 + (f >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(8), (uint)f);
        f = (ulong)h3 + _pad3 + (f >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag.Slice(12), (uint)f);
        _r0 = _r1 = _r2 = _r3 = _r4 = _pad0 = _pad1 = _pad2 = _pad3 = 0;
    }

    // Adds whole 16-byte blocks; the accumulator stays in locals across them.
    private void Block(ReadOnlySpan<byte> m, uint highBit)
    {
        uint h0 = _h0, h1 = _h1, h2 = _h2, h3 = _h3, h4 = _h4;
        ulong r0 = _r0, r1 = _r1, r2 = _r2, r3 = _r3, r4 = _r4;
        ulong s1 = r1 * 5, s2 = r2 * 5, s3 = r3 * 5, s4 = r4 * 5;
        for (var offset = 0; offset < m.Length; offset += 16)
        {
            h0 += Load(m, offset) & Mask;
            h1 += (Load(m, offset + 3) >> 2) & Mask;
            h2 += (Load(m, offset + 6) >> 4) & Mask;
            h3 += (Load(m, offset + 9) >> 6) & Mask;
            h4 += (Load(m, offset + 12) >> 8) | highBit;
            var d0 = h0 * r0 + h1 * s4 + h2 * s3 + h3 * s2 + h4 * s1;
            var d1 = h0 * r1 + h1 * r0 + h2 * s4 + h3 * s3 + h4 * s2;
            var d2 = h0 * r2 + h1 * r1 + h2 * r0 + h3 * s4 + h4 * s3;
            var d3 = h0 * r3 + h1 * r2 + h2 * r1 + h3 * r0 + h4 * s4;
            var d4 = h0 * r4 + h1 * r3 + h2 * r2 + h3 * r1 + h4 * r0;
            var c = d0 >> 26;
            h0 = (uint)d0 & Mask;
            d1 += c;
            c = d1 >> 26;
            h1 = (uint)d1 & Mask;
            d2 += c;
            c = d2 >> 26;
            h2 = (uint)d2 & Mask;
            d3 += c;
            c = d3 >> 26;
            h3 = (uint)d3 & Mask;
            d4 += c;
            c = d4 >> 26;
            h4 = (uint)d4 & Mask;
            h0 += (uint)c * 5;
            var carry = h0 >> 26;
            h0 &= Mask;
            h1 += carry;
        }

        _h0 = h0;
        _h1 = h1;
        _h2 = h2;
        _h3 = h3;
        _h4 = h4;
    }

    private static uint Load(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
}

// ChaCha20-Poly1305 (RFC 8439 section 2.8) with a replaceable key. Seals in place and opens into a destination, which
// may be the sealed bytes themselves; allocates nothing after creation. Not thread-safe.
internal sealed class GesNetworkAead
{
    public const int KeyBytes = 32;
    public const int NonceBytes = 12;
    public const int TagBytes = GesNetworkPoly1305.TagBytes;
    private const int BlockBytes = 64;

    private readonly byte[] _stream = new byte[BlockBytes];
    private readonly byte[] _lengths = new byte[16];
    private readonly byte[] _tag = new byte[TagBytes];
    private readonly GesNetworkPoly1305 _poly = new();
    private uint _k0, _k1, _k2, _k3, _k4, _k5, _k6, _k7;
    private uint _n0, _n1, _n2;

    public void SetKey(ReadOnlySpan<byte> key)
    {
        _k0 = Load(key, 0);
        _k1 = Load(key, 4);
        _k2 = Load(key, 8);
        _k3 = Load(key, 12);
        _k4 = Load(key, 16);
        _k5 = Load(key, 20);
        _k6 = Load(key, 24);
        _k7 = Load(key, 28);
    }

    public void Clear()
    {
        _k0 = _k1 = _k2 = _k3 = _k4 = _k5 = _k6 = _k7 = 0;
        Array.Clear(_stream, 0, _stream.Length);
    }

    // Encrypts the first length bytes of the buffer in place and writes the tag behind them.
    public void Seal(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, Span<byte> buffer, int length)
    {
        SetNonce(nonce);
        var data = buffer.Slice(0, length);
        Crypt(data, data);
        Authenticate(aad, data, buffer.Slice(length, TagBytes));
    }

    // Checks the tag behind the ciphertext and decrypts it into the destination, which holds at least the plaintext,
    // the sealed length minus the tag, and either starts at the sealed bytes or does not overlap them. False, with the
    // destination unchanged, when the tag does not match.
    public bool Open(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> sealedBytes, Span<byte> destination)
    {
        if (sealedBytes.Length < TagBytes) return false;
        var length = sealedBytes.Length - TagBytes;
        SetNonce(nonce);
        var ciphertext = sealedBytes.Slice(0, length);
        Authenticate(aad, ciphertext, _tag);
        var difference = 0;
        for (var index = 0; index < TagBytes; index++) difference |= _tag[index] ^ sealedBytes[length + index];
        if (difference != 0) return false;
        Crypt(ciphertext, destination.Slice(0, length));
        return true;
    }

    private void SetNonce(ReadOnlySpan<byte> nonce)
    {
        _n0 = Load(nonce, 0);
        _n1 = Load(nonce, 4);
        _n2 = Load(nonce, 8);
    }

    private void Authenticate(ReadOnlySpan<byte> aad, ReadOnlySpan<byte> ciphertext, Span<byte> tag)
    {
        Block(0, default, default);
        _poly.Start(_stream);
        _poly.Update(aad);
        _poly.PadToBlock();
        _poly.Update(ciphertext);
        _poly.PadToBlock();
        BinaryPrimitives.WriteUInt64LittleEndian(_lengths, (ulong)aad.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(_lengths.AsSpan(8), (ulong)ciphertext.Length);
        _poly.Update(_lengths);
        _poly.Finish(tag);
    }

    // XORs the key stream from block counter 1 on.
    private void Crypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        uint counter = 1;
        while (!source.IsEmpty)
        {
            var take = Math.Min(BlockBytes, source.Length);
            Block(counter++, source.Slice(0, take), destination);
            source = source.Slice(take);
            destination = destination.Slice(take);
        }
    }

    // The ChaCha20 block function (RFC 8439 section 2.3). A full block of source is XORed with the key stream into the
    // destination word by word; a shorter one leaves the key stream in its buffer and is XORed byte by byte.
    private void Block(uint counter, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        uint x0 = 0x61707865, x1 = 0x3320646e, x2 = 0x79622d32, x3 = 0x6b206574;
        uint x4 = _k0, x5 = _k1, x6 = _k2, x7 = _k3, x8 = _k4, x9 = _k5, x10 = _k6, x11 = _k7;
        uint x12 = counter, x13 = _n0, x14 = _n1, x15 = _n2;
        for (var round = 0; round < 10; round++)
        {
            QuarterRound(ref x0, ref x4, ref x8, ref x12);
            QuarterRound(ref x1, ref x5, ref x9, ref x13);
            QuarterRound(ref x2, ref x6, ref x10, ref x14);
            QuarterRound(ref x3, ref x7, ref x11, ref x15);
            QuarterRound(ref x0, ref x5, ref x10, ref x15);
            QuarterRound(ref x1, ref x6, ref x11, ref x12);
            QuarterRound(ref x2, ref x7, ref x8, ref x13);
            QuarterRound(ref x3, ref x4, ref x9, ref x14);
        }

        x0 += 0x61707865;
        x1 += 0x3320646e;
        x2 += 0x79622d32;
        x3 += 0x6b206574;
        x4 += _k0;
        x5 += _k1;
        x6 += _k2;
        x7 += _k3;
        x8 += _k4;
        x9 += _k5;
        x10 += _k6;
        x11 += _k7;
        x12 += counter;
        x13 += _n0;
        x14 += _n1;
        x15 += _n2;
        if (source.Length == BlockBytes)
        {
            Xor(source, destination, 0, x0, x1);
            Xor(source, destination, 8, x2, x3);
            Xor(source, destination, 16, x4, x5);
            Xor(source, destination, 24, x6, x7);
            Xor(source, destination, 32, x8, x9);
            Xor(source, destination, 40, x10, x11);
            Xor(source, destination, 48, x12, x13);
            Xor(source, destination, 56, x14, x15);
            return;
        }

        var stream = _stream.AsSpan();
        Store(stream, 0, x0, x1);
        Store(stream, 8, x2, x3);
        Store(stream, 16, x4, x5);
        Store(stream, 24, x6, x7);
        Store(stream, 32, x8, x9);
        Store(stream, 40, x10, x11);
        Store(stream, 48, x12, x13);
        Store(stream, 56, x14, x15);
        for (var index = 0; index < source.Length; index++) destination[index] = (byte)(source[index] ^ stream[index]);
    }

    private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        a += b;
        d = Rotate(d ^ a, 16);
        c += d;
        b = Rotate(b ^ c, 12);
        a += b;
        d = Rotate(d ^ a, 8);
        c += d;
        b = Rotate(b ^ c, 7);
    }

    private static uint Rotate(uint value, int count) => (value << count) | (value >> (32 - count));

    private static void Xor(ReadOnlySpan<byte> source, Span<byte> destination, int offset, uint low, uint high) =>
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset), BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset)) ^ ((ulong)high << 32 | low));

    private static void Store(Span<byte> stream, int offset, uint low, uint high) =>
        BinaryPrimitives.WriteUInt64LittleEndian(stream.Slice(offset), (ulong)high << 32 | low);

    private static uint Load(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
}

// HKDF-SHA256 (RFC 5869). Used at the handshake and at a key change, so its HMAC objects may allocate.
internal static class GesNetworkHkdf
{
    public const int HashBytes = 32;

    public static void Extract(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> ikm, Span<byte> prk)
    {
        using var hmac = new HMACSHA256(salt.ToArray());
        if (!hmac.TryComputeHash(ikm, prk, out _)) throw new InvalidOperationException("HMAC-SHA256 wrote no digest.");
    }

    // Expands with info made of a label and an optional suffix, which saves the callers a concatenation.
    public static void Expand(ReadOnlySpan<byte> prk, ReadOnlySpan<byte> info, Span<byte> output) => Expand(prk, info, default, output);

    public static void Expand(ReadOnlySpan<byte> prk, ReadOnlySpan<byte> label, ReadOnlySpan<byte> suffix, Span<byte> output)
    {
        using var hmac = new HMACSHA256(prk.ToArray());
        var input = new byte[HashBytes + label.Length + suffix.Length + 1];
        Span<byte> block = stackalloc byte[HashBytes];
        var previous = 0;
        byte counter = 1;
        while (!output.IsEmpty)
        {
            block.Slice(0, previous).CopyTo(input);
            label.CopyTo(input.AsSpan(previous));
            suffix.CopyTo(input.AsSpan(previous + label.Length));
            input[previous + label.Length + suffix.Length] = counter++;
            if (!hmac.TryComputeHash(input.AsSpan(0, previous + label.Length + suffix.Length + 1), block, out _))
                throw new InvalidOperationException("HMAC-SHA256 wrote no digest.");
            var take = Math.Min(HashBytes, output.Length);
            block.Slice(0, take).CopyTo(output);
            output = output.Slice(take);
            previous = HashBytes;
        }

        Array.Clear(input, 0, input.Length);
        block.Clear();
    }
}
