using StepH.GameEventScript.Api;

namespace StepH.GameEventScript;

/// <summary>
/// Represents a game event script manager.
/// </summary>
public static class GameEventScriptManager
{
    /// <summary>
    /// Creates a script builder.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptBuilder CreateScriptBuilder()
        => GameEventScriptBuilder.Create();

    /// <summary>
    /// Performs the compile operation.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptProgram Compile(string input, GameEventScriptCompileOptions? options = null)
        => CreateScriptBuilder().AddScript(input).Compile(options);

    /// <summary>
    /// Creates a host builder.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptHostBuilder CreateHostBuilder()
        => GameEventScriptHost.CreateBuilder();
}
