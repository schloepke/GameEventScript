using StepH.GameEventScript;
using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptBinaryTests
{
    [TestMethod]
    public void FromCompiledBuildsCompactExportAndImportTables()
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

        var exports = binary.ExportTable.Entries.ToArray();
        Assert.AreEqual((ushort)3, binary.ExportTable.EntryCount);
        Assert.IsTrue(exports.Any(entry => entry.Kind == GameEventScriptBinaryExportKind.MessageHandler && Resolve(binary, entry.Name) == "Start"));
        Assert.IsTrue(exports.Any(entry => entry.Kind == GameEventScriptBinaryExportKind.Function && Resolve(binary, entry.Name) == "score"));
        Assert.IsTrue(exports.Any(entry => entry.Kind == GameEventScriptBinaryExportKind.Predicate && Resolve(binary, entry.Name) == "high"));
        Assert.IsTrue(exports.All(entry => entry.EntryAddress < compiled.Code.Count));

        var start = exports.Single(entry => entry.Kind == GameEventScriptBinaryExportKind.MessageHandler);
        CollectionAssert.AreEqual(new[] { "value" }, start.ArgumentNames.Select(index => Resolve(binary, index)).ToArray());

        var imports = binary.ImportTable.Entries.ToArray();
        Assert.AreEqual((ushort)1, binary.ImportTable.EntryCount);
        Assert.AreEqual(GameEventScriptBinaryImportKind.ExtensionCall, imports[0].Kind);
        Assert.AreEqual("math.floor", Resolve(binary, imports[0].Name));
        CollectionAssert.AreEqual(new[] { "_" }, imports[0].ArgumentNames.Select(index => Resolve(binary, index)).ToArray());
    }

    private static string Resolve(GameEventScriptBinary binary, ushort index)
        => binary.StringPool[index];
}
