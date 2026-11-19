// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Vm;

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
        _ = GameEventScriptProgram.Read(bytes);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var program = GameEventScriptProgram.Read(bytes);
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

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    public void HandlersNamingOneTagAlternativesListShareItsGroups()
    {
        const int handlerCount = 512;
        const int tagCount = 2048;
        // Reading, validating and sharing one 2048-tag group for 512 handlers takes about 0.8 MiB; a group copy per handler adds over 2 MiB,
        // and resolving its tag names per handler over 100 MiB.
        const long maximumAllocatedBytes = 1536 * 1024;
        var tags = string.Join(" | ", Enumerable.Range(0, tagCount).Select(index => "#t" + index));
        var source = "module shared\n" + string.Concat(Enumerable.Range(0, handlerCount).Select(index => $"on Probe{index}() matching {tags} {{}}\n"));
        var bytes = new GesProgramBuilder().AddScript(source, "shared.ges").Compile(new GesCompileOptions { DebugInfo = GesDebugInfoOptions.None }).Encode();
        _ = GameEventScriptProgram.Read(bytes);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var program = GameEventScriptProgram.Read(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.WriteLine($"Reading {bytes.Length} bytes with {handlerCount} handlers naming one {tagCount}-tag group allocated {allocated} bytes; C# budget: {maximumAllocatedBytes} bytes.");
        var handlers = program.Bindings.Entries.Where(entry => entry.RequiredTagAlternatives.Count > 0).ToArray();
        Assert.HasCount(handlerCount, handlers);
        Assert.AreEqual(tagCount, handlers[^1].RequiredTagAlternatives[0].Count);
        Assert.AreSame(handlers[0].RequiredTagAlternatives.Storage, handlers[^1].RequiredTagAlternatives.Storage);
        Assert.IsLessThanOrEqualTo(maximumAllocatedBytes, allocated,
            $"Reading {bytes.Length} bytes with {handlerCount} handlers naming one {tagCount}-tag group allocated {allocated} bytes; C# budget: {maximumAllocatedBytes} bytes.");
    }

    [TestMethod]
    public void LinkedHandlersNamingOneTagAlternativesListShareTheirTagNames()
    {
        var source = "module shared\n" + string.Concat(Enumerable.Range(0, 8).Select(index => $"on Probe{index}() matching #a | #b, #c | #d {{}}\n"));
        var bytes = new GesProgramBuilder().AddScript(source, "shared.ges").Compile(new GesCompileOptions { DebugInfo = GesDebugInfoOptions.None }).Encode();
        var linked = new GesLinkedProgram(GameEventScriptProgram.Read(bytes), GesEmptyExtensionRegistry.Instance, GesEmptyExternalTypeRegistry.Instance);

        var handlers = linked.Handlers.Where(handler => handler.RequiredTagAlternatives is not null).ToArray();
        Assert.HasCount(8, handlers);
        Assert.AreEqual("d", handlers[^1].RequiredTagAlternatives![1][1]);
        foreach (var handler in handlers) Assert.AreSame(handlers[0].RequiredTagAlternatives, handler.RequiredTagAlternatives);
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
        WriteSection(writer, GesSectionType.ProgramMetadata, payload =>
        {
            payload.Write((ushort)0);
            payload.Write((ushort)0);
            payload.Write((ushort)0);
            payload.Write((ushort)0);
            payload.Write(0UL);
        });
        WriteSection(writer, GesSectionType.StringConstants, payload =>
        {
            payload.Write(3u);
            foreach (var value in new[] { "allocation", "core.noop", "_" })
            {
                var utf8 = Encoding.UTF8.GetBytes(value);
                payload.Write((uint)utf8.Length);
                payload.Write(utf8);
            }
        });
        WriteSection(writer, GesSectionType.UInt16IndexLists, payload =>
        {
            payload.Write(1u);
            payload.Write((uint)argumentCount);
            for (var index = 0; index < argumentCount; index++) payload.Write((ushort)2);
        });
        WriteSection(writer, GesSectionType.Bindings, payload =>
        {
            payload.Write((uint)bindingCount);
            for (var index = 0; index < bindingCount; index++)
            {
                payload.Write((byte)GesBinaryBindKind.ExtensionCall);
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
        WriteSection(writer, GesSectionType.Code, payload => payload.Write(0u));
        stream.Position = 12;
        writer.Write((uint)stream.Length);
        return stream.ToArray();
    }

    private static void WriteSection(BinaryWriter writer, GesSectionType type, Action<BinaryWriter> writePayload)
    {
        using var stream = new MemoryStream();
        using var payload = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writePayload(payload);
        writer.Write((ushort)type);
        writer.Write((ushort)GesSectionFlags.Required);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write((uint)stream.Length);
        writer.Write(stream.ToArray());
    }
}
