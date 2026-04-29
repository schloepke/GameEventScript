#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.Flow.EventScript.Runtime;

namespace StepH.Flow.EventScript.RegisterVM;

internal static class RegisterVmInvocationEngine
{
    public static void InvokeMessage(RegisterCompiledEventScript compiledScript, EventScriptContext context, EventScriptMessage message)
    {
        foreach (var handler in EventScriptInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message)) InvokeHandler(compiledScript, context, handler, message);
    }

    public static void InvokeHandler(RegisterCompiledEventScript compiledScript, EventScriptContext context, RegisterCompiledEventScriptHandler handler, EventScriptMessage message)
    {
        if (handler.SupportsFastPath && RegisterVmFastExecutionSession.TryInvokeHandler(compiledScript, context, handler, message.Arguments)) return;

        throw new RegisterVmUnsupportedException(message.Name, handler.SignatureId, handler.DeclarationOrder, GetUnsupportedReason(handler));
    }

    private static string GetUnsupportedReason(RegisterCompiledEventScriptHandler handler) => !handler.SupportsFastPath
        ? handler.FastPathPlan.UnsupportedReason ?? "Handler is not supported by the RegisterVM fast path."
        : "RegisterVM fast-path execution returned unsupported at runtime.";
}
