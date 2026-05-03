#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH.GameEventScript;

public static class GameEventScriptManager
{
    public static GameEventScriptBuilder CreateBuilder() => GameEventScriptBuilder.Create();

    public static GameEventScriptCompiled Compile(string input, GameEventScriptCompileOptions? options = null) => CreateBuilder().AddScript(input).Compile(options);

}
