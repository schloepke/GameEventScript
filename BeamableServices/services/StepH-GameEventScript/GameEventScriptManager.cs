#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeExecutor;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript;

public static class GameEventScriptManager
{
    public static GameEventScriptBuilder CreateScriptBuilder() => GameEventScriptBuilder.Create();

    public static GameEventScriptCompiled Compile(string input, GameEventScriptCompileOptions? options = null) => CreateScriptBuilder().AddScript(input).Compile(options);

    public static IGameEventScriptModule CompileModuleOldVm(string input, GameEventScriptCompileOptions? options = null) => CreateScriptBuilder().AddScript(input).CompileModule(options);
    public static IGameEventScriptModule CompileModule(string input, GameEventScriptCompileOptions? options = null) => 
        GameEventScriptVirtualMaschine.Create(CreateScriptBuilder().AddScript(input).Compile(options).ToGameEventScriptBinary(), 128, 128);
    public static IGameEventScriptModule CreateModuleNewVm(GameEventScriptBinary binary, ushort registerSize = 128, ushort stackSize = 128) =>
        GameEventScriptVirtualMaschine.Create(binary, registerSize, stackSize);
    
    public static GameEventScriptHostBuilder CreateHostBuilder() => GameEventScriptHost.CreateBuilder();
}
