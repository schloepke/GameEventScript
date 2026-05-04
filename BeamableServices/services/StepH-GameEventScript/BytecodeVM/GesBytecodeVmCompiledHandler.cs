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
        Parameters = handler.Parameters.ToArray();
        SignatureLabels = handler.SignatureLabels.ToArray();
        ParameterTypes = handler.ParameterTypes.ToArray();
        SignatureId = handler.SignatureId;
        DeclarationOrder = handler.DeclarationOrder;
        DiagnosticsEnabled = diagnosticsEnabled;
        ExecutionPlan = handler.ExecutionPlan;
        Definition = handler.Definition;
    }

    public string Message { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public string SignatureId { get; }

    public int DeclarationOrder { get; }

    public GameEventScriptMessageSignature Definition { get; }

    internal bool DiagnosticsEnabled { get; }

    internal GameEventScriptBytecodeExecutionPlan ExecutionPlan { get; }
}
