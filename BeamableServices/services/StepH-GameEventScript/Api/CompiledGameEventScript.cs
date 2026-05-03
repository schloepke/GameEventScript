#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

public sealed class CompiledGameEventScript : IGameEventScriptMessageHandlerCollection
{
    private readonly IReadOnlyList<(GameEventScriptMessageSignature Signature, Action<GameEventScriptMessage, GameEventScriptContext> Handler)> _messageHandlers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CompiledGameEventScriptHandler>> _dispatchIndex;
    private IGameEventScriptExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private IReadOnlyDictionary<string, IGameEventScriptExtensionFunction> _boundExtensions = new Dictionary<string, IGameEventScriptExtensionFunction>(StringComparer.Ordinal);
    private IGameEventScriptExtensionFunction[] _boundExtensionSlots = [];

    internal CompiledGameEventScript(
        GameEventScriptCompilationOptions options,
        GameEventScriptBytecode bytecodeModule,
        IReadOnlyDictionary<string, IReadOnlyList<CompiledGameEventScriptHandler>> handlers,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        IReadOnlyDictionary<string, BytecodeVmTypeDefinition> typeDefinitions)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        BytecodeModule = bytecodeModule ?? throw new ArgumentNullException(nameof(bytecodeModule));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));

        var handlerList = Handlers.Values.SelectMany(handlerGroup => handlerGroup).ToArray();
        _dispatchIndex = GameEventScriptInvocationKernel.BuildDispatchIndex(handlerList, handler => handler.SignatureId, handler => handler.DeclarationOrder);
        _messageHandlers = handlerList
            .Select(handler => (handler.Definition, (Action<GameEventScriptMessage, GameEventScriptContext>)((message, context) => InvokeHandler(handler, message, context))))
            .ToArray();
    }

    public GameEventScriptCompilationOptions Options { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    internal IReadOnlyDictionary<string, IReadOnlyList<CompiledGameEventScriptHandler>> Handlers { get; }

    internal GameEventScriptBytecode BytecodeModule { get; }

    internal IReadOnlyDictionary<string, GameEventScriptCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, BytecodeVmTypeDefinition> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<CompiledGameEventScriptHandler>> DispatchIndex => _dispatchIndex;

    internal IGameEventScriptExtensionRegistry ExtensionRegistry => _extensionRegistry;

    IEnumerable<(GameEventScriptMessageSignature Signature, Action<GameEventScriptMessage, GameEventScriptContext> Handler)> IGameEventScriptMessageHandlerCollection.Handlers
        => _messageHandlers;

    public void Invoke(GameEventScriptMessage message, GameEventScriptContext context)
        => BytecodeVmInvocationEngine.InvokeMessage(this, context, message);

    internal void InvokeHandler(CompiledGameEventScriptHandler handler, GameEventScriptMessage message, GameEventScriptContext context)
        => BytecodeVmInvocationEngine.InvokeHandler(this, context, handler, message);

    internal void BindExtensions(IGameEventScriptExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        if (BytecodeModule.ExternalReferences.Count == 0)
        {
            _boundExtensions = new Dictionary<string, IGameEventScriptExtensionFunction>(StringComparer.Ordinal);
            _boundExtensionSlots = [];
            return;
        }

        var bound = new Dictionary<string, IGameEventScriptExtensionFunction>(StringComparer.Ordinal);
        var slots = new IGameEventScriptExtensionFunction[BytecodeModule.ExternalReferences.Count];
        for (var index = 0; index < BytecodeModule.ExternalReferences.Count; index++)
        {
            var reference = BytecodeModule.ExternalReferences[index];
            if (ReferenceEquals(_extensionRegistry, GameEventScriptEmptyExtensionRegistry.Instance))
            {
                throw new GameEventScriptDynamicLinkException($"GameEventScript extension registry is required to bind '{reference.SignatureId}'.");
            }

            if (!_extensionRegistry.TryResolve(reference, out var function))
            {
                throw new GameEventScriptDynamicLinkException($"GameEventScript extension '{reference.SignatureId}' is not registered in the configured registry.");
            }

            bound[reference.SignatureId] = function;
            slots[index] = function;
        }

        _boundExtensions = bound;
        _boundExtensionSlots = slots;
    }

    internal bool TryGetBoundExtension(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
        => _boundExtensions.TryGetValue(reference.SignatureId, out function!);

    internal bool TryGetBoundExtension(int referenceIndex, out IGameEventScriptExtensionFunction function)
    {
        if ((uint)referenceIndex < (uint)_boundExtensionSlots.Length)
        {
            function = _boundExtensionSlots[referenceIndex];
            return true;
        }

        function = default!;
        return false;
    }
}

internal sealed class BytecodeVmTypeDefinition
{
    public BytecodeVmTypeDefinition(TypeDefinitionNode syntax)
    {
        _ = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Name = syntax.Name;
        Fields = syntax.Fields.Select(field => new BytecodeVmTypeFieldDefinition(field)).ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<BytecodeVmTypeFieldDefinition> Fields { get; }
}

internal sealed class BytecodeVmTypeFieldDefinition
{
    public BytecodeVmTypeFieldDefinition(TypeFieldDefinitionNode syntax)
    {
        _ = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Name = syntax.Name;
        TypeName = syntax.TypeName;
        MinimumExpression = syntax.MinimumExpression;
        MaximumExpression = syntax.MaximumExpression;
        ComputedExpression = syntax.ComputedExpression;
    }

    public string Name { get; }

    public string TypeName { get; }

    public ExpressionNode? MinimumExpression { get; }

    public ExpressionNode? MaximumExpression { get; }

    public ExpressionNode? ComputedExpression { get; }
}
