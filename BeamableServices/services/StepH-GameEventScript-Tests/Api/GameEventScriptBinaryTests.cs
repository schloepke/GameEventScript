using StepH.GameEventScript;
using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptBinaryTests
{
    [TestMethod]
    public void FromCompiledBuildsCompactBindTable()
    {
        const string script =
            """
            module BinaryShape

            function score(value) means value + 1
            predicate high(value) means value > 3

            on Start(value) {
              let rounded be :math.floor value
              emit Done(score: score(value: value), high: value is high, rounded: rounded)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var binary = compiled.ToGameEventScriptBinary();

        Assert.AreEqual("BinaryShape", compiled.ModuleName);
        Assert.AreEqual("BinaryShape", binary.ModuleName);
        Assert.AreEqual((ushort)1, binary.Header.Version);
        Assert.AreEqual(16u, GameEventScriptBinaryHeader.HeaderSize);

        var binds = binary.BindTable.Entries.ToArray();
        Assert.AreEqual((ushort)4, binary.BindTable.EntryCount);
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler && Resolve(binary, entry.Name) == "Start"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Function && Resolve(binary, entry.Name) == "score"));
        Assert.IsTrue(binds.Any(entry => entry.Kind == GameEventScriptBinaryBindKind.Predicate && Resolve(binary, entry.Name) == "high"));
        Assert.IsTrue(binds.Where(entry => (byte)entry.Kind is >= 0x10 and <= 0x1F).All(entry => entry.EntryAddress < compiled.Code.Count));

        var start = binds.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.MessageHandler);
        CollectionAssert.AreEqual(new[] { "value" }, start.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());

        var import = binds.Single(entry => entry.Kind == GameEventScriptBinaryBindKind.ExtensionCall);
        Assert.AreEqual("math.floor", Resolve(binary, import.Name));
        CollectionAssert.AreEqual(new[] { "_" }, import.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());
    }

    private static string Resolve(GameEventScriptBinary binary, ushort index) => binary.StringTable.Resolve(index);
}
