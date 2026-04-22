#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.Flow.EventScript.Experimental;

public sealed class ExperimentalCompiledEventScriptHandler(string message, IReadOnlyList<string> parameters, string signatureId, int declarationOrder, int programIndex, bool diagnosticsEnabled)
{
    public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

    public IReadOnlyList<string> Parameters { get; } = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public int DeclarationOrder { get; } = declarationOrder;

    public EventScriptMessageSignature Definition { get; } = new(message, parameters);

    internal int ProgramIndex { get; } = programIndex;

    internal bool DiagnosticsEnabled { get; } = diagnosticsEnabled;
}
