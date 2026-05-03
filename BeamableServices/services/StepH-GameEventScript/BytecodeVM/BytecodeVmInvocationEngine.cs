#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

internal static class BytecodeVmInvocationEngine
{
    public static void InvokeMessage(GseBytecodeVmExecutable compiledScript, GameEventScriptContext context, GameEventScriptMessage message)
    {
        foreach (var handler in GesInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message)) InvokeHandler(compiledScript, context, handler, message);
    }

    public static void InvokeHandler(GseBytecodeVmExecutable compiledScript, GameEventScriptContext context, BytecodeVmCompiledHandler handler, GameEventScriptMessage message)
    {
        BytecodeVmExecutionSession.InvokeHandler(compiledScript, context, handler, message.Arguments);
    }
}
