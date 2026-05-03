#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.RegisterVM;

namespace StepH.GameEventScript;

public static class GameEventScriptManager
{
    public static GseModuleBuilder CreateModuleBuilder() => GseModuleBuilder.Create();

    public static RegisterCompiledGse Compile(string input, RegisterVmCompilationOptions? options = null) => CreateModuleBuilder().AddScript(input).Compile(options);

}

// From here error / exception structure for compiling / module building etc.

