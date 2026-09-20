// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.InteropServices;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.BinaryFormat;

[TestClass]
public sealed class GameEventScriptProgramModelTests
{
    [TestMethod]
    public void ProgramGraphTakesDefensiveCopiesOfAllSequenceInputs()
    {
        var rawValues = new[] { 1, 2 };
        var readOnlyValues = new GameEventScriptReadOnlyArray<int>(rawValues);
        var wrappedValues = new GameEventScriptReadOnlyArray<int>(Array.AsReadOnly(rawValues));
        var sharedValues = new GameEventScriptReadOnlyArray<int>(readOnlyValues);
        rawValues[0] = 9;
        Assert.AreEqual(1, readOnlyValues[0]);
        Assert.AreEqual(1, wrappedValues[0]);
        Assert.AreEqual(1, sharedValues[0]);

        var stringSlices = new[] { new GameEventScriptStringConstantSegment.SliceEntry { Start = 0, Length = 1 } };
        var stringBytes = new byte[] { (byte)'A' };
        var strings = new GameEventScriptStringConstantSegment(stringSlices, stringBytes);
        stringSlices[0] = new GameEventScriptStringConstantSegment.SliceEntry();
        stringBytes[0] = (byte)'Z';
        Assert.AreEqual("A", strings.Resolve(0));

        var listSlices = new[] { new GameEventScriptUInt16IndexListSegment.SliceEntry { Start = 0, Length = 2 } };
        var listValues = new ushort[] { 4, 5 };
        var lists = new GameEventScriptUInt16IndexListSegment(listSlices, listValues);
        listSlices[0] = new GameEventScriptUInt16IndexListSegment.SliceEntry();
        listValues[0] = 99;
        Assert.AreEqual((ushort)4, lists.Resolve(0)[0]);

        var arguments = new ushort[] { 1 };
        var requiredTags = new ushort[] { 2 };
        var excludedTags = new ushort[] { 3 };
        var bind = new GameEventScriptBindingSegment.GameEventScriptBinaryBindEntry(
            GameEventScriptBinaryBindKind.Function,
            name: 0,
            arguments,
            entryAddress: 0,
            id: 7,
            requiredTags,
            excludedTags);
        arguments[0] = 10;
        requiredTags[0] = 11;
        excludedTags[0] = 12;
        Assert.AreEqual((ushort)1, bind.ArgumentNames[0]);
        Assert.AreEqual((ushort)2, bind.RequiredTags[0]);
        Assert.AreEqual((ushort)3, bind.ExcludedTags[0]);

        var bindEntries = new[] { bind };
        var bindings = new GameEventScriptBindingSegment(bindEntries);
        bindEntries[0] = default;
        Assert.AreEqual(GameEventScriptBinaryBindKind.Function, bindings.Entries[0].Kind);

        var instructionValues = new[]
        {
            new GameEventScriptBytecodeInstruction
            {
                OpCode = GameEventScriptBytecodeOpCode.LoadInteger,
                DestinationRegister = 2,
                I64 = 17
            }
        };
        var code = new GameEventScriptCodeSegment(instructionValues);
        instructionValues[0] = default;
        Assert.AreEqual(GameEventScriptBytecodeOpCode.LoadInteger, code[0].OpCode);
        Assert.AreEqual(17L, code[0].I64);

        var symbolValues = new[] { new GameEventScriptDebugSymbol(GameEventScriptDebugSymbolKind.Local, 2, "value", 0, 1) };
        var symbols = new GameEventScriptDebugSymbolsSegment(symbolValues);
        symbolValues[0] = default;
        Assert.AreEqual("value", symbols.Symbols[0].Name);

        var hash = new byte[32];
        hash[0] = 7;
        var lineOffsets = new uint[] { 0, 2 };
        var mapSource = new GameEventScriptSourceMapSource(0, "source.ges", 2, hash, lineOffsets);
        hash[0] = 9;
        lineOffsets[1] = 1;
        Assert.AreEqual((byte)7, mapSource.Sha256[0]);
        Assert.AreEqual((uint)2, mapSource.LineStartByteOffsets[1]);

        var mapSources = new[] { mapSource };
        var mapEntries = new[] { new GameEventScriptSourceMapEntry(0, 1, 0, 0, 1) };
        var sourceMap = new GameEventScriptSourceMapSegment(mapSources, mapEntries);
        mapSources[0] = new GameEventScriptSourceMapSource(0, "changed.ges", 0, new byte[32], new uint[] { 0 });
        mapEntries[0] = default;
        Assert.AreEqual("source.ges", sourceMap.Sources[0].SourceName);
        Assert.AreEqual((uint)1, sourceMap.Entries[0].CodeLength);

        var sourceBytes = new byte[] { (byte)'o', (byte)'k' };
        var archiveEntry = new GameEventScriptSourceArchiveEntry(0, "source.ges", sourceBytes);
        sourceBytes[0] = (byte)'n';
        Assert.AreEqual("ok", archiveEntry.ResolveText());
        var archiveEntries = new[] { archiveEntry };
        var archive = new GameEventScriptSourceArchiveSegment(archiveEntries);
        archiveEntries[0] = new GameEventScriptSourceArchiveEntry(0, "changed.ges", Array.Empty<byte>());
        Assert.AreEqual("source.ges", archive.Sources[0].SourceName);

        var opaqueBytes = new byte[] { 1, 2 };
        var opaque = new GameEventScriptOpaqueSection(0x8000, GameEventScriptSectionFlags.None, 1, opaqueBytes, 4);
        opaqueBytes[0] = 8;
        Assert.AreEqual((byte)1, opaque.RawPayload[0]);

        var baseProgram = Compile();
        var opaqueSections = new[] { opaque };
        var program = new GameEventScriptProgram(
            baseProgram.FormatVersion,
            baseProgram.ModuleName,
            baseProgram.ProgramVersion,
            baseProgram.RequiredRegisterCount,
            baseProgram.RequiredCallStackDepth,
            baseProgram.StringConstants,
            baseProgram.UInt16IndexLists,
            baseProgram.Bindings,
            baseProgram.Code,
            baseProgram.DebugSymbols,
            baseProgram.SourceMap,
            baseProgram.SourceArchive,
            baseProgram.BuildMetadata,
            opaqueSections);
        opaqueSections[0] = new GameEventScriptOpaqueSection(0x8001, GameEventScriptSectionFlags.None, 1, Array.Empty<byte>(), 5);
        Assert.AreEqual((ushort)0x8000, program.OpaqueSections[0].SectionType);
    }

