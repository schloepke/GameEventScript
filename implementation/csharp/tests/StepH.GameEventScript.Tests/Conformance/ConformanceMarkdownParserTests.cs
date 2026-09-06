// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class ConformanceMarkdownParserTests
{
    public static IEnumerable<object[]> ValidSharedFixtures()
        => ReadFixtureManifest()
            .Where(fixture => fixture.Outcome == "valid")
            .Select(fixture => new object[] { fixture.Id, fixture.RelativePath, fixture.Sha256, fixture.ExpectedSuiteId, fixture.ExpectedCaseId });

    public static IEnumerable<object[]> InvalidSharedFixtures()
        => ReadFixtureManifest()
            .Where(fixture => fixture.Outcome == "invalid")
            .Select(fixture => new object[] { fixture.Id, fixture.RelativePath, fixture.Sha256, fixture.ExpectedDiagnosticCode });

    public static string SharedFixtureDisplayName(MethodInfo method, object[] data) => method.Name + " (" + data[0] + ")";

    [TestMethod]
    [DynamicData(nameof(ValidSharedFixtures), DynamicDataDisplayName = nameof(SharedFixtureDisplayName))]
    public void ParsesSharedGoldenBootstrapFixture(string fixtureId, string relativePath, string sha256, string expectedSuiteId, string expectedCaseId)
    {
        var bytes = ReadVerifiedFixture(fixtureId, relativePath, sha256);
        var document = ConformanceMarkdownParser.Parse(bytes);

        Assert.HasCount(1, document.Cases);
        Assert.AreEqual(expectedSuiteId, document.SuiteId);
        Assert.AreEqual(expectedCaseId, document.Cases[0].Id);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidSharedFixtures), DynamicDataDisplayName = nameof(SharedFixtureDisplayName))]
    public void RejectsSharedInvalidBootstrapFixture(string fixtureId, string relativePath, string sha256, string diagnosticCode)
    {
        var bytes = ReadVerifiedFixture(fixtureId, relativePath, sha256);
        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(bytes));

        Assert.AreEqual(diagnosticCode, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    public void SharedFixtureManifestHasStableUniqueIdentity()
    {
        var fixtures = ReadFixtureManifest();

        Assert.HasCount(4, fixtures);
        Assert.AreEqual(fixtures.Count, fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(fixtures.Count, fixtures.Select(fixture => fixture.RelativePath).Distinct(StringComparer.Ordinal).Count());
        foreach (var fixture in fixtures) _ = ReadVerifiedFixture(fixture.Id, fixture.RelativePath, fixture.Sha256);
    }

    [TestMethod]
    public void ParsesAndNormalizesRuntimeCase()
    {
        const string markdown = """
---
formatVersion: 1
suiteId: runtime.math
title: Runtime math
kind: scriptApi
level: atomic
categories: [conformance, runtime]
tags: [math]
requires:
  core: [external-types]
compile:
  binaryRoundTrip: true
---

## Fixtures

| value |
| --- |
| ignored |

## Test: Integer addition

```yaml
gesBlock: case
id: integer-add
categories: [runtime, smoke]
tags: [math, addition]
random: { seed: -1 }
```

```ges
on Start(value) {
  let result be value + 5
  emit Done(result: result)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| add | Start | frames | 10 |

```yaml
gesBlock: expect
steps:
  add:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "7" }
    local:
      - name: Done
        args:
          - name: result
            value: { type: ":Number.int64", value: "12" }
    paused: true
```
""";

        var document = ConformanceMarkdownParser.Parse(markdown);

        Assert.AreEqual(1, document.FormatVersion);
        Assert.AreEqual("runtime.math", document.SuiteId);
        Assert.AreEqual("Runtime math", document.Title);
        Assert.HasCount(1, document.Cases);
        var test = document.Cases[0];
        Assert.AreEqual("runtime.math/integer-add", test.FullId);
        Assert.AreEqual(ConformanceTestKind.ScriptApi, test.Kind);
        Assert.AreEqual(ConformanceTestLevel.Atomic, test.Level);
        CollectionAssert.AreEqual(new[] { "conformance", "runtime", "smoke" }, test.Categories.ToArray());
        CollectionAssert.AreEqual(new[] { "math", "addition" }, test.Tags.ToArray());
        CollectionAssert.Contains(test.Requires.Core.ToArray(), "program-binary");
        CollectionAssert.Contains(test.Requires.Core.ToArray(), "external-types");
        CollectionAssert.Contains(test.Requires.Core.ToArray(), "observer");
        Assert.IsTrue(test.Compile.BinaryRoundTrip);
        Assert.AreEqual(-1L, test.Random!.Seed);
        Assert.HasCount(1, test.Sources);
        Assert.AreEqual("runtime.math.integer-add.ges", test.Sources[0].Name);
        Assert.AreEqual("main", test.Sources[0].ProgramId);
        Assert.Contains("emit Done", test.Sources[0].Text);
        Assert.HasCount(1, test.Steps);
        Assert.AreEqual(ConformancePumpMode.Frames, test.Steps[0].Pump);
        Assert.AreEqual((uint)10, test.Steps[0].Budget);
        Assert.IsTrue(test.Steps[0].Expectation.Paused);
        Assert.AreEqual("value", test.Steps[0].Expectation.Input.Arguments[0].Name);
        Assert.AreEqual("7", test.Steps[0].Expectation.Input.Arguments[0].Value.Value);
        Assert.AreEqual("Done", test.Steps[0].Expectation.Local[0].Name);
    }

    [TestMethod]
    public void PreservesUtf8ByteRangesBomAndCrLogicalLines()
    {
        var markdown = "---\rformatVersion: 1\rsuiteId: unicode.test\rkind: bytecodeSnapshot\rlevel: atomic\r---\r" +
                       "## Test: Ünicode\r```yaml\rgesBlock: case\rid: emoji\r```\r```ges\ron Start() { emit Done(text: \"😀\") }\r```\r" +
                       "```gesa\r.segment code\r```\r";
        var body = Encoding.UTF8.GetBytes(markdown);
        var bytes = new byte[body.Length + 3];
        bytes[0] = 0xef; bytes[1] = 0xbb; bytes[2] = 0xbf;
        Buffer.BlockCopy(body, 0, bytes, 3, body.Length);

        var document = ConformanceMarkdownParser.Parse(bytes);

        Assert.IsTrue(document.Source.HasByteOrderMark);
        Assert.AreEqual("\r", document.Source.LineEnding);
        Assert.HasCount(bytes.Length, document.Source.Utf8Bytes);
        var source = document.Cases[0].Sources[0];
        var payloadBytes = body.AsSpan(source.PayloadRange.ByteOffset, source.PayloadRange.ByteLength).ToArray();
        Assert.AreEqual(source.Text, Encoding.UTF8.GetString(payloadBytes));
        Assert.AreEqual(".segment code", document.Cases[0].ExpectedAssembler);
    }

    [TestMethod]
    public void PreservesEmbeddedByteOrderMarkScalarInsideGesSource()
    {
        var markdown = "---\nformatVersion: 1\nsuiteId: unicode.bom\nkind: bytecode\nlevel: atomic\n---\n" +
                       "## Test: Embedded BOM\n```yaml\ngesBlock: case\nid: source\n```\n```ges\n\ufeffon Start {}\n```\n" +
                       "```yaml\ngesBlock: expect\nopcodes: { contains: [ReturnVoid] }\n```\n";

        var document = ConformanceMarkdownParser.Parse(markdown);

        Assert.AreEqual("\ufeffon Start {}", document.Cases[0].Sources[0].Text);
    }

    [TestMethod]
    public void ParsesExplicitMessageArgumentMappingNegativeCase()
    {
        const string markdown = "---\nformatVersion: 1\nsuiteId: message.invalid-shape\nkind: messageApi\nlevel: atomic\n---\n" +
                                "## Test: Mapping\n```yaml\ngesBlock: case\nid: mapping\nmessageApi:\n  signature: { name: Score, parameters: [score] }\n" +
                                "  message:\n    name: Score\n    args:\n      score: { type: \":Number.int64\", value: \"1\" }\n```\n" +
                                "```yaml\ngesBlock: expect\nmessage: { error: invalidArgumentsShape }\n```\n";

        var test = ConformanceMarkdownParser.Parse(markdown).Cases[0];

        Assert.IsTrue(test.MessageApi!.ArgumentsWereMapping);
        Assert.HasCount(1, test.MessageApi.UnorderedArguments);
        Assert.AreEqual("score", test.MessageApi.UnorderedArguments[0].Key);
    }

    [TestMethod]
    public void ParsesExplicitRuntimeLimitWildcard()
    {
        const string markdown = "---\nformatVersion: 1\nsuiteId: limits.wildcard\nkind: scriptApi\nlevel: atomic\n---\n" +
                                "## Test: No limits\n```yaml\ngesBlock: case\nid: none\n```\n```ges\non Start {}\n```\n" +
                                "### Steps\n| step | receive | pump | budget |\n| --- | --- | --- | --- |\n| run | Start | completion | |\n" +
                                "```yaml\ngesBlock: expect\nsteps:\n  run:\n    runtimeLimits:\n      exclude:\n        - any: true\n```\n";

        var wildcard = ConformanceMarkdownParser.Parse(markdown).Cases[0].Steps[0].Expectation.Observations.ExcludedRuntimeLimits[0];

        Assert.IsTrue(wildcard.Any);
        Assert.IsNull(wildcard.Name);
    }

    [TestMethod]
    public void ParsesPublishSinkAndOrderedObserverTrace()
    {
        const string markdown = """
---
formatVersion: 1
suiteId: observer.trace
kind: scriptApi
level: scenario
---
## Test: Rejected publish
```yaml
gesBlock: case
id: rejected
publishSink: reject
```
```ges
on Start() { publish Remote() }
on Remote() {}
```
### Steps
| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |
```yaml
gesBlock: expect
steps:
  run:
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: publish
        message: { name: Remote }
        result:
          localAccepted: true
          outboundAttempted: true
          outboundAccepted: false
          anyAccepted: true
```
""";

        var test = ConformanceMarkdownParser.Parse(markdown).Cases[0];
        var trace = test.Steps[0].Expectation.Observations.Trace;

        Assert.AreEqual(ConformancePublishSinkMode.Reject, test.PublishSink);
        Assert.IsTrue(test.Steps[0].Expectation.Observations.TraceSpecified);
        Assert.HasCount(2, trace);
        Assert.AreEqual(ConformanceObserverEventKind.DispatchStarted, trace[0].Kind);
        Assert.AreEqual("Start()", trace[0].SignatureId);
        Assert.AreEqual(ConformanceObserverEventKind.Publish, trace[1].Kind);
        var publishResult = trace[1].PublishResult ?? throw new AssertFailedException("Publish trace result is missing.");
        Assert.IsTrue(publishResult.LocalAccepted);
        Assert.IsTrue(publishResult.OutboundAttempted);
        Assert.IsFalse(publishResult.OutboundAccepted);
        Assert.IsTrue(publishResult.AnyAccepted);
    }

    [TestMethod]
    public void ParsesPerformanceReferenceRangesAndIndependentSources()
    {
        const string markdown = """
---
formatVersion: 1
suiteId: performance.core
kind: performance
level: scenario
---
## Test: Two programs
```yaml
gesBlock: case
id: two-programs
sources:
  - name: first.ges
    program: first
  - name: second.ges
    program: second
performance:
  iterations: 100
  warmupIterations: 2
```
```ges
on Start() { emit One() }
```
```ges
on Start() { emit Two() }
```
### Steps
| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |
```yaml
gesBlock: expect
steps:
  run:
    local: []
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.per-invoke-allocated:
          reference: 0.172
          maximum: 0.200
          unit: KiB
```
""";

        var document = ConformanceMarkdownParser.Parse(markdown);
        var test = document.Cases[0];

        Assert.HasCount(2, test.Sources);
        Assert.AreEqual("first", test.Sources[0].ProgramId);
        Assert.AreEqual("second", test.Sources[1].ProgramId);
        Assert.AreEqual((uint)100, test.Performance!.Iterations);
        var metric = test.Expectation.Performance!.Profiles[0].Metrics[0];
        Assert.AreEqual("0.172", metric.Reference);
        var sourceBytes = Encoding.UTF8.GetBytes(markdown);
        Assert.AreEqual("0.172", Encoding.UTF8.GetString(sourceBytes, metric.ReferenceRange.ByteOffset, metric.ReferenceRange.ByteLength));
        CollectionAssert.Contains(test.Requires.Optional.ToArray(), "performance");
    }

    [TestMethod]
    public void ParsesYamlSubsetQuotingAndDeclarativeNativeHandler()
    {
        const string markdown = """
---
formatVersion: 1
suiteId: native.handlers
kind: scriptApi
level: atomic
tags: ['one', "two\u0020words"]
---
## Test: Native
```yaml
gesBlock: case
id: emit
nativeHandlers:
  - message: Notify
    parameters: [playerId, count]
    priority: -2
    throw: false
    emit:
      - name: Seen
        forwardArguments: true
```
```ges
on Start() { emit Notify() }
```
### Steps
| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |
```yaml
gesBlock: expect
steps: { run: { accepted: true } }
```
""";

        var test = ConformanceMarkdownParser.Parse(markdown).Cases[0];

        CollectionAssert.AreEqual(new[] { "one", "two words" }, test.Tags.ToArray());
        Assert.HasCount(1, test.NativeHandlers);
        Assert.AreEqual(-2, test.NativeHandlers[0].Priority);
        Assert.IsTrue(test.NativeHandlers[0].Emits[0].ForwardArguments);
        CollectionAssert.Contains(test.Requires.Core.ToArray(), "native-handlers");
    }

    [TestMethod]
    public void BindsTypedMetadataAndDiagnosticExpectations()
    {
        const string metadataMarkdown = """
---
formatVersion: 1
suiteId: metadata.typed
kind: compileMetadata
level: atomic
---
## Test: Resources
```yaml
gesBlock: case
id: resources
```
```ges
on Start(value) {}
```
```yaml
gesBlock: expect
metadata:
  messageDefinitions:
    - name: Start
      count: 1
      signatureIds: ["Start(value)"]
  programResources:
    requiredRegisterCount: 2
    requiredCallStackDepth: 0
  handlerResources:
    - name: Start
      signatureId: "Start(value)"
      requiredRegisterCount: 2
```
""";
        const string diagnosticMarkdown = """
---
formatVersion: 1
suiteId: diagnostic.typed
kind: compileError
level: atomic
---
## Test: Missing symbol
```yaml
gesBlock: case
id: missing
```
```ges
on Start() { missing() }
```
```yaml
gesBlock: expect
error:
  phase: validate
  code: validate.missingCallable
  symbol: missing
  line: 1
  column: 14
```
""";

        var metadata = ConformanceMarkdownParser.Parse(metadataMarkdown).Cases[0].Expectation.Metadata!;
        var diagnostic = ConformanceMarkdownParser.Parse(diagnosticMarkdown).Cases[0].Expectation.Error!;

        Assert.AreEqual("Start", metadata.MessageDefinitions[0].Name);
        Assert.AreEqual((uint)2, metadata.ProgramResources!.RequiredRegisterCount);
        Assert.AreEqual("Start(value)", metadata.HandlerResources[0].SignatureId);
        Assert.AreEqual("validate", diagnostic.Phase);
        Assert.AreEqual("missing", diagnostic.Symbol);
        Assert.AreEqual((uint)14, diagnostic.Column);
    }

    [TestMethod]
    public void BindsPortableProgramBinaryFixtureManifest()
    {
        const string markdown = """
---
formatVersion: 1
suiteId: binary.fixture
kind: programBinary
level: atomic
---
## Test: Fixture
```yaml
gesBlock: case
id: valid
binaryFixture:
  id: gesb-v1-valid
  resourceId: gesb-v1.valid
  relativePath: GesbV1/valid.gesb
  sha256: 0000000000000000000000000000000000000000000000000000000000000000
  compilerId: steph.ges.compiler.csharp
  compilerVersion: 0.1.0
  programVersion: 42
```
```yaml
gesBlock: expect
binary:
  outcome: valid
  rewriteByteExact: true
  moduleName: fixture
```
""";

        var testCase = ConformanceMarkdownParser.Parse(markdown).Cases[0];

        Assert.AreEqual(ConformanceTestKind.ProgramBinary, testCase.Kind);
        Assert.AreEqual("gesb-v1.valid", testCase.BinaryFixture!.ResourceId);
        Assert.AreEqual("GesbV1/valid.gesb", testCase.BinaryFixture.RelativePath);
        Assert.AreEqual((ulong)42, testCase.BinaryFixture.ProgramVersion);
        Assert.AreEqual(ConformanceBinaryOutcome.Valid, testCase.Expectation.Binary!.Outcome);
        Assert.IsTrue(testCase.Expectation.Binary.RewriteByteExact);
        CollectionAssert.Contains(testCase.Requires.Core.ToArray(), "program-binary");
    }

    [TestMethod]
    public void RejectsUnsupportedYamlFeatures()
    {
        const string markdown = "---\nformatVersion: 1\nsuiteId: yaml.unsupported\nkind: bytecode\nlevel: atomic\ntitle: |\n---\n## Test: A\n";

        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(markdown));

        Assert.AreEqual(ConformanceDiagnosticCodes.YamlUnsupportedFeature, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    [DataRow("not frontmatter", ConformanceDiagnosticCodes.MissingFrontmatter)]
    [DataRow("---\nformatVersion: 1", ConformanceDiagnosticCodes.UnterminatedFrontmatter)]
    [DataRow("---\nformatVersion: 2\nsuiteId: sample\nkind: bytecode\nlevel: atomic\n---\n## Test: A\n```yaml\ngesBlock: case\nid: a\n```\n" +
             "```ges\non A() {}\n```\n```yaml\ngesBlock: expect\nopcodes: { contains: [ReturnVoid] }\n```", ConformanceDiagnosticCodes.SchemaUnsupportedVersion)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecode\nlevel: atomic\n---\n## Test:A", ConformanceDiagnosticCodes.InvalidTestHeading)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecode\nlevel: atomic\n---\n## Test: A\n```ges-source\nx", ConformanceDiagnosticCodes.UnterminatedFence)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecode\nlevel: atomic\n---\n## Test: A\n```yaml\ngesBlock: case\nid: a\nid: b\n```", ConformanceDiagnosticCodes.YamlDuplicateKey)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecode\nlevel: atomic\nunknown: true\n---\n## Test: A\n```yaml\ngesBlock: case\nid: a\n```", ConformanceDiagnosticCodes.SchemaUnknownField)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: scriptApi\nlevel: atomic\n---\n## Test: A\n```yaml\ngesBlock: case\nid: a\n```\n" +
             "```ges\non A() {}\n```\n### Steps\n| wrong | table |\n| --- | --- |", ConformanceDiagnosticCodes.InvalidStepsTable)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecode\nlevel: atomic\n---\n## Test: A\n```yaml\nid: a\n```", ConformanceDiagnosticCodes.SchemaMissingField)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecode\nlevel: atomic\n---\n## Test: A\n```yaml\ngesBlock: result\nid: a\n```", ConformanceDiagnosticCodes.SchemaInvalidValue)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecode\nlevel: atomic\n---\n## Test: A\n```yaml ges-case\nid: a\n```", ConformanceDiagnosticCodes.UnknownSemanticFence)]
    [DataRow("---\nformatVersion: 1\nsuiteId: sample\nkind: bytecodeSnapshot\nlevel: atomic\n---\n## Test: A\n```yaml\ngesBlock: case\nid: a\n```\n```yaml\ngesBlock: case\nid: b\n```", ConformanceDiagnosticCodes.SchemaInvalidCardinality)]
    public void InvalidDocumentsHaveStableDiagnostic(string markdown, string code)
    {
        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(markdown));

        Assert.HasCount(1, exception.Diagnostics);
        Assert.AreEqual(code, exception.Diagnostics[0].Code);
        Assert.IsGreaterThanOrEqualTo(1, exception.Diagnostics[0].Range.Line);
        Assert.IsGreaterThanOrEqualTo(1, exception.Diagnostics[0].Range.Column);
    }

    [TestMethod]
    public void EnforcesPortableLimits()
    {
        const string markdown = """
---
formatVersion: 1
suiteId: limits
kind: bytecodeSnapshot
level: atomic
---
## Test: One
```yaml
gesBlock: case
id: one
```
```ges
on A() {}
```
```gesa
code
```
""";
        var limits = new ConformanceParserLimits { MaxDocumentBytes = 10 };

        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(markdown, limits));

        Assert.AreEqual(ConformanceDiagnosticCodes.YamlLimitExceeded, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    public void TreatsYamlOutsideTestsAsDocumentation()
    {
        const string markdown = "---\nformatVersion: 1\nsuiteId: prose.yaml\nkind: bytecodeSnapshot\nlevel: atomic\n---\n```yaml\nordinary: documentation\n```\n" +
                                "## Test: One\n```yaml\ngesBlock: case\nid: one\n```\n```ges\non A() {}\n```\n```gesa\ncode\n```\n";

        var document = ConformanceMarkdownParser.Parse(markdown);

        Assert.AreEqual("one", document.Cases[0].Id);
    }

    [TestMethod]
    public void RejectsInvalidUtf8WithoutPlatformDecoderException()
    {
        var exception = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(new byte[] { 0xff, 0xfe }));

        Assert.AreEqual(ConformanceDiagnosticCodes.InvalidUtf8, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    public void TreatsEmbeddedUfeffAsContentInsteadOfDocumentBom()
    {
        var bytes = Encoding.UTF8.GetBytes("---\nformatVersion: 1\nsuiteId: bom.content\nkind: bytecodeSnapshot\nlevel: atomic\n---\n\ufeffprose\n## Test: One\n```yaml\ngesBlock: case\nid: one\n```\n```ges\non A() {}\n```\n```gesa\ncode\n```\n");

        var document = ConformanceMarkdownParser.Parse(bytes);

        Assert.IsFalse(document.Source.HasByteOrderMark);
        Assert.AreEqual("one", document.Cases[0].Id);
    }

    [TestMethod]
    public void SourceDocumentAndCollectionsAreDefensiveCopies()
    {
        var bytes = Encoding.UTF8.GetBytes("---\nformatVersion: 1\nsuiteId: immutable\nkind: bytecodeSnapshot\nlevel: atomic\n---\n## Test: One\n```yaml\ngesBlock: case\nid: one\n```\n```ges\non A() {}\n```\n```gesa\ncode\n```\n");
        var document = ConformanceMarkdownParser.Parse(bytes);
        bytes[0] = (byte)'X';

        Assert.AreNotEqual((byte)0xef, document.Source.Utf8Bytes[0]);
        Assert.AreEqual((byte)'-', document.Source.Utf8Bytes[0]);
        Assert.IsInstanceOfType<System.Collections.ObjectModel.ReadOnlyCollection<ConformanceCase>>(document.Cases);
    }

    [TestMethod]
    public void AcceptsLiteralAstralScalarsInDoubleQuotedYamlStrings()
    {
        const string markdown = "---\nformatVersion: 1\nsuiteId: astral.scalar\nkind: valueApi\nlevel: atomic\n---\n" +
                                "## Test: One\n```yaml\ngesBlock: case\nid: one\nvalueApi:\n  value: { type: \":Text\", value: \"𐀀\" }\n```\n" +
                                "```yaml\ngesBlock: expect\nvalue:\n  normalized: { type: \":Text\", value: \"𐀀\" }\n```\n";

        var document = ConformanceMarkdownParser.Parse(markdown);

        Assert.AreEqual("𐀀", document.Cases[0].ValueApi!.Value.Value);
    }

    private static string FindFixture(string relativePath)
    {
        var candidate = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "MarkdownV1", relativePath);
        if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException("Could not find conformance parser fixture.", candidate);
    }

    private static byte[] ReadVerifiedFixture(string fixtureId, string relativePath, string expectedSha256)
    {
        var bytes = File.ReadAllBytes(FindFixture(relativePath));
        Assert.AreEqual(expectedSha256, Convert.ToHexString(SHA256.HashData(bytes)), "Fixture bytes differ for " + fixtureId + ".");
        return bytes;
    }

    private static IReadOnlyList<ParserFixture> ReadFixtureManifest()
    {
        var lines = File.ReadAllLines(FindFixture("manifest.tsv"));
        Assert.IsGreaterThan(1, lines.Length);
        Assert.AreEqual("formatVersion\tfixtureId\toutcome\trelativePath\tsha256\texpectedSuiteId\texpectedCaseId\texpectedDiagnosticCode", lines[0]);
        var result = new List<ParserFixture>(lines.Length - 1);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split('\t');
            Assert.HasCount(8, fields, "Invalid parser fixture manifest row " + (index + 1) + ".");
            Assert.AreEqual("1", fields[0]);
            Assert.IsTrue(fields[2] is "valid" or "invalid");
            Assert.AreEqual(64, fields[4].Length);
            result.Add(new ParserFixture(fields[1], fields[2], fields[3], fields[4], fields[5], fields[6], fields[7]));
        }
        return result;
    }

    private sealed record ParserFixture(string Id, string Outcome, string RelativePath, string Sha256, string ExpectedSuiteId, string ExpectedCaseId, string ExpectedDiagnosticCode);
}
