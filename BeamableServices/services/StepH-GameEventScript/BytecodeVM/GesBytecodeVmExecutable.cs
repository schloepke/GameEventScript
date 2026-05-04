using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed class GesBytecodeVmExecutable : IGameEventScriptMessageHandlerCollection
{
    private readonly IReadOnlyList<(GameEventScriptMessageSignature Signature, Action<GameEventScriptMessage, GameEventScriptContext> Handler)> _messageHandlers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> _dispatchIndex;
    private IGameEventScriptExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private IReadOnlyDictionary<string, IGameEventScriptExtensionFunction> _boundExtensions = new Dictionary<string, IGameEventScriptExtensionFunction>(StringComparer.Ordinal);
    private IGameEventScriptExtensionFunction[] _boundExtensionSlots = [];

    internal GesBytecodeVmExecutable(
        GameEventScriptCompileOptions options,
        GameEventScriptCompiled bytecodeModule,
        IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> handlers,
        IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> typeDefinitions)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        BytecodeModule = bytecodeModule ?? throw new ArgumentNullException(nameof(bytecodeModule));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Constants = BytecodeModule.ConstantPool.Select(BytecodeVmValue.FromBytecodeConstant).ToArray();

        CompiledHandlers = Handlers.Values.SelectMany(handlerGroup => handlerGroup).ToArray();
        _dispatchIndex = GesInvocationKernel.BuildDispatchIndex(CompiledHandlers, handler => handler.SignatureId, handler => handler.DeclarationOrder);
        _messageHandlers = CompiledHandlers
            .Select(handler => (handler.Definition, (Action<GameEventScriptMessage, GameEventScriptContext>)((message, context) => InvokeHandler(handler, message, context))))
            .ToArray();
    }

    public GameEventScriptCompileOptions Options { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    internal IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> Handlers { get; }

    internal IReadOnlyList<GesBytecodeVmCompiledHandler> CompiledHandlers { get; }

    internal GameEventScriptCompiled BytecodeModule { get; }

    internal IReadOnlyList<BytecodeVmValue> Constants { get; }

    internal IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> DispatchIndex => _dispatchIndex;

    internal IGameEventScriptExtensionRegistry ExtensionRegistry => _extensionRegistry;

    IEnumerable<(GameEventScriptMessageSignature Signature, Action<GameEventScriptMessage, GameEventScriptContext> Handler)> IGameEventScriptMessageHandlerCollection.Handlers
        => _messageHandlers;

    public void Invoke(GameEventScriptMessage message, GameEventScriptContext context)
        => GesBytecodeVmInvocationEngine.InvokeMessage(this, context, message);

    internal void InvokeHandler(GesBytecodeVmCompiledHandler handler, GameEventScriptMessage message, GameEventScriptContext context)
        => GesBytecodeVmInvocationEngine.InvokeHandler(this, context, handler, message);

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
