#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Api;

namespace StepH.GameEventScript;

public static class GameEventScriptManager
{
    public static GameEventScriptBuilder CreateScriptBuilder()
        => GameEventScriptBuilder.Create();

    public static GameEventScriptProgram Compile(string input, GameEventScriptCompileOptions? options = null)
        => CreateScriptBuilder().AddScript(input).Compile(options);

    public static GameEventScriptHostBuilder CreateHostBuilder()
        => GameEventScriptHost.CreateBuilder();
}
