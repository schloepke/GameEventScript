using System.Text;
using System.Text.Json;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class ConformanceReportWriterTests
{
    [TestMethod]
    public void WritesCanonicalResultJsonWithStableShapeAndUtf8()
    {
        var document = ParseBytecode("suite", "case", "A | title");
        var report = ConformanceRunner.RunDocument(document, Environment(["compiler", "vm"]));

        var text = ConformanceResultJsonWriter.ToText(report);
        var bytes = ConformanceResultJsonWriter.ToArray(report);

        Assert.AreEqual("""
{
  "schemaVersion": 1,
  "runner": {
    "id": "runner",
    "version": "1"
  },
  "implementation": {
    "id": "implementation",
    "version": "1"
  },
  "capabilities": [
    "compiler",
    "vm"
  ],
  "performanceProfile": null,
  "status": "passed",
  "summary": {
    "total": 1,
    "passed": 1,
    "failed": 0,
    "skipped": 0,
    "error": 0
  },
  "cases": [
    {
      "id": "suite/case",
      "suiteId": "suite",
      "caseId": "case",
      "title": "A | title",
      "kind": "bytecode",
      "level": "atomic",
      "categories": [],
      "tags": [],
      "status": "passed",
      "code": "conformance.passed",
      "missingCapabilities": [],
      "mismatches": [],
      "diagnostics": [],
      "runtimeLimits": [],
      "actualAssembler": null,
      "performance": null
    }
  ]
}
""" + "\n", text);
        Assert.IsFalse(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.IsTrue(text.EndsWith("\n", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("\r", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("runner", root.GetProperty("runner").GetProperty("id").GetString());
        CollectionAssert.AreEqual(new[] { "compiler", "vm" }, root.GetProperty("capabilities").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.AreEqual("passed", root.GetProperty("status").GetString());
        var result = root.GetProperty("cases")[0];
        Assert.AreEqual("suite/case", result.GetProperty("id").GetString());
        Assert.AreEqual("bytecode", result.GetProperty("kind").GetString());
        Assert.AreEqual(JsonValueKind.Null, result.GetProperty("actualAssembler").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, result.GetProperty("performance").ValueKind);
    }

    [TestMethod]
    public void WritesReadableMarkdownSummaryCapabilitiesAndEscapedCaseCells()
    {
        var document = ParseBytecode("suite", "case", "A | title", optionalCapability: "bytecode-snapshot");
        var report = ConformanceRunner.RunDocument(document, Environment(["compiler"]));

        var markdown = ConformanceMarkdownReportWriter.ToText(report);

        StringAssert.Contains(markdown, "# GES Conformance Report");
        StringAssert.Contains(markdown, "| 1 | 0 | 0 | 1 | 0 |");
        StringAssert.Contains(markdown, "Advertised: `compiler`");
        StringAssert.Contains(markdown, "Missing: `bytecode-snapshot`");
        StringAssert.Contains(markdown, "A \\| title");
        StringAssert.Contains(markdown, "`suite/case`");
    }

    [TestMethod]
    public void WritesPerformanceTableAndFailureDetails()
    {
        var document = ParsePerformance("\n", includeBom: false, reference: "1");
        var report = ConformanceRunner.RunDocument(document, PerformanceEnvironment("3"));

        var markdown = ConformanceMarkdownReportWriter.ToText(report);
        var json = ConformanceResultJsonWriter.ToText(report);

        Assert.AreEqual(ConformanceCaseStatus.Failed, report.Status);
        StringAssert.Contains(markdown, "## Performance");
        StringAssert.Contains(markdown, "| `performance/case` | `test-profile` | `run.elapsed` | 3 | 1 | 2 | ms | failed |");
        StringAssert.Contains(markdown, "## Failures and errors");
        StringAssert.Contains(json, "\"run.elapsed\": {");
        StringAssert.Contains(json, "\"measured\": \"3\"");
    }

    [TestMethod]
    public void ReceivedWriterUpdatesOnlyPerformanceReferenceAndPreservesBomAndCrLf()
    {
        var document = ParsePerformance("\r\n", includeBom: true, reference: "1.0");
        var report = ConformanceRunner.RunDocument(document, PerformanceEnvironment("0.75"));
        var original = document.Source.Utf8Bytes.ToArray();

        var received = ConformanceReceivedMarkdownWriter.ToArray(document, report);
        var text = Encoding.UTF8.GetString(received);

        Assert.IsTrue(received.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        StringAssert.Contains(text, "reference: 0.75\r\n");
        StringAssert.Contains(text, "maximum: 2\r\n");
        Assert.AreEqual(Count(original, (byte)'\n'), Count(received, (byte)'\n'));
        Assert.AreEqual(ReplaceOnce(original, Encoding.UTF8.GetBytes("1.0"), Encoding.UTF8.GetBytes("0.75")), Convert.ToHexString(received));
    }

    [TestMethod]
    public void ReceivedWriterUpdatesOnlyGesaPayloadAndUsesDocumentLineEndings()
    {
        var source = "on Start() {}";
        var input = "---\r\nformatVersion: 1\r\nsuiteId: snapshot\r\nkind: bytecodeSnapshot\r\nlevel: atomic\r\n---\r\n## Test: Case\r\n```yaml\r\ngesBlock: case\r\nid: case\r\n```\r\n```ges\r\n" + source + "\r\n```\r\n```gesa\r\nwrong\r\n```\r\n";
        var document = ConformanceMarkdownParser.Parse(input);
        var report = ConformanceRunner.RunDocument(document, Environment(["bytecode-snapshot", "compiler"]));

        var received = ConformanceReceivedMarkdownWriter.ToText(document, report);

        Assert.AreEqual(ConformanceCaseStatus.Failed, report.Status);
        StringAssert.Contains(received, "```gesa\r\n// -------------------------------------------------------------------------------");
        StringAssert.Contains(received, ".gesb 1\r\n");
        Assert.IsFalse(received.Contains("\nwrong\r\n", StringComparison.Ordinal));
        StringAssert.Contains(received, "```ges\r\n" + source + "\r\n```");
    }

    [TestMethod]
    public void ReceivedWriterRejectsReportFromStaleExpectation()
    {
        var source = ParsePerformance("\n", includeBom: false, reference: "1");
        var stale = ParsePerformance("\n", includeBom: false, reference: "1.5");
        var report = ConformanceRunner.RunDocument(stale, PerformanceEnvironment("1.25"));

        var exception = Assert.ThrowsExactly<ConformanceReceivedWriteException>(() => ConformanceReceivedMarkdownWriter.ToArray(source, report));

        Assert.AreEqual(ConformanceReceivedWriterCodes.InvalidReport, exception.Code);
    }

    private static ConformanceDocument ParseBytecode(string suiteId, string caseId, string title, string? optionalCapability = null)
    {
        var optional = optionalCapability is null ? "" : "requires:\n  optional: [" + optionalCapability + "]\n";
        return ConformanceMarkdownParser.Parse("---\nformatVersion: 1\nsuiteId: " + suiteId + "\nkind: bytecode\nlevel: atomic\n" + optional + "---\n## Test: " + title + "\n```yaml\ngesBlock: case\nid: " + caseId + "\n```\n```ges\non Start() {}\n```\n```yaml\ngesBlock: expect\nopcodes: { contains: [ReturnVoid] }\n```\n");
    }

    private static ConformanceDocument ParsePerformance(string lineEnding, bool includeBom, string reference)
    {
        var source = "---\nformatVersion: 1\nsuiteId: performance\nkind: performance\nlevel: atomic\n---\n## Test: Case\n```yaml\ngesBlock: case\nid: case\nperformance: { iterations: 1 }\n```\n```ges\non Start() { emit Done() }\n```\n### Steps\n| step | receive | pump | budget |\n| --- | --- | --- | --- |\n| run | Start | completion | |\n```yaml\ngesBlock: expect\nsteps:\n  run:\n    local:\n      - name: Done\nperformance:\n  profiles:\n    test-profile:\n      metrics:\n        run.elapsed:\n          reference: " + reference + "\n          maximum: 2\n          unit: ms\n```\n";
        var bytes = Encoding.UTF8.GetBytes(source.Replace("\n", lineEnding));
        if (!includeBom) return ConformanceMarkdownParser.Parse(bytes);
        var withBom = new byte[bytes.Length + 3];
        withBom[0] = 0xef; withBom[1] = 0xbb; withBom[2] = 0xbf;
        Array.Copy(bytes, 0, withBom, 3, bytes.Length);
        return ConformanceMarkdownParser.Parse(withBom);
    }

    private static ConformanceRunnerEnvironment Environment(IReadOnlyCollection<string> capabilities)
        => new("runner", "1", "implementation", "1", capabilities);

    private static ConformanceRunnerEnvironment PerformanceEnvironment(string measured)
        => new("runner", "1", "implementation", "1", ["compiler", "host", "observer", "performance", "publish-sink", "vm"], performanceProfileId: "test-profile", performanceProvider: new Provider(measured));

    private static int Count(IReadOnlyList<byte> source, byte value) { var count = 0; for (var index = 0; index < source.Count; index++) if (source[index] == value) count++; return count; }
    private static string ReplaceOnce(byte[] source, byte[] oldValue, byte[] newValue)
    {
        var index = source.AsSpan().IndexOf(oldValue);
        Assert.IsGreaterThanOrEqualTo(0, index);
        var result = new byte[source.Length - oldValue.Length + newValue.Length];
        source.AsSpan(0, index).CopyTo(result);
        newValue.CopyTo(result.AsSpan(index));
        source.AsSpan(index + oldValue.Length).CopyTo(result.AsSpan(index + newValue.Length));
        return Convert.ToHexString(result);
    }

    private sealed class Provider(string measured) : IConformancePerformanceProvider
    {
        public ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId)
            => new([new ConformanceMeasuredMetric("run.elapsed", measured, "ms")]);
    }
}
