using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.BytecodeVM;

internal static class GesBytecodeVmInvocationEngine
{
    public static void InvokeMessage(GesBytecodeVmExecutable compiledScript, GameEventScriptContext context, GameEventScriptMessage message)
    {
        foreach (var handler in GesInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message))
        {
            if (MatchesTags(handler, message))
            {
                InvokeHandler(compiledScript, context, handler, message);
            }
        }
    }

    public static void InvokeHandler(GesBytecodeVmExecutable compiledScript, GameEventScriptContext context, GesBytecodeVmCompiledHandler handler, GameEventScriptMessage message)
    {
        GesBytecodeVmExecutionSession.InvokeHandler(compiledScript, context, handler, message.Arguments);
    }

    private static bool MatchesTags(GesBytecodeVmCompiledHandler handler, GameEventScriptMessage message)
    {
        foreach (var tag in handler.RequiredTags)
        {
            if (!message.HasTag(tag))
            {
                return false;
            }
        }

        foreach (var tag in handler.ExcludedTags)
        {
            if (message.HasTag(tag))
            {
                return false;
            }
        }

        return true;
    }
}
