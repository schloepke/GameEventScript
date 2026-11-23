// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkCookiesTests
{
    private const string Source = """
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)
        network :Game as { client sends :Commands reliable }
        """;

    private const long Window = 10_000_000;
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    private static readonly byte[] Address = [10, 0, 0, 1, 0x1f, 0x90];
    private static readonly GesNetworkOptions Options = new() { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None, Transport = GesNetworkTransport.Datagram };

    private static GesNetworkConnection Client() =>
        GesNetworkConnection.CreateClient(GesSchemaSet.Parse(Source).Network("Game").Bind(), "t"u8, 0, Options);

    private static byte[] Hello(GesNetworkConnection client)
    {
        var buffer = new byte[2048];
        var length = client.Transmit(buffer);
        Assert.AreEqual(1200, length);
        return buffer[..length];
    }

    [TestMethod]
    public void HelloWithoutCookieGetsARetryWhoseCookieIsAccepted()
    {
        using var gate = new GesNetworkCookies(Key, Window);
        var client = Client();
        var retry = new byte[GesNetworkCookies.RetryBytes];
        Assert.AreEqual(GesNetworkCookieResult.Retry, gate.Check(Hello(client), Address, 5, retry));
        Assert.AreEqual(2, retry[0]);
        Assert.AreEqual(24, retry[1]);

        client.Receive(retry, 10);
        var hello = Hello(client);
        Assert.AreEqual(GesNetworkCookieResult.Accept, gate.Check(hello, Address, 20, new byte[GesNetworkCookies.RetryBytes]));
        Assert.AreEqual(GesNetworkCookieResult.Accept, gate.Check(hello, Address, Window + 20, new byte[GesNetworkCookies.RetryBytes]));
        Assert.AreEqual(GesNetworkCookieResult.Retry, gate.Check(hello, Address, 2 * Window, new byte[GesNetworkCookies.RetryBytes]));
        Assert.AreEqual(GesNetworkCookieResult.Retry, gate.Check(hello, [10, 0, 0, 2, 0x1f, 0x90], 20, new byte[GesNetworkCookies.RetryBytes]));
        using var other = new GesNetworkCookies(new byte[32], Window);
        Assert.AreEqual(GesNetworkCookieResult.Retry, other.Check(hello, Address, 20, new byte[GesNetworkCookies.RetryBytes]));

        var server = GesNetworkConnection.CreateServer(GesSchemaSet.Parse(Source).Network("Game").Bind(), 20, Options);
        server.Receive(hello, 20);
        Assert.AreEqual(GesNetworkState.AwaitAccept, server.State);
    }

    [TestMethod]
    public void NegativeTimesUseTheFloorWindow()
    {
        using var gate = new GesNetworkCookies(Key, Window);
        var retry = new byte[GesNetworkCookies.RetryBytes];
        Assert.AreEqual(GesNetworkCookieResult.Retry, gate.Check(Hello(Client()), Address, -1, retry));
        Assert.AreEqual(ulong.MaxValue, BitConverter.ToUInt64(retry, 2));
    }

    [TestMethod]
    public void ShortForeignOrCutDatagramsAreDropped()
    {
        using var gate = new GesNetworkCookies(Key, Window);
        var hello = Hello(Client());
        var retry = new byte[GesNetworkCookies.RetryBytes];
        Assert.AreEqual(GesNetworkCookieResult.Drop, gate.Check(hello.AsSpan(0, 1199), Address, 0, retry));
        var foreign = (byte[])hello.Clone();
        foreign[4] ^= 1;
        Assert.AreEqual(GesNetworkCookieResult.Drop, gate.Check(foreign, Address, 0, retry));
        var data = (byte[])hello.Clone();
        data[0] = 5;
        Assert.AreEqual(GesNetworkCookieResult.Drop, gate.Check(data, Address, 0, retry));
        Assert.AreEqual(GesNetworkCookieResult.Drop, gate.Check(new byte[1200], Address, 0, retry));
    }

    [TestMethod]
    public void InvalidArgumentsThrow()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new GesNetworkCookies(new byte[31], Window));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GesNetworkCookies(Key, 0));
        using var gate = new GesNetworkCookies(Key, Window);
        Assert.ThrowsExactly<ArgumentException>(() => gate.Check(new byte[1200], Address, 0, new byte[25]));
    }
}
