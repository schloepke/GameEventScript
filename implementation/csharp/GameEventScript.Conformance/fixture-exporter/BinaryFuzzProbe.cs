// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

// Executable-only probe: unexpected exceptions and writer failures must escape.
internal static class BinaryFuzzProbe
{
    internal static int Run(string input, string output)
    {
        var options = new GameEventScriptProgramReadOptions
        {
            Limits = new GameEventScriptProgramReadLimits
            {
                MaxFileBytes = 32768,
                MaxSectionCount = 64,
                MaxSourceArchiveBytes = 16384,
                MaxRetainedOpaqueBytes = 16384,
                MaxInstructions = 2048,
                MaxBindings = 256,
                MaxStringEntries = 2048,
                MaxIndexLists = 2048
            }
        };
        using var writer = new StreamWriter(output) { AutoFlush = true };
        foreach (var line in File.ReadLines(input))
        {
            var bytes = Convert.FromBase64String(line);
            GameEventScriptProgram program;
            try
            {
                program = GameEventScriptProgramReader.Read(bytes, options);
            }
            catch (GameEventScriptProgramFormatException error)
            {
                writer.WriteLine("reject:" + error.ErrorCode);
                continue;
            }
            var canonical = GameEventScriptProgramWriter.ToArray(program);
            var second = GameEventScriptProgramWriter.ToArray(GameEventScriptProgramReader.Read(canonical, options));
            if (!canonical.AsSpan().SequenceEqual(second)) throw new InvalidOperationException("Unstable accepted binary rewrite.");
            writer.WriteLine("accept:" + Convert.ToBase64String(canonical));
        }
        return 0;
    }
}
