// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Api;

[TestClass]
public sealed class GameEventScriptDiagnosticContractTests
{
    [TestMethod]
    public void ProgramFormatAndLinkExceptionsExposePortableDiagnostics()
    {
        var format = Assert.ThrowsExactly<GesProgramFormatException>(() =>
            GameEventScriptProgram.Read([0x00]));
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

    [TestMethod]
    public void MissingExtensionNamesTheExtensionAtEachCallSite()
    {
        const string source = "on Boot(bot) {\n  publish Probe(value: :navigation.nope(x: bot))\n}\nfunction probe(_ value) be [1, :navigation.nope(x: value)]\n";
        var program = new GesProgramBuilder().AddScript(source, "bot.ges").Compile();
        var link = Assert.ThrowsExactly<GesDynamicLinkException>(() => GameEventScriptHost.CreateBuilder().Build().StartForTest().Load(program));
        Assert.HasCount(2, link.Diagnostics);
        Assert.AreSame(link.Diagnostics[0], link.Diagnostic);
        Assert.AreEqual(new GesSourceLocation("bot.ges", 2, 24, 2, 48, program.ModuleName, 0), link.Diagnostics[0].SourceLocation);
        Assert.AreEqual(new GesSourceLocation("bot.ges", 4, 32, 4, 58, program.ModuleName, 0), link.Diagnostics[1].SourceLocation);
        foreach (var diagnostic in link.Diagnostics)
        {
            Assert.AreEqual(GesDiagnosticCodes.LinkMissingExtension, diagnostic.Code);
            Assert.AreEqual("Unknown extension :navigation.nope(x)", diagnostic.Message);
            Assert.AreEqual("navigation.nope(x)", diagnostic.Symbol);
        }

        var bare = new GesProgramBuilder().AddScript(source, "bot.ges").Compile(new GesCompileOptions { DebugInfo = GesDebugInfoOptions.None });
        link = Assert.ThrowsExactly<GesDynamicLinkException>(() => GameEventScriptHost.CreateBuilder().Build().StartForTest().Load(bare));
        Assert.HasCount(1, link.Diagnostics);
        Assert.AreEqual("navigation.nope(x)", link.Diagnostic.Symbol);
        Assert.IsNull(link.Diagnostic.SourceLocation);
    }

    // Columns count Unicode scalars after a leading byte order mark, and a carriage return ends no column early.
    [TestMethod]
    public void CallSiteColumnsSkipTheByteOrderMarkAndCountScalars()
    {
        const string source = "\uFEFFon Boot() { emit A(value: :x.f(1)) }\r\non Tick() {\r\n  emit B(t: 'äö𝄞', value: :x.f(2))\r\n}\r\n";
        var program = new GesProgramBuilder().AddScript(source, "bot.ges").Compile();
        var link = Assert.ThrowsExactly<GesDynamicLinkException>(() => GameEventScriptHost.CreateBuilder().Build().StartForTest().Load(program));
        Assert.AreEqual(new GesSourceLocation("bot.ges", 1, 27, 1, 34, program.ModuleName, 0), link.Diagnostics[0].SourceLocation);
        Assert.AreEqual(new GesSourceLocation("bot.ges", 3, 27, 3, 34, program.ModuleName, 0), link.Diagnostics[1].SourceLocation);
        Assert.AreEqual($"Unknown extension :x.f(_) at {link.Diagnostics[0].SourceLocation}, {link.Diagnostics[1].SourceLocation}", link.Message);
    }

    [TestMethod]
    public void ExceptionMessageListsTheLocationsThatExist()
    {
        const string source = "on Boot() {\n  emit A(value: :x.f(1))\n}\n";
        var lines = new GesProgramBuilder().AddScript(source, "bot.ges").Compile(new GesCompileOptions { DebugInfo = GesDebugInfoOptions.SourceMap });
        var link = Assert.ThrowsExactly<GesDynamicLinkException>(() => GameEventScriptHost.CreateBuilder().Build().StartForTest().Load(lines));
        Assert.IsNull(link.Diagnostic.SourceLocation!.Column);
        StringAssert.EndsWith(link.Message, "bot.ges (line 2)");
        Assert.AreEqual(link.Message, new GesDynamicLinkException(link.Diagnostic).Message);
    }

    [TestMethod]
    public void ExceptionMessageNamesTheFirstLocationsAndCountsTheRest()
    {
        var diagnostics = new GesDiagnostic[10];
        for (var index = 0; index < diagnostics.Length; index++)
            diagnostics[index] = new GesDiagnostic(GesDiagnosticPhase.Link, GesDiagnosticCodes.LinkMissingExtension, "Unknown", SourceLocation: new GesSourceLocation("bot.ges", index + 1, ModuleName: ""));
        var link = new GesDynamicLinkException(diagnostics);
        StringAssert.EndsWith(link.Message, "bot.ges (line 8) and 2 more");
        Assert.HasCount(10, link.Diagnostics);
    }

    [TestMethod]
    public void DynamicLinkExceptionRejectsAnEmptyOrNullDiagnosticList()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GesDynamicLinkException((IReadOnlyList<GesDiagnostic>)null!));
        Assert.ThrowsExactly<ArgumentException>(() => new GesDynamicLinkException(Array.Empty<GesDiagnostic>()));
        Assert.ThrowsExactly<ArgumentException>(() => new GesDynamicLinkException(new GesDiagnostic[] { null! }));
    }
}
