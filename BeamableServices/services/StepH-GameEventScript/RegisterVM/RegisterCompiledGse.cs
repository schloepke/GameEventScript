#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.RegisterVM;

public sealed class RegisterCompiledGse : IGseMessageHandlerCollection
{
    private readonly IReadOnlyList<(GseMessageSignature Signature, Action<GseMessage, GseContext> Handler)> _messageHandlers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledGseHandler>> _dispatchIndex;
    private IGseExtensionRegistry _extensionRegistry = GseEmptyExtensionRegistry.Instance;
    private IReadOnlyDictionary<string, IGseExtensionFunction> _boundExtensions = new Dictionary<string, IGseExtensionFunction>(StringComparer.Ordinal);
    private IGseExtensionFunction[] _boundExtensionSlots = [];

    internal RegisterCompiledGse(
        RegisterVmCompilationOptions options,
        RegisterBytecodeModule bytecodeModule,
        IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledGseHandler>> handlers,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, RegisterVmTypeDefinition> typeDefinitions)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        BytecodeModule = bytecodeModule ?? throw new ArgumentNullException(nameof(bytecodeModule));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));

        var handlerList = Handlers.Values.SelectMany(handlerGroup => handlerGroup).ToArray();
        _dispatchIndex = GseInvocationKernel.BuildDispatchIndex(handlerList, handler => handler.SignatureId, handler => handler.DeclarationOrder);
        _messageHandlers = handlerList
            .Select(handler => (handler.Definition, (Action<GseMessage, GseContext>)((message, context) => InvokeHandler(handler, message, context))))
            .ToArray();
    }

    public RegisterVmCompilationOptions Options { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    public IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledGseHandler>> Handlers { get; }

    internal RegisterBytecodeModule BytecodeModule { get; }

    internal IReadOnlyDictionary<string, GseCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, RegisterVmTypeDefinition> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledGseHandler>> DispatchIndex => _dispatchIndex;

    internal IGseExtensionRegistry ExtensionRegistry => _extensionRegistry;

    IEnumerable<(GseMessageSignature Signature, Action<GseMessage, GseContext> Handler)> IGseMessageHandlerCollection.Handlers
        => _messageHandlers;

    public void Invoke(GseMessage message, GseContext context)
        => RegisterVmInvocationEngine.InvokeMessage(this, context, message);

    internal void InvokeHandler(RegisterCompiledGseHandler handler, GseMessage message, GseContext context)
        => RegisterVmInvocationEngine.InvokeHandler(this, context, handler, message);

    internal void BindExtensions(IGseExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? GseEmptyExtensionRegistry.Instance;
        if (BytecodeModule.ExternalReferences.Count == 0)
        {
            _boundExtensions = new Dictionary<string, IGseExtensionFunction>(StringComparer.Ordinal);
            _boundExtensionSlots = [];
            return;
        }

        var bound = new Dictionary<string, IGseExtensionFunction>(StringComparer.Ordinal);
        var slots = new IGseExtensionFunction[BytecodeModule.ExternalReferences.Count];
        for (var index = 0; index < BytecodeModule.ExternalReferences.Count; index++)
        {
            var reference = BytecodeModule.ExternalReferences[index];
            if (ReferenceEquals(_extensionRegistry, GseEmptyExtensionRegistry.Instance))
            {
                throw new GseDynamicLinkException($"Gse extension registry is required to bind '{reference.SignatureId}'.");
            }

            if (!_extensionRegistry.TryResolve(reference, out var function))
            {
                throw new GseDynamicLinkException($"Gse extension '{reference.SignatureId}' is not registered in the configured registry.");
            }

            bound[reference.SignatureId] = function;
            slots[index] = function;
        }

        _boundExtensions = bound;
        _boundExtensionSlots = slots;
    }

    internal bool TryGetBoundExtension(GseExtensionReference reference, out IGseExtensionFunction function)
        => _boundExtensions.TryGetValue(reference.SignatureId, out function!);

    internal bool TryGetBoundExtension(int referenceIndex, out IGseExtensionFunction function)
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
