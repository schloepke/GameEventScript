#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Linker;
using StepH.GameEventScript.Parser;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.RegisterVM;

public sealed class RegisterCompiledEventScript : IEventScriptMessageHandlerCollection
{
    private readonly IReadOnlyList<(EventScriptMessageSignature Signature, Action<EventScriptMessage, EventScriptContext> Handler)> _messageHandlers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> _dispatchIndex;
    private IEventScriptExtensionRegistry _extensionRegistry = EventScriptEmptyExtensionRegistry.Instance;
    private IReadOnlyDictionary<string, IEventScriptExtensionFunction> _boundExtensions = new Dictionary<string, IEventScriptExtensionFunction>(StringComparer.Ordinal);
    private IEventScriptExtensionFunction[] _boundExtensionSlots = [];

    internal RegisterCompiledEventScript(
        RegisterEventScriptCompilationOptions options,
        RegisterBytecodeModule bytecodeModule,
        IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> handlers,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        IReadOnlyDictionary<string, RegisterVmTypeDefinition> typeDefinitions)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        BytecodeModule = bytecodeModule ?? throw new ArgumentNullException(nameof(bytecodeModule));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));

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

    internal IReadOnlyDictionary<string, RegisterVmTypeDefinition> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> DispatchIndex => _dispatchIndex;

    internal IEventScriptExtensionRegistry ExtensionRegistry => _extensionRegistry;

    IEnumerable<(EventScriptMessageSignature Signature, Action<EventScriptMessage, EventScriptContext> Handler)> IEventScriptMessageHandlerCollection.Handlers
        => _messageHandlers;

    public void Invoke(EventScriptMessage message, EventScriptContext context)
        => RegisterVmInvocationEngine.InvokeMessage(this, context, message);

    internal void InvokeHandler(RegisterCompiledEventScriptHandler handler, EventScriptMessage message, EventScriptContext context)
        => RegisterVmInvocationEngine.InvokeHandler(this, context, handler, message);

    internal void BindExtensions(IEventScriptExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? EventScriptEmptyExtensionRegistry.Instance;
        if (BytecodeModule.ExternalReferences.Count == 0)
        {
            _boundExtensions = new Dictionary<string, IEventScriptExtensionFunction>(StringComparer.Ordinal);
            _boundExtensionSlots = [];
            return;
        }

        var bound = new Dictionary<string, IEventScriptExtensionFunction>(StringComparer.Ordinal);
        var slots = new IEventScriptExtensionFunction[BytecodeModule.ExternalReferences.Count];
        for (var index = 0; index < BytecodeModule.ExternalReferences.Count; index++)
        {
            var reference = BytecodeModule.ExternalReferences[index];
            if (ReferenceEquals(_extensionRegistry, EventScriptEmptyExtensionRegistry.Instance))
            {
                throw new EventScriptDynamicLinkException($"EventScript extension registry is required to bind '{reference.SignatureId}'.");
            }

            if (!_extensionRegistry.TryResolve(reference, out var function))
            {
                throw new EventScriptDynamicLinkException($"EventScript extension '{reference.SignatureId}' is not registered in the configured registry.");
            }

            bound[reference.SignatureId] = function;
            slots[index] = function;
        }

        _boundExtensions = bound;
        _boundExtensionSlots = slots;
    }

    internal bool TryGetBoundExtension(EventScriptExtensionReference reference, out IEventScriptExtensionFunction function)
        => _boundExtensions.TryGetValue(reference.SignatureId, out function!);

    internal bool TryGetBoundExtension(int referenceIndex, out IEventScriptExtensionFunction function)
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

internal sealed class RegisterVmTypeDefinition
{
    public RegisterVmTypeDefinition(TypeDefinitionNode syntax)
    {
        _ = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Name = syntax.Name;
        Fields = syntax.Fields.Select(field => new RegisterVmTypeFieldDefinition(field)).ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<RegisterVmTypeFieldDefinition> Fields { get; }
}

internal sealed class RegisterVmTypeFieldDefinition
{
    public RegisterVmTypeFieldDefinition(TypeFieldDefinitionNode syntax)
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
