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

        while (data.Length >= 16)
        {
            Block(data, 1u << 24);
            data = data.Slice(16);
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

    private void Block(ReadOnlySpan<byte> m, uint highBit)
    {
        var h0 = _h0 + (Load(m, 0) & Mask);
        var h1 = _h1 + ((Load(m, 3) >> 2) & Mask);
        var h2 = _h2 + ((Load(m, 6) >> 4) & Mask);
        var h3 = _h3 + ((Load(m, 9) >> 6) & Mask);
        var h4 = _h4 + ((Load(m, 12) >> 8) | highBit);
        ulong r0 = _r0, r1 = _r1, r2 = _r2, r3 = _r3, r4 = _r4;
        ulong s1 = r1 * 5, s2 = r2 * 5, s3 = r3 * 5, s4 = r4 * 5;
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
        _h0 = h0;
        _h1 = h1;
        _h2 = h2;
        _h3 = h3;
        _h4 = h4;
    }

    private static uint Load(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
}

// ChaCha20-Poly1305 (RFC 8439 section 2.8) with a replaceable key. Seals and opens in place; allocates nothing after
// creation. Not thread-safe.
internal sealed class GesNetworkAead
{
    public const int KeyBytes = 32;
    public const int NonceBytes = 12;
    public const int TagBytes = GesNetworkPoly1305.TagBytes;

    private readonly uint[] _key = new uint[8];
    private readonly uint[] _state = new uint[16];
    private readonly uint[] _working = new uint[16];
    private readonly byte[] _stream = new byte[64];
    private readonly byte[] _lengths = new byte[16];
    private readonly byte[] _tag = new byte[TagBytes];
    private readonly GesNetworkPoly1305 _poly = new();

    public void SetKey(ReadOnlySpan<byte> key)
    {
        for (var index = 0; index < 8; index++) _key[index] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(4 * index));
    }

    public void Clear()
    {
        Array.Clear(_key, 0, _key.Length);
        Array.Clear(_state, 0, _state.Length);
        Array.Clear(_working, 0, _working.Length);
        Array.Clear(_stream, 0, _stream.Length);
    }

    // Encrypts the first length bytes of the buffer in place and writes the tag behind them.
    public void Seal(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, Span<byte> buffer, int length)
    {
        Crypt(nonce, buffer.Slice(0, length));
        Authenticate(nonce, aad, buffer.Slice(0, length), buffer.Slice(length, TagBytes));
    }

    // Checks the tag behind the ciphertext and decrypts it in place; false, with the buffer unchanged, when the tag does
    // not match. The plaintext is the sealed length minus the tag.
    public bool Open(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, Span<byte> sealedBuffer)
    {
        if (sealedBuffer.Length < TagBytes) return false;
        var length = sealedBuffer.Length - TagBytes;
        Authenticate(nonce, aad, sealedBuffer.Slice(0, length), _tag);
        var difference = 0;
        for (var index = 0; index < TagBytes; index++) difference |= _tag[index] ^ sealedBuffer[length + index];
        if (difference != 0) return false;
        Crypt(nonce, sealedBuffer.Slice(0, length));
        return true;
    }

    private void Authenticate(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> ciphertext, Span<byte> tag)
    {
        Block(nonce, 0);
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
    private void Crypt(ReadOnlySpan<byte> nonce, Span<byte> data)
    {
        uint counter = 1;
        while (!data.IsEmpty)
        {
            Block(nonce, counter++);
            var take = Math.Min(64, data.Length);
            for (var index = 0; index < take; index++) data[index] ^= _stream[index];
            data = data.Slice(take);
        }
    }

    // The ChaCha20 block function (RFC 8439 section 2.3) into the key stream buffer.
    private void Block(ReadOnlySpan<byte> nonce, uint counter)
    {
        var s = _state;
        s[0] = 0x61707865;
        s[1] = 0x3320646e;
        s[2] = 0x79622d32;
        s[3] = 0x6b206574;
        for (var index = 0; index < 8; index++) s[4 + index] = _key[index];
        s[12] = counter;
        s[13] = BinaryPrimitives.ReadUInt32LittleEndian(nonce);
        s[14] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(4));
        s[15] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(8));
        var w = _working;
        Array.Copy(s, w, 16);
        for (var round = 0; round < 10; round++)
        {
            QuarterRound(w, 0, 4, 8, 12);
            QuarterRound(w, 1, 5, 9, 13);
            QuarterRound(w, 2, 6, 10, 14);
            QuarterRound(w, 3, 7, 11, 15);
            QuarterRound(w, 0, 5, 10, 15);
            QuarterRound(w, 1, 6, 11, 12);
            QuarterRound(w, 2, 7, 8, 13);
            QuarterRound(w, 3, 4, 9, 14);
        }

        for (var index = 0; index < 16; index++) BinaryPrimitives.WriteUInt32LittleEndian(_stream.AsSpan(4 * index), w[index] + s[index]);
    }

    private static void QuarterRound(uint[] x, int a, int b, int c, int d)
    {
        x[a] += x[b];
        x[d] = Rotate(x[d] ^ x[a], 16);
        x[c] += x[d];
        x[b] = Rotate(x[b] ^ x[c], 12);
        x[a] += x[b];
        x[d] = Rotate(x[d] ^ x[a], 8);
        x[c] += x[d];
        x[b] = Rotate(x[b] ^ x[c], 7);
    }

    private static uint Rotate(uint value, int count) => (value << count) | (value >> (32 - count));
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
