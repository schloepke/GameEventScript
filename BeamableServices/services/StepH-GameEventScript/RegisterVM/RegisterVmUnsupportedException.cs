#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.RegisterVM;

public sealed class RegisterVmUnsupportedException : EventScriptFatalRuntimeException
{
    internal RegisterVmUnsupportedException(
        string messageName,
        string handlerSignatureId,
        int declarationOrder,
        string reason)
        : base(BuildMessage(messageName, handlerSignatureId, declarationOrder, reason))
    {
        MessageName = messageName;
        HandlerSignatureId = handlerSignatureId;
        DeclarationOrder = declarationOrder;
        Reason = reason;
    }

    public string MessageName { get; }

    public string HandlerSignatureId { get; }

    public int DeclarationOrder { get; }

    public string Reason { get; }

    private static string BuildMessage(
        string messageName,
        string handlerSignatureId,
        int declarationOrder,
        string reason)
        => $"RegisterVM cannot execute message '{messageName}', handler '{handlerSignatureId}' #{declarationOrder}: {reason}";
}
