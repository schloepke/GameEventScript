// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Wire = GameEventScript.Network.GesNetworkWire;

namespace GameEventScript.Network;

public sealed partial class GesNetworkConnection
{
    // The security byte of a hello.
    private const byte HelloPlain = 0;
    private const byte HelloKeyRequest = 1;
    private const byte HelloSealed = 2;

    // The bytes a sealed data packet adds: the type and the tag.
    private const int SealedOverhead = 1 + GesNetworkAead.TagBytes;
    private const int EpochShift = 24;
    private const long PacketNumberLimit = 1L << 62;

    private static readonly byte[] LabelHello = "ges hello"u8.ToArray();
    private static readonly byte[] LabelWelcome = "ges welcome"u8.ToArray();
    private static readonly byte[] LabelClient = "ges client"u8.ToArray();
    private static readonly byte[] LabelServer = "ges server"u8.ToArray();
    private static readonly byte[] LabelIv = "ges iv"u8.ToArray();
    private static readonly byte[] LabelKey = "ges key"u8.ToArray();
    private static readonly byte[] ZeroNonce = new byte[GesNetworkAead.NonceBytes];

    private readonly GesNetworkSecurity _minimumSecurity;
    private readonly GesNetworkKey? _key;
    private readonly bool _pinned;
    private readonly bool _testKeys;
    private readonly ulong _testKeySeed;
    private long _ephemeralCount;

    // The server's public key: the pinned one or the one a retry gave the client.
    private byte[]? _serverKey;
    private Keys? _keys;
    private GesNetworkSecurity _security;
    // The session is encrypted: the client read a sealed welcome, the server opened a sealed hello.
    private bool _encrypted;
    // This side sends sealed packets: from the server's welcome or the client's reading of it on.
    private bool _sealing;
    private bool _retryPending;
    private bool _retryAccepted;
    // The hello the client holds is sealed; its next one needs a fresh ephemeral key.
    private bool _helloSealed;
    private bool _ephemeralStale = true;

    /// <summary>
    /// Gets the security level the session reached: <see cref="GesNetworkSecurity.None"/> until it is connected; a
    /// server reports <see cref="GesNetworkSecurity.Encrypted"/> for every encrypted session.
    /// </summary>
    public GesNetworkSecurity Security => _security;

    /// <summary>Gets whether the connection derives its ephemeral keys from <see cref="GesNetworkOptions.InsecureTestKeySeed"/>.</summary>
    public bool UsesInsecureTestKeys => _testKeys;

    // The keys of an encrypted connection, allocated once it uses encryption.
    private sealed class Keys
    {
        public readonly GesNetworkAead Aead = new();
        public readonly byte[] Ephemeral = new byte[GesNetworkX25519.KeyBytes];
        public readonly byte[] EphemeralPublic = new byte[GesNetworkX25519.KeyBytes];
        public readonly byte[] Shared = new byte[GesNetworkX25519.KeyBytes];
        public readonly byte[] HelloKey = new byte[GesNetworkAead.KeyBytes];
        public readonly byte[] WelcomeKey = new byte[GesNetworkAead.KeyBytes];
        public readonly byte[] Nonce = new byte[GesNetworkAead.NonceBytes];
        public readonly byte[] SendSecret = new byte[GesNetworkHkdf.HashBytes];
        public readonly byte[] SendIv = new byte[GesNetworkAead.NonceBytes];
        public readonly byte[] SendKey = new byte[GesNetworkAead.KeyBytes];
        public long SendEpoch = -1;
        public readonly byte[] ReceiveSecret = new byte[GesNetworkHkdf.HashBytes];
        public readonly byte[] ReceiveIv = new byte[GesNetworkAead.NonceBytes];
        public readonly byte[][] ReceiveKeys = { new byte[GesNetworkAead.KeyBytes], new byte[GesNetworkAead.KeyBytes], new byte[GesNetworkAead.KeyBytes] };
        public readonly long[] ReceiveEpochs = { -1, -1, -1 };
        // The largest authenticated packet number; -1 before the first.
        public long Largest = -1;
        // The client's sealed hello through its token, for the transcript.
        public byte[] Hello = Array.Empty<byte>();
        public int HelloLength;
        // Holds a packet while it is opened, with room to rebuild a data packet's sequence in front of it.
        public byte[] Buffer = Array.Empty<byte>();
    }

