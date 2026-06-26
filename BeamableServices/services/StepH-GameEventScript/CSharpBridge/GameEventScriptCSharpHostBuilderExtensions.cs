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
        return builder.WithAutomaticDispatch(GameEventScriptCSharpDispatcher.Shared);
    }
}
