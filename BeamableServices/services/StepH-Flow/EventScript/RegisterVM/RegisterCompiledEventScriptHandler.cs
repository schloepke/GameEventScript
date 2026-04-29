#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.RegisterVM;

public sealed class RegisterCompiledEventScriptHandler
{
    internal RegisterCompiledEventScriptHandler(
        string message,
        IReadOnlyList<string> parameters,
        string signatureId,
        int declarationOrder,
        int programIndex,
        bool diagnosticsEnabled,
        CompiledEventScriptHandler compatibilityHandler,
        IReadOnlyList<StatementNode> statements,
        RegisterVmFastPathPlan fastPathPlan)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        SignatureId = signatureId ?? throw new ArgumentNullException(nameof(signatureId));
        DeclarationOrder = declarationOrder;
        ProgramIndex = programIndex;
        DiagnosticsEnabled = diagnosticsEnabled;
        CompatibilityHandler = compatibilityHandler ?? throw new ArgumentNullException(nameof(compatibilityHandler));
        Statements = statements ?? throw new ArgumentNullException(nameof(statements));
        FastPathPlan = fastPathPlan ?? throw new ArgumentNullException(nameof(fastPathPlan));
        Definition = new EventScriptMessageSignature(message, parameters);
    }

    public string Message { get; }

    public IReadOnlyList<string> Parameters { get; }

    public string SignatureId { get; }

    public int DeclarationOrder { get; }

    public EventScriptMessageSignature Definition { get; }

    internal int ProgramIndex { get; }

    internal bool DiagnosticsEnabled { get; }

    internal CompiledEventScriptHandler CompatibilityHandler { get; }

    internal IReadOnlyList<StatementNode> Statements { get; }

    internal RegisterVmFastPathPlan FastPathPlan { get; }

    internal bool SupportsFastPath => FastPathPlan.IsSupported;
}
