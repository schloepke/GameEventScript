#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Runtime;

namespace StepH.Flow.EventScript.RegisterVM;

public sealed class RegisterCompiledEventScript : IEventScriptMessageHandlerCollection
{
    private readonly IReadOnlyList<(EventScriptMessageSignature Signature, Action<EventScriptMessage, EventScriptContext> Handler)> _messageHandlers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> _dispatchIndex;

    internal RegisterCompiledEventScript(
        RegisterEventScriptCompilationOptions options,
        RegisterBytecodeModule bytecodeModule,
        IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> handlers,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        CompiledEventScript compatibilityRuntime)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        BytecodeModule = bytecodeModule ?? throw new ArgumentNullException(nameof(bytecodeModule));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        CompatibilityRuntime = compatibilityRuntime ?? throw new ArgumentNullException(nameof(compatibilityRuntime));

        var handlerList = Handlers.Values.SelectMany(handlerGroup => handlerGroup).ToArray();
        _dispatchIndex = EventScriptInvocationKernel.BuildDispatchIndex(handlerList, handler => handler.SignatureId, handler => handler.DeclarationOrder);
        _messageHandlers = handlerList
            .Select(handler => (handler.Definition, (Action<EventScriptMessage, EventScriptContext>)((message, context) => InvokeHandler(handler, message, context))))
            .ToArray();
    }

    public RegisterEventScriptCompilationOptions Options { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    public IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> Handlers { get; }

    internal RegisterBytecodeModule BytecodeModule { get; }

    internal IReadOnlyDictionary<string, LinkedCallableDefinition> Callables { get; }

    internal CompiledEventScript CompatibilityRuntime { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> DispatchIndex => _dispatchIndex;

    IEnumerable<(EventScriptMessageSignature Signature, Action<EventScriptMessage, EventScriptContext> Handler)> IEventScriptMessageHandlerCollection.Handlers
        => _messageHandlers;

    public void Invoke(EventScriptMessage message, EventScriptContext context)
        => RegisterVmInvocationEngine.InvokeMessage(this, context, message);

    internal void InvokeHandler(RegisterCompiledEventScriptHandler handler, EventScriptMessage message, EventScriptContext context)
        => RegisterVmInvocationEngine.InvokeHandler(this, context, handler, message);
}
