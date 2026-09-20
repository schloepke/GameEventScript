// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Tests.Native.Runtime;

namespace GameEventScript.Tests.Native.CSharpBridge;

/// <summary>
/// Verifies that Reflection adapters preserve deliberate faults and the original
/// cause of unexpected constructor, factory, and property-getter failures.
/// </summary>
[TestClass]
public sealed class GameEventScriptExternalTypeCallbackFailureTests
{
    /// <summary>
    /// Runs each Reflection callback boundary with a declared and unexpected fault.
    /// </summary>
    /// <param name="clrType">The attributed external type.</param>
    /// <param name="declared">Whether the callback deliberately reports a fault.</param>
    /// <param name="readsField">Whether the failure occurs in a field getter.</param>
    [TestMethod]
    [DataRow(typeof(ThrowingConstructor), true, false)]
    [DataRow(typeof(ThrowingConstructor), false, false)]
    [DataRow(typeof(ThrowingFactory), true, false)]
    [DataRow(typeof(ThrowingFactory), false, false)]
    [DataRow(typeof(ThrowingGetter), true, true)]
    [DataRow(typeof(ThrowingGetter), false, true)]
    public void ReflectionCallbackPreservesFaultIdentity(Type clrType, bool declared, bool readsField)
    {
        var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(clrType);
        var valueExpression = readsField ? "sample.value" : "sample";
        var script = $"on Start {{ let sample be :{clrType.Name}(declared: {(declared ? "true" : "false")}); emit MustNotRun(value: {valueExpression}) }}\non Check {{ emit Done }}";
        var program = GameEventScriptBuilder.Create().WithExternalTypeCatalog(registry).AddScript(script).Compile();
        var diagnostics = new List<GameEventScriptDiagnostic>();
        var emitted = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithExternalTypeRegistry(registry)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveMessages(messageEmitted: emitted.Add, runtimeError: diagnostics.Add))
            .Build().StartForTest();
        host.Load(program);
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Start")));

        var failed = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.RuntimeError, failed.State);
        Assert.HasCount(1, diagnostics);
        Assert.AreSame(diagnostics[0], failed.Diagnostic);
        Assert.IsEmpty(emitted);
        var diagnostic = diagnostics[0];
        Assert.AreEqual(GameEventScriptDiagnosticPhase.Runtime, diagnostic.Phase);
        Assert.AreEqual(program.ModuleName, diagnostic.ProgramName);
        Assert.AreEqual("Start()", diagnostic.HandlerName);
        if (declared)
        {
            Assert.AreEqual("test.reflectionFault", diagnostic.Code);
            Assert.AreEqual("Declared callback failure.", diagnostic.Message);
            Assert.AreEqual("originalSymbol", diagnostic.Symbol);
            Assert.Contains("Original inner cause.", diagnostic.TechnicalDetails!);
        }
        else
        {
            Assert.AreEqual(readsField ? GameEventScriptDiagnosticCodes.RuntimeExternalFieldAccessFailed : GameEventScriptDiagnosticCodes.RuntimeExternalConstructorFailed, diagnostic.Code);
            Assert.AreEqual(readsField ? clrType.Name + ".value" : clrType.Name, diagnostic.Symbol);
            Assert.Contains("Unexpected callback failure.", diagnostic.TechnicalDetails!);
            Assert.Contains(nameof(ThrowCallbackFailure), diagnostic.TechnicalDetails!);
        }
        Assert.Contains(nameof(InvalidOperationException), diagnostic.TechnicalDetails!);
        Assert.DoesNotContain(nameof(System.Reflection.TargetInvocationException), diagnostic.TechnicalDetails!);

        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Check")));
        Assert.AreEqual(GameEventScriptExecutionState.Completed, host.RunToCompletion().State);
        Assert.HasCount(1, diagnostics);
        Assert.HasCount(1, emitted);
        Assert.AreEqual("Done", emitted[0].Name);
    }

    /// <summary>
    /// Keeps a wrapper originating inside application code classified as an unexpected failure.
    /// </summary>
    [TestMethod]
    public void ApplicationReflectionWrapperRemainsAnUnexpectedFailure()
    {
        var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(NestedReflectionConstructor));
        var program = GameEventScriptBuilder.Create().WithExternalTypeCatalog(registry)
            .AddScript("module callbacks\non Start { let sample be :NestedReflectionConstructor(); emit MustNotRun(value: sample) }").Compile();
        var host = GameEventScriptHost.CreateBuilder().WithExternalTypeRegistry(registry).Build().StartForTest();
        host.Load(program);
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Start")));

        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.RuntimeError, result.State);
        Assert.AreEqual(0, result.EmittedMessages);
        Assert.AreEqual(GameEventScriptDiagnosticCodes.RuntimeExternalConstructorFailed, result.Diagnostic!.Code);
        Assert.AreEqual(nameof(NestedReflectionConstructor), result.Diagnostic.Symbol);
        Assert.Contains(nameof(System.Reflection.TargetInvocationException), result.Diagnostic.TechnicalDetails!);
        Assert.Contains(nameof(GameEventScriptExtensionFaultException), result.Diagnostic.TechnicalDetails!);
        Assert.Contains("Declared callback failure.", result.Diagnostic.TechnicalDetails!);
    }

    [GesType(nameof(NestedReflectionConstructor))]
    private sealed class NestedReflectionConstructor
    {
        [GesConstruct]
        public NestedReflectionConstructor() => typeof(NestedReflectionConstructor).GetMethod(nameof(Fail))!.Invoke(null, null);

        public static void Fail() => ThrowCallbackFailure(true);
    }

    private static void ThrowCallbackFailure(bool declared)
    {
        if (declared)
            throw new GameEventScriptExtensionFaultException("test.reflectionFault", "Declared callback failure.", "originalSymbol", new InvalidOperationException("Original inner cause."));
        throw new InvalidOperationException("Unexpected callback failure.");
    }

    [GesType(nameof(ThrowingConstructor))]
    private sealed class ThrowingConstructor
    {
        [GesConstruct]
        public ThrowingConstructor([GesParam("declared", GameEventScriptBytecodeTypeKind.Boolean)] bool declared) => ThrowCallbackFailure(declared);

        [GesField("declared", GameEventScriptBytecodeTypeKind.Boolean)]
        public bool Declared => false;
    }

    [GesType(nameof(ThrowingFactory))]
    private sealed class ThrowingFactory
    {
        private ThrowingFactory() { }

        [GesConstruct]
        public static ThrowingFactory Create([GesParam("declared", GameEventScriptBytecodeTypeKind.Boolean)] bool declared)
        {
            ThrowCallbackFailure(declared);
            return new ThrowingFactory();
        }

        [GesField("declared", GameEventScriptBytecodeTypeKind.Boolean)]
        public bool Declared => false;
    }

    [GesType(nameof(ThrowingGetter))]
    private sealed class ThrowingGetter
    {
        [GesConstruct]
        public ThrowingGetter([GesParam("declared", GameEventScriptBytecodeTypeKind.Boolean)] bool declared) => Declared = declared;

        [GesField("declared", GameEventScriptBytecodeTypeKind.Boolean)]
        public bool Declared { get; }

        [GesField("value", GameEventScriptBytecodeTypeKind.Float)]
        public double Value
        {
            get
            {
                ThrowCallbackFailure(Declared);
                return 0;
            }
        }
    }
}
