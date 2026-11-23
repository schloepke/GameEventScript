// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Api;

[TestClass]
public sealed class GesClockTests
{
    [TestMethod]
    public void SystemClockStartsNearZeroAndNeverGoesBack()
    {
        var clock = new GesSystemClock();
        var first = clock.ElapsedMicroseconds;
        Assert.IsTrue(first >= 0 && first < 60_000_000, $"{first}");
        var previous = first;
        for (var index = 0; index < 1000; index++)
        {
            var now = clock.ElapsedMicroseconds;
            Assert.IsGreaterThanOrEqualTo(previous, now);
            previous = now;
        }
    }

    [TestMethod]
    public void VirtualClockMovesOnlyWhenAdvanced()
    {
        var clock = new GesVirtualClock();
        Assert.AreEqual(0, clock.ElapsedMicroseconds);
        clock.Advance(0);
        clock.Advance(1500);
        Assert.AreEqual(1500, clock.ElapsedMicroseconds);
        Assert.AreEqual(7, new GesVirtualClock(7).ElapsedMicroseconds);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GesVirtualClock(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clock.Advance(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clock.Advance(long.MaxValue));
        Assert.AreEqual(1500, clock.ElapsedMicroseconds);
        clock.Advance(long.MaxValue - 1500);
        Assert.AreEqual(long.MaxValue, clock.ElapsedMicroseconds);
    }
}
