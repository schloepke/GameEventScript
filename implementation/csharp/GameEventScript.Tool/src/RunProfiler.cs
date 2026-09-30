// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

internal sealed class RunProfiler : IGameEventScriptProfiler
{
    internal sealed class Measurement(GameEventScriptProgram program, Func<long> clock) : IGameEventScriptProgramProfiler
    {
        internal readonly GameEventScriptProgram Program = program;
        internal readonly long[] Counts = new long[program.Code.Count];
        internal readonly long[] Ticks = new long[program.Code.Count];
        private int _pending = -1;
        private long _start;

        public void InstructionStarting(int address)
        {
            FinishSlice();
            Counts[address]++;
            _pending = address;
            _start = clock();
        }

        public void FinishSlice()
        {
            if (_pending < 0) return;
            var end = clock();
            Ticks[_pending] += Math.Max(0, end - _start);
            _pending = -1;
        }
    }

    internal string Outcome = "Incomplete or failed";
    internal readonly List<Measurement> Programs = [];
    private readonly Func<long> _clock;
    private readonly long _frequency;

    internal RunProfiler(Func<long>? clock = null, long? frequency = null)
    {
        _clock = clock ?? Stopwatch.GetTimestamp;
        _frequency = frequency ?? Stopwatch.Frequency;
    }

    public IGameEventScriptProgramProfiler CreateProgramProfiler(GameEventScriptProgram program)
    {
        var result = new Measurement(program, _clock);
        Programs.Add(result);
        return result;
    }

    private sealed record Row(string Name, long Count, double Nanoseconds);

    internal string Markdown()
    {
        var instructions = new List<Row>();
        var opcodes = new Dictionary<string, (long Count, double Nanoseconds)>(StringComparer.Ordinal);
        var sources = new Dictionary<string, (long Count, double Nanoseconds)>(StringComparer.Ordinal);
        for (var index = 0; index < Programs.Count; index++)
        {
            var measurement = Programs[index];
            var program = measurement.Program;
            for (var address = 0; address < measurement.Counts.Length; address++)
            {
                var count = measurement.Counts[address];
                if (count == 0) continue;
                var ns = measurement.Ticks[address] * (1_000_000_000.0 / _frequency);
                var opcode = program.Code[address].OpCode.ToString();
                var source = "[unmapped]";
                var sourceId = "none";
                if (program.SourceMap is { } map)
                    foreach (var entry in map.Entries)
                        if (address >= entry.CodeStart && address < entry.CodeStart + entry.CodeLength)
                        {
                            var document = map.Sources.First(item => item.SourceId == entry.SourceId);
                            var line = document.LineStartByteOffsets.Count(offset => offset <= entry.SourceStartByteOffset);
                            source = $"{document.SourceName}:{line}";
                            sourceId = document.SourceId.ToString(CultureInfo.InvariantCulture);
                            break;
                        }
                Add(opcodes, opcode, count, ns);
                Add(sources, $"{index + 1}/{sourceId} / {source}", count, ns);
                instructions.Add(new Row($"{index + 1}:{address} / {opcode} / {source}", count, ns));
            }
        }
        var total = instructions.Sum(row => row.Nanoseconds);
        var output = new StringBuilder("# GES opcode profile\n\n");
        output.AppendLine($"Run status: {Escape(Outcome)}.\n");
        output.AppendLine($"Implementation: C# ({Escape(RuntimeInformation.FrameworkDescription)}). Platform: {Escape(RuntimeInformation.OSDescription)} / {RuntimeInformation.ProcessArchitecture}.");
        output.AppendLine($"\nInstruction starts: {instructions.Sum(row => row.Count)}. Measured VM time: {Number(total / 1_000_000)} ms. Clock frequency: {_frequency} Hz.");
        output.AppendLine("\nInstrumented wall time, not uninstrumented execution cost. Profiler bookkeeping is excluded; clock/callback and VM loop overhead remain. Calls are exclusive; synchronous extension work is charged to its calling opcode.");
        output.AppendLine("\nCompilation, loading, native message handlers, queue waits and time between slices are excluded. "
            + "Initialization is included. Faulting instructions count as starts; a failure report may be partial. No source map means [unmapped]. Shares use total measured VM time.");
        output.AppendLine("\n## Programs\n\n| ID | Module | Code instructions |\n| --- | --- | ---: |");
        for (var index = 0; index < Programs.Count; index++) output.AppendLine($"| {index + 1} | {Escape(Programs[index].Program.ModuleName)} | {Programs[index].Program.Code.Count} |");
        Table("Opcodes", opcodes.Select(pair => new Row(pair.Key, pair.Value.Count, pair.Value.Nanoseconds)), null);
        Table("Source lines", sources.Select(pair => new Row(pair.Key, pair.Value.Count, pair.Value.Nanoseconds)), 100);
        Table("Instructions", instructions, 100);
        return output.ToString();

        void Table(string title, IEnumerable<Row> rows, int? limit)
        {
            var sorted = rows.OrderByDescending(row => row.Nanoseconds).ThenBy(row => row.Name, StringComparer.Ordinal).ToArray();
            output.AppendLine($"\n## {title}\n");
            if (limit is { } max) output.AppendLine($"Top {Math.Min(max, sorted.Length)} of {sorted.Length}, sorted by measured time. Counts are instruction starts.\n");
            output.AppendLine("| Location | Executions | Total ms | Share | Mean ns |\n| --- | ---: | ---: | ---: | ---: |");
            foreach (var row in sorted.Take(limit ?? sorted.Length))
                output.AppendLine($"| {Escape(row.Name)} | {row.Count} | {Number(row.Nanoseconds / 1_000_000)} | {Number(total == 0 ? 0 : row.Nanoseconds / total * 100)}% | {Number(row.Nanoseconds / row.Count)} |");
        }
    }

    private static void Add(Dictionary<string, (long Count, double Nanoseconds)> rows, string key, long count, double ns)
    {
        rows.TryGetValue(key, out var previous);
        rows[key] = (previous.Count + count, previous.Nanoseconds + ns);
    }

    private static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("[", "&#91;").Replace("]", "&#93;").Replace("|", "&#124;").Replace("`", "&#96;").Replace("\r", " ").Replace("\n", " ");

    internal void Write(string path) => ToolFileOutput.Write(Path.GetFullPath(path), Encoding.UTF8.GetBytes(Markdown()));
}
