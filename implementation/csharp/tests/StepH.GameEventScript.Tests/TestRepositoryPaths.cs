// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace StepH_GameEventScript_Tests;

internal static class TestRepositoryPaths
{
    internal static string Root { get; } = FindRoot();

    internal static string LibraryProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "src", "StepH.GameEventScript");

    internal static string CSharpBridgeProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "src", "StepH.GameEventScript.CSharpBridge");

    internal static string ConformanceProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "src", "StepH.GameEventScript.Conformance");

    internal static IReadOnlyList<string> ProductProjectDirectories { get; } = [LibraryProjectDirectory, CSharpBridgeProjectDirectory, ConformanceProjectDirectory];

    internal static string TestProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "tests", "StepH.GameEventScript.Tests");

    internal static string SpecificationsDirectory { get; } = Path.Combine(Root, "specs");

    internal static string DocumentationDirectory { get; } = Path.Combine(Root, "docs");

    internal static string ConformanceDirectory { get; } = Path.Combine(Root, "conformance");

    internal static string ConformanceArtifactsDirectory { get; } = Path.Combine(Root, "artifacts", "conformance");

    private static string FindRoot([CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LICENSE")) &&
                File.Exists(Path.Combine(directory.FullName, "specs", "Language.md")) &&
                File.Exists(Path.Combine(directory.FullName, "implementation", "csharp", "src", "StepH.GameEventScript", "StepH.GameEventScript.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Game Event Script monorepo root.");
    }
}
