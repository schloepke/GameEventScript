using StepH.GameEventScript.Api;
using StepH.GameEventScript.Conformance;

namespace StepH_GameEventScript_Tests.Conformance;

[TestClass]
public sealed class ConformanceRunnerTests
{
    private static readonly string[] RuntimeCapabilities = ["compiler", "host", "observer", "publish-sink", "vm"];

    [TestMethod]
    public void RunsRuntimeCaseWithoutMSTestAssertionsInRunner()
    {
        var document = Parse("scriptApi", """
```ges
on Start(value) {
  let result be value + 5
  emit Done(result: result)
}
```
### Steps
| step | receive | pump | budget |
| --- | --- | --- | --- |
| add | Start | frames | 2 |
```yaml
gesBlock: expect
steps:
  add:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "7" }
    local:
      - name: Done
        args:
          - name: result
            value: { type: ":integer", value: "12" }
    paused: true
```
""");

        var result = ConformanceRunner.RunCase(document, "case", Environment(RuntimeCapabilities));

        Assert.AreEqual(ConformanceCaseStatus.Passed, result.Status);
        Assert.AreEqual(ConformanceRunnerCodes.Passed, result.Code);
    }

    [TestMethod]
    public void RunsCompileErrorLoadErrorMessageMetadataAndOpcodeKinds()
    {
        var compileError = Parse("compileError", """
```ges
on Start() { missing() }
```
```yaml
gesBlock: expect
error: { phase: validate, code: validate.missingCallable, symbol: missing }
```
""");
        var loadError = Parse("loadError", """
```yaml
gesBlock: case
id: case
runtimeLimits: { maxCallDepth: 1 }
```
```ges
function leaf(value) be value + 1
function middle(value) be leaf(value: value)
on Start(value) { emit Done(result: middle(value: value)) }
```
```yaml
gesBlock: expect
error: { phase: link, code: link.requiredCallStackDepthExceeded }
```
""", includeDefaultCase: false);
        var messageApi = Parse("messageApi", """
```yaml
gesBlock: case
id: case
messageApi:
  signature: { name: Start, parameters: [value] }
  message:
    name: Start
    args:
      - name: value
        value: { type: ":integer", value: "3" }
```
```yaml
gesBlock: expect
message: { name: Start, signatureId: "Start(value)", messageSignatureId: "Start(value)", matches: true, argumentCount: 1 }
```
""", includeDefaultCase: false);
        var metadata = Parse("compileMetadata", """
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
```
""");
        var bytecode = Parse("bytecode", """
```ges
on Start() {}
```
```yaml
gesBlock: expect
opcodes: { contains: [ReturnVoid], excludes: [EmitMessage] }
```
""");

        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunCase(compileError, "case", Environment(["compiler"])).Status);
        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunCase(loadError, "case", Environment(["compiler", "host", "vm"])).Status);
        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunCase(messageApi, "case", Environment(["message-api"])).Status);
        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunCase(metadata, "case", Environment(["compiler"])).Status);
        Assert.AreEqual(ConformanceCaseStatus.Passed, ConformanceRunner.RunCase(bytecode, "case", Environment(["compiler"])).Status);
    }

    [TestMethod]
    public void RunsBytecodeSnapshotAndExposesActualOnMismatch()
    {
        const string source = "on Start() {}";
        var expected = GameEventScriptBuilder.Create().AddScript(source, "runner.case.ges").Compile().Dump();
        var passing = Parse("bytecodeSnapshot", "```ges\n" + source + "\n```\n```gesa\n" + expected + "\n```");
        var failing = Parse("bytecodeSnapshot", "```ges\n" + source + "\n```\n```gesa\nwrong\n```");
        var environment = Environment(["bytecode-snapshot", "compiler"]);

        var passed = ConformanceRunner.RunCase(passing, "case", environment);
        var failed = ConformanceRunner.RunCase(failing, "case", environment);

        Assert.AreEqual(ConformanceCaseStatus.Passed, passed.Status);
        Assert.AreEqual(ConformanceCaseStatus.Failed, failed.Status);
        Assert.IsNotNull(failed.ActualAssembler);
    }

    [TestMethod]
    public void NativeOnlyCaseDoesNotRequireCompilerVmOrPublishSink()
    {
        var document = Parse("scriptApi", """
```yaml
gesBlock: case
id: case
nativeHandlers:
  - id: ping
    message: Ping
    emit:
      - name: Pong
        args: []
```
### Steps
| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Ping | completion | |
```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Pong
```
""", includeDefaultCase: false);

        var testCase = document.Cases[0];
        CollectionAssert.AreEquivalent(new[] { "host", "native-handlers", "observer" }, testCase.Requires.Core.ToArray());
        var result = ConformanceRunner.RunCase(document, "case", Environment(["host", "native-handlers", "observer"]));
        Assert.AreEqual(ConformanceCaseStatus.Passed, result.Status);
    }

    [TestMethod]
    public void AppliesCapabilityStatusAggregateOrderingAndResultSink()
    {
        var document = Parse("bytecode", """
```ges
on Start() {}
```
```yaml
gesBlock: expect
opcodes: { contains: [ReturnVoid] }
```
""", optionalRequires: "bytecode-snapshot");
        var sink = new RecordingSink();

        var report = ConformanceRunner.RunDocument(document, Environment(["compiler"]), resultSink: sink);

        Assert.AreEqual(ConformanceCaseStatus.Skipped, report.Status);
        Assert.AreEqual(1, report.Summary.Skipped);
        CollectionAssert.AreEqual(new[] { "bytecode-snapshot" }, report.Cases[0].MissingCapabilities.ToArray());
        Assert.AreSame(report.Cases[0], sink.Results[0]);

        var missingCore = ConformanceRunner.RunCase(document, "case", Environment([]));
        Assert.AreEqual(ConformanceCaseStatus.Error, missingCore.Status);
        Assert.AreEqual(ConformanceRunnerCodes.MissingCoreCapability, missingCore.Code);

        var duplicateCorpus = ConformanceRunner.RunCorpus([document, document], Environment(["compiler"]));
        Assert.AreEqual(ConformanceCaseStatus.Error, duplicateCorpus.Status);
        Assert.AreEqual(2, duplicateCorpus.Summary.Error);
        Assert.AreEqual(ConformanceRunnerCodes.InvalidModel, duplicateCorpus.Cases[0].Code);
    }

    [TestMethod]
    public void RunsPerformanceCorrectnessBeforeProviderAndChecksBounds()
    {
        var document = Parse("performance", """
```yaml
gesBlock: case
id: case
performance: { iterations: 10, warmupIterations: 1 }
```
```ges
on Start() { emit Done() }
```
### Steps
| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |
```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
performance:
  profiles:
    test-profile:
      metrics:
        run.elapsed:
          reference: 1
          toleranceRelative: 0.1
          unit: ms
```
""", includeDefaultCase: false);
        var provider = new FixedPerformanceProvider("run.elapsed", "1.05", "ms");
        var environment = new ConformanceRunnerEnvironment("runner", "1", "impl", "1", ["compiler", "host", "observer", "performance", "publish-sink", "vm"], performanceProfileId: "test-profile", performanceProvider: provider);

        var result = ConformanceRunner.RunCase(document, "case", environment);

        Assert.AreEqual(ConformanceCaseStatus.Passed, result.Status);
        Assert.AreEqual(1, provider.CallCount);
        Assert.AreEqual("1.1", result.Performance!.Metrics[0].Allowed);
    }

    [TestMethod]
    public void ClassifiesInvalidPerformanceMeasurementsAsErrorsAndRegressionsAsFailures()
    {
        var document = Parse("performance", """
```yaml
gesBlock: case
id: case
performance: { iterations: 1 }
```
```ges
on Start() { emit Done() }
```
### Steps
| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |
```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Done
performance:
  profiles:
    test-profile:
      metrics:
        run.elapsed: { reference: 1, maximum: 2, unit: ms }
```
""", includeDefaultCase: false);

        ConformanceCaseResult Run(IConformancePerformanceProvider provider) => ConformanceRunner.RunCase(
            document,
            "case",
            new ConformanceRunnerEnvironment("runner", "1", "impl", "1", ["compiler", "host", "observer", "performance", "publish-sink", "vm"], performanceProfileId: "test-profile", performanceProvider: provider));

        var invalid = Run(new FixedPerformanceProvider("other.metric", "1", "ms"));
        var regression = Run(new FixedPerformanceProvider("run.elapsed", "3", "ms"));

        Assert.AreEqual(ConformanceCaseStatus.Error, invalid.Status);
        Assert.AreEqual(ConformanceCaseStatus.Failed, regression.Status);
        Assert.AreEqual(ConformanceRunnerCodes.PerformanceRegression, regression.Code);
    }

    [TestMethod]
    public void MSTestAdapterExposesEveryHeadingCaseSeparately()
    {
        var document = ConformanceMarkdownParser.Parse("""
---
formatVersion: 1
suiteId: adapter
kind: bytecodeSnapshot
level: atomic
---
## Test: First
```yaml
gesBlock: case
id: first
```
```ges
on First() {}
```
```gesa
first
```
## Test: Second
```yaml
gesBlock: case
id: second
```
```ges
on Second() {}
```
```gesa
second
```
""");

        var discovered = ConformanceMSTestAdapter.Cases(document).ToArray();

        Assert.HasCount(2, discovered);
        Assert.AreEqual("first", discovered[0][1]);
        Assert.AreEqual("second", discovered[1][1]);
    }

    private static ConformanceDocument Parse(string kind, string body, bool includeDefaultCase = true, string? optionalRequires = null)
    {
        var requires = optionalRequires is null ? string.Empty : "requires:\n  optional: [" + optionalRequires + "]\n";
        var caseBlock = includeDefaultCase ? "```yaml\ngesBlock: case\nid: case\n```\n" : string.Empty;
        return ConformanceMarkdownParser.Parse("---\nformatVersion: 1\nsuiteId: runner\nkind: " + kind + "\nlevel: atomic\n" + requires + "---\n## Test: Case\n" + caseBlock + body + "\n");
    }

    private static ConformanceRunnerEnvironment Environment(IReadOnlyCollection<string> capabilities)
        => new("runner", "1", "impl", "1", capabilities);

    private sealed class RecordingSink : IConformanceResultSink
    {
        internal List<ConformanceCaseResult> Results { get; } = new();
        public void CaseCompleted(ConformanceCaseResult result) => Results.Add(result);
    }

    private sealed class FixedPerformanceProvider(string id, string value, string unit) : IConformancePerformanceProvider
    {
        internal int CallCount { get; private set; }
        public ConformancePerformanceMeasurement Measure(ConformanceCase testCase, string profileId)
        {
            CallCount++;
            return new ConformancePerformanceMeasurement([new ConformanceMeasuredMetric(id, value, unit)]);
        }
    }
}
