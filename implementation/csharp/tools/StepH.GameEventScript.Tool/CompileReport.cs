// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using BindEntry = StepH.GameEventScript.Api.GameEventScriptBindingSegment.GameEventScriptBinaryBindEntry;
using BindKind = StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;

namespace StepH.GameEventScript.Tool;

internal static class CompileReport
{
    internal static void Write(TextWriter writer, GameEventScriptProgram program, IReadOnlyList<string> sources, string outputPath, int fileSize, TimeSpan elapsed, bool verbose)
    {
        writer.WriteLine("Compiled successfully.");
        writer.WriteLine($"Sources ({sources.Count}):");
        foreach (var source in sources) writer.WriteLine($"  {source}");
        writer.WriteLine($"Output: {outputPath}");
        writer.WriteLine($"Module: {program.ModuleName}");
        var handlerCount = program.Bindings.Entries.Count(binding => binding.Kind is BindKind.MessageHandler or BindKind.MessageNameHandler);
        writer.WriteLine($"Handlers: {handlerCount}");
        writer.WriteLine($"File size: {fileSize} bytes");
        var debugSections = new List<string>();
        if (program.DebugSymbols is not null) debugSections.Add("symbols");
        if (program.SourceMap is not null) debugSections.Add("source map");
        if (program.SourceArchive is not null) debugSections.Add("source archive");
        writer.WriteLine($"Debug info: {(debugSections.Count == 0 ? "none" : string.Join(", ", debugSections))}");
        writer.WriteLine(FormattableString.Invariant($"Duration: {elapsed.TotalMilliseconds:F1} ms"));
        if (!verbose) return;

        writer.WriteLine();
        writer.WriteLine($"Binary format: .gesb V{program.FormatVersion}");
        // V1 serializes each instruction as exactly 16 bytes, independent of CLR layout.
        writer.WriteLine($"Bytecode: {program.Code.Count} instructions, {16L * program.Code.Count} bytes (instruction data)");
        writer.WriteLine($"Required registers: {program.RequiredRegisterCount}");
        writer.WriteLine($"Required call stack depth: {program.RequiredCallStackDepth}");
        WriteBindings(writer, program, "Message bindings", BindKind.MessageHandler, BindKind.MessageNameHandler, BindKind.OutboundMessage);
        WriteBindings(writer, program, "Required extensions", BindKind.ExtensionCall);
        WriteBindings(writer, program, "Required external types", BindKind.ExternalType);
    }

    private static void WriteBindings(TextWriter writer, GameEventScriptProgram program, string title, params BindKind[] kinds)
    {
        var entries = program.Bindings.Entries.Where(binding => kinds.Contains(binding.Kind)).ToArray();
        writer.WriteLine($"{title} ({entries.Length}):");
        foreach (var binding in entries)
        {
            var signature = FormatSignature(program, binding);
            var prefix = binding.Kind switch
            {
                BindKind.MessageHandler => "handler ",
                BindKind.MessageNameHandler => "handler ",
                BindKind.OutboundMessage => "outbound ",
                _ => string.Empty
            };
            writer.WriteLine($"  {prefix}{signature}");
        }
    }

    private static string FormatSignature(GameEventScriptProgram program, BindEntry binding)
    {
        var name = program.StringConstants.Resolve(binding.Name);
        if (binding.Kind == BindKind.MessageNameHandler) return $"{name} as message";
        return $"{name}({string.Join(", ", binding.ArgumentNames.Select(program.StringConstants.Resolve))})";
    }
}
