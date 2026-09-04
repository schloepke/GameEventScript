// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private GameEventScriptDebugInfoOptions _debugInfoOptions;
    private IReadOnlyList<GesSourceDocument> _sourceDocuments = [];

    internal GesBinaryBuilder WithDebugInfo(GameEventScriptDebugInfoOptions options, IReadOnlyList<GesSourceDocument> sources)
    {
        _debugInfoOptions = options;
        _sourceDocuments = sources ?? [];
        return this;
    }

    private DebugSegments BuildDebugSegments(IReadOnlyList<PlanItem> items, IReadOnlyDictionary<int, ushort> registerMap)
    {
        var symbols = (_debugInfoOptions & GameEventScriptDebugInfoOptions.DebugSymbols) != 0
            ? BuildDebugSymbols(items, registerMap)
            : null;
        var map = (_debugInfoOptions & GameEventScriptDebugInfoOptions.SourceMap) != 0
            ? BuildSourceMap(items)
            : null;
        var archive = (_debugInfoOptions & GameEventScriptDebugInfoOptions.SourceArchive) != 0
            ? BuildSourceArchive()
            : null;
        return new DebugSegments(symbols, map, archive);
    }

    private GameEventScriptDebugSymbolsSegment BuildDebugSymbols(IReadOnlyList<PlanItem> items, IReadOnlyDictionary<int, ushort> registerMap)
    {
        var routineStarts = new int[_routines.Count];
        var routineEnds = new int[_routines.Count];
        Array.Fill(routineStarts, -1);
        var codeAddress = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Instruction is not { } instruction) continue;
            if (instruction.RoutineId >= 0)
            {
                if (routineStarts[instruction.RoutineId] < 0) routineStarts[instruction.RoutineId] = codeAddress;
                routineEnds[instruction.RoutineId] = codeAddress + 1;
            }
            codeAddress++;
        }

        var symbols = new List<GameEventScriptDebugSymbol>();
        for (var registerIndex = 0; registerIndex < _registers.Count; registerIndex++)
        {
            var register = _registers[registerIndex];
            if (register.IsTemporary || !register.IsDebugVisible || string.IsNullOrWhiteSpace(register.Name) || register.RoutineId < 0 || !registerMap.TryGetValue(register.Id, out var physical)) continue;
            var routine = _routines[register.RoutineId];
            if (!routine.Kind.HasValue || routineStarts[routine.Id] < 0) continue;
            var kind = ContainsRegister(routine.ArgumentRegisters, register.Id)
                ? GameEventScriptDebugSymbolKind.Parameter
                : GameEventScriptDebugSymbolKind.Local;
            symbols.Add(new GameEventScriptDebugSymbol(kind, physical, register.Name!, checked((uint)routineStarts[routine.Id]), checked((uint)(routineEnds[routine.Id] - routineStarts[routine.Id]))));
        }
        return new GameEventScriptDebugSymbolsSegment(symbols);
    }

    private GameEventScriptSourceMapSegment BuildSourceMap(IReadOnlyList<PlanItem> items)
    {
        var sourceMetadata = new GameEventScriptSourceMapSource[_sourceDocuments.Count];
        var sourceData = new SourceData[_sourceDocuments.Count];
        using var sha256 = SHA256.Create();
        for (var index = 0; index < _sourceDocuments.Count; index++)
        {
            var document = _sourceDocuments[index];
            var utf8 = Encoding.UTF8.GetBytes(document.Text);
            var charLines = BuildLineStartCharacters(document.Text);
            var byteLines = new uint[charLines.Length];
            for (var line = 0; line < charLines.Length; line++) byteLines[line] = checked((uint)Encoding.UTF8.GetByteCount(document.Text, 0, charLines[line]));
            var hash = sha256.ComputeHash(utf8);
            sourceMetadata[index] = new GameEventScriptSourceMapSource(document.SourceId, document.SourceName, checked((uint)utf8.Length), hash, byteLines);
            sourceData[index] = new SourceData(document, charLines, utf8.Length);
        }

        var mappings = new List<GameEventScriptSourceMapEntry>();
        uint codeAddress = 0;
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var item = items[itemIndex];
            if (item.Instruction is null) continue;
            if (ResolveSourceData(item.SourceRange, sourceData) is { } data && ResolveByteRange(item.SourceRange!, data) is { } range)
            {
                var mapping = new GameEventScriptSourceMapEntry(codeAddress, 1, data.Document.SourceId, range.Start, range.Length);
                if (mappings.Count > 0)
                {
                    var previous = mappings[^1];
                    if (previous.CodeStart + previous.CodeLength == mapping.CodeStart && previous.SourceId == mapping.SourceId && previous.SourceStartByteOffset == mapping.SourceStartByteOffset &&
                        previous.SourceByteLength == mapping.SourceByteLength)
                    {
                        mappings[^1] = previous with { CodeLength = previous.CodeLength + 1 };
                    }
                    else mappings.Add(mapping);
                }
                else mappings.Add(mapping);
            }
            codeAddress++;
        }
        return new GameEventScriptSourceMapSegment(sourceMetadata, mappings);
    }

    private GameEventScriptSourceArchiveSegment BuildSourceArchive()
    {
        var sources = new GameEventScriptSourceArchiveEntry[_sourceDocuments.Count];
        for (var index = 0; index < _sourceDocuments.Count; index++)
        {
            var source = _sourceDocuments[index];
            sources[index] = new GameEventScriptSourceArchiveEntry(source.SourceId, source.SourceName, Encoding.UTF8.GetBytes(source.Text));
        }
        return new GameEventScriptSourceArchiveSegment(sources);
    }

    private static SourceData? ResolveSourceData(GameEventScriptSourceLocation? location, IReadOnlyList<SourceData> sources)
    {
        if (location is null) return null;
        if (location.SourceId.HasValue)
        {
            for (var index = 0; index < sources.Count; index++) if (sources[index].Document.SourceId == location.SourceId.Value) return sources[index];
        }
        for (var index = 0; index < sources.Count; index++) if (string.Equals(sources[index].Document.SourceName, location.SourceName, StringComparison.Ordinal)) return sources[index];
        return null;
    }

    private static ByteRange? ResolveByteRange(GameEventScriptSourceLocation location, SourceData source)
    {
        if (!location.Line.HasValue || !location.Column.HasValue) return null;
        var startChar = ResolveCharacterOffset(source.Document.Text, source.LineStartCharacters, location.Line.Value, location.Column.Value);
        var endChar = ResolveCharacterOffset(source.Document.Text, source.LineStartCharacters, location.EndLine ?? location.Line.Value, location.EndColumn ?? location.Column.Value);
        if (endChar < startChar) endChar = startChar;
        var startByte = Encoding.UTF8.GetByteCount(source.Document.Text, 0, startChar);
        var byteLength = Encoding.UTF8.GetByteCount(source.Document.Text, startChar, endChar - startChar);
        return new ByteRange(checked((uint)startByte), checked((uint)byteLength));
    }

    private static int ResolveCharacterOffset(string text, IReadOnlyList<int> lineStarts, int line, int column)
    {
        var lineIndex = Math.Clamp(line - 1, 0, Math.Max(0, lineStarts.Count - 1));
        var start = lineStarts.Count == 0 ? 0 : lineStarts[lineIndex];
        var end = lineIndex + 1 < lineStarts.Count ? lineStarts[lineIndex + 1] : text.Length;
        return GameEventScriptText.Utf16OffsetForScalarOffset(text, start, end, Math.Max(0, column - 1));
    }

    private static int[] BuildLineStartCharacters(string text)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n') index++;
                starts.Add(index + 1);
            }
            else if (text[index] == '\n') starts.Add(index + 1);
        }
        return starts.ToArray();
    }

    private static bool ContainsRegister(IReadOnlyList<GesRegisterRef> registers, int id)
    {
        for (var index = 0; index < registers.Count; index++) if (registers[index].Id == id) return true;
        return false;
    }

    private sealed record DebugSegments(GameEventScriptDebugSymbolsSegment? Symbols, GameEventScriptSourceMapSegment? SourceMap, GameEventScriptSourceArchiveSegment? SourceArchive);
    private sealed record SourceData(GesSourceDocument Document, int[] LineStartCharacters, int Utf8Length);
    private readonly record struct ByteRange(uint Start, uint Length);
}
