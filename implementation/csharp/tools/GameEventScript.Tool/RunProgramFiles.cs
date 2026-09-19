// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

internal static class RunProgramFiles
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static GameEventScriptProgram Compile(IReadOnlyList<string> paths, ref string? activePath)
    {
        var builder = GameEventScriptBuilder.Create();
        foreach (var path in paths)
        {
            activePath = path;
            builder.AddScript(StrictUtf8.GetString(File.ReadAllBytes(path)), path);
        }
        return builder.Compile();
    }

    internal static GameEventScriptProgram ReadOne(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".gesb", StringComparison.OrdinalIgnoreCase)) return GameEventScriptProgramReader.Read(File.ReadAllBytes(path));
        string? activePath = path;
        return Compile([path], ref activePath);
    }
}
