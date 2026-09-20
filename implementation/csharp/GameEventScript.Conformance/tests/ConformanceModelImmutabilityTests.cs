// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Conformance;

namespace GameEventScript.Tests.Conformance;

[TestClass]
public sealed class ConformanceModelImmutabilityTests
{
    [TestMethod]
    public void SourceBytesCannotChangeReceivedOutputOrSourceIdentity()
    {
        var input = Encoding.UTF8.GetBytes(Source);
        var original = (byte[])input.Clone();
        var document = ConformanceMarkdownParser.Parse(input);
        var report = ConformanceRunner.RunDocument(document, ConformanceCSharpTestEnvironment.Deterministic());
        Assert.AreEqual(ConformanceCaseStatus.Passed, report.Status);
        input[0] = (byte)'Y';

        CollectionMutationProbe.ReplaceFirstIfWritable(document.Source.Utf8Bytes, (byte)'X');

        CollectionAssert.AreEqual(original, document.Source.Utf8Bytes.ToArray());
        CollectionAssert.AreEqual(original, ConformanceReceivedMarkdownWriter.ToArray(document, report));
    }

    [TestMethod]
    public void CasesRetainValidatedUniqueIdentity()
    {
        var document = ConformanceMarkdownParser.Parse(Source);

        CollectionMutationProbe.ReplaceFirstIfWritable(document.Cases, document.Cases[1]);

        CollectionAssert.AreEqual(new[] { "first", "second" }, document.Cases.Select(test => test.Id).ToArray());
        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunDocument(document, ConformanceCSharpTestEnvironment.Deterministic()).Status);
    }

    [TestMethod]
    public void NestedListsCannotChangeExecutionOrExpectations()
    {
        var document = ConformanceMarkdownParser.Parse(Source);
        var test = document.Cases[0];
        var input = test.ValueApi!.Value.Items;
        var expected = test.Expectation.ValueApi!.Normalized.Items;

        CollectionMutationProbe.ReplaceFirstIfWritable(input, input[1]);
        CollectionMutationProbe.ReplaceFirstIfWritable(expected, expected[1]);
        CollectionMutationProbe.ReplaceFirstIfWritable(test.Tags, "changed");

        Assert.AreEqual("1", input[0].Value);
        Assert.AreEqual("1", expected[0].Value);
        CollectionAssert.AreEqual(new[] { "kept", "other" }, test.Tags.ToArray());
        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunDocument(document, ConformanceCSharpTestEnvironment.Deterministic()).Status);
    }

    [TestMethod]
    public void ResourceResultsRetainTheirCopiedBytes()
    {
        var input = new byte[] { 1, 2, 3 };
        var resource = new ConformanceResourceResult(ConformanceResourceStatus.Found, input);
        input[0] = 8;

        CollectionMutationProbe.ReplaceFirstIfWritable(resource.Bytes, (byte)9);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, resource.Bytes.ToArray());
    }

    [TestMethod]
    public void EnvironmentCapabilitiesAreCopiedAndCannotBeRewritten()
    {
        var capabilities = new[] { "vm", "value-api" };
        var environment = new ConformanceRunnerEnvironment("runner", "1", "implementation", "1", capabilities);
        capabilities[0] = "changed";

        CollectionMutationProbe.ReplaceFirstIfWritable(environment.Capabilities, "changed");

        CollectionAssert.AreEqual(new[] { "value-api", "vm" }, environment.Capabilities.ToArray());
        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunDocument(ConformanceMarkdownParser.Parse(Source), environment).Status);
    }

    [TestMethod]
    public void ParseExceptionRetainsItsOriginalDiagnostic()
    {
        var error = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse("invalid"));
        var other = Assert.ThrowsExactly<ConformanceParseException>(() => ConformanceMarkdownParser.Parse(new byte[] { 0xff }));
        var diagnostic = error.Diagnostics[0];
        Assert.AreNotEqual(diagnostic.Code, other.Diagnostics[0].Code);

        CollectionMutationProbe.ReplaceFirstIfWritable(error.Diagnostics, other.Diagnostics[0]);

        Assert.AreSame(diagnostic, error.Diagnostics.Single());
    }

    [TestMethod]
    public void MeasurementsCannotBeChangedAfterPublication()
    {
        var metric = new ConformanceMeasuredMetric("run.allocated", "0", "B");
        var changed = new ConformanceMeasuredMetric("run.allocated", "999", "B");
        var input = new[] { metric };
        var measurement = new ConformancePerformanceMeasurement(input);
        input[0] = changed;

        CollectionMutationProbe.ReplaceFirstIfWritable(measurement.Metrics, changed);

        Assert.AreSame(metric, measurement.Metrics.Single());
    }

    [TestMethod]
    public void ReportCollectionsRemainConsistentWithTheirSummaryAndSerialization()
    {
        var document = ConformanceMarkdownParser.Parse(Source);
        var report = ConformanceRunner.RunDocument(document, ConformanceCSharpTestEnvironment.Deterministic());
        var original = ConformanceResultJsonWriter.ToArray(report);

        CollectionMutationProbe.ReplaceFirstIfWritable(report.Cases, report.Cases[1]);
        CollectionMutationProbe.ReplaceFirstIfWritable(report.Capabilities, "changed");
        CollectionMutationProbe.ReplaceFirstIfWritable(report.Cases[0].Tags, "changed");

        CollectionAssert.AreEqual(original, ConformanceResultJsonWriter.ToArray(report));
    }

    [TestMethod]
    [DataRow("runtime")]
    [DataRow("counts")]
    [DataRow("minimumCounts")]
    public void DictionaryViewsDoNotExposeWritableBackingStorage(string surface)
    {
        var test = ConformanceMarkdownParser.Parse(surface == "runtime" ? Source : OpcodeSource).Cases[0];
        var values = surface switch
        {
            "runtime" => test.RuntimeLimits.Values,
            "counts" => test.Expectation.Opcodes!.Counts,
            _ => test.Expectation.Opcodes!.MinimumCounts
        };
        var key = surface == "runtime" ? "maxLoopIterations" : "ReturnVoid";

        CollectionMutationProbe.ReplaceIfWritable(values, key, 999UL);

        Assert.AreEqual(2UL, values[key]);
        Assert.IsTrue(values.ContainsKey(key));
        Assert.IsFalse(values.ContainsKey("missing"));
        Assert.IsTrue(values.TryGetValue(key, out var retained));
        Assert.AreEqual(2UL, retained);
        Assert.IsFalse(values.TryGetValue("missing", out _));
        Assert.AreEqual(new KeyValuePair<string, ulong>(key, 2UL), values.Single());
        CollectionAssert.AreEqual(new[] { key }, values.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { 2UL }, values.Values.ToArray());
    }

    private const string OpcodeSource = """
---
formatVersion: 1
suiteId: immutable-opcodes
kind: bytecode
level: atomic
---
## Test: Opcodes
```yaml
gesBlock: case
id: opcodes
```
```ges
on Start() {}
```
```yaml
gesBlock: expect
opcodes:
  counts: { ReturnVoid: 2 }
  minimumCounts: { ReturnVoid: 2 }
```
""";

    private const string Source = """
---
formatVersion: 1
suiteId: immutable-model
kind: valueApi
level: atomic
tags: [kept, other]
runtimeLimits: { maxLoopIterations: 2 }
---
## Test: First
```yaml
gesBlock: case
id: first
valueApi:
  value:
    type: ":List"
    items:
      - { type: ":Number.int64", value: "1" }
      - { type: ":Number.int64", value: "2" }
```
```yaml
gesBlock: expect
value:
  normalized:
    type: ":List"
    items:
      - { type: ":Number.int64", value: "1" }
      - { type: ":Number.int64", value: "2" }
```
## Test: Second
```yaml
gesBlock: case
id: second
valueApi:
  value: { type: ":Number.int64", value: "7" }
```
```yaml
gesBlock: expect
value:
  normalized: { type: ":Number.int64", value: "7" }
```
""";
}
