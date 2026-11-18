// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkCryptoTests
{
    private static readonly JsonElement Fixture =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "Network", "crypto.json"))).RootElement;

    private static byte[] Hex(JsonElement testCase, string name) => Convert.FromHexString(testCase.GetProperty(name).GetString()!);

    private static string Hex(ReadOnlySpan<byte> data) => Convert.ToHexString(data).ToLowerInvariant();

    [TestMethod]
    public void X25519MatchesTheFixture()
    {
        var result = new byte[32];
        foreach (var testCase in Fixture.GetProperty("x25519").EnumerateArray())
        {
            GesNetworkX25519.ScalarMultiply(Hex(testCase, "scalar"), Hex(testCase, "u"), result);
            Assert.AreEqual(testCase.GetProperty("result").GetString(), Hex(result), testCase.GetProperty("name").GetString());
        }
    }

    [TestMethod]
    public void X25519IteratesLikeRfc7748()
    {
        var k = new byte[32];
        k[0] = 9;
        var u = (byte[])k.Clone();
        var next = new byte[32];
        for (var iteration = 1; iteration <= 1000; iteration++)
        {
            GesNetworkX25519.ScalarMultiply(k, u, next);
            k.CopyTo(u, 0);
            next.CopyTo(k, 0);
            if (iteration == 1) Assert.AreEqual("422c8e7a6227d7bca1350b3e2bb7279f7897b87bb6854b783c60e80311ae3079", Hex(k));
        }

        Assert.AreEqual("684cf59ba83309552800ef566f2f4d3c1c3887c49360e3875f2eb94d99532c51", Hex(k));
    }

    [TestMethod]
    public void SharedSecretRejectsLowOrderKeys()
    {
        var secret = new byte[32];
        var scalar = Convert.FromHexString("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a");
        Assert.IsFalse(GesNetworkX25519.SharedSecret(scalar, new byte[32], secret));
        var one = new byte[32];
        one[0] = 1;
        Assert.IsFalse(GesNetworkX25519.SharedSecret(scalar, one, secret));
        Assert.IsTrue(GesNetworkX25519.SharedSecret(scalar, Convert.FromHexString("de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f"), secret));
        Assert.AreEqual("4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742", Hex(secret));
    }

    [TestMethod]
    public void Poly1305MatchesTheFixtureWholeAndInPieces()
    {
        var poly = new GesNetworkPoly1305();
        var tag = new byte[16];
        foreach (var testCase in Fixture.GetProperty("poly1305").EnumerateArray())
        {
            var name = testCase.GetProperty("name").GetString();
            var message = Hex(testCase, "message");
            poly.Start(Hex(testCase, "key"));
            poly.Update(message);
            poly.Finish(tag);
            Assert.AreEqual(testCase.GetProperty("tag").GetString(), Hex(tag), name);
            poly.Start(Hex(testCase, "key"));
            for (var offset = 0; offset < message.Length; offset += 7) poly.Update(message.AsSpan(offset, Math.Min(7, message.Length - offset)));
            poly.Finish(tag);
            Assert.AreEqual(testCase.GetProperty("tag").GetString(), Hex(tag), name);
        }
    }

    [TestMethod]
    public void AeadMatchesTheFixtureAndRejectsChanges()
    {
        var aead = new GesNetworkAead();
        foreach (var testCase in Fixture.GetProperty("aead").EnumerateArray())
        {
            var name = testCase.GetProperty("name").GetString();
            var plaintext = Hex(testCase, "plaintext");
            var buffer = new byte[plaintext.Length + 16];
            plaintext.CopyTo(buffer, 0);
            aead.SetKey(Hex(testCase, "key"));
            aead.Seal(Hex(testCase, "nonce"), Hex(testCase, "aad"), buffer, plaintext.Length);
            Assert.AreEqual(testCase.GetProperty("sealed").GetString(), Hex(buffer), name);
            Assert.IsTrue(aead.Open(Hex(testCase, "nonce"), Hex(testCase, "aad"), buffer, buffer), name);
            CollectionAssert.AreEqual(plaintext, buffer[..plaintext.Length], name);
        }

        foreach (var testCase in Fixture.GetProperty("aeadRejected").EnumerateArray())
        {
            var sealedBytes = Hex(testCase, "sealed");
            var copy = (byte[])sealedBytes.Clone();
            aead.SetKey(Hex(testCase, "key"));
            Assert.IsFalse(aead.Open(Hex(testCase, "nonce"), Hex(testCase, "aad"), copy, copy), testCase.GetProperty("name").GetString());
            CollectionAssert.AreEqual(sealedBytes, copy);
        }
    }

    [TestMethod]
    public void HkdfMatchesTheFixture()
    {
        foreach (var testCase in Fixture.GetProperty("hkdf").EnumerateArray())
        {
            var prk = new byte[32];
            GesNetworkHkdf.Extract(Hex(testCase, "salt"), Hex(testCase, "ikm"), prk);
            var okm = new byte[testCase.GetProperty("length").GetInt32()];
            GesNetworkHkdf.Expand(prk, Hex(testCase, "info"), okm);
            Assert.AreEqual(testCase.GetProperty("okm").GetString(), Hex(okm), testCase.GetProperty("name").GetString());
        }
    }

    // The platform implementations of the same suite agree on random inputs: a port may use either.
    [TestMethod]
    public void AeadAndHkdfAgreeWithThePlatform()
    {
        if (!ChaCha20Poly1305.IsSupported) Assert.Inconclusive("The platform has no ChaCha20-Poly1305.");
        var random = new Random(5);
        var aead = new GesNetworkAead();
        for (var round = 0; round < 200; round++)
        {
            var key = new byte[32];
            var nonce = new byte[12];
            var aad = new byte[random.Next(0, 40)];
            var plaintext = new byte[random.Next(0, 1300)];
            random.NextBytes(key);
            random.NextBytes(nonce);
            random.NextBytes(aad);
            random.NextBytes(plaintext);
            var expected = new byte[plaintext.Length];
            var expectedTag = new byte[16];
            using (var platform = new ChaCha20Poly1305(key)) platform.Encrypt(nonce, plaintext, expected, expectedTag, aad);
            var buffer = new byte[plaintext.Length + 16];
            plaintext.CopyTo(buffer, 0);
            aead.SetKey(key);
            aead.Seal(nonce, aad, buffer, plaintext.Length);
            CollectionAssert.AreEqual(expected.Concat(expectedTag).ToArray(), buffer);

            var info = new byte[random.Next(0, 30)];
            random.NextBytes(info);
            var okm = new byte[random.Next(1, 100)];
            var prk = new byte[32];
            GesNetworkHkdf.Extract(aad, key, prk);
            GesNetworkHkdf.Expand(prk, info, okm);
            CollectionAssert.AreEqual(HKDF.DeriveKey(HashAlgorithmName.SHA256, key, okm.Length, aad, info), okm);
        }
    }
}
