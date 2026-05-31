using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed class GesBytecodeVmExecutable : IGameEventScriptModule
{
    private readonly IReadOnlyList<GameEventScriptMessageHandlerDescriptor> _messageHandlers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> _dispatchIndex;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> _messageEnvelopeDispatchIndex;
    private IGameEventScriptExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private IReadOnlyDictionary<string, IGameEventScriptExtensionFunction> _boundExtensions = new Dictionary<string, IGameEventScriptExtensionFunction>(StringComparer.Ordinal);
    private IGameEventScriptExtensionFunction[] _boundExtensionSlots = [];
    private IGameEventScriptExternalTypeRegistry _externalTypeRegistry = GameEventScriptEmptyExternalTypeRegistry.Instance;
    private IReadOnlyDictionary<string, IGameEventScriptExternalTypeConstructor> _boundExternalTypeConstructors =
        new Dictionary<string, IGameEventScriptExternalTypeConstructor>(StringComparer.Ordinal);
    private readonly object _linearEntrySupportCacheLock = new();
    private readonly Dictionary<int, bool> _linearEntrySupportCache = [];

    internal GesBytecodeVmExecutable(
        GameEventScriptCompileOptions options,
        GameEventScriptCompiled bytecodeModule,
        GesBytecodeVmLinearExecutable linearExecutable,
        IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> handlers,
        IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> typeDefinitions)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        BytecodeModule = bytecodeModule ?? throw new ArgumentNullException(nameof(bytecodeModule));
        LinearExecutable = linearExecutable ?? throw new ArgumentNullException(nameof(linearExecutable));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));

        CompiledHandlers = Handlers.Values.SelectMany(handlerGroup => handlerGroup).ToArray();
        _dispatchIndex = GesInvocationKernel.BuildDispatchIndex(
            CompiledHandlers.Where(handler => handler.DispatchKind == GameEventScriptBytecodeHandlerDispatchKind.ExactSignature),
            handler => handler.SignatureId,
            handler => handler.DeclarationOrder);
        _messageEnvelopeDispatchIndex = GesInvocationKernel.BuildDispatchIndex(
            CompiledHandlers.Where(handler => handler.DispatchKind == GameEventScriptBytecodeHandlerDispatchKind.MessageEnvelope),
            handler => handler.Message,
            handler => handler.DeclarationOrder);
        _messageHandlers = CompiledHandlers
            .Select(handler => new GameEventScriptMessageHandlerDescriptor(
                handler.Definition,
                (message, context) =>
                {
                    var dispatchMessage = handler.DispatchKind == GameEventScriptBytecodeHandlerDispatchKind.MessageEnvelope &&
                                          !GameEventScriptSystemEndpoints.IsUndeliverableName(handler.Message)
                        ? GameEventScriptSystemEndpoints.CreateEnvelopeDispatchMessage(message)
                        : message;
                    return GesBytecodeVmExecutionSession.CreateFiber(this, context, handler, dispatchMessage.Arguments);
                },
                handler.RequiredTags,
                handler.ExcludedTags,
                matchArguments: handler.DispatchKind != GameEventScriptBytecodeHandlerDispatchKind.MessageEnvelope))
            .ToArray();
    }

    public GameEventScriptCompileOptions Options { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    public string ModuleName => BytecodeModule.ModuleName;

    internal IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> Handlers { get; }

    internal IReadOnlyList<GesBytecodeVmCompiledHandler> CompiledHandlers { get; }

    internal GameEventScriptCompiled BytecodeModule { get; }

    internal GesBytecodeVmLinearExecutable LinearExecutable { get; }

    internal IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> DispatchIndex => _dispatchIndex;

    internal IReadOnlyDictionary<string, IReadOnlyList<GesBytecodeVmCompiledHandler>> MessageEnvelopeDispatchIndex => _messageEnvelopeDispatchIndex;

    internal IGameEventScriptExtensionRegistry ExtensionRegistry => _extensionRegistry;

    internal IGameEventScriptExternalTypeRegistry ExternalTypeRegistry => _externalTypeRegistry;

    IEnumerable<GameEventScriptMessageHandlerDescriptor> IGameEventScriptModule.Handlers
        => _messageHandlers;

    public void Invoke(GameEventScriptMessage message, GameEventScriptSession context)
        => GesBytecodeVmInvocationEngine.InvokeMessage(this, context, message);

    internal void InvokeHandler(GesBytecodeVmCompiledHandler handler, GameEventScriptMessage message, GameEventScriptSession context)
        => GesBytecodeVmInvocationEngine.InvokeHandler(this, context, handler, message);

    public void Bind(IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        BindExtensions(extensionRegistry);
        BindExternalTypes(typeRegistry);
    }
    
    internal void BindExtensions(IGameEventScriptExtensionRegistry registry)
    {
        _extensionRegistry = registry;
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

    internal void BindExternalTypes(IGameEventScriptExternalTypeRegistry registry)
    {
        _externalTypeRegistry = registry ?? GameEventScriptEmptyExternalTypeRegistry.Instance;
        if (BytecodeModule.ExternalTypeConstructorReferences.Count == 0)
        {
            _boundExternalTypeConstructors = new Dictionary<string, IGameEventScriptExternalTypeConstructor>(StringComparer.Ordinal);
            return;
        }

        if (ReferenceEquals(_externalTypeRegistry, GameEventScriptEmptyExternalTypeRegistry.Instance))
        {
            var reference = BytecodeModule.ExternalTypeConstructorReferences[0];
            throw new GameEventScriptDynamicLinkException($"GameEventScript external type registry is required to bind ':{reference.SignatureId}'.");
        }

        var bound = new Dictionary<string, IGameEventScriptExternalTypeConstructor>(StringComparer.Ordinal);
        foreach (var reference in BytecodeModule.ExternalTypeConstructorReferences)
        {
            if (!_externalTypeRegistry.TryResolve(reference, out var constructor))
            {
                throw new GameEventScriptDynamicLinkException($"GameEventScript external type constructor ':{reference.SignatureId}' is not registered in the configured registry.");
            }

            bound[reference.SignatureId] = constructor;
        }

        _boundExternalTypeConstructors = bound;
    }

    internal bool TryGetBoundExternalTypeConstructor(int referenceIndex, out GameEventScriptExternalTypeConstructorReference reference, out IGameEventScriptExternalTypeConstructor constructor)
    {
        if ((uint)referenceIndex < (uint)BytecodeModule.ExternalTypeConstructorReferences.Count)
        {
            reference = BytecodeModule.ExternalTypeConstructorReferences[referenceIndex];
            return _boundExternalTypeConstructors.TryGetValue(reference.SignatureId, out constructor!);
        }

        reference = default!;
        constructor = default!;
        return false;
    }

    internal bool TryGetLinearEntrySupport(int entryAddress, out bool supported)
    {
        lock (_linearEntrySupportCacheLock)
        {
            return _linearEntrySupportCache.TryGetValue(entryAddress, out supported);
        }
    }

    internal void SetLinearEntrySupport(int entryAddress, bool supported)
    {
        lock (_linearEntrySupportCacheLock)
        {
            _linearEntrySupportCache[entryAddress] = supported;
        }
    }
}
