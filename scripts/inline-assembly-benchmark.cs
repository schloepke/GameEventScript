// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

var root = args[0];
var output = args[1];
var sourceRoot = Path.Combine(root, "examples", "inline-assembly", "benchmarks");
var driver = File.ReadAllText(Path.Combine(sourceRoot, "driver.ges"));
var results = new List<object>();
const int repetitions = 2000;
const int samples = 5;
foreach (var operation in new[] { "count", "any", "all", "first" })
{
    foreach (var variant in new[] { "opcode", "loop" })
    {
        var source = File.ReadAllText(Path.Combine(sourceRoot, operation + "-" + variant + ".ges"));
        var program = GameEventScriptBuilder.Create().WithDebugInfo(GameEventScriptDebugInfoOptions.None)
            .AddScript(driver, "driver.ges").AddScript(source, operation + "-" + variant + ".ges").Compile();
        var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(1)
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxExecutionSteps = 0, MaxLoopIterations = 0 }).Build();
        var observed = GesValue.GesNothing();
        var deliveries = 0;
        var subscription = host.Subscribe("Result", ["value"], (message, _) => { observed = message.Arguments[0]; deliveries++; });
        host.Load(program);
        if (host.Start().State != GameEventScriptStartState.Ready) throw new InvalidOperationException("Startup failed.");

        // Independent semantic oracle. Controls are outside every timed sample.
        GesValue[][] controls = [[], [GesValue.GesNothing()], [GesValue.GesBoolean(false), GesValue.GesNothing(), GesValue.GesBoolean(true)],
            [GesValue.GesInteger(1), GesValue.GesFloat(double.NaN)], [GesValue.GesFloat(double.NaN)], [GesValue.GesInteger(7, GameEventScriptBytecodeInstructionUnit.UnitMeter)]];
        foreach (var values in controls) Execute(Message(values, 1), Expected(values));
        foreach (var length in new[] { 8, 128, 4096 })
        {
            var values = Enumerable.Range(0, length).Select(i => operation switch
            {
                "any" => GesValue.GesBoolean(i == length - 1),
                "all" => GesValue.GesBoolean(true),
                _ => GesValue.GesInteger(i + 1)
            }).ToArray();
            var expected = Expected(values);
            var message = Message(values, repetitions);
            for (var warmup = 0; warmup < 3; warmup++) Execute(message, expected);
            var times = new double[samples];
            var allocations = new long[samples];
            var opcodes = 0;
            for (var sample = 0; sample < samples; sample++)
            {
                var previous = deliveries;
                var allocationStart = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                host.Receive(message);
                var result = host.RunToCompletion();
                var elapsed = Stopwatch.GetTimestamp() - start;
                allocations[sample] = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                times[sample] = elapsed * (1_000_000_000d / Stopwatch.Frequency) / repetitions;
                if (result.State != GameEventScriptExecutionState.Completed || deliveries != previous + 1 || !Same(observed, expected))
                    throw new InvalidOperationException("Measurement failed.");
                opcodes = result.ExecutedOpcodes;
            }
            Array.Sort(times);
            var frameMessage = Message(values, 1);
            var previousFrameDeliveries = deliveries;
            host.Receive(frameMessage);
            var frames = 0;
            GameEventScriptExecutionResult frame;
            do
            {
                frame = host.ExecuteFrame(1);
                frames++;
            } while (frame.State == GameEventScriptExecutionState.Paused);
            if (frame.State != GameEventScriptExecutionState.Completed || deliveries != previousFrameDeliveries + 1 || !Same(observed, expected))
                throw new InvalidOperationException("Frame-budget control failed.");
            results.Add(new { operation, variant, length, repetitions, samples, medianNanoseconds = times[samples / 2], minimumNanoseconds = times[0],
                maximumNanoseconds = times[^1], maxAllocatedBytesPerOperation = allocations.Max() / (double)repetitions,
                executedOpcodesPerOperation = opcodes / (double)repetitions, codeInstructions = program.Code.Instructions.Count,
                binaryBytes = GameEventScriptProgramWriter.GetEncodedSize(program), registers = program.RequiredRegisterCount, singleOperationFramesAtBudgetOne = frames });
        }

        subscription.Unsubscribe();

        GameEventScriptMessage Message(GesValue[] values, int repeat) => GameEventScriptMessage.Create("Measure",
            [new("items", GesValue.GesList(values)), new("repetitions", GesValue.GesInteger(repeat))]);

        GesValue Expected(GesValue[] values) => operation switch
        {
            "count" => GesValue.GesInteger(values.Length),
            "any" => GesValue.GesBoolean(values.Any(Truthy)),
            "all" => GesValue.GesBoolean(values.All(Truthy)),
            _ => values.Length == 0 ? GesValue.GesNothing() : values[0]
        };

        void Execute(GameEventScriptMessage message, GesValue expected)
        {
            var previous = deliveries;
            host.Receive(message);
            var result = host.RunToCompletion();
            if (result.State != GameEventScriptExecutionState.Completed || deliveries != previous + 1 || !Same(observed, expected))
                throw new InvalidOperationException($"Control failed for {operation}/{variant}: {result.State}.");
        }
    }
}
var report = new { framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"), samples, repetitions, results };
File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(output);

static bool Same(GesValue left, GesValue right) => left.Equals(right);

static bool Truthy(GesValue value) => !value.IsNothing && (value.IsNumeric ? value.AsNumber() != 0 : value.AsBoolean());
