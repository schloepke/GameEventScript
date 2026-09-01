using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptDiagnosticContractTests
{
    [TestMethod]
    public void NativeFailureIsReportedAndLaterHandlerContinues()
    {
        var diagnostics = new List<GameEventScriptDiagnostic>();
        var calls = 0;
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(runtimeError: diagnostics.Add))
            .Build();
        host.Subscribe("Ping", [], new ThrowingHandler());
        host.Subscribe("Ping", [], new CountingHandler(() => calls++));

        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Ping")));
        var result = host.RunToCompletion();

        Assert.AreEqual(1, calls);
        Assert.AreEqual(GameEventScriptExecutionState.RuntimeError, result.State);
        Assert.IsNotNull(result.Diagnostic);
        Assert.AreEqual(GameEventScriptDiagnosticPhase.Runtime, result.Diagnostic.Phase);
        Assert.AreEqual(GameEventScriptDiagnosticCodes.RuntimeNativeHandlerFailure, result.Diagnostic.Code);
        Assert.AreEqual("Ping()", result.Diagnostic.HandlerName);
        Assert.HasCount(1, diagnostics);
        Assert.AreSame(result.Diagnostic, diagnostics[0]);
        Assert.IsTrue(host.IsIdle);
    }

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

    private sealed class ThrowingHandler : IGameEventScriptNativeMessageHandler
    {
        public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
            => throw new InvalidOperationException("test failure");
    }

    private sealed class CountingHandler(Action count) : IGameEventScriptNativeMessageHandler
    {
        public void Handle(GameEventScriptMessage message, GameEventScriptContext context) => count();
    }
}
