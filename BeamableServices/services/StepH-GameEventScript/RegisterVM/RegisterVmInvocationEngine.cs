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
        RegisterVmExecutionSession.InvokeHandler(compiledScript, context, handler, message.Arguments);
    }
}
