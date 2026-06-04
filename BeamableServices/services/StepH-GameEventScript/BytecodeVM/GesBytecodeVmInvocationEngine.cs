using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.BytecodeVM;

internal static class GesBytecodeVmInvocationEngine
{
    public static void InvokeMessage(GesBytecodeVmExecutable compiledScript, GameEventScriptSession context, GameEventScriptMessage message)
    {
        var exactHandlers = GesInvocationKernel.GetMatchingHandlers(compiledScript.DispatchIndex, message);
        var nameHandlers = GesInvocationKernel.GetMatchingHandlers(compiledScript.MessageNameDispatchIndex, message.Name);
        foreach (var handler in exactHandlers.Concat(nameHandlers).OrderBy(handler => handler.DeclarationOrder))
        {
            if (MatchesTags(handler, message))
            {
                var dispatchMessage = handler.DispatchKind == GameEventScriptBytecodeHandlerDispatchKind.MessageName
                    ? GameEventScriptSystemEndpoints.CreateMessageDispatchMessage(message)
                    : message;
                InvokeHandler(compiledScript, context, handler, dispatchMessage);
            }
        }
    }

    public static void InvokeHandler(GesBytecodeVmExecutable compiledScript, GameEventScriptSession context, GesBytecodeVmCompiledHandler handler, GameEventScriptMessage message)
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
