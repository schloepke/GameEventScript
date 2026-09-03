using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.Native.Compiler;

[TestClass]
public sealed class GesUnicodeSemanticsTests
{
    [TestMethod]
    public void LexerCountsUnicodeScalarsAndAllPortableNewlines()
    {
        var lexer = new GesLexer("\uFEFF'😀'\rfoo\r\nbar\nbaz");

        AssertToken(lexer.ReadNextToken(), GesTokenKind.Text, "😀", 1, 1, 1, 4);
        AssertToken(lexer.ReadNextToken(), GesTokenKind.NewLine, "\\n", 1, 4, 2, 1);
        AssertToken(lexer.ReadNextToken(), GesTokenKind.Identifier, "foo", 2, 1, 2, 4);
        AssertToken(lexer.ReadNextToken(), GesTokenKind.NewLine, "\\n", 2, 4, 3, 1);
        AssertToken(lexer.ReadNextToken(), GesTokenKind.Identifier, "bar", 3, 1, 3, 4);
        AssertToken(lexer.ReadNextToken(), GesTokenKind.NewLine, "\\n", 3, 4, 4, 1);
        AssertToken(lexer.ReadNextToken(), GesTokenKind.Identifier, "baz", 4, 1, 4, 4);
    }

    [TestMethod]
    public void LexerRecognizesOfAsReservedToken()
    {
        var lexer = new GesLexer("of of_2");

        AssertToken(lexer.ReadNextToken(), GesTokenKind.Of, "of", 1, 1, 1, 3);
        AssertToken(lexer.ReadNextToken(), GesTokenKind.Identifier, "of_2", 1, 4, 1, 8);
    }

    [TestMethod]
    public void BuilderRejectsInvalidUnicodeScalarSequences()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            GameEventScriptManager.CreateScriptBuilder().AddScript("on Start { let value be '\uD800' }").Compile());

        StringAssert.Contains(exception.Message, "valid Unicode scalar values");
    }

    [TestMethod]
    public void SourceArchiveStoresLogicalSourceWithoutBom()
    {
        const string logicalSource = "module Bom\r\non Start { emit Done(value: '😀') }\r";
        var program = GameEventScriptManager.CreateScriptBuilder()
            .AddScript("\uFEFF" + logicalSource, "bom.ges")
            .WithDebugInfo(GameEventScriptDebugInfoOptions.SourceMap | GameEventScriptDebugInfoOptions.SourceArchive)
            .Compile();

        Assert.AreEqual(logicalSource, program.SourceArchive!.Sources[0].ResolveText());
        CollectionAssert.AreEqual(new uint[] { 0, 12, 50 }, program.SourceMap!.Sources[0].LineStartByteOffsets.ToArray());
        GameEventScriptProgramValidator.Validate(program);
    }

    private static void AssertToken(GesToken actual, GesTokenKind kind, string text, int line, int column, int endLine, int endColumn)
    {
        Assert.AreEqual(kind, actual.Kind);
        Assert.AreEqual(text, actual.Text);
        Assert.AreEqual(line, actual.Line);
        Assert.AreEqual(column, actual.Column);
        Assert.AreEqual(endLine, actual.EndLine);
        Assert.AreEqual(endColumn, actual.EndColumn);
    }
}
