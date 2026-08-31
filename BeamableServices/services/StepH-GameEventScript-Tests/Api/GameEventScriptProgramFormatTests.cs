using System.Text;
using System.Security.Cryptography;
using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Api;

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
    public void CanonicalBinaryRoundTripsWithoutKnownRawSections()
    {
        var program = Compile(GameEventScriptDebugInfoOptions.All);
        var bytes = GameEventScriptProgramWriter.ToArray(program);
        var decoded = GameEventScriptProgramReader.Read(bytes);
        var rewritten = GameEventScriptProgramWriter.ToArray(decoded);

        CollectionAssert.AreEqual(bytes, rewritten);
        Assert.AreEqual((ulong)42, decoded.ProgramVersion);
        Assert.AreEqual("steph.ges.compiler.csharp", decoded.BuildMetadata?.CompilerId);
        Assert.IsNotNull(decoded.DebugSymbols);
        Assert.IsNotNull(decoded.SourceMap);
        Assert.IsNotNull(decoded.SourceArchive);
        Assert.HasCount(0, decoded.OpaqueSections);
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
    public void NonCanonicalRequiredSectionOrderReadsAndRewritesCanonically()
    {
        var canonical = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None));
        var sections = SplitSections(canonical);
        sections.Reverse();
        var reordered = JoinSections(canonical[..16], sections);

        var decoded = GameEventScriptProgramReader.Read(reordered);
        CollectionAssert.AreEqual(canonical, GameEventScriptProgramWriter.ToArray(decoded));
    }

    [TestMethod]
    public void PreserveAllRetainsUnknownOptionalPayloadAndRelativeOrder()
    {
        var baseProgram = Compile(GameEventScriptDebugInfoOptions.None);
        var program = CopyWithOpaque(baseProgram,
        [
            new GameEventScriptOpaqueSection(0x8002, GameEventScriptSectionFlags.None, 7, new byte[] { 1, 2 }, 9),
            new GameEventScriptOpaqueSection(0x8001, GameEventScriptSectionFlags.None, 3, new byte[] { 4, 5, 6 }, 2)
        ]);
        var decoded = GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(program));

        Assert.HasCount(2, decoded.OpaqueSections);
        Assert.AreEqual((ushort)0x8001, decoded.OpaqueSections[0].SectionType);
        Assert.AreEqual((ushort)0x8002, decoded.OpaqueSections[1].SectionType);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, decoded.OpaqueSections[0].RawPayload.ToArray());
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
    public void UnknownRequiredSectionIsRejectedWithStableError()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(CopyWithOpaque(Compile(GameEventScriptDebugInfoOptions.None),
            [new GameEventScriptOpaqueSection(0x8000, GameEventScriptSectionFlags.None, 1, new byte[] { 9 }, 0)]));
        var sectionOffset = FindSection(bytes, 0x8000);
        WriteU16(bytes, sectionOffset + 2, (ushort)GameEventScriptSectionFlags.Required);

        var exception = Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(bytes));
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.UnknownRequiredSection, exception.ErrorCode);
        Assert.AreEqual((ushort)0x8000, exception.SectionType);
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
    public void MissingDuplicateAndInvalidReferencesProduceStableErrors()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None));
        var sections = SplitSections(bytes);
        var missing = JoinSections(bytes[..16], sections.Where(section => ReadU16(section, 0) != (ushort)GameEventScriptSectionType.Code).ToArray());
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.MissingRequiredSection,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(missing)).ErrorCode);

        var stringSection = sections.Single(section => ReadU16(section, 0) == (ushort)GameEventScriptSectionType.StringConstants);
        var duplicate = JoinSections(bytes[..16], sections.Concat([stringSection]).ToArray());
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.DuplicateSection,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(duplicate)).ErrorCode);

        var invalidReference = (byte[])bytes.Clone();
        var bindings = FindSection(invalidReference, (ushort)GameEventScriptSectionType.Bindings);
        WriteU16(invalidReference, bindings + 20, 0xFFFE);
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.InvalidStringIndex,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(invalidReference)).ErrorCode);
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
    public void InvalidUtf8AndReaderLimitsProduceStableErrors()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None));
        var invalidUtf8 = (byte[])bytes.Clone();
        var strings = FindSection(invalidUtf8, (ushort)GameEventScriptSectionType.StringConstants);
        invalidUtf8[strings + 20] = 0xFF;
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.InvalidUtf8,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(invalidUtf8)).ErrorCode);

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
    public void CompressionOnRequiredSectionIsRejected()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None));
        WriteU16(bytes, 18, (ushort)(GameEventScriptSectionFlags.Required | (GameEventScriptSectionFlags)0x0010));
        var exception = Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(bytes));
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.UnsupportedCompression, exception.ErrorCode);
    }

    [TestMethod]
    public void CorruptHeaderAndTruncatedPayloadProduceFormatErrors()
    {
        var bytes = GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.None));
        var badMagic = (byte[])bytes.Clone();
        badMagic[0] = (byte)'X';
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.InvalidMagic,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(badMagic)).ErrorCode);

        var truncated = bytes[..^1];
        WriteU32(truncated, 12, (uint)truncated.Length);
        Assert.AreEqual(GameEventScriptProgramFormatErrorCode.TruncatedSectionPayload,
            Assert.ThrowsExactly<GameEventScriptProgramFormatException>(() => GameEventScriptProgramReader.Read(truncated)).ErrorCode);
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
    public void ReadProgramLoadsAndExecutesLikeCompiledProgram()
    {
        var decoded = GameEventScriptProgramReader.Read(GameEventScriptProgramWriter.ToArray(Compile(GameEventScriptDebugInfoOptions.All)));
        var received = new List<long>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(decoded);
        host.Subscribe("Done", new[] { "value" }, (message, _) => received.Add(message.Arguments.GetAsInteger(0)));
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Start", ("value", GesValue.GesInteger(2)))));
        host.RunToCompletion();
        CollectionAssert.AreEqual(new long[] { 3 }, received);
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

        StringAssert.Contains(dump, "// Source: main.ges");
        StringAssert.Contains(dump, "// main.ges:");
        StringAssert.Contains(dump, "let result be value + 1");
        StringAssert.Contains(dump, "Add r1(result), r0(value), r2");
        Assert.AreEqual(1, dump.ReplaceLineEndings("\n").Split('\n').Count(line => line == "// main.ges:4"));
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

    private static byte[] JoinSections(ReadOnlySpan<byte> header, IReadOnlyList<byte[]> sections)
    {
        var length = 16 + sections.Sum(section => section.Length);
        var result = new byte[length];
        header.CopyTo(result);
        WriteU32(result, 12, (uint)length);
        var offset = 16;
        foreach (var section in sections) { section.CopyTo(result, offset); offset += section.Length; }
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
    private static void WriteU32(IList<byte> bytes, int offset, uint value) { WriteU16(bytes, offset, (ushort)value); WriteU16(bytes, offset + 2, (ushort)(value >> 16)); }
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
