#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.CSharpBridge;

public static class GameEventScriptCSharpHostBuilderExtensions
{
    /// <summary>
    /// Configures the host to drain queued messages on a C# background dispatch pump.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public static GameEventScriptHostBuilder WithAutomaticDispatch(this GameEventScriptHostBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));
        var dispatcher = GameEventScriptCSharpDispatcher.Shared;
        return builder
            .WithAutomaticDispatch(dispatcher)
            .WithRuntimeGate(dispatcher);
    }
}
