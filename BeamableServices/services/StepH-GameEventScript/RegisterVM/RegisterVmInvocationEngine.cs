#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.RegisterVM;

internal static class RegisterVmInvocationEngine
{
    public static void InvokeMessage(RegisterCompiledGse compiledScript, GseContext context, GseMessage message)
    {
        foreach (var handler in GseInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message)) InvokeHandler(compiledScript, context, handler, message);
    }

    public static void InvokeHandler(RegisterCompiledGse compiledScript, GseContext context, RegisterCompiledGseHandler handler, GseMessage message)
    {
        if (handler.SupportsFastPath && RegisterVmFastExecutionSession.TryInvokeHandler(compiledScript, context, handler, message.Arguments)) return;

        throw new RegisterVmUnsupportedException(message.Name, handler.SignatureId, handler.DeclarationOrder, GetUnsupportedReason(handler));
    }

    private static string GetUnsupportedReason(RegisterCompiledGseHandler handler) => !handler.SupportsFastPath
        ? handler.FastPathPlan.UnsupportedReason ?? "Handler is not supported by the RegisterVM fast path."
        : "RegisterVM fast-path execution returned unsupported at runtime.";
}
