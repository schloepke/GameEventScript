// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.BinaryFormat;

// Untrusted Programs may reference one long list from many instructions. Validation must stay linear in the list and code
// sizes; scanning the list per reference took hours for files of about a megabyte.
[TestClass]
public sealed class GameEventScriptProgramValidationCostTests
{
    private const int ListLength = 20_000;
    private const int References = 4_000;

    [TestMethod]
    public void ManyReferencesToOneLongRegisterListAreRejectedQuickly()
    {
        var program = new GesProgramBuilder()
            .AddScript("module longlist\non Start(values) { for key, value in values { emit Pair(key: key, value: value) } }", "longlist.ges")
            .WithDebugInfo(GesDebugInfoOptions.None)
            .Compile();
        var code = new List<GesBytecodeInstruction>(program.Code.Instructions);
        var next = code.First(instruction => instruction.OpCode == GesBytecodeOpCode.IteratorNext && (instruction.UnitAndFlags & 0x20) != 0);
        var slices = new List<GesUInt16IndexListSegment.SliceEntry>(program.UInt16IndexLists.Slices);
        var data = new List<ushort>(program.UInt16IndexLists.Data);
        var start = data.Count;
        // Distinct registers beyond every frame: each reference is invalid, but only the frame check can tell.
        for (var register = 0; register < ListLength; register++) data.Add((ushort)(register + 64));
        slices.Add(new GesUInt16IndexListSegment.SliceEntry { Start = start, Length = ListLength });
        var reference = next;
        reference.DestinationRegister = (ushort)(slices.Count - 1);
        for (var copy = 0; copy < References; copy++) code.Add(reference);
        var crafted = new GameEventScriptProgram(
            program.FormatVersion,
            program.ModuleName,
            program.Metadata.ProgramVersion,
            program.Metadata.RequiredRegisterCount,
            program.Metadata.RequiredCallStackDepth,
            program.StringConstants,
            new GesUInt16IndexListSegment(slices, data),
            program.Bindings,
            new GesCodeSegment(code));

        var watch = Stopwatch.StartNew();
        var exception = Assert.ThrowsExactly<GesProgramFormatException>(crafted.Validate);
        watch.Stop();
        Assert.AreEqual(GesProgramFormatErrorCode.InvalidOperand, exception.ErrorCode);
        Assert.IsLessThan(5_000L, watch.ElapsedMilliseconds, "Validation must not scan the long list once per reference.");
    }

    [TestMethod]
    public void SourceMapsOfManyLinesValidateLinearly()
    {
        var source = new System.Text.StringBuilder("module manylines\non Start { emit Done() }\n");
        for (var line = 0; line < 200_000; line++) source.Append("// x\n");
        var program = new GesProgramBuilder().AddScript(source.ToString(), "manylines.ges").Compile();

        var watch = Stopwatch.StartNew();
        program.Validate();
        watch.Stop();
        Assert.IsLessThan(1_000L, watch.ElapsedMilliseconds, "Line offsets must not re-count each line's prefix.");
    }

    [TestMethod]
    public void CompiledAndDecodedProgramsSkipRevalidationButUncheckedInstancesDoNot()
    {
        var program = new GesProgramBuilder()
            .AddScript("module once\non Start() {}", "once.ges")
            .WithDebugInfo(GesDebugInfoOptions.None)
            .Compile();
        Assert.IsTrue(program.IsValidated, "The Compiler validates before it returns the Program.");
        Assert.IsTrue(GameEventScriptProgram.Read(program.Encode()).IsValidated, "The Reader validates before it returns the Program.");

        var unvalidated = new GameEventScriptProgram(
            program.FormatVersion,
            program.ModuleName,
            program.Metadata.ProgramVersion,
            program.Metadata.RequiredRegisterCount,
            program.Metadata.RequiredCallStackDepth,
            program.StringConstants,
            program.UInt16IndexLists,
            program.Bindings,
            program.Code);
        Assert.IsFalse(unvalidated.IsValidated);
        GameEventScriptHost.CreateBuilder().Build().StartForTest().Load(unvalidated);
        Assert.IsTrue(unvalidated.IsValidated, "Load validates an unchecked instance and records the result.");
    }
}
