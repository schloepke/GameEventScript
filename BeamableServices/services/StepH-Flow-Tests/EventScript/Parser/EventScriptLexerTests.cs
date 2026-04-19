using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript;

namespace StepH_Flow_Tests.EventScript.Parser;

[TestClass]
public class EventScriptLexingScenarios
{
    [TestMethod]
    public void KeywordsMessagesIdentifiersAndBooleansAreTokenized()
    {
        var tokens = Lex("on Start publish let value as be when otherwise has empty if else for in starts ends with is or xor and not to true false");

        AssertTokenKinds(tokens,
            EventScriptTokenKind.On,
            EventScriptTokenKind.Message,
            EventScriptTokenKind.Publish,
            EventScriptTokenKind.Let,
            EventScriptTokenKind.Identifier,
            EventScriptTokenKind.As,
            EventScriptTokenKind.Be,
            EventScriptTokenKind.When,
            EventScriptTokenKind.Otherwise,
            EventScriptTokenKind.Has,
            EventScriptTokenKind.Empty,
            EventScriptTokenKind.If,
            EventScriptTokenKind.Else,
            EventScriptTokenKind.For,
            EventScriptTokenKind.In,
            EventScriptTokenKind.Starts,
            EventScriptTokenKind.Ends,
            EventScriptTokenKind.With,
            EventScriptTokenKind.Is,
            EventScriptTokenKind.Or,
            EventScriptTokenKind.Xor,
            EventScriptTokenKind.And,
            EventScriptTokenKind.Not,
            EventScriptTokenKind.To,
            EventScriptTokenKind.True,
            EventScriptTokenKind.False,
            EventScriptTokenKind.EndOfFile);
    }

    [TestMethod]
    public void TagsSelectorsAndPunctuationAreTokenized()
    {
        var tokens = Lex(":decimal :filter :choose :sort [ ] ( ) { } , ; . :");

        AssertTokenKinds(tokens,
            EventScriptTokenKind.Tag,
            EventScriptTokenKind.SelectorFilter,
            EventScriptTokenKind.SelectorChoose,
            EventScriptTokenKind.SelectorSort,
            EventScriptTokenKind.LeftBracket,
            EventScriptTokenKind.RightBracket,
            EventScriptTokenKind.LeftParen,
            EventScriptTokenKind.RightParen,
            EventScriptTokenKind.LeftBrace,
            EventScriptTokenKind.RightBrace,
            EventScriptTokenKind.Comma,
            EventScriptTokenKind.Semicolon,
            EventScriptTokenKind.Dot,
            EventScriptTokenKind.Colon,
            EventScriptTokenKind.EndOfFile);
    }

    [TestMethod]
    public void OperatorsAreTokenized()
    {
        var tokens = Lex("= <> < <= > >= -> | ^ & + - * / % ! ~");

        AssertTokenKinds(tokens,
            EventScriptTokenKind.Equal,
            EventScriptTokenKind.NotEqual,
            EventScriptTokenKind.Less,
            EventScriptTokenKind.LessOrEqual,
            EventScriptTokenKind.Greater,
            EventScriptTokenKind.GreaterOrEqual,
            EventScriptTokenKind.Arrow,
            EventScriptTokenKind.Or,
            EventScriptTokenKind.Xor,
            EventScriptTokenKind.And,
            EventScriptTokenKind.Plus,
            EventScriptTokenKind.Minus,
            EventScriptTokenKind.Multiply,
            EventScriptTokenKind.Divide,
            EventScriptTokenKind.Modulo,
            EventScriptTokenKind.Not,
            EventScriptTokenKind.Not,
            EventScriptTokenKind.EndOfFile);
    }

    [TestMethod]
    public void NumbersPercentagesTextsAndCompactDicePiecesAreTokenized()
    {
        var tokens = Lex("12 12.5 75% 'Mark''s' 4d6 d8");

        AssertTokenKinds(tokens,
            EventScriptTokenKind.Decimal,
            EventScriptTokenKind.Decimal,
            EventScriptTokenKind.Percentage,
            EventScriptTokenKind.Text,
            EventScriptTokenKind.Decimal,
            EventScriptTokenKind.Identifier,
            EventScriptTokenKind.Identifier,
            EventScriptTokenKind.EndOfFile);

        Assert.AreEqual("12", tokens[0].Text);
        Assert.IsTrue(tokens[0].TryGetIntegerValue(out var integerValue));
        Assert.AreEqual(12L, integerValue);
        Assert.AreEqual("12.5", tokens[1].Text);
        Assert.AreEqual("75", tokens[2].Text);
        Assert.AreEqual("Mark's", tokens[3].Text);
        Assert.AreEqual("4", tokens[4].Text);
        Assert.AreEqual("d6", tokens[5].Text);
        Assert.AreEqual("d8", tokens[6].Text);
    }

