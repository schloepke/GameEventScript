// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkDeflateTests
{
    private static readonly JsonElement Fixture =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "Network", "deflate.json"))).RootElement;

    private static byte[] Generate(JsonElement spec)
    {
        var length = spec.GetProperty("length").GetInt32();
        var alphabet = spec.GetProperty("alphabet").GetInt32();
        var repeat = spec.TryGetProperty("repeat", out var value) ? value.GetInt32() : 0;
        var state = spec.GetProperty("seed").GetInt64();
        var data = new byte[length + repeat];
        for (var index = 0; index < length; index++)
        {
            state = (state * 1103515245 + 12345) % (1L << 31);
            data[index] = (byte)(((state >> 16) & 0xff) % alphabet);
        }

        data.AsSpan(0, repeat).CopyTo(data.AsSpan(length));
        return data;
    }

    [TestMethod]
    public void EncoderProducesTheReferenceBytesAndStopsAtTheOutputLimit()
    {
        var encoder = new GesNetworkDeflater();
        var decoder = new GesNetworkInflater();
        foreach (var testCase in Fixture.GetProperty("encode").EnumerateArray())
        {
            var name = testCase.GetProperty("name").GetString();
            var data = Generate(testCase.GetProperty("data"));
            var expectedLength = testCase.GetProperty("deflateLength").GetInt32();
            var output = new byte[expectedLength + 16];
            Assert.AreEqual(expectedLength, encoder.Compress(data, output), name);
            var stream = output[..expectedLength];
            Assert.AreEqual(testCase.GetProperty("deflateSha256").GetString(), Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(), name);
            if (testCase.GetProperty("deflate").ValueKind == JsonValueKind.String)
            {
                Assert.AreEqual(testCase.GetProperty("deflate").GetString(), Convert.ToHexString(stream).ToLowerInvariant(), name);
            }

            Assert.AreEqual(expectedLength, encoder.Compress(data, new byte[expectedLength]), name);
            Assert.AreEqual(-1, encoder.Compress(data, new byte[expectedLength - 1]), name);
            var restored = new byte[data.Length];
            Assert.IsTrue(decoder.Inflate(stream, restored), name);
            CollectionAssert.AreEqual(data, restored, name);
        }
    }

    [TestMethod]
    public void DecoderAcceptsExactlyTheStreamsZlibAccepts()
    {
        var decoder = new GesNetworkInflater();
        foreach (var testCase in Fixture.GetProperty("inflate").EnumerateArray())
        {
            var name = testCase.GetProperty("name").GetString();
            var stream = Convert.FromHexString(testCase.GetProperty("stream").GetString()!);
            var output = new byte[testCase.GetProperty("size").GetInt32()];
            var expected = testCase.GetProperty("sha256");
            var accepted = decoder.Inflate(stream, output);
            if (expected.ValueKind == JsonValueKind.Null)
            {
                Assert.IsFalse(accepted, name);
                continue;
            }

            Assert.IsTrue(accepted, name);
            Assert.AreEqual(expected.GetString(), Convert.ToHexString(SHA256.HashData(output)).ToLowerInvariant(), name);
        }
    }
}