    private Keys SessionKeys => _keys ??= new Keys();

    // ---- Ephemeral keys ----

    private void DrawEphemeral(Keys keys)
    {
        if (_testKeys)
        {
            Span<byte> input = stackalloc byte[4 + 1 + 8 + 8];
            "GESK"u8.CopyTo(input);
            input[4] = _client ? (byte)0 : (byte)1;
            BinaryPrimitives.WriteUInt64LittleEndian(input.Slice(5), _testKeySeed);
            BinaryPrimitives.WriteUInt64LittleEndian(input.Slice(13), (ulong)_ephemeralCount);
            using var sha = SHA256.Create();
            if (!sha.TryComputeHash(input, keys.Ephemeral, out _)) throw new InvalidOperationException("SHA-256 wrote no digest.");
        }
        else
        {
            RandomNumberGenerator.Fill(keys.Ephemeral);
        }

        _ephemeralCount++;
        GesNetworkX25519.PublicKey(keys.Ephemeral, keys.EphemeralPublic);
    }

    // Derives hello_key from es and the server's public key.
    private static void DeriveHelloKey(Keys keys, ReadOnlySpan<byte> serverKey)
    {
        Span<byte> prk = stackalloc byte[GesNetworkHkdf.HashBytes];
        GesNetworkHkdf.Extract(serverKey, keys.Shared, prk);
        GesNetworkHkdf.Expand(prk, LabelHello, keys.HelloKey);
        prk.Clear();
    }

    // Derives the welcome key and both secrets from the transcript, es (in Shared) and ee; false when ee is zero.
    private bool DeriveSessionKeys(Keys keys, ReadOnlySpan<byte> hello, ReadOnlySpan<byte> serverEphemeral, ReadOnlySpan<byte> peerEphemeral)
    {
        Span<byte> material = stackalloc byte[2 * GesNetworkX25519.KeyBytes];
        keys.Shared.CopyTo(material);
        var valid = GesNetworkX25519.SharedSecret(keys.Ephemeral, peerEphemeral, material.Slice(GesNetworkX25519.KeyBytes));
        Span<byte> transcript = stackalloc byte[GesNetworkHkdf.HashBytes];
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            hash.AppendData(hello);
            hash.AppendData(serverEphemeral);
            if (!hash.TryGetHashAndReset(transcript, out _)) throw new InvalidOperationException("SHA-256 wrote no digest.");
        }

