// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Api;

// Handler and Series values have no Conformance transport encoding, so their document roundtrip is pinned natively.
[TestClass]
public sealed class GesDocumentRoundTripTests
{
    private const string Values = "[handler: :Handler(Done(value, _)), series: :Series(fibonacci, offset: 3), "
        + "range: :Range(from: 0.5, to: 2.5, step: 0.5), signal: :Message(Done(power: 1kW, note: 'line\nbreak') with #ready), "
        + "nested: [[:Series(factorial, offset: 1), :Handler(Ping())], [deep: :Range(from: 1, to: 9, step: 2)]]]";

    [TestMethod]
    public void DocumentsWithHandlerSeriesRangeAndMessageValuesReadBack()
    {
        var document = new GesDocument(GesValue.ParseLiteral(Values));
        foreach (var width in new[] { 1, 20, 1000 })
        {
            var printed = document.Print(new GesDocumentFormat { LineWidth = width });
            Assert.AreEqual(document, GesDocument.Parse(printed), printed);
        }
    }

    [TestMethod]
    public void FormattedValuesReadBackWithLiteralParsingInEveryLayout()
    {
        var value = GesValue.ParseLiteral(Values);
        foreach (var width in new[] { 1, 20, 1000 })
        {
            var text = value.Format(new GesDocumentFormat { LineWidth = width, Indentation = 4 });
            Assert.AreEqual(value, GesValue.ParseLiteralStrict(text), text);
        }
        Assert.AreEqual("[1kW, 250ms]", GesValue.ParseLiteral("[1000W, 0.25s]").Format());
        // Prefixes keep the unit symbol, never its ASCII spelling; units without prefixes print their symbol.
        Assert.AreEqual("[3cm², 2t, 14mW/m², 2.5kW/m², 3km², 4mW, 2m³, 6dB, 3Hz, 2.5kW/K, 250mW/K, 4MJ/K, 3J/K]", GesValue.ParseLiteral("[0.0003m², 2000kg, 0.014W/m², 2500W/m², 3000000m², 0.004W, 2m³, 6dB, 3Hz, 2500W/K, 0.25W/K, 4000000J/K, 3J/K]").Format());
    }

    [TestMethod]
    public void ValuesWithoutReadableLiteralTextAreRejected()
    {
        foreach (var unreadable in new[] { GesValue.GesDice([0]), GesValue.GesRecord("lower", ["damage"], [GesValue.GesInteger(5)]), GesValue.GesHandler(GesMessageSignature.Create("undeliverable", ["message"])) })
        {
            var root = GesValue.GesMap(["v"], [unreadable]);
            Assert.ThrowsExactly<ArgumentException>(() => new GesDocument(root));
            Assert.ThrowsExactly<ArgumentException>(() => root.Format());
        }
        var recordRoot = GesValue.GesRecord("Unit", ["two words"], [GesValue.GesInteger(1)]);
        Assert.ThrowsExactly<ArgumentException>(() => new GesDocument(recordRoot));
        Assert.AreEqual("[v: :Record(\"Unit\", [\"two words\": 1])]", GesValue.GesMap(["v"], [recordRoot]).Format());
    }

    [TestMethod]
    public void ExtremeLayoutOptionsStayValidOrAreRejected()
    {
        var value = GesValue.ParseLiteral("[a: 1, b: [2, 3]]");
        Assert.AreEqual("[a: 1, b: [2, 3]]", value.Format(new GesDocumentFormat { LineWidth = int.MaxValue }));
        var deep = GesValue.ParseLiteral("[[[[['a long text that cannot fit']]]]]");
        Assert.AreEqual(deep, GesValue.ParseLiteralStrict(deep.Format(new GesDocumentFormat { LineWidth = 1, Indentation = GesDocumentFormat.MaximumIndentation })));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GesDocumentFormat { Indentation = GesDocumentFormat.MaximumIndentation + 1 });
    }
}
