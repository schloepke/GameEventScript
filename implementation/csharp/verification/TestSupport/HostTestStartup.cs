// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Tests;

internal static class HostTestStartup
{
    internal static GameEventScriptHost StartForTest(this GameEventScriptHost host)
    {
        Assert.AreEqual(GesStartState.Ready, host.Start().State);
        return host;
    }
}
