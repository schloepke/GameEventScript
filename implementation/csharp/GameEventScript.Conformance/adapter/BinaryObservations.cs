// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using GameEventScript.Api;

internal static class BinaryObservations
{
    internal const int MaxResourceBytes = 64 * 1024 * 1024;

    internal static object Run(JsonElement test)
    {
        // Retain the bounded native-stack gate formerly owned by the legacy worker.
        // The Python process boundary enforces timeout/output limits and detects crashes.
        object? result = null;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { result = Observe(test); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }, 1024 * 1024);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result!;
    }

    private static object Observe(JsonElement test)
    {
        var input = test.GetProperty("binary");
        var bytes = Convert.FromBase64String(input.GetProperty("data").GetString()!);
        GameEventScriptProgram program;
        try
        {
            program = GameEventScriptProgramReader.Read(bytes, new GesProgramReadOptions
            {
                Retention = GesProgramRetention.PreserveAll,
                Limits = new GesProgramReadLimits { MaxFileBytes = MaxResourceBytes }
            });
        }
        catch (GesProgramFormatException exception)
        {
            return new
            {
                status = "ok",
                binary = new
                {
                    outcome = exception.ErrorCode <= GesProgramFormatErrorCode.TooManyEntries ? "readError" : "validationError",
                    errorCode = exception.ErrorCode.ToString(),
                    byteOffset = exception.ByteOffset,
                    sectionType = exception.SectionType,
                    entryIndex = exception.EntryIndex
                }
            };
        }
        var rewritten = GameEventScriptProgramWriter.ToArray(program);
        var compare = input.GetProperty("compareCompiledRuntime").GetBoolean();
        var compiled = compare ? GameEventScriptProgramWriter.ToArray(AdapterCompiler.Compile(test, input.GetProperty("programVersion").GetUInt64())) : null;
        var result = new Dictionary<string, object?>
        {
            ["status"] = "ok",
            ["binary"] = new
            {
                outcome = "valid",
                moduleName = program.ModuleName,
                requiredRegisterCount = program.RequiredRegisterCount,
                requiredCallStackDepth = program.RequiredCallStackDepth,
                opaqueSectionCount = program.OpaqueSections.Count,
                programVersion = program.ProgramVersion,
                buildMetadata = program.BuildMetadata is { } metadata ? new { compilerId = metadata.CompilerId, compilerVersion = metadata.CompilerVersion } : null,
                rewritten = Convert.ToBase64String(rewritten),
                runtimeSections = compare ? Convert.ToBase64String(RuntimeSections(rewritten)) : null,
                compiledRuntimeSections = compare ? Convert.ToBase64String(RuntimeSections(compiled!)) : null
            }
        };
        if (input.GetProperty("includeDump").GetBoolean()) result["dump"] = program.Dump();
        if (input.GetProperty("runtime").ValueKind != JsonValueKind.Null) result["runtime"] = RuntimeObservations.Run(program, input.GetProperty("runtime"));
        return result;
    }

    // Extract required sections only from canonical writer output, never from unvalidated fixture bytes.
    private static byte[] RuntimeSections(byte[] encoded)
    {
        using var result = new MemoryStream();
        var offset = 16;
        while (offset < encoded.Length)
        {
            var sectionLength = checked(12 + (int)BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(offset + 8)));
            if ((BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(offset + 2)) & 1) != 0) result.Write(encoded, offset, sectionLength);
            offset += sectionLength;
        }
        return result.ToArray();
    }
}
