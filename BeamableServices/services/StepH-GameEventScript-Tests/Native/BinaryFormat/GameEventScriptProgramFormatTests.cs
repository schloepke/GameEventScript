using System.Security.Cryptography;
using System.Text;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Native.BinaryFormat;

[TestClass]
public sealed class GameEventScriptProgramFormatTests
{
    [TestMethod]
    public void CompilerGeneratesAllDebugSectionsByDefault()
    {
        var program = GameEventScriptManager.CreateScriptBuilder()
            .AddScript("module DefaultDebug\n\non Start { emit Done(value: 1) }", "default-debug.ges")
            .Compile();

        Assert.IsNotNull(program.DebugSymbols);
        Assert.IsNotNull(program.SourceMap);
        Assert.IsNotNull(program.SourceArchive);
        Assert.AreEqual("default-debug.ges", program.SourceArchive.Sources[0].SourceName);
    }

    [TestMethod]
    public void FileAndSectionFramingIsLittleEndianAndUnpadded()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None));
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("GESB"), bytes[..4]);
        Assert.AreEqual((ushort)1, ReadU16(bytes, 4));
        Assert.AreEqual((ushort)0, ReadU16(bytes, 6));
        Assert.AreEqual((uint)16, ReadU32(bytes, 8));
        Assert.AreEqual((uint)bytes.Length, ReadU32(bytes, 12));
        Assert.AreEqual((ushort)GameEventScriptSectionType.ProgramMetadata, ReadU16(bytes, 16));
        Assert.AreEqual((ushort)GameEventScriptSectionFlags.Required, ReadU16(bytes, 18));
        Assert.AreEqual((ushort)1, ReadU16(bytes, 20));
        Assert.AreEqual((ushort)0, ReadU16(bytes, 22));
        Assert.AreEqual((uint)16, ReadU32(bytes, 24));
        Assert.AreEqual((ushort)GameEventScriptSectionType.StringConstants, ReadU16(bytes, 44));
    }

    [TestMethod]
    public void CanonicalGoldenBinariesRemainByteStable()
    {
        var emptyHash = Sha256(GameEventScriptProgramWriter.ToArray(
            GameEventScriptManager.CreateScriptBuilder().Compile(new GameEventScriptCompileOptions
            {
                DebugInfo = GameEventScriptDebugInfoOptions.None
            })));
        var runtimeHash = Sha256(GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None)));
        var debugHash = Sha256(GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.All)));

        var multipleSources = GameEventScriptManager.CreateScriptBuilder()
            .AddScript("module First\nfunction plusOne(value) be value + 1", "first.ges")
            .AddScript("module Second\non Start(value) { emit Done(value: plusOne(value: value)) }", "second.ges")
            .WithDebugInfo()
            .Compile();
        var multiHash = Sha256(GameEventScriptProgramWriter.ToArray(multipleSources));
        Assert.AreEqual("C550BCC23BD72C059296924C229DA6C582C2CBD7BCF2B3B3356E12F04101370F", emptyHash);
        Assert.AreEqual("F1E5568B144107B148D8C03E7972B5C540628B5CEF40A1DDB54A4998CE196DFE", runtimeHash);
        Assert.AreEqual("E25E8690CF93DFE347E06C2EA014957F34D72FFC752328BCFBE11BC2E5BA0DA8", debugHash);
        Assert.AreEqual("D531D9FC08F7E79D8DF91F1E211B86DE2FC41F9A1EC067B7ABDCE0DC9B8524F6", multiHash);
    }

    [TestMethod]
    public void PreserveKnownAndRuntimeOnlyDropUnknownPayloads()
    {
        var baseProgram = Compile(GameEventScriptDebugInfoOptions.All);
        var bytes = GameEventScriptProgramWriter.ToArray(CopyWithOpaque(baseProgram,
            [new GameEventScriptOpaqueSection(0x8000, GameEventScriptSectionFlags.None, 1, new byte[] { 9 }, 0)]));

        var known = GameEventScriptProgramReader.Read(bytes, new GameEventScriptProgramReadOptions { Retention = GameEventScriptProgramRetention.PreserveKnown });
        Assert.HasCount(0, known.OpaqueSections);
        Assert.IsNotNull(known.SourceMap);

        var runtime = GameEventScriptProgramReader.Read(bytes, new GameEventScriptProgramReadOptions { Retention = GameEventScriptProgramRetention.RuntimeOnly });
        Assert.HasCount(0, runtime.OpaqueSections);
        Assert.IsNull(runtime.DebugSymbols);
        Assert.IsNull(runtime.SourceMap);
        Assert.IsNull(runtime.SourceArchive);
        Assert.IsNull(runtime.BuildMetadata);
    }

    [TestMethod]
    public void ReservedSignatureSectionIsOpaqueAndHasNoTrustSemantics()
    {
        var program = CopyWithOpaque(Compile(GameEventScriptDebugInfoOptions.None),
            [new GameEventScriptOpaqueSection((ushort)GameEventScriptSectionType.ReservedSignature, GameEventScriptSectionFlags.None, 1, new byte[] { 7, 8, 9 }, 0)]);
        var bytes = GameEventScriptProgramWriter.ToArray(program);
        var decoded = GameEventScriptProgramReader.Read(bytes);
        Assert.HasCount(1, decoded.OpaqueSections);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, decoded.OpaqueSections[0].RawPayload.ToArray());

        var section = FindSection(bytes, (ushort)GameEventScriptSectionType.ReservedSignature);
        WriteU16(bytes, section + 2, (ushort)GameEventScriptSectionFlags.Required);
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.UnknownRequiredSection,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(bytes)).ErrorCode);
    }

    [TestMethod]
    public void DuplicateBindingIdsProduceStableError()
    {
        var program = GameEventScriptManager.CreateScriptBuilder()
            .AddScript("module DuplicateIds\non First { }\non Second { }")
            .Compile();
        var bytes = GameEventScriptProgramWriter.ToArray(program);
        var bindings = FindSection(bytes, (ushort)GameEventScriptSectionType.Bindings);
        var firstEntry = bindings + 12 + 4;
        var secondEntry = firstEntry + 20;
        WriteU16(bytes, secondEntry + 4, ReadU16(bytes, firstEntry + 4));

        var exception = Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(bytes));
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.DuplicateBindingId, exception.ErrorCode);
    }

    [TestMethod]
    public void ProgramValidatorEnforcesPortableCallableIdentity()
    {
        var program = GameEventScriptManager.CreateScriptBuilder()
            .AddScript("function resolve(unit, enemy) be unit + enemy\nfunction resolve(unit, collision) be unit * collision\non Start { emit Done(a: resolve(unit: 2, enemy: 3), b: resolve(unit: 2, collision: 3)) }")
            .Compile();
        var entries = program.Bindings.Entries.ToArray();
        var functionIndexes = entries.Select((entry, index) => (entry, index)).Where(item => item.entry.Kind == GameEventScriptBinaryBindKind.Function).Select(item => item.index).ToArray();
        Assert.HasCount(2, functionIndexes);

        var first = entries[functionIndexes[0]];
        var second = entries[functionIndexes[1]];
        entries[functionIndexes[1]] = CopyBinding(second, argumentNames: first.ArgumentNames);
        var duplicateSignature = CopyWithBindings(program, entries);
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.InvalidProgram,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramValidator.Validate(duplicateSignature)).ErrorCode);

        entries[functionIndexes[1]] = CopyBinding(second, kind: GameEventScriptBinaryBindKind.Predicate);
        var overlappingKinds = CopyWithBindings(program, entries);
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.InvalidProgram,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramValidator.Validate(overlappingKinds)).ErrorCode);
    }

    [TestMethod]
    public void ReaderFileLimitProducesStableError()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None));
        var options = new GameEventScriptProgramReadOptions
        {
            Limits = new GameEventScriptProgramReadLimits { MaxFileBytes = bytes.Length - 1 }
        };
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.FileTooLarge,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(bytes, options)).ErrorCode);
    }

    [TestMethod]
    public void UnsupportedKnownOptionalVersionUsesRetentionPolicy()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.All));
        var sourceMap = FindSection(bytes, (ushort)GameEventScriptSectionType.SourceMap);
        WriteU16(bytes, sourceMap + 4, 2);

        var all = GameEventScriptProgramReader.Read(bytes);
        Assert.IsNull(all.SourceMap);
        Assert.IsTrue(all.OpaqueSections.ToArray().Any(section => section.SectionType == (ushort)GameEventScriptSectionType.SourceMap));

        var known = GameEventScriptProgramReader.Read(bytes, new GameEventScriptProgramReadOptions { Retention = GameEventScriptProgramRetention.PreserveKnown });
        Assert.IsNull(known.SourceMap);
        Assert.HasCount(0, known.OpaqueSections);
    }

    [TestMethod]
    public void UnsupportedKnownOptionalCompressionUsesRetentionPolicy()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.All));
        var sourceMap = FindSection(bytes, (ushort)GameEventScriptSectionType.SourceMap);
        WriteU16(bytes, sourceMap + 2, 0x0010);

        var all = GameEventScriptProgramReader.Read(bytes);
        Assert.IsNull(all.SourceMap);
        Assert.IsTrue(all.OpaqueSections.ToArray().Any(section =>
            section.SectionType == (ushort)GameEventScriptSectionType.SourceMap &&
            ((ushort)section.Flags & (ushort)GameEventScriptSectionFlags.CompressionMask) != 0));

        var known = GameEventScriptProgramReader.Read(bytes, new GameEventScriptProgramReadOptions { Retention = GameEventScriptProgramRetention.PreserveKnown });
        Assert.IsNull(known.SourceMap);
        Assert.HasCount(0, known.OpaqueSections);
    }

    [TestMethod]
    public void UnicodeSourcesUseUtf8ByteOffsetsAndRoundTrip()
    {
        const string source = "module Unicode\n\non Start(name) {\n  let text be \"🙂 é \" + name\n  emit Done(text: text)\n}\n";
        var program = GameEventScriptManager.CreateScriptBuilder().AddScript(source, "ä/logic.ges").WithDebugInfo().Compile();
        var bytes = GameEventScriptProgramWriter.ToArray(program);
        var sourceMapSection = FindSection(bytes, (ushort)GameEventScriptSectionType.SourceMap);
        Assert.AreEqual((uint)1, ReadU32(bytes, sourceMapSection + 12));
        Assert.AreEqual((uint)Encoding.UTF8.GetByteCount("ä/logic.ges"), ReadU32(bytes, sourceMapSection + 16));
        var decoded = GameEventScriptProgramReader.Read(bytes);

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
    [DataRow(GameEventScriptDebugInfoOptions.DebugSymbols, true, false, false)]
    [DataRow(GameEventScriptDebugInfoOptions.SourceMap, false, true, false)]
    [DataRow(GameEventScriptDebugInfoOptions.SourceArchive, false, false, true)]
    public void OptionalDebugSectionsCanExistIndependently(GameEventScriptDebugInfoOptions options, bool symbols, bool map, bool archive)
    {
        var decoded = GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(Compile(options)));
        Assert.AreEqual(symbols, decoded.DebugSymbols is not null);
        Assert.AreEqual(map, decoded.SourceMap is not null);
        Assert.AreEqual(archive, decoded.SourceArchive is not null);
    }

    [TestMethod]
    public void CompilerIdentityDoesNotChangeRequiredRuntimeSections()
    {
        var csharp = Compile(GameEventScriptDebugInfoOptions.None);
        var swift = CopyWithBuildMetadata(csharp, new GameEventScriptBuildMetadataSegment("steph.ges.compiler.swift", "0.1.0"));
        var csharpBytes = GameEventScriptProgramWriter.ToArray(csharp);
        var swiftBytes = GameEventScriptProgramWriter.ToArray(swift);

        Assert.IsFalse(csharpBytes.SequenceEqual(swiftBytes));
        CollectionAssert.AreEqual(ReadRequiredSections(csharpBytes), ReadRequiredSections(swiftBytes));
    }

    [TestMethod]
    public void DumperUsesEmbeddedSourceAndInterleavesMappedLines()
    {
        var decoded = GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.All)));
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

    private static GameEventScriptProgram Compile(GameEventScriptDebugInfoOptions options)
        => GameEventScriptManager.CreateScriptBuilder()
            .AddScript("module BinaryOne\n\non Start(value) {\n  let result be value + 1\n  emit Done(value: result)\n}\n", "main.ges")
            .WithProgramVersion(42)
            .WithDebugInfo(options)
            .Compile();

    private static GameEventScriptProgram CopyWithOpaque(GameEventScriptProgram program, IReadOnlyList<GameEventScriptOpaqueSection> opaque)
        => new(program.FormatVersion, program.ModuleName, program.ProgramVersion, program.RequiredRegisterCount, program.RequiredCallStackDepth,
            program.StringConstants, program.UInt16IndexLists, program.Bindings, program.Code, program.DebugSymbols, program.SourceMap,
            program.SourceArchive, program.BuildMetadata, opaque);

    private static GameEventScriptProgram CopyWithBuildMetadata(GameEventScriptProgram program, GameEventScriptBuildMetadataSegment metadata)
        => new(program.FormatVersion, program.ModuleName, program.ProgramVersion, program.RequiredRegisterCount, program.RequiredCallStackDepth,
            program.StringConstants, program.UInt16IndexLists, program.Bindings, program.Code, program.DebugSymbols, program.SourceMap,
            program.SourceArchive, metadata, program.OpaqueSections);

    private static GameEventScriptProgram CopyWithBindings(GameEventScriptProgram program, IReadOnlyList<GameEventScriptBindingSegment.GameEventScriptBinaryBindEntry> entries)
        => new(program.FormatVersion, program.ModuleName, program.ProgramVersion, program.RequiredRegisterCount, program.RequiredCallStackDepth,
            program.StringConstants, program.UInt16IndexLists, new GameEventScriptBindingSegment(entries), program.Code, program.DebugSymbols, program.SourceMap,
            program.SourceArchive, program.BuildMetadata, program.OpaqueSections);

    private static GameEventScriptBindingSegment.GameEventScriptBinaryBindEntry CopyBinding(
        GameEventScriptBindingSegment.GameEventScriptBinaryBindEntry source,
        GameEventScriptBinaryBindKind? kind = null,
        IReadOnlyList<ushort>? argumentNames = null)
        => new(kind ?? source.Kind, source.Name, argumentNames ?? source.ArgumentNames, source.EntryAddress, source.Id, source.RequiredTags, source.ExcludedTags, source.RequiredRegisterCount, source.RequiredCallStackDepth);

    private static byte[] ReadRequiredSections(byte[] bytes)
        => SplitSections(bytes)
            .Where(section => (ReadU16(section, 2) & (ushort)GameEventScriptSectionFlags.Required) != 0)
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
