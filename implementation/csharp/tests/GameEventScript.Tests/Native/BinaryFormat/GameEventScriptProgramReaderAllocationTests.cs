// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.BinaryFormat;

[TestClass]
public sealed class GameEventScriptProgramReaderAllocationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    [DataRow(8)]
    [DataRow(1000)]
    public void SharedBindingListsStayWithinTheCSharpReaderAllocationBudget(int argumentCount)
    {
        const int bindingCount = 2000;
        // This C# regression budget allows 1 MiB for a roughly 40 KiB fixture.
        // It permits binding objects and validation scratch space while rejecting
        // multi-megabyte copying of the same argument list for every binding.
        const long maximumAllocatedBytes = 1024 * 1024;
        var bytes = CreateSharedListFixture(bindingCount, argumentCount);
        _ = GameEventScriptProgramReader.Read(bytes);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var program = GameEventScriptProgramReader.Read(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.WriteLine($"Reading {bytes.Length} bytes with {bindingCount} references to one {argumentCount}-element list allocated {allocated} bytes; C# budget: {maximumAllocatedBytes} bytes.");

        Assert.HasCount(bindingCount, program.Bindings.Entries);
        Assert.HasCount(1, program.UInt16IndexLists.Slices);
        Assert.AreEqual(argumentCount, program.Bindings.Entries[0].ArgumentNames.Count);
        Assert.AreEqual(argumentCount, program.Bindings.Entries[^1].ArgumentNames.Count);
        Assert.AreEqual("_", program.StringConstants.Resolve(program.Bindings.Entries[^1].ArgumentNames[^1]));
        Assert.IsLessThanOrEqualTo(maximumAllocatedBytes, allocated,
            $"Reading {bytes.Length} bytes with {bindingCount} references to one {argumentCount}-element list allocated {allocated} bytes; C# budget: {maximumAllocatedBytes} bytes.");
    }

    private static byte[] CreateSharedListFixture(int bindingCount, int argumentCount)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("GESB"u8);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write(16u);
        writer.Write(0u);
        WriteSection(writer, GameEventScriptSectionType.ProgramMetadata, payload =>
        {
            payload.Write((ushort)0);
            payload.Write((ushort)0);
            payload.Write((ushort)0);
            payload.Write((ushort)0);
            payload.Write(0UL);
        });
        WriteSection(writer, GameEventScriptSectionType.StringConstants, payload =>
        {
            payload.Write(3u);
            foreach (var value in new[] { "allocation", "core.noop", "_" })
            {
                var utf8 = Encoding.UTF8.GetBytes(value);
                payload.Write((uint)utf8.Length);
                payload.Write(utf8);
            }
        });
        WriteSection(writer, GameEventScriptSectionType.UInt16IndexLists, payload =>
        {
            payload.Write(1u);
            payload.Write((uint)argumentCount);
            for (var index = 0; index < argumentCount; index++) payload.Write((ushort)2);
        });
        WriteSection(writer, GameEventScriptSectionType.Bindings, payload =>
        {
            payload.Write((uint)bindingCount);
            for (var index = 0; index < bindingCount; index++)
            {
                payload.Write((byte)GameEventScriptBinaryBindKind.ExtensionCall);
                payload.Write((byte)0);
                payload.Write((ushort)index);
                payload.Write((ushort)1);
                payload.Write((ushort)0); // Every binding references the same list.
                payload.Write(ushort.MaxValue);
                payload.Write(ushort.MaxValue);
                payload.Write(ushort.MaxValue);
                payload.Write((ushort)0);
                payload.Write((ushort)0);
                payload.Write((ushort)0);
            }
        });
        WriteSection(writer, GameEventScriptSectionType.Code, payload => payload.Write(0u));
        stream.Position = 12;
        writer.Write((uint)stream.Length);
        return stream.ToArray();
    }

    private static void WriteSection(BinaryWriter writer, GameEventScriptSectionType type, Action<BinaryWriter> writePayload)
    {
        using var stream = new MemoryStream();
        using var payload = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writePayload(payload);
        writer.Write((ushort)type);
        writer.Write((ushort)GameEventScriptSectionFlags.Required);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write((uint)stream.Length);
        writer.Write(stream.ToArray());
    }
}
