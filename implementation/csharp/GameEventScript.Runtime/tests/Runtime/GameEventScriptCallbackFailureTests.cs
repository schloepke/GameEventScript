// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.VM;
using static GameEventScript.Api.GameEventScriptMessage;

namespace GameEventScript.Tests.Native.Runtime;

/// <summary>
/// Covers C# exception transport, CLR technical details, diagnostic object identity,
/// and the internal VM Begin guard. Portable callback failures and context rules
/// are specified by the Markdown external-callback-failures and host-dispatch suites.
/// </summary>
[TestClass]
public sealed class GameEventScriptCallbackFailureTests
{
    [TestMethod]
    public void ExternalConstructorFailureRetainsClrTechnicalDetails()
    {
        var definition = FaultyDefinition();
        var constructor = new DelegateExternalTypeConstructor(definition.Constructors[0],
            _ => throw new InvalidOperationException("Configured constructor failure."));
        var host = BuildHost(definition, constructor, "on Start { let sample be :Faulty(); emit Done(sample: sample) }", out var diagnostics);

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();

        Assert.HasCount(1, diagnostics);
        Assert.Contains("InvalidOperationException", diagnostics[0].TechnicalDetails!);
        Assert.Contains("Configured constructor failure.", diagnostics[0].TechnicalDetails!);
    }

    [TestMethod]
    public void DeclaredCallbackWithoutInnerCauseUsesClrTypeFallback()
    {
        var definition = FaultyDefinition();
        var constructor = new DelegateExternalTypeConstructor(definition.Constructors[0],
            _ => throw new GameEventScriptExtensionFaultException("faulty.outOfStock", "No stock left."));
        var host = BuildHost(definition, constructor, "on Start { let sample be :Faulty(); emit Done(sample: sample) }", out var diagnostics);

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();

        Assert.HasCount(1, diagnostics);
        // This non-normative CLR fallback is intentionally excluded from Markdown expectations.
        Assert.AreEqual(nameof(GameEventScriptExtensionFaultException), diagnostics[0].TechnicalDetails);
    }

    [TestMethod]
    public void ExternalFieldFailureRetainsClrTechnicalDetails()
    {
        var definition = FaultyDefinition();
        var constructor = new DelegateExternalTypeConstructor(definition.Constructors[0],
            call => call.SetExternalValue(new DelegateExternalValue(definition,
                _ => throw new IndexOutOfRangeException("Configured field failure."))));
        var host = BuildHost(definition, constructor, "on Start { let sample be :Faulty(); let x be sample.value; emit Done(x: x) }", out var diagnostics);

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();

        Assert.HasCount(1, diagnostics);
        Assert.Contains("IndexOutOfRangeException", diagnostics[0].TechnicalDetails!);
    }

