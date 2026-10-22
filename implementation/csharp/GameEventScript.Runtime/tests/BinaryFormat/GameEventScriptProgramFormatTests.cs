// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.BinaryFormat;

[TestClass]
public sealed class GameEventScriptProgramFormatTests
{
    [TestMethod]
    public void CompilerGeneratesAllDebugSectionsByDefault()
    {
        var program = new GesProgramBuilder()
            .AddScript("module defaultdebug\n\non Start { emit Done(value: 1) }", "default-debug.ges")
            .Compile();

        Assert.IsNotNull(program.DebugSymbols);
        Assert.IsNotNull(program.SourceMap);
        Assert.IsNotNull(program.SourceArchive);
        Assert.AreEqual("default-debug.ges", program.SourceArchive.Sources[0].SourceName);
    }

    [TestMethod]
    public void FileAndSectionFramingIsLittleEndianAndUnpadded()
    {
        var bytes = Compile(GesDebugInfoOptions.None).Encode();
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("GESB"), bytes[..4]);
        Assert.AreEqual((ushort)1, ReadU16(bytes, 4));
        Assert.AreEqual((ushort)0, ReadU16(bytes, 6));
        Assert.AreEqual((uint)16, ReadU32(bytes, 8));
        Assert.AreEqual((uint)bytes.Length, ReadU32(bytes, 12));
        Assert.AreEqual((ushort)GesSectionType.ProgramMetadata, ReadU16(bytes, 16));
        Assert.AreEqual((ushort)GesSectionFlags.Required, ReadU16(bytes, 18));
        Assert.AreEqual((ushort)1, ReadU16(bytes, 20));
        Assert.AreEqual((ushort)0, ReadU16(bytes, 22));
        Assert.AreEqual((uint)16, ReadU32(bytes, 24));
        Assert.AreEqual((ushort)GesSectionType.StringConstants, ReadU16(bytes, 44));
    }

    [TestMethod]
    public void CanonicalGoldenBinariesRemainByteStable()
    {
        var emptyHash = Sha256(GesProgramWriter.ToArray(
            new GesProgramBuilder().Compile(new GesCompileOptions
            {
                DebugInfo = GesDebugInfoOptions.None
            })));
        var runtimeHash = Sha256(Compile(GesDebugInfoOptions.None).Encode());
        var debugHash = Sha256(Compile(GesDebugInfoOptions.All).Encode());

        var multipleSources = new GesProgramBuilder()
            .AddScript("module first\nfunction plusOne(value) be value + 1", "first.ges")
            .AddScript("module first\non Start(value) { emit Done(value: plusOne(value: value)) }", "second.ges")
            .WithDebugInfo()
            .Compile();
        var multiHash = Sha256(multipleSources.Encode());
        Assert.AreEqual("58B9A2C558CFA5711E9967D6FFF5C3AAC99F2D4AE3DF59BA8CFEBADCAC6D53B5", emptyHash);
        Assert.AreEqual("2B7082CDE65F79533135A6B4FA54CB984B146FD84034DFD36C9E0459454B2050", runtimeHash);
        Assert.AreEqual("15A52F001A8F75A36EAFF7ECEAD9233268706038ED557CC3815CF2035188EDAC", debugHash);
        Assert.AreEqual("3F903994FAA14EE5AAF0F8C53415BFDB76F2C422444C431B18716B62C03D5D01", multiHash);
    }

    [TestMethod]
    public void PreserveKnownAndRuntimeOnlyDropUnknownPayloads()
    {
        var baseProgram = Compile(GesDebugInfoOptions.All);
        var bytes = GesProgramWriter.ToArray(CopyWithOpaque(baseProgram,
            [new GesOpaqueSection(0x8000, GesSectionFlags.None, 1, new byte[] { 9 }, 0)]));

        var known = GameEventScriptProgram.Read(bytes, new GesProgramReadOptions { Retention = GesProgramRetention.PreserveKnown });
        Assert.HasCount(0, known.OpaqueSections);
        Assert.IsNotNull(known.SourceMap);

        var runtime = GameEventScriptProgram.Read(bytes, new GesProgramReadOptions { Retention = GesProgramRetention.RuntimeOnly });
        Assert.HasCount(0, runtime.OpaqueSections);
        Assert.IsNull(runtime.DebugSymbols);
        Assert.IsNull(runtime.SourceMap);
        Assert.IsNull(runtime.SourceArchive);
        Assert.IsNull(runtime.BuildMetadata);
    }

    [TestMethod]
    public void ReservedSignatureSectionIsOpaqueAndHasNoTrustSemantics()
    {
        var program = CopyWithOpaque(Compile(GesDebugInfoOptions.None),
            [new GesOpaqueSection((ushort)GesSectionType.ReservedSignature, GesSectionFlags.None, 1, new byte[] { 7, 8, 9 }, 0)]);
        var bytes = program.Encode();
        var decoded = GameEventScriptProgram.Read(bytes);
        Assert.HasCount(1, decoded.OpaqueSections);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, decoded.OpaqueSections[0].RawPayload.ToArray());

        var section = FindSection(bytes, (ushort)GesSectionType.ReservedSignature);
        WriteU16(bytes, section + 2, (ushort)GesSectionFlags.Required);
        Assert.AreEqual(GesProgramFormatErrorCode.UnknownRequiredSection,
            Assert.ThrowsExactly<GesProgramFormatException>(() => GameEventScriptProgram.Read(bytes)).ErrorCode);
    }

    [TestMethod]
    public void DuplicateBindingIdsProduceStableError()
    {
        var program = new GesProgramBuilder()
            .AddScript("module duplicateids\non First { }\non Second { }")
            .Compile();
        var bytes = program.Encode();
        var bindings = FindSection(bytes, (ushort)GesSectionType.Bindings);
        var firstEntry = bindings + 12 + 4;
        var secondEntry = firstEntry + 20;
        WriteU16(bytes, secondEntry + 4, ReadU16(bytes, firstEntry + 4));

        var exception = Assert.ThrowsExactly<GesProgramFormatException>(() => GameEventScriptProgram.Read(bytes));
        Assert.AreEqual(GesProgramFormatErrorCode.DuplicateBindingId, exception.ErrorCode);
    }

    [TestMethod]
    public void ProgramValidatorEnforcesPortableCallableIdentity()
    {
        var program = new GesProgramBuilder()
            .AddScript("function resolve(unit, enemy) be unit + enemy\nfunction resolve(unit, collision) be unit * collision\non Start { emit Done(a: resolve(unit: 2, enemy: 3), b: resolve(unit: 2, collision: 3)) }")
            .Compile();
        var entries = program.Bindings.Entries.ToArray();
        var functionIndexes = entries.Select((entry, index) => (entry, index)).Where(item => item.entry.Kind == GesBinaryBindKind.Function).Select(item => item.index).ToArray();
        Assert.HasCount(2, functionIndexes);

        var first = entries[functionIndexes[0]];
        var second = entries[functionIndexes[1]];
        entries[functionIndexes[1]] = CopyBinding(second, argumentNames: first.ArgumentNames);
        var duplicateSignature = CopyWithBindings(program, entries);
        Assert.AreEqual(GesProgramFormatErrorCode.InvalidProgram,
            Assert.ThrowsExactly<GesProgramFormatException>(() => duplicateSignature.Validate()).ErrorCode);

        entries[functionIndexes[1]] = CopyBinding(second, kind: GesBinaryBindKind.Predicate);
        var overlappingKinds = CopyWithBindings(program, entries);
        Assert.AreEqual(GesProgramFormatErrorCode.InvalidProgram,
            Assert.ThrowsExactly<GesProgramFormatException>(() => overlappingKinds.Validate()).ErrorCode);
    }

    [TestMethod]
    public void ReaderFileLimitProducesStableError()
    {
        var bytes = Compile(GesDebugInfoOptions.None).Encode();
        var options = new GesProgramReadOptions
        {
            Limits = new GesProgramReadLimits { MaxFileBytes = bytes.Length - 1 }
        };
        Assert.AreEqual(GesProgramFormatErrorCode.FileTooLarge,
            Assert.ThrowsExactly<GesProgramFormatException>(() => GameEventScriptProgram.Read(bytes, options)).ErrorCode);
    }

    [TestMethod]
    public void UnsupportedKnownOptionalVersionUsesRetentionPolicy()
    {
        var bytes = Compile(GesDebugInfoOptions.All).Encode();
        var sourceMap = FindSection(bytes, (ushort)GesSectionType.SourceMap);
        WriteU16(bytes, sourceMap + 4, 2);

        var all = GameEventScriptProgram.Read(bytes);
        Assert.IsNull(all.SourceMap);
        Assert.IsTrue(all.OpaqueSections.ToArray().Any(section => section.SectionType == (ushort)GesSectionType.SourceMap));

        var known = GameEventScriptProgram.Read(bytes, new GesProgramReadOptions { Retention = GesProgramRetention.PreserveKnown });
        Assert.IsNull(known.SourceMap);
        Assert.HasCount(0, known.OpaqueSections);
    }

    [TestMethod]
    public void UnsupportedKnownOptionalCompressionUsesRetentionPolicy()
    {
        var bytes = Compile(GesDebugInfoOptions.All).Encode();
        var sourceMap = FindSection(bytes, (ushort)GesSectionType.SourceMap);
        WriteU16(bytes, sourceMap + 2, 0x0010);

        var all = GameEventScriptProgram.Read(bytes);
        Assert.IsNull(all.SourceMap);
        Assert.IsTrue(all.OpaqueSections.ToArray().Any(section =>
            section.SectionType == (ushort)GesSectionType.SourceMap &&
            ((ushort)section.Flags & (ushort)GesSectionFlags.CompressionMask) != 0));

        var known = GameEventScriptProgram.Read(bytes, new GesProgramReadOptions { Retention = GesProgramRetention.PreserveKnown });
        Assert.IsNull(known.SourceMap);
        Assert.HasCount(0, known.OpaqueSections);
    }

    [TestMethod]
    public void UnicodeSourcesUseUtf8ByteOffsetsAndRoundTrip()
    {
        const string source = "module unicode\n\non Start(name) {\n  let text be \"🙂 é \" + name\n  emit Done(text: text)\n}\n";
        var program = new GesProgramBuilder().AddScript(source, "ä/logic.ges").WithDebugInfo().Compile();
        var bytes = program.Encode();
        var sourceMapSection = FindSection(bytes, (ushort)GesSectionType.SourceMap);
        Assert.AreEqual((uint)1, ReadU32(bytes, sourceMapSection + 12));
        Assert.AreEqual((uint)Encoding.UTF8.GetByteCount("ä/logic.ges"), ReadU32(bytes, sourceMapSection + 16));
        var decoded = GameEventScriptProgram.Read(bytes);

        Assert.AreEqual(source, decoded.SourceArchive!.Sources[0].ResolveText());
        Assert.AreEqual((uint)Encoding.UTF8.GetByteCount(source), decoded.SourceMap!.Sources[0].SourceByteLength);
        Assert.IsGreaterThan(0, decoded.SourceMap.Entries.Count);
        foreach (var mapping in decoded.SourceMap.Entries)
        {
            var content = decoded.SourceArchive.Sources[0].Utf8Content;
            if (mapping.SourceStartByteOffset > 0 && mapping.SourceStartByteOffset < content.Count)
                Assert.AreNotEqual(0x80, content[(int)mapping.SourceStartByteOffset] & 0xC0);
        }
    }

    [TestMethod]
    [DataRow(GesDebugInfoOptions.DebugSymbols, true, false, false)]
    [DataRow(GesDebugInfoOptions.SourceMap, false, true, false)]
    [DataRow(GesDebugInfoOptions.SourceArchive, false, false, true)]
    public void OptionalDebugSectionsCanExistIndependently(GesDebugInfoOptions options, bool symbols, bool map, bool archive)
    {
        var decoded = GameEventScriptProgram.Read(Compile(options).Encode());
        Assert.AreEqual(symbols, decoded.DebugSymbols is not null);
        Assert.AreEqual(map, decoded.SourceMap is not null);
        Assert.AreEqual(archive, decoded.SourceArchive is not null);
    }

    [TestMethod]
    public void CompilerIdentityDoesNotChangeRequiredRuntimeSections()
    {
        var csharp = Compile(GesDebugInfoOptions.None);
        var swift = CopyWithBuildMetadata(csharp, new GesBuildMetadataSegment("steph.ges.compiler.swift", "0.1.0"));
        var csharpBytes = csharp.Encode();
        var swiftBytes = swift.Encode();

        Assert.IsFalse(csharpBytes.SequenceEqual(swiftBytes));
        CollectionAssert.AreEqual(ReadRequiredSections(csharpBytes), ReadRequiredSections(swiftBytes));
    }

    [TestMethod]
    public void DumperUsesEmbeddedSourceAndInterleavesMappedLines()
    {
        var decoded = GameEventScriptProgram.Read(Compile(GesDebugInfoOptions.All).Encode());
        var dump = decoded.Dump(includeInstructionAddresses: true);

        StringAssert.Contains(dump, ".region \"Source: main.ges\"\n\n.segment source \"main.ges\"\n\n");
        StringAssert.Contains(dump, ".source-line \"main.ges\" 4 |   let result be value + 1");
        StringAssert.Contains(dump, "let result be value + 1");
        StringAssert.Contains(dump, "Add r1(result), r0(value), r2");
        var lines = dump.ReplaceLineEndings("\n").Split('\n');
        Assert.AreEqual(1, lines.Count(line => line.StartsWith(".source-line \"main.ges\" 4 |", StringComparison.Ordinal)));
        Assert.AreEqual(5, lines.Count(line => line.StartsWith(".region-end \"", StringComparison.Ordinal)));
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(".region \"", StringComparison.Ordinal))
            {
                Assert.IsTrue(index > 0 && lines[index - 1] == "// -------------------------------------------------------------------------------", $"Expected a separator before: {lines[index]}");
                Assert.IsTrue(index + 1 < lines.Length && lines[index + 1].Length == 0, $"Expected a blank line after: {lines[index]}");
            }
            else if (lines[index].StartsWith(".region-end \"", StringComparison.Ordinal))
            {
                Assert.IsTrue(index > 0 && lines[index - 1].Length == 0, $"Expected a blank line before: {lines[index]}");
                Assert.IsTrue(index + 1 < lines.Length && lines[index + 1] == "// -------------------------------------------------------------------------------", $"Expected a separator after: {lines[index]}");
            }
        }

        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(".source-line ", StringComparison.Ordinal))
            {
                Assert.IsTrue(index > 0 && lines[index - 1].Length == 0, $"Expected a blank line before: {lines[index]}");
            }
        }
    }

    private static GameEventScriptProgram Compile(GesDebugInfoOptions options)
        => new GesProgramBuilder()
            .AddScript("module binaryone\n\non Start(value) {\n  let result be value + 1\n  emit Done(value: result)\n}\n", "main.ges")
            .WithProgramVersion(42)
            .WithDebugInfo(options)
            .Compile();

    private static GameEventScriptProgram CopyWithOpaque(GameEventScriptProgram program, IReadOnlyList<GesOpaqueSection> opaque)
        => new(program.FormatVersion, program.ModuleName, program.ProgramVersion, program.RequiredRegisterCount, program.RequiredCallStackDepth,
            program.StringConstants, program.UInt16IndexLists, program.Bindings, program.Code, program.DebugSymbols, program.SourceMap,
            program.SourceArchive, program.BuildMetadata, opaque);

    private static GameEventScriptProgram CopyWithBuildMetadata(GameEventScriptProgram program, GesBuildMetadataSegment metadata)
        => new(program.FormatVersion, program.ModuleName, program.ProgramVersion, program.RequiredRegisterCount, program.RequiredCallStackDepth,
            program.StringConstants, program.UInt16IndexLists, program.Bindings, program.Code, program.DebugSymbols, program.SourceMap,
            program.SourceArchive, metadata, program.OpaqueSections);

    private static GameEventScriptProgram CopyWithBindings(GameEventScriptProgram program, IReadOnlyList<GesBindingSegment.Entry> entries)
        => new(program.FormatVersion, program.ModuleName, program.ProgramVersion, program.RequiredRegisterCount, program.RequiredCallStackDepth,
            program.StringConstants, program.UInt16IndexLists, new GesBindingSegment(entries), program.Code, program.DebugSymbols, program.SourceMap,
            program.SourceArchive, program.BuildMetadata, program.OpaqueSections);

    private static GesBindingSegment.Entry CopyBinding(
        GesBindingSegment.Entry source,
        GesBinaryBindKind? kind = null,
        IReadOnlyList<ushort>? argumentNames = null)
        => new(kind ?? source.Kind, source.Name, argumentNames ?? source.ArgumentNames, source.EntryAddress, source.Id, source.RequiredTags, source.ExcludedTags, source.RequiredRegisterCount, source.RequiredCallStackDepth);

    private static byte[] ReadRequiredSections(byte[] bytes)
        => SplitSections(bytes)
            .Where(section => (ReadU16(section, 2) & (ushort)GesSectionFlags.Required) != 0)
            .SelectMany(section => section)
            .ToArray();

    private static List<byte[]> SplitSections(byte[] bytes)
    {
        var result = new List<byte[]>();
        var offset = 16;
        while (offset < bytes.Length)
        {
            var length = checked((int)ReadU32(bytes, offset + 8));
            var section = bytes.AsSpan(offset, 12 + length).ToArray();
            result.Add(section);
            offset += section.Length;
        }
        return result;
    }

    private static int FindSection(byte[] bytes, ushort type)
    {
        var offset = 16;
        while (offset < bytes.Length)
        {
            if (ReadU16(bytes, offset) == type) return offset;
            offset += 12 + checked((int)ReadU32(bytes, offset + 8));
        }
        Assert.Fail($"Section 0x{type:X4} was not found.");
        return -1;
    }

    private static ushort ReadU16(IReadOnlyList<byte> bytes, int offset) => (ushort)(bytes[offset] | bytes[offset + 1] << 8);
    private static uint ReadU32(IReadOnlyList<byte> bytes, int offset) => (uint)(ReadU16(bytes, offset) | ReadU16(bytes, offset + 2) << 16);
    private static void WriteU16(IList<byte> bytes, int offset, ushort value) { bytes[offset] = (byte)value; bytes[offset + 1] = (byte)(value >> 8); }
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
