#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.BytecodeVM;

internal static class BytecodeVmInvocationEngine
{
    public static void InvokeMessage(CompiledGameEventScript compiledScript, GameEventScriptContext context, GameEventScriptMessage message)
    {
        foreach (var handler in GameEventScriptInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message)) InvokeHandler(compiledScript, context, handler, message);
    }

    public static void InvokeHandler(CompiledGameEventScript compiledScript, GameEventScriptContext context, CompiledGameEventScriptHandler handler, GameEventScriptMessage message)
    {
        BytecodeVmExecutionSession.InvokeHandler(compiledScript, context, handler, message.Arguments);
    }
}
