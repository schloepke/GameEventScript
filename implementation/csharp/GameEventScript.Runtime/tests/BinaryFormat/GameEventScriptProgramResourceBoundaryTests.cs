// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.BinaryFormat;

[TestClass]
public sealed class GameEventScriptProgramResourceBoundaryTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void WriterAndHostRejectUnderdeclaredInMemoryProgramsBeforeSideEffects(bool nested, bool underdeclareDepth)
    {
        var source = "module resources\nfunction add(_ value) be value + 1\n" +
                     (nested ? "function twice(_ value) be add(add(value))\non Start(value) { emit Done(value: twice(value)) }" : "on Start(value) { emit Done(value: add(value)) }");
        var valid = new GesProgramBuilder().AddScript(source).Compile(new GesCompileOptions { DebugInfo = GesDebugInfoOptions.None });
        var registers = underdeclareDepth ? valid.RequiredRegisterCount : checked((ushort)(valid.RequiredRegisterCount - 1));
        var depth = underdeclareDepth ? (ushort)0 : valid.RequiredCallStackDepth;
        var bindings = valid.Bindings.Entries.ToArray();
        for (var index = 0; index < bindings.Length; index++)
        {
            var bind = bindings[index];
            if (bind.Kind is not (GesBinaryBindKind.MessageHandler or GesBinaryBindKind.MessageNameHandler)) continue;
            bindings[index] = new(bind.Kind, bind.Name, bind.ArgumentNames, bind.EntryAddress, bind.Id, bind.RequiredTags, bind.ExcludedTags, registers, depth);
        }

        // Only friend access can create an unchecked Program. This exercises the
        // independent writer/host trust boundaries, beyond the portable reader corpus.
        var invalid = new GameEventScriptProgram(valid.FormatVersion, valid.ModuleName, valid.ProgramVersion, registers, depth, valid.StringConstants, valid.UInt16IndexLists, new GesBindingSegment(bindings), valid.Code);
        AssertResourceError(() => GameEventScriptProgramValidator.Validate(invalid));
        AssertResourceError(() => GameEventScriptProgramWriter.GetEncodedSize(invalid));
        AssertResourceError(() => GameEventScriptProgramWriter.ToArray(invalid));
        var destination = new byte[GameEventScriptProgramWriter.GetEncodedSize(valid)];
        Array.Fill(destination, (byte)0xA5);
        var before = (byte[])destination.Clone();
        AssertResourceError(() => GameEventScriptProgramWriter.Write(invalid, destination));
        CollectionAssert.AreEqual(before, destination);

        var host = GameEventScriptHost.CreateBuilder().Build().StartForTest();
        var handler = new CountingHandler();
        host.Subscribe("Start", new[] { "value" }, handler);
        Assert.IsTrue(host.IsIdle);
        AssertResourceError(() => host.Load(invalid));
        Assert.IsTrue(host.IsIdle, "A rejected load must not queue initialization.");
        host.Receive(GameEventScriptMessage.Create("Start", [new GesMessageArgument("value", GesValue.GesInteger(2))]));
        var execution = host.RunToCompletion();
        Assert.AreEqual(GesExecutionState.Completed, execution.State);
        Assert.IsTrue(host.IsIdle);
        Assert.AreEqual(1, handler.Count);
        Assert.AreEqual(0, execution.ExecutedOpcodes, "A rejected load must not leave a script handler registered.");
    }

    private static void AssertResourceError(Action action)
        => Assert.AreEqual(GesProgramFormatErrorCode.InvalidResourceMetadata, Assert.ThrowsExactly<GesProgramFormatException>(action).ErrorCode);

    private sealed class CountingHandler : IGesNativeMessageHandler
    {
        internal int Count { get; private set; }

        public void Handle(GameEventScriptMessage message, GesContext context) => Count++;
    }
}
