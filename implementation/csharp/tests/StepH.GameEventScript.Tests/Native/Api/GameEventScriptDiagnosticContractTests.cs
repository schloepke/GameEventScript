// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Native.Api;

[TestClass]
public sealed class GameEventScriptDiagnosticContractTests
{
    [TestMethod]
    public void ProgramFormatAndLinkExceptionsExposePortableDiagnostics()
    {
        var format = Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() =>
            GameEventScriptProgramReader.Read([0x00]));
        Assert.AreEqual(GameEventScriptDiagnosticPhase.Decode, format.Diagnostic.Phase);
        Assert.AreEqual("decode.invalidHeaderSize", format.Diagnostic.Code);

        var program = GameEventScriptBuilder.Create()
            .AddScript("function leaf(value) be value + 1\n\non Start(value) {\n  let result be leaf(value: value)\n}")
            .Compile();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxCallDepth = 0 })
            .Build();
        var link = Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() => host.Load(program));
        Assert.AreEqual(GameEventScriptDiagnosticPhase.Link, link.Diagnostic.Phase);
        Assert.AreEqual(GameEventScriptDiagnosticCodes.LinkRequiredCallStackDepthExceeded, link.Diagnostic.Code);
        Assert.AreEqual(program.ModuleName, link.Diagnostic.ProgramName);
    }

}
