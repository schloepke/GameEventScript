// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tool;

// Measures the steady state of a compiled program in this process; the contract it shares with `ges bench` is in the
// CLI README (In-process benchmark).
internal static class BenchCommand
{
    private const string HelpText = """
Usage:
  dotnet ges bench <program.gesb> [--entry <name>] [--warmup <count>] [--iterations <count>] [--min-time <ms>]
                   [--fresh-instance] [--gc-between] [--per-iteration] [--json]
                   [--seed <integer>] [--max-messages <count>] [--max-steps <count>]

Measure the steady state of a compiled program in this process: load it once, warm up, then time every run of the
entry message. The CLI README describes the output, which 'ges bench' shares.

Options:
  --entry <name>        Message every run sends (default: Main); Main(args) receives an empty list.
  --warmup <count>      Untimed runs before measuring (default: 20).
  --iterations <count>  Timed runs (default: 50).
  --min-time <ms>       Continue warming up until this many milliseconds have passed.
  --fresh-instance      Create the Host and instance for every run; their creation is timed separately.
  --gc-between          Collect garbage before every run, untimed.
  --per-iteration       List every timed run.
  --json                Print one JSON object instead of text.
  --seed <integer>      Signed 64-bit random seed. Omit for a fresh host seed.
  --max-messages <n>    Maximum processed messages per pump (default: 64).
  --max-steps <n>       Maximum execution steps per handler (default: 100000).
  -h, --help            Show this help.
""";

    private static readonly string[] ConsoleEndpoints = ["ConsoleOut", "ConsoleErr", "ErrorCode"];

    private sealed class Options
    {
        internal string Path = "";
        internal string Entry = "Main";
        internal int Warmup = 20;
        internal int Iterations = 50;
        internal double MinTimeMs;
        internal bool FreshInstance, GcBetween, PerIteration, Json;
        internal long? Seed;
        internal int MaxMessages = GesRuntimeLimits.Default.MaxProcessedMessagesPerRun;
        internal int MaxSteps = GesRuntimeLimits.Default.MaxExecutionSteps;
    }

    // Records the console endpoints instead of writing them, and the first failure of a run.
    private sealed class Recorder : IGesNativeMessageHandler, IGesRuntimeObserver
    {
        internal readonly List<GameEventScriptMessage> Messages = new(64);
        internal string? Failure;

        public void Handle(GameEventScriptMessage message, GesContext context) => Messages.Add(message);

        public void RuntimeLimitReached(string limitName, string detail, int limit) => Failure ??= $"{limitName}={limit}: {detail}";

        public void RuntimeError(GesDiagnostic diagnostic) => Failure ??= $"{diagnostic.Code}: {diagnostic.Message}";
    }

    // A failure that ends the benchmark with exit code 1, reported with its code.
    private sealed class BenchFailure(string message, string code = "cli.benchFailed") : Exception(message)
    {
        internal string Code { get; } = code;
    }

    internal static int Run(string[] arguments)
    {
        if (arguments is ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        Options options;
        try { options = Parse(arguments); }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"error cli.usage: {exception.Message} Run 'dotnet ges bench --help' for usage.");
            return 2;
        }

