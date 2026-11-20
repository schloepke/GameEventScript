// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkBuffersTests
{
    [TestMethod]
    public void StartReservesExactlyTheTotal()
    {
        var message = new GesNetworkPartial();
        message.Start(0, 0, 1001);
        Assert.AreEqual(1001 + 126, message.Capacity);
    }

    [TestMethod]
    public void CancelFreesTheStorage()
    {
        var message = new GesNetworkPartial();
        message.Start(0, 0, 4096);
        message.Cancel(0, 0);
        Assert.AreEqual(0, message.Capacity);
        Assert.IsTrue(message.Cancelled);
    }

    [TestMethod]
    public void ReleasedMessagesKeepStorageOnlyWithinTheRetention()
    {
        var receive = new GesNetworkOrderedReceive(1500);
        var first = receive.Message(0);
        first.Start(0, 0, 1000);
        var second = receive.Message(1);
        second.Start(0, 1, 1000);
        receive.Release(first);
        Assert.AreEqual(1125, receive.Retained);
        receive.Release(second);
        Assert.AreEqual(0, second.Capacity);
        Assert.AreEqual(1125, receive.Retained);

        // Taking a kept message back counts its storage out of the retention.
        var reused = receive.Message(2);
        Assert.AreSame(second, reused);
        reused = receive.Message(3);
        Assert.AreSame(first, reused);
        Assert.AreEqual(0, receive.Retained);
    }
}