    [TestMethod]
    public void NewLinesKeepTheirOwnTokensAndLocations()
    {
        var tokens = Lex("on Start\r\npublish Done\nlet x be 1");

        var firstNewLine = tokens[2];
        var secondNewLine = tokens[5];

        Assert.AreEqual(EventScriptTokenKind.NewLine, firstNewLine.Kind);
        Assert.AreEqual(1, firstNewLine.Line);
        Assert.AreEqual(9, firstNewLine.Column);

        Assert.AreEqual(EventScriptTokenKind.NewLine, secondNewLine.Kind);
        Assert.AreEqual(2, secondNewLine.Line);
        Assert.AreEqual(13, secondNewLine.Column);
    }

    [TestMethod]
    public void IllegalCharactersAndAttachedIllegalSequencesAreTokenizedAsSingleSpans()
    {
        var tokens = Lex("hello%&some @ 'Mark");

        AssertTokenKinds(tokens,
            EventScriptTokenKind.Illegal,
            EventScriptTokenKind.Illegal,
            EventScriptTokenKind.Illegal,
            EventScriptTokenKind.EndOfFile);

        Assert.AreEqual("hello%&some", tokens[0].Text);
        Assert.AreEqual("@", tokens[1].Text);
        Assert.AreEqual("'Mark", tokens[2].Text);
    }

    [TestMethod]
    public void LexerContinuesAfterIllegalTokensAndFindsFollowingValidTokens()
    {
        var tokens = Lex("let broken be hello%&some\npublish Done");

        AssertTokenKinds(tokens,
            EventScriptTokenKind.Let,
            EventScriptTokenKind.Identifier,
            EventScriptTokenKind.Be,
            EventScriptTokenKind.Illegal,
            EventScriptTokenKind.NewLine,
            EventScriptTokenKind.Publish,
            EventScriptTokenKind.Message,
            EventScriptTokenKind.EndOfFile);
    }

    [TestMethod]
    public void IllegalNumberSuffixesAreGroupedWhileMalformedDecimalContinuationsSplitAtDotBoundary()
    {
        var tokens = Lex("12abc 75%value 10.5.3");

        AssertTokenKinds(tokens,
            EventScriptTokenKind.Illegal,
            EventScriptTokenKind.Illegal,
            EventScriptTokenKind.Decimal,
            EventScriptTokenKind.Dot,
            EventScriptTokenKind.Decimal,
            EventScriptTokenKind.EndOfFile);

        Assert.AreEqual("12abc", tokens[0].Text);
        Assert.AreEqual("75%value", tokens[1].Text);
        Assert.AreEqual("10.5", tokens[2].Text);
        Assert.AreEqual(".", tokens[3].Text);
        Assert.AreEqual("3", tokens[4].Text);
    }

    [TestMethod]
    public void ParserTurnsIllegalTokensIntoLexerErrorsWithSourceContext()
    {
        const string script =
            """
            on Broken {
                let value be hello%&some
                let other be @
            }
            """;

        var exception = Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script, "Broken.es"));
        var lexerErrors = exception.Errors.Where(error => error.Kind == EventScriptSyntaxErrorKind.Lexer).ToArray();

        Assert.IsGreaterThanOrEqualTo(2, lexerErrors.Length);
        Assert.IsTrue(lexerErrors.Any(error => error.Message.Contains("hello%&some", StringComparison.Ordinal)));
        Assert.IsTrue(lexerErrors.Any(error => error.Message.Contains("@", StringComparison.Ordinal)));
        Assert.IsTrue(lexerErrors.All(error => error.SourceLocation.SourceName == "Broken.es"));
    }

    private static EventScriptToken[] Lex(string script) => new EventScriptLexer(script).Tokenize().ToArray();

    private static void AssertTokenKinds(IReadOnlyList<EventScriptToken> tokens, params EventScriptTokenKind[] expectedKinds)
        => CollectionAssert.AreEqual(expectedKinds, tokens.Select(token => token.Kind).ToArray());
}
