using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.BytecodeVM;

internal static class GesBytecodeVmInvocationEngine
{
    public static void InvokeMessage(GseBytecodeVmExecutable compiledScript, GameEventScriptContext context, GameEventScriptMessage message)
    {
        foreach (var handler in GesInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message)) InvokeHandler(compiledScript, context, handler, message);
    }

    public static void InvokeHandler(GseBytecodeVmExecutable compiledScript, GameEventScriptContext context, GesBytecodeVmCompiledHandler handler, GameEventScriptMessage message)
    {
        GesBytecodeVmExecutionSession.InvokeHandler(compiledScript, context, handler, message.Arguments);
    }
}
