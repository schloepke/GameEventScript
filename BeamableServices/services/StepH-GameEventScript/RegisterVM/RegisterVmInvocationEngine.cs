#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.RegisterVM;

internal static class RegisterVmInvocationEngine
{
    public static void InvokeMessage(CompiledGameEventScript compiledScript, GameEventScriptContext context, GameEventScriptMessage message)
    {
        foreach (var handler in GameEventScriptInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message)) InvokeHandler(compiledScript, context, handler, message);
    }

    public static void InvokeHandler(CompiledGameEventScript compiledScript, GameEventScriptContext context, CompiledGameEventScriptHandler handler, GameEventScriptMessage message)
    {
        RegisterVmExecutionSession.InvokeHandler(compiledScript, context, handler, message.Arguments);
    }
}