    [TestMethod]
    public void ExtensionFaultExceptionRejectsTheReservedRuntimePrefix()
        => Assert.ThrowsExactly<ArgumentException>(() => new GameEventScriptExtensionFaultException("runtime.custom", "boom"));

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t\r\n")]
    [DataRow("\u00a0")]
    public void ExtensionFaultExceptionRejectsBlankCodes(string code)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new GameEventScriptExtensionFaultException(code, "boom"));
        Assert.AreEqual("code", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null, "message", "code")]
    [DataRow("test.fault", null, "message")]
    public void ExtensionFaultExceptionRejectsNullArguments(string? code, string? message, string parameter)
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new GameEventScriptExtensionFaultException(code!, message!));
        Assert.AreEqual(parameter, exception.ParamName);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("reported.program", "Reported()")]
    public void NativeDiagnosticTransportPreservesIdentityAndOriginalObject(string? programName, string? handlerName)
    {
        var original = new GameEventScriptDiagnostic(GameEventScriptDiagnosticPhase.Runtime, "faulty.native", "Reported failure.", Symbol: "origin", ProgramName: programName, HandlerName: handlerName, TechnicalDetails: "Original details.");
        var unchanged = original with { };
        var diagnostics = new List<GameEventScriptDiagnostic>();
        var host = GameEventScriptHost.CreateBuilder().WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(runtimeError: diagnostics.Add)).Build().StartForTest();
        host.Subscribe("Start", [], (_, _) => throw new ReportedRuntimeException(original));
        Assert.IsTrue(host.Receive(Create("Start")));

        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.RuntimeError, result.State);
        Assert.HasCount(1, diagnostics);
        Assert.AreSame(diagnostics[0], result.Diagnostic);
        Assert.AreEqual(unchanged, original);
    }

    [TestMethod]
    public void VmBeginFailureReportsHostContextAndResetsTheVm()
    {
        var diagnostics = new List<GameEventScriptDiagnostic>();
        var host = GameEventScriptHost.CreateBuilder().WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(runtimeError: diagnostics.Add)).Build().StartForTest();
        host.Load(GameEventScriptBuilder.Create().AddScript("module callbacks\non Start {}").Compile());
        host.RunToCompletion();
        // Force the internal Begin guard before the VM has its own handler context.
        host.VmState!.State = GesVmState.StateValue.Processing;
        Assert.IsTrue(host.Receive(Create("Start")));

        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.RuntimeError, result.State);
        Assert.HasCount(1, diagnostics);
        Assert.AreSame(diagnostics[0], result.Diagnostic);
        Assert.AreEqual(GameEventScriptDiagnosticCodes.RuntimeVmStateConflict, diagnostics[0].Code);
        Assert.AreEqual("callbacks", diagnostics[0].ProgramName);
        Assert.AreEqual("Start()", diagnostics[0].HandlerName);
        Assert.AreEqual(GesVmState.StateValue.Ready, host.VmState.State);
        Assert.IsTrue(host.Receive(Create("Start")));
        Assert.AreEqual(GameEventScriptExecutionState.Completed, host.RunToCompletion().State);
        Assert.HasCount(1, diagnostics);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("reported.program", "Reported()")]
    public void CallbackDiagnosticTransportPreservesIdentityAndOriginalObject(string? programName, string? handlerName)
    {
        var original = new GameEventScriptDiagnostic(GameEventScriptDiagnosticPhase.Runtime, "faulty.reported", "Reported failure.", Symbol: "origin", ProgramName: programName, HandlerName: handlerName, TechnicalDetails: "Original details.");
        var unchanged = original with { };
        var definition = FaultyDefinition();
        var constructor = new DelegateExternalTypeConstructor(definition.Constructors[0], _ => throw new ReportedRuntimeException(original));
        var host = BuildHost(definition, constructor, "module callbacks\non Start { let sample be :Faulty(); emit Done(sample: sample) }", out var diagnostics);

        Assert.IsTrue(host.Receive(Create("Start")));
        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.RuntimeError, result.State);
        Assert.HasCount(1, diagnostics);
        Assert.AreSame(diagnostics[0], result.Diagnostic);
        Assert.AreEqual(unchanged, original);
    }

    [TestMethod]
    public void ExtensionFaultExceptionRendersItsInnerExceptionIntoTechnicalDetailsOnly()
    {
        var inner = new InvalidOperationException("Root cause.");
        var fault = new GameEventScriptExtensionFaultException("faulty.wrapped", "Public message.", innerException: inner);

        Assert.AreEqual("faulty.wrapped", fault.Diagnostic.Code);
        Assert.AreEqual("Public message.", fault.Diagnostic.Message);
        Assert.AreSame(inner, fault.InnerException);
        Assert.Contains("Root cause.", fault.Diagnostic.TechnicalDetails!);
    }

    private static GameEventScriptExternalTypeDefinition FaultyDefinition()
        => new(
            "Faulty",
            [new GameEventScriptExternalTypeFieldDefinition("value", GameEventScriptBytecodeTypeKind.Float)],
            [new GameEventScriptExternalTypeConstructorDefinition("Faulty", [])]);

    private static GameEventScriptHost BuildHost(
        GameEventScriptExternalTypeDefinition definition,
        IGameEventScriptExternalTypeConstructor constructor,
        string script,
        out List<GameEventScriptDiagnostic> diagnostics)
    {
        var catalog = new GameEventScriptExternalTypeCatalog([definition]);
        var bytecode = GameEventScriptBuilder.Create()
            .WithExternalTypeCatalog(catalog)
            .AddScript(script)
            .Compile();

        var capturedDiagnostics = new List<GameEventScriptDiagnostic>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypeRegistry(new SingleConstructorRegistry(constructor))
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(runtimeError: capturedDiagnostics.Add))
            .Build().StartForTest();
        host.Load(bytecode);
        // The failing statement halts the handler before `emit Done(...)` runs, so
        // no subscription is needed; only the resulting diagnostic is observed.
        diagnostics = capturedDiagnostics;
        return host;
    }

    private sealed class DelegateExternalTypeConstructor(GameEventScriptExternalTypeConstructorDefinition definition, Action<GesExternalTypeConstructorCall> invoke)
        : IGameEventScriptExternalTypeConstructor
    {
        public GameEventScriptExternalTypeConstructorDefinition Definition { get; } = definition;

        public void Invoke(GesExternalTypeConstructorCall call) => invoke(call);
    }

    private sealed class DelegateExternalValue(GameEventScriptExternalTypeDefinition definition, Func<string, GesValue?> getField)
        : IGameEventScriptExternalValue
    {
        public GameEventScriptExternalTypeDefinition Definition { get; } = definition;

        public GesValue? GetField(string fieldName) => getField(fieldName);
    }

    private sealed class SingleConstructorRegistry(IGameEventScriptExternalTypeConstructor constructor) : IGameEventScriptExternalTypeRegistry
    {
        public IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference)
            => string.Equals(reference.SignatureId, constructor.Definition.SignatureId, StringComparison.Ordinal) ? constructor : null;
    }

    private sealed class ReportedRuntimeException(GameEventScriptDiagnostic diagnostic) : GameEventScriptFatalRuntimeException(diagnostic);
}
