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
    internal sealed class Measurement(GameEventScriptProgram program, Func<long>? clock = null) : IGameEventScriptProgramProfiler
    {
        internal readonly GameEventScriptProgram Program = program;
        internal readonly long[] Counts = new long[program.Code.Count];
        internal readonly long[] Ticks = new long[program.Code.Count];
        internal readonly long[] PhaseTicks = new long[program.Code.Count * 6];
        internal readonly long[] PhaseTotals = new long[6];
        internal readonly long[] PhaseCounts = new long[6];
        private readonly long[] _prelude = new long[6];
        private int _pending = -1;
        private int _phase = -1;
        private long _start;

        public void PhaseStarting(GameEventScriptProfilePhase phase)
        {
            var end = Stopwatch.GetTimestamp();
            ClosePhase(end);
            if (phase == GameEventScriptProfilePhase.StateCheck) _pending = -1;
            _phase = (int)phase;
            _start = clock?.Invoke() ?? Stopwatch.GetTimestamp();
        }

        public void InstructionStarting(int address)
        {
            var end = Stopwatch.GetTimestamp();
            ClosePhase(end);
            Counts[address]++;
            _pending = address;
            for (var phase = 0; phase < 6; phase++)
            {
                PhaseTicks[address * 6 + phase] += _prelude[phase];
                Ticks[address] += _prelude[phase];
                _prelude[phase] = 0;
            }
            _phase = (int)GameEventScriptProfilePhase.Execute;
            _start = clock?.Invoke() ?? Stopwatch.GetTimestamp();
        }

        private void ClosePhase(long end)
        {
            if (_phase < 0) return;
            // The production timestamp is captured at callback entry. Only tests
            // substitute a deterministic clock, outside the measured interval.
            if (clock is not null) end = clock();
            var elapsed = Math.Max(0, end - _start);
            PhaseTotals[_phase] += elapsed;
            PhaseCounts[_phase]++;
            if (_pending >= 0)
            {
                PhaseTicks[_pending * 6 + _phase] += elapsed;
                Ticks[_pending] += elapsed;
            }
            else _prelude[_phase] += elapsed;
            _phase = -1;
        }

        public void FinishSlice()
        {
            var end = Stopwatch.GetTimestamp();
            ClosePhase(end);
            _pending = -1;
            Array.Clear(_prelude);
        }
    }

    internal string Outcome = "Incomplete or failed";
    internal readonly List<Measurement> Programs = [];
    private readonly Func<long>? _clock;
    private readonly long _frequency;

    internal RunProfiler(Func<long>? clock = null, long? frequency = null)
    {
        _clock = clock;
        _frequency = frequency ?? Stopwatch.Frequency;
    }

    public IGameEventScriptProgramProfiler CreateProgramProfiler(GameEventScriptProgram program)
    {
        var result = new Measurement(program, _clock);
        Programs.Add(result);
        return result;
    }

    private sealed record Row(string Name, long Count, double Nanoseconds, double[] Phases);

    internal string Markdown()
    {
        var instructions = new List<Row>();
        var opcodes = new Dictionary<string, Row>(StringComparer.Ordinal);
        var sources = new Dictionary<string, Row>(StringComparer.Ordinal);
        for (var index = 0; index < Programs.Count; index++)
        {
            var measurement = Programs[index];
            var program = measurement.Program;
            for (var address = 0; address < measurement.Counts.Length; address++)
            {
                var count = measurement.Counts[address];
                if (count == 0) continue;
                var ns = measurement.Ticks[address] * (1_000_000_000.0 / _frequency);
                var phases = Enumerable.Range(0, 6).Select(phase => measurement.PhaseTicks[address * 6 + phase] * (1_000_000_000.0 / _frequency)).ToArray();
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
                Add(opcodes, opcode, count, ns, phases);
                Add(sources, $"{index + 1}/{sourceId} / {source}", count, ns, phases);
                instructions.Add(new Row($"{index + 1}:{address} / {opcode} / {source}", count, ns, phases));
            }
        }
        var totals = Enumerable.Range(0, 6).Select(phase => Programs.Sum(p => p.PhaseTotals[phase]) * (1_000_000_000.0 / _frequency)).ToArray();
        var total = totals.Sum();
        var output = new StringBuilder("# GES opcode profile\n\n");
        output.AppendLine($"Run status: {Escape(Outcome)}.\n");
        output.AppendLine($"Implementation: C# ({Escape(RuntimeInformation.FrameworkDescription)}). Platform: {Escape(RuntimeInformation.OSDescription)} / {RuntimeInformation.ProcessArchitecture}.");
        output.AppendLine($"\nInstruction starts: {instructions.Sum(row => row.Count)}. Measured VM time: {Number(total / 1_000_000)} ms. Clock frequency: {_frequency} Hz.");
        output.AppendLine("\nInstrumented wall time, not uninstrumented execution cost. Profiler bookkeeping is excluded; clock/callback overhead remains and can dominate short phases. "
            + "Calls are exclusive; synchronous extension work is charged to its calling opcode.");
        output.AppendLine("\nCompilation, loading, native message handlers, queue waits and time between slices are excluded. "
            + "Initialization is included. Faulting instructions count as starts; a failure report may be partial. No source map means [unmapped]. Shares use total measured VM time.");
        output.AppendLine("\n## Programs\n\n| ID | Module | Code instructions |\n| --- | --- | ---: |");
        for (var index = 0; index < Programs.Count; index++) output.AppendLine($"| {index + 1} | {Escape(Programs[index].Program.ModuleName)} | {Programs[index].Program.Code.Count} |");
        output.AppendLine("\n## Loop phases\n\n| Phase | Visits | Total ms |\n| --- | ---: | ---: |");
        for (var phase = 0; phase < 6; phase++)
            output.AppendLine($"| {(GameEventScriptProfilePhase)phase} | {Programs.Sum(p => p.PhaseCounts[phase])} | {Number(totals[phase] / 1_000_000)} |");
        output.AppendLine($"\nTerminal checks without an instruction: {Number((total - instructions.Sum(row => row.Nanoseconds)) / 1_000_000)} ms. "
            + "Phase columns below are mean nanoseconds per instruction start. Execute includes opcode dispatch. Budget reservation/completion is outside the loop and excluded.");
        Table("Opcodes", opcodes.Values, null);
        Table("Source lines", sources.Values, 100);
        Table("Instructions", instructions, 100);
        return output.ToString();

        void Table(string title, IEnumerable<Row> rows, int? limit)
        {
            var sorted = rows.OrderByDescending(row => row.Nanoseconds).ThenBy(row => row.Name, StringComparer.Ordinal).ToArray();
            output.AppendLine($"\n## {title}\n");
            if (limit is { } max) output.AppendLine($"Top {Math.Min(max, sorted.Length)} of {sorted.Length}, sorted by measured time. Counts are instruction starts.\n");
            output.AppendLine("| Location | Executions | Total ms | Share | Mean ns | State ns | Budget ns | Slice ns | Fetch ns | Execute ns | Advance ns |\n| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var row in sorted.Take(limit ?? sorted.Length))
                output.AppendLine($"| {Escape(row.Name)} | {row.Count} | {Number(row.Nanoseconds / 1_000_000)} | {Number(total == 0 ? 0 : row.Nanoseconds / total * 100)}% | {Number(row.Nanoseconds / row.Count)} | "
                    + $"{string.Join(" | ", row.Phases.Select(value => Number(value / row.Count)))} |");
        }
    }

    private static void Add(Dictionary<string, Row> rows, string key, long count, double ns, double[] phases)
    {
        if (!rows.TryGetValue(key, out var previous)) previous = new Row(key, 0, 0, new double[6]);
        for (var phase = 0; phase < 6; phase++) previous.Phases[phase] += phases[phase];
        rows[key] = previous with { Count = previous.Count + count, Nanoseconds = previous.Nanoseconds + ns };
    }

    private static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("[", "&#91;").Replace("]", "&#93;").Replace("|", "&#124;").Replace("`", "&#96;").Replace("\r", " ").Replace("\n", " ");

    internal void Write(string path) => ToolFileOutput.Write(Path.GetFullPath(path), Encoding.UTF8.GetBytes(Markdown()));
}
