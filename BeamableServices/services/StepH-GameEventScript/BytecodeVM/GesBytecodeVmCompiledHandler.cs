using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed class GesBytecodeVmCompiledHandler
{
    internal GesBytecodeVmCompiledHandler(GameEventScriptBytecodeHandler handler, bool diagnosticsEnabled)
    {
        _ = handler ?? throw new ArgumentNullException(nameof(handler));
        Message = handler.Message;
        DispatchKind = handler.DispatchKind;
        Parameters = handler.Parameters.ToArray();
        SignatureLabels = handler.SignatureLabels.ToArray();
        ParameterTypes = handler.ParameterTypes.ToArray();
        RequiredTags = handler.RequiredTags.ToArray();
        ExcludedTags = handler.ExcludedTags.ToArray();
        SignatureId = handler.SignatureId;
        DeclarationOrder = handler.DeclarationOrder;
        EntryAddress = handler.EntryAddress;
        LocalSlotCount = handler.LocalSlotCount;
        Slots = handler.Slots.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        DiagnosticsEnabled = diagnosticsEnabled;
        ExecutionPlan = handler.ExecutionPlan;
        Definition = handler.Definition;
    }

    public string Message { get; }

    public GameEventScriptBytecodeHandlerDispatchKind DispatchKind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public IReadOnlyList<string> RequiredTags { get; }

    public IReadOnlyList<string> ExcludedTags { get; }

    public string SignatureId { get; }

    public int DeclarationOrder { get; }

    public int EntryAddress { get; }

    public int LocalSlotCount { get; }

    public IReadOnlyDictionary<string, int> Slots { get; }

    public GameEventScriptMessageSignature Definition { get; }

    internal bool DiagnosticsEnabled { get; }

    internal GameEventScriptBytecodeExecutionPlan ExecutionPlan { get; }
}
