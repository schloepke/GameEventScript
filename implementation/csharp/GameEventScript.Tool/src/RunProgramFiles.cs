// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

internal static class RunProgramFiles
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static GameEventScriptProgram Compile(IReadOnlyList<string> paths, IReadOnlyList<GesHandleType>? handleTypes, ref string? activePath)
    {
        var builder = ToolSchemas.Builder(handleTypes);
        foreach (var path in paths)
        {
            activePath = path;
            builder.AddScript(StrictUtf8.GetString(File.ReadAllBytes(path)), path);
        }
        return builder.Compile();
    }

    internal static GameEventScriptProgram ReadOne(string path, IReadOnlyList<GesHandleType>? handleTypes)
    {
        if (string.Equals(Path.GetExtension(path), ".gesb", StringComparison.OrdinalIgnoreCase)) return ReadBinary(path, handleTypes);
        string? activePath = path;
        return Compile([path], handleTypes, ref activePath);
    }

    // A binary Program carries the handle types it was compiled with; they must match the schemas of `--schema`.
    internal static GameEventScriptProgram ReadBinary(string path, IReadOnlyList<GesHandleType>? handleTypes)
    {
        var program = GameEventScriptProgram.Read(File.ReadAllBytes(path));
        ToolSchemas.CheckImports(program, handleTypes);
        return program;
    }
}
