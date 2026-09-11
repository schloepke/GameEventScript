// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace StepH.GameEventScript.Tool;

internal static class CompileSources
{
    internal static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static IReadOnlyList<string> Expand(IReadOnlyList<string> arguments)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(PathComparer);
        foreach (var argument in arguments)
        {
            if (argument.IndexOfAny(['*', '?']) < 0 || File.Exists(argument))
            {
                Add(argument);
                continue;
            }

            var directory = Path.GetDirectoryName(argument) ?? string.Empty;
            var pattern = Path.GetFileName(argument);
            if (directory.IndexOfAny(['*', '?']) >= 0 || pattern.Contains("**", StringComparison.Ordinal))
                throw new ArgumentException("Wildcards are supported only in file names; recursive patterns are not supported.");
            var matches = Directory.GetFiles(directory.Length == 0 ? "." : directory, pattern, SearchOption.TopDirectoryOnly);
            Array.Sort(matches, StringComparer.Ordinal);
            if (matches.Length == 0) throw new FileNotFoundException($"No source files match '{argument}'.", argument);
            foreach (var match in matches) Add(directory.Length == 0 ? Path.GetFileName(match) : match);
        }
        return result;

        void Add(string path)
        {
            if (seen.Add(Path.GetFullPath(path))) result.Add(path);
        }
    }
}