        try
        {
            return Measure(options);
        }
        catch (BenchFailure failure)
        {
            Console.Error.WriteLine($"error {failure.Code}: {failure.Message}");
            return 1;
        }
        catch (GesProgramFormatException exception)
        {
            RunObserver.WriteDiagnostic(exception.Diagnostic, options.Path);
            return 1;
        }
        catch (GesDynamicLinkException exception)
        {
            foreach (var diagnostic in exception.Diagnostics) RunObserver.WriteDiagnostic(diagnostic, "link");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error cli.io: {exception.Message}");
            return 1;
        }
    }

    private static Options Parse(string[] arguments)
    {
        var options = new Options();
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--fresh-instance": options.FreshInstance = true; continue;
                case "--gc-between": options.GcBetween = true; continue;
                case "--per-iteration": options.PerIteration = true; continue;
                case "--json": options.Json = true; continue;
                case "--entry" or "--warmup" or "--iterations" or "--min-time" or "--seed" or "--max-messages" or "--max-steps":
                    if (index + 1 == arguments.Length) throw new ArgumentException($"Specify a value after {argument}.");
                    if (!supplied.Add(argument)) throw new ArgumentException($"Specify {argument} only once.");
                    var value = arguments[++index];
                    switch (argument)
                    {
                        case "--entry":
                            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-')) throw new ArgumentException("Specify the entry message name after --entry.");
                            options.Entry = value;
                            break;
                        case "--warmup": options.Warmup = Count(argument, value, 0); break;
                        case "--iterations": options.Iterations = Count(argument, value, 1); break;
                        case "--max-messages": options.MaxMessages = Count(argument, value, 1); break;
                        case "--max-steps": options.MaxSteps = Count(argument, value, 1); break;
                        case "--min-time":
                            if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var milliseconds) || !double.IsFinite(milliseconds))
                                throw new ArgumentException("--min-time requires a nonnegative number of milliseconds.");
                            options.MinTimeMs = milliseconds;
                            break;
                        default:
                            if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed)) throw new ArgumentException("The seed must be a signed 64-bit integer.");
                            options.Seed = seed;
                            break;
                    }
                    continue;
            }

            if (argument.StartsWith('-')) throw new ArgumentException($"Unknown bench option '{argument}'.");
            if (options.Path.Length > 0) throw new ArgumentException("Specify exactly one compiled program.");
            if (!string.Equals(Path.GetExtension(argument), ".gesb", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("bench measures a compiled .gesb and never compiles; compile the source with 'dotnet ges compile' first.");
            options.Path = argument;
        }

        if (options.Path.Length == 0) throw new ArgumentException("Specify the compiled program to measure.");
        return options;
    }

    private static int Count(string option, string value, int minimum)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum
            ? parsed
            : throw new ArgumentException($"{option} requires an integer of at least {minimum}.");

    private static int Measure(Options options)
    {
        // Load: read, decode and validate, then build, link and initialize the Host.
        var loadStarted = Stopwatch.GetTimestamp();
        var program = GameEventScriptProgram.Read(File.ReadAllBytes(options.Path));
        var entry = EntryMessage(program, options.Entry);
        var (host, recorder) = CreateHost(program, options);
        var loadMs = Milliseconds(Stopwatch.GetTimestamp() - loadStarted);

        var instanceTimes = new List<double>();
        GameEventScriptHost Instance()
        {
            if (!options.FreshInstance) return host;
            var started = Stopwatch.GetTimestamp();
            (host, recorder) = CreateHost(program, options);
            instanceTimes.Add(Milliseconds(Stopwatch.GetTimestamp() - started));
            return host;
        }

        // Warm-up: the given count, then on until --min-time has passed.
        var warmupStarted = Stopwatch.GetTimestamp();
        var warmups = 0;
        while (warmups < options.Warmup || Milliseconds(Stopwatch.GetTimestamp() - warmupStarted) < options.MinTimeMs)
        {
            var current = Instance();
            if (options.GcBetween) Collect();
            if (RunOnce(current, recorder, entry, out _) is { } failure) throw new BenchFailure($"warm-up {warmups + 1}: {failure}");
            warmups++;
        }
        instanceTimes.Clear();

        var samples = new double[options.Iterations];
        var checksum = new Fnv();
        long allocated = 0;
        var collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        for (var iteration = 0; iteration < options.Iterations; iteration++)
        {
            var current = Instance();
            if (options.GcBetween) Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var failure = RunOnce(current, recorder, entry, out var ticks);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            if (failure is not null) throw new BenchFailure($"iteration {iteration + 1}: {failure}");
            samples[iteration] = Milliseconds(ticks);
            foreach (var message in recorder.Messages) checksum.Add(message);
        }

        var collected = new long[3];
        for (var generation = 0; generation < 3; generation++) collected[generation] = GC.CollectionCount(generation) - collections[generation];
        var report = new BenchReport(options, loadMs, warmups, samples, checksum.Hex, Median(instanceTimes))
        {
            AllocatedBytesPerIteration = allocated / options.Iterations,
            Collections = collected,
        };
        Console.Write(options.Json ? report.Json() : report.Text());
        return 0;
    }

    // `<entry>(args: [])` for an `<entry>(args)` handler, otherwise `<entry>()`, as `run` sends Main(args).
    private static GameEventScriptMessage EntryMessage(GameEventScriptProgram program, string entry)
    {
        // Only the host sends its system endpoints; no message can name them.
        if (entry is "initialization" or "undeliverable")
            throw new BenchFailure($"{entry} is a system endpoint that only the host sends. Choose another message with --entry.", "cli.missingEntry");
        bool withArgs = false, withoutArgs = false;
        foreach (var binding in program.Bindings.Entries)
        {
            if (binding.RequiredTags.Count > 0 || program.StringConstants.Resolve(binding.Name) != entry) continue;
            if (binding.Kind == GesBinaryBindKind.MessageNameHandler) withoutArgs = true;
            if (binding.Kind != GesBinaryBindKind.MessageHandler) continue;
            if (binding.ArgumentNames.Count == 0) withoutArgs = true;
            if (binding.ArgumentNames.Count == 1 && program.StringConstants.Resolve(binding.ArgumentNames[0]) == "args") withArgs = true;
        }

        if (withArgs) return GameEventScriptMessage.Create(entry, [new GesMessageArgument("args", GesValue.GesList([]))]);
        if (withoutArgs) return GameEventScriptMessage.Create(entry);
        throw new BenchFailure($"No handler matches {entry}(args) or {entry}(). Choose another message with --entry.", "cli.missingEntry");
    }

    private static (GameEventScriptHost Host, Recorder Recorder) CreateHost(GameEventScriptProgram program, Options options)
    {
        var recorder = new Recorder();
        var builder = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(recorder)
            .WithRuntimeLimits(new GesRuntimeLimits { MaxProcessedMessagesPerRun = options.MaxMessages, MaxExecutionSteps = options.MaxSteps });
        if (options.Seed is { } seed) builder.WithRandomSeed(seed);
        var host = builder.Build();
        foreach (var endpoint in ConsoleEndpoints) host.SubscribeMessageName(endpoint, recorder);
        host.Load(program);
        var start = host.Start();
        if (start.State != GesStartState.Ready)
            throw new BenchFailure("initialization: " + (start.Diagnostic is { } diagnostic ? $"{diagnostic.Code}: {diagnostic.Message}" : recorder.Failure ?? start.State.ToString()));
        return (host, recorder);
    }

    // One run: send the entry message and pump until the Host is idle; only that is timed.
    private static string? RunOnce(GameEventScriptHost host, Recorder recorder, GameEventScriptMessage entry, out long ticks)
    {
        recorder.Messages.Clear();
        var started = Stopwatch.GetTimestamp();
        var accepted = host.Receive(entry);
        var result = host.RunToCompletion();
        ticks = Stopwatch.GetTimestamp() - started;
        if (!accepted) return "the host rejected the entry message";
        if (recorder.Failure is { } failure) return failure;
        if (result.State == GesExecutionState.Waiting) return "delayed messages remain; a benchmark run must finish without waiting";
        if (result.State != GesExecutionState.Completed)
            return result.Diagnostic is { } diagnostic ? $"{diagnostic.Code}: {diagnostic.Message}" : result.State.ToString();
        foreach (var message in recorder.Messages)
        {
            if (message.Name != "ErrorCode" || message.Arguments.Count != 1) continue;
            var code = message.Arguments[0];
            if (!code.IsNothing && code.AsNumber() != 0) return $"the script set ErrorCode({code})";
        }

        return null;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static double? Median(List<double> values) => values.Count == 0 ? null : BenchReport.Quantile([.. values.Order()], 0.5);

    // FNV-1a 64 over one UTF-8 line per recorded message: its name, ':' and its arguments' texts.
    private sealed class Fnv
    {
        private ulong _hash = 14695981039346656037;

        internal string Hex => _hash.ToString("x16", CultureInfo.InvariantCulture);

        internal void Add(GameEventScriptMessage message)
        {
            var line = new StringBuilder(message.Name).Append(':');
            for (var index = 0; index < message.Arguments.Count; index++) line.Append(message.Arguments[index].ToString());
            foreach (var value in Encoding.UTF8.GetBytes(line.Append('\n').ToString()))
            {
                _hash ^= value;
                _hash *= 1099511628211;
            }
        }
    }

    private sealed class BenchReport(Options options, double loadMs, int warmups, double[] samples, string checksum, double? instanceMedianMs)
    {
        internal long AllocatedBytesPerIteration { get; init; }

        internal long[] Collections { get; init; } = [0, 0, 0];

        private static bool Aot => !RuntimeFeature.IsDynamicCodeSupported;

        internal static double Quantile(double[] sorted, double q)
        {
            var position = (sorted.Length - 1) * q;
            var lower = (int)Math.Floor(position);
            var upper = Math.Min(lower + 1, sorted.Length - 1);
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
        }

        private (double Min, double Median, double Mean, double P95, double Max, double StdDev) Statistics()
        {
            double[] sorted = [.. samples.Order()];
            var mean = samples.Average();
            var variance = samples.Length < 2 ? 0 : samples.Sum(sample => (sample - mean) * (sample - mean)) / (samples.Length - 1);
            return (sorted[0], Quantile(sorted, 0.5), mean, Quantile(sorted, 0.95), sorted[^1], Math.Sqrt(variance));
        }

        private static string Os => OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsWindows() ? "windows" : "linux";

        private static string Arch => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";

        private static string RuntimeName => Aot ? "dotnet-aot" : "dotnet-jit";

        private static string RuntimeVersion => Environment.Version.ToString();

        // A setting from its DOTNET_ (or legacy COMPlus_) variable, else the runtime configuration, else its default.
        private static bool Setting(string variable, string configuration, bool fallback)
        {
            var value = Environment.GetEnvironmentVariable("DOTNET_" + variable) ?? Environment.GetEnvironmentVariable("COMPlus_" + variable);
            if (value is "0") return false;
            if (value is "1") return true;
            return AppContext.GetData(configuration) is { } data && bool.TryParse(data.ToString(), out var configured) ? configured : fallback;
        }

        private static bool? TieredCompilation => Aot ? null : Setting("TieredCompilation", "System.Runtime.TieredCompilation", true);

        private static bool? TieredPgo => Aot ? null : Setting("TieredPGO", "System.Runtime.TieredPGO", true);

        // ReadyToRun needs precompiled code in the Runtime assembly, which the JIT uses unless DOTNET_ReadyToRun=0.
        private static bool? ReadyToRun
        {
            get
            {
                if (Aot) return null;
                if (!Setting("ReadyToRun", "System.Runtime.ReadyToRun", true)) return false;
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "GameEventScript.Runtime.dll");
                if (!File.Exists(path)) return false;
                using var reader = new PEReader(File.OpenRead(path));
                return reader.PEHeaders.CorHeader?.ManagedNativeHeaderDirectory.Size > 0;
            }
        }

        private static string Number(double value, int decimals) => value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        private static string Flag(bool? value) => value is { } flag ? (flag ? "true" : "false") : "null";

        internal string Text()
        {
            var statistics = Statistics();
            var text = new StringBuilder();
            text.Append($"Benchmark: {options.Path}, entry {options.Entry}\n");
            text.Append($"Runtime: {RuntimeName} {RuntimeVersion} ({Os} {Arch})\n");
            text.Append(Aot ? "Tiering: n/a\n" : $"Tiering: TieredCompilation={Flag(TieredCompilation)}, TieredPGO={Flag(TieredPgo)}, ReadyToRun={Flag(ReadyToRun)}\n");
            text.Append($"Load: {Number(loadMs, 3)} ms\n");
            if (instanceMedianMs is { } instance) text.Append($"Instance creation (median): {Number(instance, 3)} ms\n");
            text.Append($"Warm-up: {warmups} runs\n");
            text.Append($"Iterations: {samples.Length}\n");
            if (options.PerIteration)
                for (var index = 0; index < samples.Length; index++) text.Append($"Run {index + 1}: {Number(samples[index], 3)} ms\n");
            text.Append($"Time per run (ms): min {Number(statistics.Min, 3)}, median {Number(statistics.Median, 3)}, mean {Number(statistics.Mean, 3)}, ");
            text.Append($"p95 {Number(statistics.P95, 3)}, max {Number(statistics.Max, 3)}, stddev {Number(statistics.StdDev, 3)}\n");
            text.Append($"Allocated per run: {AllocatedBytesPerIteration} bytes; collections gen0 {Collections[0]}, gen1 {Collections[1]}, gen2 {Collections[2]}\n");
            text.Append($"Checksum: {checksum}\n");
            return text.ToString();
        }

        internal string Json()
        {
            var statistics = Statistics();
            var members = new List<(string Name, string Value)>
            {
                ("runtime", ToolText.Quote(RuntimeName)),
                ("runtimeVersion", ToolText.Quote(RuntimeVersion)),
                ("os", ToolText.Quote(Os)),
                ("arch", ToolText.Quote(Arch)),
                ("script", ToolText.Quote(options.Path)),
                ("entry", ToolText.Quote(options.Entry)),
                ("freshInstance", Flag(options.FreshInstance)),
                ("loadMs", Number(loadMs, 6)),
                ("instanceMedianMs", instanceMedianMs is { } instance ? Number(instance, 6) : "null"),
                ("warmup", warmups.ToString(CultureInfo.InvariantCulture)),
                ("iterations", samples.Length.ToString(CultureInfo.InvariantCulture)),
                ("minMs", Number(statistics.Min, 6)),
                ("medianMs", Number(statistics.Median, 6)),
                ("meanMs", Number(statistics.Mean, 6)),
                ("p95Ms", Number(statistics.P95, 6)),
                ("maxMs", Number(statistics.Max, 6)),
                ("stddevMs", Number(statistics.StdDev, 6)),
                ("allocatedBytesPerIteration", AllocatedBytesPerIteration.ToString(CultureInfo.InvariantCulture)),
                ("gen0Collections", Collections[0].ToString(CultureInfo.InvariantCulture)),
                ("gen1Collections", Collections[1].ToString(CultureInfo.InvariantCulture)),
                ("gen2Collections", Collections[2].ToString(CultureInfo.InvariantCulture)),
                ("tieredCompilation", Flag(TieredCompilation)),
                ("tieredPgo", Flag(TieredPgo)),
                ("readyToRun", Flag(ReadyToRun)),
                ("checksum", ToolText.Quote(checksum)),
            };
            if (options.PerIteration) members.Add(("samplesMs", "[" + string.Join(", ", samples.Select(sample => Number(sample, 6))) + "]"));
            return "{\n" + string.Join(",\n", members.Select(member => $"  \"{member.Name}\": {member.Value}")) + "\n}\n";
        }
    }
}
