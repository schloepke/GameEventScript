// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies the document check and format commands and the console's :show through the CLI process.</summary>
[TestClass]
public sealed class GameEventScriptDocumentCommandTests
{
    private const string Valid = ":Schema(:DriveUnit)\n// Engine\nid: 'drive'\nmass: 550kg, ready:\nmount: :Vector(x: 0m, y: -0.8m)\n";
    private string _directory = null!;

    /// <summary>Creates an isolated directory for document files.</summary>
    [TestInitialize]
    public void CreateWorkspace()
    {
        _directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "tool-document", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Deletes the test's files.</summary>
    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_directory, true);

    /// <summary>Verifies that check reports the first error of each document with its line and column.</summary>
    [TestMethod]
    public void CheckReportsEachInvalidDocument()
    {
        Write("valid.gesd", Valid);
        Write("text.gesd", "a: 1\nname: Basic Track\n");
        Write("form.gesd", "range: :Range(1)\n");
        var valid = Run("document", "check", "valid.gesd");
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual(Lines("Checked valid.gesd."), valid.StandardOutput);
        Assert.AreEqual(string.Empty, valid.StandardError);
        Assert.AreEqual(string.Empty, Run("document", "check", "-q", "valid.gesd").StandardOutput);

        var invalid = Run("document", "check", "valid.gesd", "text.gesd", "form.gesd");
        Assert.AreEqual(1, invalid.ExitCode);
        Assert.AreEqual(string.Empty, invalid.StandardOutput);
        Assert.AreEqual(Lines(
            "text.gesd(2,7): error document.unquotedText: Text values must be quoted.",
            "form.gesd(1,8): error document.invalidLiteral: This is not a valid literal."), invalid.StandardError);

        // An unreadable file does not stop the check of the remaining files.
        var partial = Run("document", "check", "missing.gesd", "text.gesd");
        Assert.AreEqual(1, partial.ExitCode);
        StringAssert.Contains(partial.StandardError, "error cli.io:");
        StringAssert.EndsWith(partial.StandardError, Lines("text.gesd(2,7): error document.unquotedText: Text values must be quoted."));
    }

    /// <summary>Verifies that format prints the document's recommended layout, which reads back to an equal document.</summary>
    [TestMethod]
    public void FormatPrintsAndWritesTheRecommendedLayout()
    {
        Write("valid.gesd", Valid);
        var expected = GesDocument.Parse(Valid).Print();
        var printed = Run("document", "format", "valid.gesd");
        Assert.AreEqual(0, printed.ExitCode, printed.StandardError);
        Assert.AreEqual(expected, printed.StandardOutput);
        Assert.AreEqual(GesDocument.Parse(Valid), GesDocument.Parse(printed.StandardOutput));

        var written = Run("document", "format", "valid.gesd", "-o", "out/formatted.gesd");
        Assert.AreEqual(0, written.ExitCode, written.StandardError);
        Assert.AreEqual(expected, File.ReadAllText(Path.Combine(_directory, "out", "formatted.gesd")));
    }

    /// <summary>Verifies that an invalid document keeps an existing output file.</summary>
    [TestMethod]
    public void FormatOfAnInvalidDocumentKeepsTheOutput()
    {
        Write("bad.gesd", "a: [1, 2\n");
        Write("previous.gesd", "previous");
        var result = Run("document", "format", "bad.gesd", "-o", "previous.gesd");
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(Lines("bad.gesd(1,4): error document.unclosedBracket: This bracket is never closed."), result.StandardError);
        Assert.AreEqual("previous", File.ReadAllText(Path.Combine(_directory, "previous.gesd")));
    }

    /// <summary>Verifies usage errors.</summary>
    [TestMethod]
    [DataRow("document")]
    [DataRow("document", "lint", "a.gesd")]
    [DataRow("document", "check")]
    [DataRow("document", "format", "a.gesd", "b.gesd")]
    [DataRow("document", "format", "a.gesd", "-o", "a.gesd")]
    [DataRow("document", "check", "--color", "a.gesd")]
    public void UsageErrorsExitWithTwo(params string[] arguments)
    {
        Write("a.gesd", Valid);
        Write("b.gesd", Valid);
        var result = Run(arguments);
        Assert.AreEqual(2, result.ExitCode, result.StandardError + result.StandardOutput);
        if (arguments.Length > 1) StringAssert.Contains(result.StandardError, "cli.usage");
    }

    /// <summary>Verifies that :show displays a file and marks its errors without loading it.</summary>
    [TestMethod]
    public void ShowMarksErrorsBelowTheText()
    {
        Write("bad.gesd", "a: 1\nname: Basic Track\n");
        Write("form.gesd", "range: :Range(1)\n");
        Write("source.ges", "on Start {\n");
        var result = ToolProcess.ExecuteWithInput(_directory, ":show bad.gesd\n:show form.gesd\n:show source.ges\n:quit\n", ["run", "--interactive", "--quiet"]);
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(Lines(
            "", "// File: \"bad.gesd\"", "a: 1", "name: Basic Track",
            "bad.gesd(2,7): error document.unquotedText: Text values must be quoted.", "  name: Basic Track", "        ^^^^^", "",
            "", "// File: \"form.gesd\"", "range: :Range(1)",
            "form.gesd(1,8): error document.invalidLiteral: This is not a valid literal.", "  range: :Range(1)", "         ^^^^^^", "",
            "", "// File: \"source.ges\"", "on Start {"), result.StandardError[..result.StandardError.IndexOf("source.ges(", StringComparison.Ordinal)]);
        StringAssert.Contains(result.StandardError, "source.ges(2,1): error parse.syntax:");
        Assert.AreEqual(string.Empty, result.StandardOutput);
    }

    private void Write(string file, string text) => File.WriteAllText(Path.Combine(_directory, file), text, new UTF8Encoding(false, true));

    private DotNetProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);

    private static string Lines(params string[] values) => string.Join(Environment.NewLine, values) + Environment.NewLine;
}
