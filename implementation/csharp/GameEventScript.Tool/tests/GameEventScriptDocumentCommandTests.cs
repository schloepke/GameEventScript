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
        File.WriteAllBytes(Path.Combine(_directory, "binary.gesd"), [0x61, 0x3A, 0x20, 0xFF, 0x0A]);
        var encoding = Run("document", "check", "binary.gesd", "text.gesd");
        Assert.AreEqual(1, encoding.ExitCode);
        StringAssert.StartsWith(encoding.StandardError, "binary.gesd: error cli.invalidEncoding:");
        StringAssert.EndsWith(encoding.StandardError, Lines("text.gesd(2,7): error document.unquotedText: Text values must be quoted."));
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
    [DataRow("document", "check", "")]
    [DataRow("document", "format", "", "-o", "out.gesd")]
    [DataRow("document", "check", "a.gesd", "--schema")]
    [DataRow("document", "check", "a.gesd", "--schema", "-q")]
    [DataRow("document", "format", "a.gesd", "--schema", "units.gess")]
    [DataRow("document", "check", "--catalog", "@Unit", "a.gesd")]
    [DataRow("document", "check", "--schema", "a.gess", "--catalog", "Unit", "a.gesd")]
    [DataRow("document", "check", "--schema", "a.gess", "--catalog", "@Unit")]
    [DataRow("document", "format", "a.gesd", "--catalog", "@Unit", "b.gesd")]
    public void UsageErrorsExitWithTwo(params string[] arguments)
    {
        Write("a.gesd", Valid);
        Write("b.gesd", Valid);
        var result = Run(arguments);
        Assert.AreEqual(2, result.ExitCode, result.StandardError + result.StandardOutput);
        if (arguments.Length > 1) StringAssert.Contains(result.StandardError, "cli.usage");
    }

    /// <summary>
    /// Verifies that check with <c>--schema</c> checks each document against the schema its header names: a pinned
    /// reference in an unpinned handle field breaks it, which reading alone accepts.
    /// </summary>
    [TestMethod]
    public void CheckWithSchemasChecksEachDocumentAgainstItsHeader()
    {
        Write("state.gess", "schema :Composition as { name: :Text max 32 }\nhandle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }\n");
        Write("army.gess", "schema :Army as { parts: :List(@Unit) max 4 }\n");
        Write("pinned.gesd", ":Schema(:Army)\nparts: [@Unit:x~#live]\n");
        Write("army.gesd", ":Schema(:Army)\nparts: [@Unit:x, @Unit:y]\n");
        Write("plain.gesd", "parts: [@Unit:x~#live]\n");
        Directory.CreateDirectory(Path.Combine(_directory, "sub"));
        Write(Path.Combine("sub", "file.gesd"), ":Schema('../army.gess')\nparts: [@Unit:x]\n");
        Write(Path.Combine("sub", "pinned.gesd"), ":Schema('../army.gess')\nparts: [@Unit:x~#live]\n");
        Write("other.gesd", ":Schema('other.gess')\nparts: [@Unit:x]\n");
        Write("unknown.gesd", ":Schema(:Navy)\nparts: [@Unit:x]\n");

        // Without --schema only reading is checked.
        var read = Run("document", "check", "pinned.gesd", "other.gesd", "unknown.gesd");
        Assert.AreEqual(0, read.ExitCode, read.StandardError);
        Assert.AreEqual(Lines("Checked 3 documents."), read.StandardOutput);

        var valid = Run("document", "check", "army.gesd", "plain.gesd", Path.Combine("sub", "file.gesd"), "--schema", "state.gess", "--schema", "army.gess");
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual(Lines("Checked 3 documents."), valid.StandardOutput);

        var invalid = Run("document", "check", "pinned.gesd", Path.Combine("sub", "pinned.gesd"), "other.gesd", "unknown.gesd", "army.gesd", "--schema", "*.gess");
        Assert.AreEqual(1, invalid.ExitCode);
        Assert.AreEqual(string.Empty, invalid.StandardOutput);
        Assert.AreEqual(Lines(
            "pinned.gesd(2,9): error document.unexpectedVersion: The document breaks its schema (document.unexpectedVersion) at line 2, column 9.",
            $"{Path.Combine("sub", "pinned.gesd")}(2,9): error document.unexpectedVersion: The document breaks its schema (document.unexpectedVersion) at line 2, column 9.",
            "other.gesd: error schema.unknownName: The schema file 'other.gess' is none of the files given with --schema.",
            "unknown.gesd: error schema.unknownName: ':Navy' is not declared."), invalid.StandardError);

        // A schema error stops before any document, as for compile.
        Write("broken.gess", "schema :Broken { }\n");
        var broken = Run("document", "check", "army.gesd", "--schema", "broken.gess");
        Assert.AreEqual(1, broken.ExitCode);
        StringAssert.StartsWith(broken.StandardError, "broken.gess(1,16): error schema.syntax:");
        Assert.AreEqual(string.Empty, broken.StandardOutput);
    }

    /// <summary>
    /// Verifies that check with <c>--catalog</c> reads each catalog after the ones before it, reports every error with
    /// its file and position and counts the entries of a valid catalog.
    /// </summary>
    [TestMethod]
    public void CheckWithCatalogsReadsTheCatalogsInOrder()
    {
        Write("units.gess", "schema :Hull as { armor: :Number integer between 0 and 100 }\n"
            + "handle @Hull of :Hull as { id: :Text max 32, version: :ContentHash }\n"
            + "schema :Composition as { name: :Text max 64, hull: @Hull~, escort: @Unit optional }\n"
            + "handle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\n");
        Directory.CreateDirectory(Path.Combine(_directory, "content", "drones"));
        Write(Path.Combine("content", "hulls.gesd"), ":Catalog(@Hull)\nlight: [armor: 20]\nheavy: [armor: 80]\n");
        Write(Path.Combine("content", "units.gesd"), ":Catalog(@Unit)\nimport 'drones/all.gesd'\nscout: [name: 'Scout', hull: @Hull:light~]\n");
        Write(Path.Combine("content", "drones", "all.gesd"), ":Catalog(@Unit)\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escort: @scout]\n}\n");
        string[] catalogs = ["--catalog", "@Hull", Path.Combine("content", "hulls.gesd"), "--catalog", "@Unit", Path.Combine("content", "units.gesd")];

        var valid = Run(["document", "check", "--schema", "units.gess", .. catalogs]);
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual(Lines("Checked catalog @Hull: 2 entries.", "Checked catalog @Unit: 2 entries."), valid.StandardOutput);
        Assert.AreEqual(string.Empty, Run(["document", "check", "--schema", "units.gess", "-q", .. catalogs]).StandardOutput);

        Write(Path.Combine("content", "drones", "all.gesd"), ":Catalog(@Unit)\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:hevy~, escort: @scout]\n}\n");
        var invalid = Run(["document", "check", "--schema", "units.gess", .. catalogs]);
        Assert.AreEqual(1, invalid.ExitCode);
        Assert.AreEqual(Lines("Checked catalog @Hull: 2 entries."), invalid.StandardOutput);
        Assert.AreEqual(Lines(
            $"{Path.Combine("content", "drones", "all.gesd")}(3,36): error catalog.unknownReference: @Hull:hevy names no entry. Did you mean @Hull:heavy?"),
            invalid.StandardError);

        var unknown = Run("document", "check", "--schema", "units.gess", "--catalog", "@Ship", Path.Combine("content", "hulls.gesd"));
        Assert.AreEqual(1, unknown.ExitCode);
        Assert.AreEqual(Lines($"{Path.Combine("content", "hulls.gesd")}: error schema.unknownName: The schemas of --schema declare no handle type @Ship."), unknown.StandardError);
    }

    /// <summary>Verifies that :show displays a file and marks its errors without loading it.</summary>
    [TestMethod]
    public void ShowMarksErrorsBelowTheText()
    {
        Write("bad.gesd", "a: 1\nname: Basic Track\n");
        Write("form.gesd", "range: :Range(1)\n");
        Write("source.ges", "on Start {\n");
        Write("wide.gesd", "t: ['\u65E5\u672C', '\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67',\tBasic]\n");
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

        // Tabs and wide characters before the range move the caret by their display width.
        var wide = ToolProcess.ExecuteWithInput(_directory, ":show wide.gesd\n:quit\n", ["run", "--interactive", "--quiet"]);
        StringAssert.Contains(wide.StandardError, Lines(
            "wide.gesd(1,20): error document.unquotedText: Text values must be quoted.", "  t: ['\u65E5\u672C', '\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67',   Basic]", "                      ^^^^^"));
    }

    private void Write(string file, string text) => File.WriteAllText(Path.Combine(_directory, file), text, new UTF8Encoding(false, true));

    private DotNetProcessResult Run(params string[] arguments) => ToolProcess.Execute(_directory, arguments);

    private static string Lines(params string[] values) => string.Join(Environment.NewLine, values) + Environment.NewLine;
}
