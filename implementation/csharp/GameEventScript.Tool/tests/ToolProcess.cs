// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace GameEventScript.Tests.Native.Tool;

internal static class ToolProcess
{
    private static readonly IReadOnlyDictionary<string, string?> Environment = new Dictionary<string, string?> { ["NO_COLOR"] = null };

    internal static DotNetProcessResult Execute(string directory, params string[] arguments)
        => DotNetProcess.ExecuteConfigured(AssemblyPath(), directory, null, Environment, arguments);

    internal static DotNetProcessResult ExecuteWithInput(string directory, string input, params string[] arguments)
        => DotNetProcess.ExecuteConfigured(AssemblyPath(), directory, input, Environment, arguments);

    internal static DotNetProcessResult ExecuteWithNoColor(string directory, params string[] arguments)
        => DotNetProcess.ExecuteConfigured(AssemblyPath(), directory, null, new Dictionary<string, string?> { ["NO_COLOR"] = "1" }, arguments);

    internal static DotNetProcessResult ExecuteWithInputAndNoColor(string directory, string input, params string[] arguments)
        => DotNetProcess.ExecuteConfigured(AssemblyPath(), directory, input, new Dictionary<string, string?> { ["NO_COLOR"] = "1" }, arguments);

    private static string AssemblyPath()
    {
        var configuration = typeof(ToolProcess).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        return Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tool", "bin", configuration, "net8.0", "GameEventScript.Tool.dll");
    }
}
