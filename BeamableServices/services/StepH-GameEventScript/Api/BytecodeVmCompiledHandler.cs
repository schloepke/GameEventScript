#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.Api;

internal sealed class BytecodeVmCompiledHandler
{
    internal BytecodeVmCompiledHandler(
        string message,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> signatureLabels,
        string signatureId,
        int declarationOrder,
        int programIndex,
        bool diagnosticsEnabled,
        BytecodeVmExecutionPlan executionPlan)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Parameters = parameters?.ToArray() ?? throw new ArgumentNullException(nameof(parameters));
        SignatureLabels = signatureLabels?.ToArray() ?? throw new ArgumentNullException(nameof(signatureLabels));
        SignatureId = signatureId ?? throw new ArgumentNullException(nameof(signatureId));
        DeclarationOrder = declarationOrder;
        ProgramIndex = programIndex;
        DiagnosticsEnabled = diagnosticsEnabled;
        ExecutionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
        Definition = GameEventScriptMessageSignature.Create(message, SignatureLabels);
    }

    public string Message { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public string SignatureId { get; }

    public int DeclarationOrder { get; }

    public GameEventScriptMessageSignature Definition { get; }

    internal int ProgramIndex { get; }

    internal bool DiagnosticsEnabled { get; }

    internal BytecodeVmExecutionPlan ExecutionPlan { get; }
}
