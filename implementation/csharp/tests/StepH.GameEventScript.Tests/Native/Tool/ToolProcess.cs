// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace StepH_GameEventScript_Tests.Native.Tool;

internal static class ToolProcess
{
    internal static DotNetProcessResult Execute(string directory, params string[] arguments)
    {
        var configuration = typeof(ToolProcess).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var tool = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tool", "bin", configuration, "net8.0", "StepH.GameEventScript.Tool.dll");
        return DotNetProcess.Execute(tool, directory, arguments);
    }
}