    [TestMethod]
    [DataRow(0, 3, 0, 10)]
    [DataRow(0, 3, 2, 30)]
    [DataRow(1, 1, 0, 20)]
    [DataRow(1, 2, 1, 30)]
    public void UInt16IndexListReadsValidIndicesRelativeToSlice(int start, int length, int index, int expected)
    {
        var segment = new GameEventScriptUInt16IndexListSegment([new() { Start = start, Length = length }], new ushort[] { 10, 20, 30 });
        var list = segment.Resolve(0);

        Assert.AreEqual(start, list.Start);
        Assert.AreEqual(length, list.Length);
        Assert.AreEqual((ushort)expected, list[index]);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void UInt16IndexListRejectsIndicesOutsideSlice(int index)
    {
        var segment = new GameEventScriptUInt16IndexListSegment([new() { Start = 1, Length = 1 }], new ushort[] { 10, 20, 30 });
        var list = segment.Resolve(0);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = list[index]);

        Assert.AreEqual("index", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0, -1)]
    [DataRow(0, 0)]
    [DataRow(1, -1)]
    [DataRow(1, 0)]
    [DataRow(3, -1)]
    [DataRow(3, 0)]
    public void EmptyUInt16IndexListRejectsIndicesRegardlessOfBackingStorage(int start, int index)
    {
        var segment = new GameEventScriptUInt16IndexListSegment([new() { Start = start, Length = 0 }], new ushort[] { 10, 20, 30 });
        var list = segment.Resolve(0);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = list[index]);

        Assert.AreEqual("index", exception.ParamName);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    public void DefaultUInt16IndexListRejectsIndices(int index)
    {
        var list = default(GameEventScriptUInt16IndexList);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = list[index]);

        Assert.AreEqual("index", exception.ParamName);
    }

    [TestMethod]
    public void ProgramHasNoPublicConstructor()
    {
        Assert.IsTrue(typeof(GameEventScriptProgram).IsSealed);
        Assert.HasCount(0, typeof(GameEventScriptProgram).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [TestMethod]
    public void InstructionAliasesUseNumericWordsAndPayloadBits()
    {
        var instruction = new GameEventScriptBytecodeInstruction
        {
            DestinationRegister = 0x1234,
            ImmediateX = -2,
            TypeKind = GameEventScriptBytecodeTypeKind.Custom,
            AU = 0x1122,
            BU = 0x3344,
            CU = 0x5566,
            DU = 0x7788
        };

        Assert.AreEqual((ushort)0x1234, instruction.MessageDestination);
        Assert.AreEqual((ushort)0xFFFE, instruction.XRegister);
        Assert.AreEqual((ushort)GameEventScriptBytecodeTypeKind.Custom, instruction.TypeOperand);
        Assert.AreEqual(0x7788556633441122UL, instruction.Payload);

        instruction.Payload = 0xFEDCBA9876543210UL;
        Assert.AreEqual((ushort)0x3210, instruction.AU);
        Assert.AreEqual((ushort)0x7654, instruction.BU);
        Assert.AreEqual((ushort)0xBA98, instruction.CU);
        Assert.AreEqual((ushort)0xFEDC, instruction.DU);
        Assert.AreEqual(unchecked((long)0xFEDCBA9876543210UL), instruction.I64);

        instruction.F64 = -0d;
        Assert.AreEqual(0x8000000000000000UL, instruction.Payload);
        instruction.Payload = 0x3FF0000000000000UL;
        Assert.AreEqual(1d, instruction.F64);
        Assert.AreEqual(16, Marshal.SizeOf<GameEventScriptBytecodeInstruction>());
    }

    private static GameEventScriptProgram Compile()
        => GameEventScriptBuilder.Create()
            .AddScript("module portablemodel\n\non Start(value) { emit Done(result: value + 1) }", "portable-model.ges")
            .WithProgramVersion(7)
            .WithDebugInfo(GameEventScriptDebugInfoOptions.All)
            .Compile();

}
