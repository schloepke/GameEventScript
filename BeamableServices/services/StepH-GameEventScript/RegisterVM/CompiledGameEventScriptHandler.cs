#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Compiler;

namespace StepH.GameEventScript.RegisterVM;

internal sealed class CompiledGameEventScriptHandler
{
    internal CompiledGameEventScriptHandler(
        string message,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> signatureLabels,
        string signatureId,
        int declarationOrder,
        int programIndex,
        bool diagnosticsEnabled,
        IReadOnlyList<StatementNode> statements,
        RegisterVmExecutionPlan executionPlan)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Parameters = parameters?.ToArray() ?? throw new ArgumentNullException(nameof(parameters));
        SignatureLabels = signatureLabels?.ToArray() ?? throw new ArgumentNullException(nameof(signatureLabels));
        SignatureId = signatureId ?? throw new ArgumentNullException(nameof(signatureId));
        DeclarationOrder = declarationOrder;
        ProgramIndex = programIndex;
        DiagnosticsEnabled = diagnosticsEnabled;
        Statements = statements?.ToArray() ?? throw new ArgumentNullException(nameof(statements));
        ExecutionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
        Definition = new GameEventScriptMessageSignature(message, SignatureLabels);
    }

    public string Message { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public string SignatureId { get; }

    public int DeclarationOrder { get; }

    public GameEventScriptMessageSignature Definition { get; }

    internal int ProgramIndex { get; }

    internal bool DiagnosticsEnabled { get; }

    internal IReadOnlyList<StatementNode> Statements { get; }

    internal RegisterVmExecutionPlan ExecutionPlan { get; }
}
