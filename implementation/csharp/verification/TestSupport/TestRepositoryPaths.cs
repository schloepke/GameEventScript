// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Tests;

internal static class TestRepositoryPaths
{
    internal static string Root { get; } = FindRoot();

    internal static string CSharpDirectory { get; } = Path.Combine(Root, "implementation", "csharp");

    internal static string LibraryProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "GameEventScript.Runtime", "src");

    internal static string CompilerProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "GameEventScript.Compiler", "src");

    internal static string CSharpBridgeProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "GameEventScript.CSharpBridge", "src");

    internal static string ConformanceProjectDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "GameEventScript.Conformance", "src");

    internal static IReadOnlyList<string> ProductProjectDirectories { get; } = [LibraryProjectDirectory, CompilerProjectDirectory, CSharpBridgeProjectDirectory, ConformanceProjectDirectory];

    internal static string RepositoryTestDirectory { get; } = Path.Combine(Root, "implementation", "csharp", "verification", "GameEventScript.Repository.Tests");

    internal static string SpecificationsDirectory { get; } = Path.Combine(Root, "specs");

    internal static string DocumentationDirectory { get; } = Path.Combine(Root, "docs");

    internal static string ConformanceDirectory { get; } = Path.Combine(Root, "conformance");

    internal static string ConformanceArtifactsDirectory { get; } = Path.Combine(Root, "artifacts", "conformance");

    private static string FindRoot()
    {
        foreach (var candidate in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            if (FindRootFrom(candidate) is { } root)
                return root;

        throw new DirectoryNotFoundException("Could not locate the Game Event Script monorepo root.");
    }

    private static string? FindRootFrom(string path)
    {
        var directory = new DirectoryInfo(path);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LICENSE")) &&
                File.Exists(Path.Combine(directory.FullName, "specs", "Language.md")) &&
                File.Exists(Path.Combine(directory.FullName, "implementation", "csharp", "GameEventScript.Runtime", "src", "GameEventScript.Runtime.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
