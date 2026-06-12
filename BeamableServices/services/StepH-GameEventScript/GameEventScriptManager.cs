#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Api;
using StepH.GameEventScript.VirtualMachine;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript;

public static class GameEventScriptManager
{
    public static bool UseNewCompiler { get; set; } = true;
    
    public static GameEventScriptBuilder CreateScriptBuilder()
        => GameEventScriptBuilder.Create();

    public static GameEventScriptBinary Compile(string input, GameEventScriptCompileOptions? options = null)
        => CreateScriptBuilder().AddScript(input).Compile(options);

    public static IGameEventScriptModule CompileModule(string input, GameEventScriptCompileOptions? options = null)
        => CreateScriptBuilder().AddScript(input).CompileModule(options);

    public static IGameEventScriptModule CreateModule(GameEventScriptBinary binary, ushort registerSize = 512, ushort stackSize = 128)
        => GameEventScriptVirtualMaschine.Create(binary, registerSize, stackSize);
    
    public static GameEventScriptHostBuilder CreateHostBuilder()
        => GameEventScriptHost.CreateBuilder();
}