        Span<byte> prk = stackalloc byte[GesNetworkHkdf.HashBytes];
        GesNetworkHkdf.Extract(transcript, material, prk);
        GesNetworkHkdf.Expand(prk, LabelWelcome, keys.WelcomeKey);
        GesNetworkHkdf.Expand(prk, _client ? LabelClient : LabelServer, keys.SendSecret);
        GesNetworkHkdf.Expand(prk, _client ? LabelServer : LabelClient, keys.ReceiveSecret);
        GesNetworkHkdf.Expand(keys.SendSecret, LabelIv, keys.SendIv);
        GesNetworkHkdf.Expand(keys.ReceiveSecret, LabelIv, keys.ReceiveIv);
        // A welcome that failed to authenticate may have derived keys before; none of them stays.
        keys.SendEpoch = -1;
        Array.Fill(keys.ReceiveEpochs, -1);
        UpdateReceiveKeys(keys, 0);
        material.Clear();
        prk.Clear();
        Array.Clear(keys.Ephemeral, 0, keys.Ephemeral.Length);
        Array.Clear(keys.Shared, 0, keys.Shared.Length);
        if (keys.Buffer.Length == 0) keys.Buffer = new byte[_maxPacket + 1];
        return valid;
    }

    private static void DeriveKey(ReadOnlySpan<byte> secret, long epoch, Span<byte> key)
    {
        Span<byte> suffix = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(suffix, (ulong)epoch);
        GesNetworkHkdf.Expand(secret, LabelKey, suffix, key);
    }

    // Keeps the receive keys of the epochs next to the given one; derives only those not yet held.
    private static void UpdateReceiveKeys(Keys keys, long epoch)
    {
        for (var wanted = Math.Max(0, epoch - 1); wanted <= epoch + 1; wanted++)
        {
            if (Array.IndexOf(keys.ReceiveEpochs, wanted) >= 0) continue;
            var slot = 0;
            while (keys.ReceiveEpochs[slot] >= 0 && Math.Abs(keys.ReceiveEpochs[slot] - epoch) <= 1) slot++;
            DeriveKey(keys.ReceiveSecret, wanted, keys.ReceiveKeys[slot]);
            keys.ReceiveEpochs[slot] = wanted;
        }
    }

    private static void WriteNonce(Keys keys, ReadOnlySpan<byte> iv, long number)
    {
        iv.CopyTo(keys.Nonce);
        for (var index = 0; index < 8; index++) keys.Nonce[index] ^= (byte)(number >> (8 * index));
    }

    // ---- Hello ----

    // The security byte of the client's next hello.
    private byte HelloSecurity => _serverKey is not null ? HelloSealed : _minimumSecurity == GesNetworkSecurity.None ? HelloPlain : HelloKeyRequest;

    // Writes the security byte, the ephemeral key and the token from the position on; -1 when X25519 gave zero.
    private int WriteHelloSecurity(Span<byte> packet, int position)
    {
        var security = HelloSecurity;
        packet[position++] = security;
        _helloSealed = security == HelloSealed;
        if (security == HelloKeyRequest) return Wire.WriteVarint(packet, position, 0);
        if (security == HelloPlain)
        {
            position = Wire.WriteVarint(packet, position, (uint)_token.Length);
            _token.CopyTo(packet.Slice(position));
            return position + _token.Length;
        }

        var keys = SessionKeys;
        if (_ephemeralStale)
        {
            DrawEphemeral(keys);
            if (!GesNetworkX25519.SharedSecret(keys.Ephemeral, _serverKey, keys.Shared)) return -1;
            DeriveHelloKey(keys, _serverKey);
            _ephemeralStale = false;
        }

        keys.EphemeralPublic.CopyTo(packet.Slice(position));
        position = Wire.WriteVarint(packet, position + GesNetworkX25519.KeyBytes, (uint)(_token.Length + GesNetworkAead.TagBytes));
        _token.CopyTo(packet.Slice(position));
        keys.Aead.SetKey(keys.HelloKey);
        keys.Aead.Seal(ZeroNonce, packet.Slice(0, position), packet.Slice(position), _token.Length);
        position += _token.Length + GesNetworkAead.TagBytes;
        if (keys.Hello.Length < position) keys.Hello = new byte[Math.Max(position, Wire.MinimumPacketBytes)];
        packet.Slice(0, position).CopyTo(keys.Hello);
        keys.HelloLength = position;
        return position;
    }

    // Reads the security byte, the ephemeral key and the token of a hello and checks the end; false when it closed.
    private bool ReceiveHelloSecurity(ReadOnlySpan<byte> packet, int position)
    {
        if (position >= packet.Length) return Fail(GesNetworkCodes.Malformed);
        var security = packet[position++];
        if (security > HelloSealed) return Fail(GesNetworkCodes.Malformed);
        var ephemeral = position;
        if (security == HelloSealed)
        {
            if (packet.Length - position < GesNetworkX25519.KeyBytes) return Fail(GesNetworkCodes.Malformed);
            position += GesNetworkX25519.KeyBytes;
        }

        if (Wire.ReadVarint(packet, ref position, out var length) != 1 || packet.Length - position < length) return Fail(GesNetworkCodes.Malformed);
        var valid = security switch
        {
            HelloPlain => length <= Wire.MaximumTokenBytes,
            HelloKeyRequest => length == 0,
            _ => length is >= GesNetworkAead.TagBytes and <= Wire.MaximumTokenBytes + GesNetworkAead.TagBytes,
        };
        if (!valid) return Fail(GesNetworkCodes.Malformed);
        var sealedStart = position;
        position += (int)length;
        if (!ReadHelloEnd(packet, position)) return false;
        if (_key is null)
        {
            if (security != HelloPlain) return Fail(GesNetworkCodes.Insecure);
            return AcceptHelloToken(packet.Slice(sealedStart, (int)length));
        }

        if (security != HelloSealed)
        {
            _retryPending = true;
            return true;
        }

        var keys = SessionKeys;
        var clientEphemeral = packet.Slice(ephemeral, GesNetworkX25519.KeyBytes);
        var sharedValid = GesNetworkX25519.SharedSecret(_key.PrivateKey, clientEphemeral, keys.Shared);
        DeriveHelloKey(keys, _key.PublicKey);
        if (keys.Buffer.Length == 0) keys.Buffer = new byte[_maxPacket + 1];
        keys.Aead.SetKey(keys.HelloKey);
        if (!keys.Aead.Open(ZeroNonce, packet.Slice(0, sealedStart), packet.Slice(sealedStart, (int)length), keys.Buffer) || !sharedValid)
            return Fail(GesNetworkCodes.AuthenticationFailed);
        DrawEphemeral(keys);
        if (!DeriveSessionKeys(keys, packet.Slice(0, position), keys.EphemeralPublic, clientEphemeral)) return Fail(GesNetworkCodes.AuthenticationFailed);
        _encrypted = true;
        return AcceptHelloToken(keys.Buffer.AsSpan(0, (int)length - GesNetworkAead.TagBytes));
    }

    // Checks what follows a hello's token: nothing on a stream or message transport, zero padding to at least 1200
    // bytes on a datagram transport. False when it closed the connection.
    private bool ReadHelloEnd(ReadOnlySpan<byte> packet, int position)
    {
        if (!_datagram) return position == packet.Length || Fail(GesNetworkCodes.Malformed);
        if (packet.Length < Wire.MinimumPacketBytes) return Fail(GesNetworkCodes.Malformed);
        foreach (var value in packet.Slice(position))
        {
            if (value != 0) return Fail(GesNetworkCodes.Malformed);
        }

        return true;
    }

    private bool AcceptHelloToken(ReadOnlySpan<byte> token)
    {
        State = GesNetworkState.AwaitAccept;
        if (!AddEvent(GesNetworkEventKind.Connecting, -1, token, Counter.Pending, string.Empty, string.Empty, false)) Fail(GesNetworkCodes.QueueFull);
        return true;
    }

    // ---- Retry ----

    private int WriteRetry(Span<byte> packet)
    {
        packet[0] = Wire.Retry;
        packet[1] = 0;
        packet[2] = GesNetworkX25519.KeyBytes;
        _key!.PublicKey.CopyTo(packet.Slice(3));
        return 3 + GesNetworkX25519.KeyBytes;
    }

    private void ReceiveRetry(ReadOnlySpan<byte> packet)
    {
        var position = 1;
        if (Wire.ReadVarint(packet, ref position, out var cookieLength) != 1 || cookieLength > Wire.MaximumCookieBytes || packet.Length - position < cookieLength)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        var cookie = packet.Slice(position, (int)cookieLength);
        position += cookie.Length;
        if (Wire.ReadVarint(packet, ref position, out var keyLength) != 1 || keyLength is not (0 or GesNetworkX25519.KeyBytes) || packet.Length - position != keyLength ||
            (cookieLength == 0 && keyLength == 0))
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        if (!_datagram)
        {
            // One retry answers a hello that is not sealed, with a key and without a cookie.
            if (cookieLength != 0 || keyLength == 0 || _retryAccepted || _helloSealed)
            {
                Fail(GesNetworkCodes.Malformed);
                return;
            }

            _retryAccepted = true;
        }

        if (cookieLength != 0 && !cookie.SequenceEqual(_cookie))
        {
            _cookie = cookie.ToArray();
            _ephemeralStale = true;
        }

        // A later retry replaces the key of an earlier one: until a welcome authenticates, any of them may be forged.
        var key = packet.Slice(position);
        if (keyLength != 0 && !_pinned && !key.SequenceEqual(_serverKey))
        {
            _serverKey = key.ToArray();
            _ephemeralStale = true;
        }

        _helloSent = false;
        _helloInterval = HelloInterval;
    }

    // ---- Welcome ----

    private int WriteSealedWelcome(Span<byte> packet)
    {
        var keys = _keys!;
        packet[0] = Wire.Welcome;
        keys.EphemeralPublic.CopyTo(packet.Slice(1));
        const int fields = 1 + GesNetworkX25519.KeyBytes;
        var length = WriteWelcomeFields(packet, fields) - fields;
        keys.Aead.SetKey(keys.WelcomeKey);
        keys.Aead.Seal(ZeroNonce, packet.Slice(0, fields), packet.Slice(fields), length);
        _sealing = true;
        return fields + length + GesNetworkAead.TagBytes;
    }

    // Opens a sealed welcome and reads its fields; false when it does not authenticate.
    private bool ReceiveSealedWelcome(ReadOnlySpan<byte> packet)
    {
        const int fields = 1 + GesNetworkX25519.KeyBytes;
        var keys = _keys!;
        if (packet.Length < fields + GesNetworkAead.TagBytes) return false;
        // The ephemeral key and es stay until a welcome authenticates; the attempt works on copies.
        Span<byte> ephemeral = stackalloc byte[GesNetworkX25519.KeyBytes];
        Span<byte> shared = stackalloc byte[GesNetworkX25519.KeyBytes];
        keys.Ephemeral.CopyTo(ephemeral);
        keys.Shared.CopyTo(shared);
        var serverEphemeral = packet.Slice(1, GesNetworkX25519.KeyBytes);
        var valid = DeriveSessionKeys(keys, keys.Hello.AsSpan(0, keys.HelloLength), serverEphemeral, serverEphemeral);
        keys.Aead.SetKey(keys.WelcomeKey);
        if (!valid || !keys.Aead.Open(ZeroNonce, packet.Slice(0, fields), packet.Slice(fields), keys.Buffer))
        {
            ephemeral.CopyTo(keys.Ephemeral);
            shared.CopyTo(keys.Shared);
            ephemeral.Clear();
            shared.Clear();
            return false;
        }

        ephemeral.Clear();
        shared.Clear();
        _encrypted = true;
        _sealing = true;
        AcceptWelcome(keys.Buffer.AsSpan(0, packet.Length - fields - GesNetworkAead.TagBytes), 0);
        return true;
    }

    // ---- Sealed packets ----

    // Writes a data packet sealed: the plain writer fills the bytes from offset 1, which are then rearranged into
    // type 6, the sequence, and the inner packet without its sequence. 0 when nothing is due.
    private int WriteSealedData(Span<byte> packet, int limit)
    {
        var number = _sentCount;
        var length = _datagram ? WriteDatagramData(packet.Slice(1), limit - SealedOverhead) : WriteData(packet.Slice(1), limit - SealedOverhead);
        if (length == 0) return 0;
        var flags = packet[2];
        packet[0] = Wire.Sealed;
        packet[1] = packet[3];
        packet[2] = packet[4];
        packet[3] = Wire.Data;
        packet[4] = flags;
        Seal(packet, number, length - 2);
        return length + SealedOverhead;
    }

    private int WriteSealedClose(Span<byte> packet)
    {
        var number = _sentCount++;
        var length = WriteClose(packet.Slice(3));
        packet[0] = Wire.Sealed;
        Wire.WriteUInt16(packet, 1, (ushort)number);
        Seal(packet, number, length);
        return 3 + length + GesNetworkAead.TagBytes;
    }

    // Seals the inner packet of the given length behind the 3 header bytes.
    private void Seal(Span<byte> packet, long number, int length)
    {
        var keys = _keys!;
        var epoch = number >> EpochShift;
        if (epoch != keys.SendEpoch)
        {
            DeriveKey(keys.SendSecret, epoch, keys.SendKey);
            keys.SendEpoch = epoch;
        }

        WriteNonce(keys, keys.SendIv, number);
        keys.Aead.SetKey(keys.SendKey);
        keys.Aead.Seal(keys.Nonce, packet.Slice(0, 3), packet.Slice(3), length);
    }

    // Opens a sealed packet and handles its inner packet. One that does not authenticate is dropped on a datagram
    // transport and closes the connection on the others.
    private void ReceiveSealed(ReadOnlySpan<byte> packet)
    {
        var keys = _keys!;
        var number = !_datagram ? keys.Largest + 1 : packet.Length < 3 ? -1 : PacketNumber(keys, Wire.ReadUInt16(packet, 1));
        var slot = number < 0 ? -1 : Array.IndexOf(keys.ReceiveEpochs, number >> EpochShift);
        var length = packet.Length - 3 - GesNetworkAead.TagBytes;
        var opened = false;
        if (length > 0 && slot >= 0)
        {
            WriteNonce(keys, keys.ReceiveIv, number);
            keys.Aead.SetKey(keys.ReceiveKeys[slot]);
            opened = keys.Aead.Open(keys.Nonce, packet.Slice(0, 3), packet.Slice(3), keys.Buffer.AsSpan(2));
        }

        if (!opened)
        {
            if (!_datagram) Fail(GesNetworkCodes.AuthenticationFailed);
            return;
        }

        _lastReceive = _now;
        if (number > keys.Largest)
        {
            if (number >> EpochShift != Math.Max(0, keys.Largest) >> EpochShift) UpdateReceiveKeys(keys, number >> EpochShift);
            keys.Largest = number;
        }

        var buffer = keys.Buffer;
        switch (buffer[2])
        {
            case Wire.Close:
                ReceiveClose(buffer.AsSpan(2, length));
                break;
            case Wire.Data when length >= 2:
                // Rebuilds the plain data packet in front of the inner one: type, flags and the sequence.
                buffer[0] = Wire.Data;
                buffer[1] = buffer[3];
                buffer[2] = packet[1];
                buffer[3] = packet[2];
                if (_datagram) ReceiveDatagramData(buffer.AsSpan(0, length + 2));
                else ReceiveData(buffer.AsSpan(0, length + 2));
                break;
            default:
                Fail(GesNetworkCodes.Malformed);
                break;
        }
    }

    // The packet number of a received sealed datagram: the number closest to the expected one with the same low 16
    // bits (RFC 9000 appendix A.3); -1 when it would reach 2^62.
    private static long PacketNumber(Keys keys, ushort sequence)
    {
        var expected = keys.Largest + 1;
        const long window = 1L << 16;
        var candidate = (expected & ~(window - 1)) | sequence;
        if (candidate <= expected - window / 2 && candidate < PacketNumberLimit - window) candidate += window;
        else if (candidate > expected + window / 2 && candidate >= window) candidate -= window;
        return candidate < PacketNumberLimit ? candidate : -1;
    }
}
