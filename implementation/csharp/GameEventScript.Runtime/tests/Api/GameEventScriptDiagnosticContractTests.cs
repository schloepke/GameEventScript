// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Api;

[TestClass]
public sealed class GameEventScriptDiagnosticContractTests
{
    [TestMethod]
    public void ProgramFormatAndLinkExceptionsExposePortableDiagnostics()
    {
        var format = Assert.ThrowsExactly<GesProgramFormatException>(() =>
            GameEventScriptProgramReader.Read([0x00]));
        Assert.AreEqual(GesDiagnosticPhase.Decode, format.Diagnostic.Phase);
        Assert.AreEqual("decode.invalidHeaderSize", format.Diagnostic.Code);

        var program = new GesProgramBuilder()
            .AddScript("function leaf(value) be value + 1\n\non Start(value) {\n  let result be leaf(value: value)\n}")
            .Compile();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GesRuntimeLimits { MaxCallDepth = 0 })
            .Build().StartForTest();
        var link = Assert.ThrowsExactly<GesDynamicLinkException>(() => host.Load(program));
        Assert.AreEqual(GesDiagnosticPhase.Link, link.Diagnostic.Phase);
        Assert.AreEqual(GesDiagnosticCodes.LinkRequiredCallStackDepthExceeded, link.Diagnostic.Code);
        Assert.AreEqual(program.ModuleName, link.Diagnostic.ProgramName);
    }

}
