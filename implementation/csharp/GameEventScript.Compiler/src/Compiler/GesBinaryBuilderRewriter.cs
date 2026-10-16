// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal interface IGesBinaryOptimizationPass
{
    bool Rewrite(GesBinaryBuilder.RewriteContext context);
}

internal sealed partial class GesBinaryBuilder
{
    // Two cleanup rounds with loop-invariant hoisting between them.
    private static readonly IGesBinaryOptimizationPass[] DefaultOptimizationPasses =
    [
        LocalConstantFoldingPass.Instance, DeadTempWriteEliminationPass.Instance, RegisterPressureReductionPass.Instance, PeepholeMoveEliminationPass.Instance, BranchSimplificationPass.Instance, JumpCleanupPass.Instance,
        LoopInvariantHoistingPass.Instance,
        LocalConstantFoldingPass.Instance, DeadTempWriteEliminationPass.Instance, RegisterPressureReductionPass.Instance, PeepholeMoveEliminationPass.Instance, BranchSimplificationPass.Instance, JumpCleanupPass.Instance
    ];

    public GesBinaryBuilder Optimize(Action<RewriteContext> rewrite)
    {
        _ = rewrite ?? throw new ArgumentNullException(nameof(rewrite));
        EnsureScopesClosed();
        var context = new RewriteContext(this, LinearizePlanItems());
        rewrite(context);
        _rewrittenItems = context.CopyItems();
        return this;
    }

    private PlanItem[] RunDefaultOptimizationPasses(IReadOnlyList<PlanItem> items)
    {
        if (_preservedRoutines.Count == 0) return OptimizeRegion(items);
        var result = new List<PlanItem>();
        var region = new List<PlanItem>();
        var entries = new HashSet<int>();
        foreach (var routine in _routines) entries.Add(routine.EntryLabel.Id);
        foreach (var item in items)
        {
            if (item.Label is { } label && entries.Contains(label.Id) && region.Count != 0) Flush();
            region.Add(item);
        }
        Flush();
        return result.ToArray();

        void Flush()
        {
            var preserved = false;
            foreach (var item in region)
                if (item.Instruction is { } instruction && _preservedRoutines.Contains(instruction.RoutineId)) preserved = true;
            result.AddRange(preserved ? region.ToArray() : OptimizeRegion(region));
            region.Clear();
        }
    }

    private readonly HashSet<int> _preservedRoutines = [];

    internal void PreserveCurrentRoutine() => _preservedRoutines.Add(CurrentRoutineId);

    private PlanItem[] OptimizeRegion(IReadOnlyList<PlanItem> items)
    {
        var context = new RewriteContext(this, items);
        foreach (var pass in DefaultOptimizationPasses) pass.Rewrite(context);
        return context.CopyItems();
    }

    internal sealed class RewriteContext
    {
        private readonly GesBinaryBuilder _builder;
        private readonly List<PlanItem> _items;

        internal RewriteContext(GesBinaryBuilder builder, IReadOnlyList<PlanItem> items)
        {
            _builder = builder;
            _items = new List<PlanItem>(items);
        }

        internal IReadOnlyList<PlanItem> Items => _items;

        public int Count => _items.Count;

        internal int RegisterCount => _builder._registers.Count;

        internal EffectAnalysis AnalyzeEffects() => new(_builder, _items);

        internal int LabelCount => _builder._labels.Count;

        // Moves the marked items, in their order, in front of `target`; every marked index is after `target`.
        internal void MoveMarkedBefore(int target, bool[] marked)
        {
            var moved = new List<PlanItem>();
            var writeIndex = target;
            for (var readIndex = target; readIndex < _items.Count; readIndex++)
            {
                if (marked[readIndex]) moved.Add(_items[readIndex]);
                else _items[writeIndex++] = _items[readIndex];
            }

            for (var index = _items.Count - 1; index >= target + moved.Count; index--) _items[index] = _items[index - moved.Count];
            for (var index = 0; index < moved.Count; index++) _items[target + index] = moved[index];
        }

        internal bool IsRoutineEntryLabel(GesLabelRef label)
        {
            foreach (var routine in _builder._routines)
                if (routine.EntryLabel.Id == label.Id) return true;
            return false;
        }

        public InstructionPlan? GetInstruction(int index) => (uint)index < (uint)_items.Count ? _items[index].Instruction : null;

        public GesLabelRef? GetLabel(int index) => (uint)index < (uint)_items.Count ? _items[index].Label : null;

        public GesSourceLocation? GetSourceRange(int index) => (uint)index < (uint)_items.Count ? _items[index].SourceRange : null;

        // An instruction of the routine that owns the items around `index`.
        public InstructionPlan CreateInstructionNear(int index, GesBytecodeOpCode opcode, GesQuantityUnit unit = GesQuantityUnit.UnitNone, GesInstructionFlag flags = GesInstructionFlag.None, GesOperand dst = default,
            GesOperand x = default, GesOperand y = default, GesOperand a = default, GesOperand b = default, GesOperand c = default, GesOperand d = default, GesOperand secondaryList = default, short? count = null,
            long? i64 = null, double? f64 = null)
        {
            var instruction = new InstructionPlan(ResolveRoutineIdNear(index), opcode, unit, flags, dst, x, y, a, b, c, d, secondaryList, count, i64, f64);
            ValidateInstruction(instruction);
            return instruction;
        }

        public bool IsTemporary(GesRegisterRef register)
        {
            _builder.RequireRegister(register);
            return _builder._registers[register.Id].IsTemporary;
        }

        public void ReplaceInstruction(int index, InstructionPlan instruction)
        {
            RequireIndex(index);
            ValidateInstruction(instruction);
            _items[index] = PlanItem.ForInstruction(instruction, _items[index].SourceRange);
        }

        public void InsertInstructionAfter(int index, InstructionPlan instruction, GesSourceLocation? sourceRange = null)
        {
            RequireIndex(index);
            ValidateInstruction(instruction);
            _items.Insert(index + 1, PlanItem.ForInstruction(instruction, sourceRange));
        }

        public void RemoveAt(int index)
        {
            RequireIndex(index);
            _items.RemoveAt(index);
        }

        internal void RemoveMarked(bool[] remove)
        {
            if (remove.Length != _items.Count) throw new ArgumentException("Removal marker length must match the current rewrite item count.", nameof(remove));
            var writeIndex = 0;
            for (var readIndex = 0; readIndex < _items.Count; readIndex++)
                if (!remove[readIndex])
                    _items[writeIndex++] = _items[readIndex];
            _items.RemoveRange(writeIndex, _items.Count - writeIndex);
        }

        internal PlanItem[] CopyItems() => _items.Count == 0 ? [] : _items.ToArray();

        private int ResolveRoutineIdNear(int index)
        {
            if (_items.Count == 0) return NoRoutineId;
            if (index < 0 || index > _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < _items.Count && _items[index].Instruction is { } current) return current.RoutineId;

            for (var i = Math.Min(index - 1, _items.Count - 1); i >= 0; i--)
                if (_items[i].Instruction is { } previous)
                    return previous.RoutineId;
            for (var i = index; i < _items.Count; i++)
                if (_items[i].Instruction is { } next)
                    return next.RoutineId;
            return NoRoutineId;
        }

        private void ValidateInstruction(InstructionPlan instruction)
        {
            if (instruction.RoutineId != NoRoutineId && (instruction.RoutineId < 0 || instruction.RoutineId >= _builder._routines.Count))
                throw new ArgumentOutOfRangeException(nameof(instruction), "Instruction references an unknown routine.");
            _builder.ValidateOperand(instruction.Destination);
            _builder.ValidateOperand(instruction.X);
            _builder.ValidateOperand(instruction.Y);
            _builder.ValidateOperand(instruction.A);
            _builder.ValidateOperand(instruction.B);
            _builder.ValidateOperand(instruction.C);
            _builder.ValidateOperand(instruction.D);
            _builder.ValidateOperand(instruction.SecondaryList);
        }

        private void RequireIndex(int index)
        {
            if ((uint)index >= (uint)_items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private static void CountRegisterReads(RewriteContext context, int[] readCounts)
    {
        var items = context.Items;
        for (var index = 0; index < items.Count; index++)
            if (items[index].Instruction is { } instruction)
                CountReads(readCounts, instruction);
    }

    // Read and write counts per register and, when requested, each register's last writing item.
    private static void CountRegisterUsage(RewriteContext context, int[] readCounts, int[] writeCounts, int[]? writeIndexes = null)
    {
        var items = context.Items;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Instruction is not { } instruction) continue;
            if (instruction.Destination.Kind == GesOperandKind.Register)
            {
                var registerId = instruction.Destination.RegisterRef.Id;
                writeCounts[registerId]++;
                if (writeIndexes is not null) writeIndexes[registerId] = index;
            }
            else if (instruction.Destination.Kind == GesOperandKind.RegisterList)
            {
                foreach (var target in instruction.Destination.RegisterListValue!)
                {
                    writeCounts[target.Id]++;
                    if (writeIndexes is not null) writeIndexes[target.Id] = index;
                }
            }

            CountReads(readCounts, instruction);
        }
    }

    // Every operand except the destination reads its registers.
    private static void CountReads(int[] readCounts, InstructionPlan instruction)
    {
        CountOperandReads(readCounts, instruction.X);
        CountOperandReads(readCounts, instruction.Y);
        CountOperandReads(readCounts, instruction.A);
        CountOperandReads(readCounts, instruction.B);
        CountOperandReads(readCounts, instruction.C);
        CountOperandReads(readCounts, instruction.D);
        CountOperandReads(readCounts, instruction.SecondaryList);
    }

    private static void CountOperandReads(int[] readCounts, GesOperand operand)
    {
        if (operand.Kind == GesOperandKind.Register) readCounts[operand.RegisterRef.Id]++;
        else if (operand.Kind == GesOperandKind.RegisterList && operand.RegisterListValue is { } registers)
            for (var index = 0; index < registers.Count; index++)
                readCounts[registers[index].Id]++;
    }

    private static int[] BuildLabelInstructionIndexes(RewriteContext context)
    {
        var maxLabelId = -1;
        for (var index = 0; index < context.Count; index++)
            if (context.GetLabel(index) is { } label)
                maxLabelId = Math.Max(maxLabelId, label.Id);
        if (maxLabelId < 0) return [];
        var result = new int[maxLabelId + 1];
        Array.Fill(result, -1);
        for (var index = 0; index < context.Count; index++)
            if (context.GetLabel(index) is { } label)
                result[label.Id] = FindNextInstructionIndex(context, index);
        return result;
    }

    private static int FindNextInstructionIndex(RewriteContext context, int index)
    {
        for (var candidate = index + 1; candidate < context.Count; candidate++)
            if (context.GetInstruction(candidate) is not null)
                return candidate;
        return -1;
    }

    private static GesLabelRef? ReadJumpTarget(InstructionPlan instruction)
    {
        if ((instruction.OpCode is GesBytecodeOpCode.Jump or
                GesBytecodeOpCode.JumpIfTrue or
                GesBytecodeOpCode.JumpIfFalse or
                GesBytecodeOpCode.JumpIfNotTrue or
                GesBytecodeOpCode.JumpIfNothing) &&
            instruction.Y.Kind == GesOperandKind.Label)
        {
            return instruction.Y.LabelRef;
        }

        return null;
    }

    private sealed class LocalConstantFoldingPass : IGesBinaryOptimizationPass
    {
        public static readonly LocalConstantFoldingPass Instance = new();

        private LocalConstantFoldingPass() { }

        public bool Rewrite(RewriteContext context)
        {
            var readCounts = new int[context.RegisterCount];
            var writeCounts = new int[context.RegisterCount];
            var writeIndexes = new int[context.RegisterCount];
            Array.Fill(writeIndexes, -1);
            CountRegisterUsage(context, readCounts, writeCounts, writeIndexes);
            var changed = false;
            for (var index = 0; index < context.Count; index++)
            {
                if (context.GetInstruction(index) is not { Destination.Kind: GesOperandKind.Register } instruction) continue;
                var replacement = FoldUnary(context, index, instruction, readCounts, writeCounts, writeIndexes) ?? FoldBinary(context, index, instruction, readCounts, writeCounts, writeIndexes);
                if (replacement is null) continue;
                context.ReplaceInstruction(index, replacement);
                changed = true;
            }

            return changed;
        }

        private static InstructionPlan? FoldUnary(RewriteContext context, int index, InstructionPlan instruction, int[] readCounts, int[] writeCounts, int[] writeIndexes)
        {
            if (instruction.X.Kind != GesOperandKind.Register) return null;
            var value = ReadLocalConstant(context, index, instruction.X.RegisterRef, readCounts, writeCounts, writeIndexes);
            if (value.Kind == ConstantKind.None) return null;
            var integer = value.Kind == ConstantKind.Integer && value.Integer != long.MinValue;
            return instruction.OpCode switch
            {
                GesBytecodeOpCode.Not when value.Kind == ConstantKind.Boolean => CreateLoadBoolean(instruction, !value.Boolean),
                GesBytecodeOpCode.HasValue => CreateLoadBoolean(instruction, value.Kind != ConstantKind.Nothing),
                GesBytecodeOpCode.Negate when integer => CreateLoadInteger(instruction, -value.Integer),
                GesBytecodeOpCode.Abs when integer => CreateLoadInteger(instruction, value.Integer < 0 ? -value.Integer : value.Integer),
                _ => null
            };
        }

        private static InstructionPlan? FoldBinary(RewriteContext context, int index, InstructionPlan instruction, int[] readCounts, int[] writeCounts, int[] writeIndexes)
        {
            if (instruction.X.Kind != GesOperandKind.Register || instruction.Y.Kind != GesOperandKind.Register) return null;
            var left = ReadLocalConstant(context, index, instruction.X.RegisterRef, readCounts, writeCounts, writeIndexes);
            var right = ReadLocalConstant(context, index, instruction.Y.RegisterRef, readCounts, writeCounts, writeIndexes);
            if (left.Kind == ConstantKind.None || right.Kind == ConstantKind.None) return null;
            // A case that does not fold, such as an overflowing sum, falls through to the later operand-kind groups.
            if (left.Kind == ConstantKind.Integer && right.Kind == ConstantKind.Integer)
            {
                var (a, b) = (left.Integer, right.Integer);
                var divisible = b != 0 && !(a == long.MinValue && b == -1);
                var folded = instruction.OpCode switch
                {
                    GesBytecodeOpCode.Equal => CreateLoadBoolean(instruction, a == b),
                    GesBytecodeOpCode.NotEqual => CreateLoadBoolean(instruction, a != b),
                    GesBytecodeOpCode.Less => CreateLoadBoolean(instruction, a < b),
                    GesBytecodeOpCode.Greater => CreateLoadBoolean(instruction, a > b),
                    GesBytecodeOpCode.LessOrEqual => CreateLoadBoolean(instruction, a <= b),
                    GesBytecodeOpCode.GreaterOrEqual => CreateLoadBoolean(instruction, a >= b),
                    GesBytecodeOpCode.Add when CanAdd(a, b) => CreateLoadInteger(instruction, a + b),
                    GesBytecodeOpCode.Subtract when CanSubtract(a, b) => CreateLoadInteger(instruction, a - b),
                    GesBytecodeOpCode.Multiply when CanMultiply(a, b) => CreateLoadInteger(instruction, a * b),
                    GesBytecodeOpCode.IntegerDivide when divisible => CreateLoadInteger(instruction, a / b),
                    GesBytecodeOpCode.Modulo when divisible => CreateLoadInteger(instruction, FloorModulo(a, b)),
                    _ => null
                };
                if (folded is not null) return folded;
            }

            if (left.Kind == ConstantKind.Boolean && right.Kind == ConstantKind.Boolean)
            {
                var (a, b) = (left.Boolean, right.Boolean);
                var folded = instruction.OpCode switch
                {
                    GesBytecodeOpCode.Equal => CreateLoadBoolean(instruction, a == b),
                    GesBytecodeOpCode.NotEqual => CreateLoadBoolean(instruction, a != b),
                    GesBytecodeOpCode.And => CreateLoadBoolean(instruction, a && b),
                    GesBytecodeOpCode.Or => CreateLoadBoolean(instruction, a || b),
                    GesBytecodeOpCode.Xor => CreateLoadBoolean(instruction, a ^ b),
                    GesBytecodeOpCode.Implies => CreateLoadBoolean(instruction, !a || b),
                    _ => null
                };
                if (folded is not null) return folded;
            }

            if (left.Kind != ConstantKind.Nothing && right.Kind != ConstantKind.Nothing) return null;
            return instruction.OpCode switch
            {
                GesBytecodeOpCode.Equal => CreateLoadBoolean(instruction, left.Kind == ConstantKind.Nothing && right.Kind == ConstantKind.Nothing),
                GesBytecodeOpCode.NotEqual => CreateLoadBoolean(instruction, left.Kind != ConstantKind.Nothing || right.Kind != ConstantKind.Nothing),
                _ => null
            };
        }

        // The modulo result takes the divisor's sign, as the VM computes it.
        private static long FloorModulo(long a, long b)
        {
            var modulo = a % b;
            return modulo != 0 && (modulo < 0 && b > 0 || modulo > 0 && b < 0) ? modulo + b : modulo;
        }

        private static ConstantValue ReadLocalConstant(RewriteContext context, int consumerIndex, GesRegisterRef register, int[] readCounts, int[] writeCounts, int[] writeIndexes)
        {
            if (!context.IsTemporary(register) || readCounts[register.Id] != 1 || writeCounts[register.Id] != 1) return default;
            var writerIndex = writeIndexes[register.Id];
            if (writerIndex < 0 || writerIndex >= consumerIndex || !IsSameBasicBlock(context, writerIndex, consumerIndex)) return default;
            if (context.GetInstruction(writerIndex) is not { Destination.Kind: GesOperandKind.Register } writer || writer.Destination.RegisterRef.Id != register.Id) return default;
            return writer.OpCode switch
            {
                GesBytecodeOpCode.LoadInteger when writer.Unit == GesQuantityUnit.UnitNone && writer.I64.HasValue => ConstantValue.IntegerValue(writer.I64.Value),
                GesBytecodeOpCode.LoadTrue => ConstantValue.BooleanValue(true),
                GesBytecodeOpCode.LoadFalse => ConstantValue.BooleanValue(false),
                GesBytecodeOpCode.LoadNothing => ConstantValue.NothingValue,
                _ => default
            };
        }

        // No label lies between the two items, so control reaches the later one only through the earlier one.
        private static bool IsSameBasicBlock(RewriteContext context, int start, int end)
        {
            for (var index = start + 1; index < end; index++)
                if (context.GetLabel(index).HasValue)
                    return false;
            return true;
        }

        private static InstructionPlan CreateLoadInteger(InstructionPlan source, long value) => CreateLoad(source, GesBytecodeOpCode.LoadInteger, value);

        private static InstructionPlan CreateLoadBoolean(InstructionPlan source, bool value) => CreateLoad(source, value ? GesBytecodeOpCode.LoadTrue : GesBytecodeOpCode.LoadFalse, null);

        // Keeps routine and destination; every other operand of the folded operation is cleared.
        private static InstructionPlan CreateLoad(InstructionPlan source, GesBytecodeOpCode opcode, long? value)
            => new(source.RoutineId, opcode, GesQuantityUnit.UnitNone, GesInstructionFlag.None, source.Destination, default, default, default, default, default, default, default, null, value, null);

        private static bool CanAdd(long left, long right) => right > 0 ? left <= long.MaxValue - right : left >= long.MinValue - right;

        private static bool CanSubtract(long left, long right) => right < 0 ? left <= long.MaxValue + right : left >= long.MinValue + right;

        private static bool CanMultiply(long left, long right)
        {
            if (left == 0 || right == 0) return true;
            if (left == -1) return right != long.MinValue;
            if (right == -1) return left != long.MinValue;
            var result = left * right;
            return result / right == left;
        }

        private enum ConstantKind { None, Nothing, Boolean, Integer }

        private readonly record struct ConstantValue(ConstantKind Kind, bool Boolean = false, long Integer = 0)
        {
            public static readonly ConstantValue NothingValue = new(ConstantKind.Nothing);

            public static ConstantValue BooleanValue(bool value) => new(ConstantKind.Boolean, Boolean: value);

            public static ConstantValue IntegerValue(long value) => new(ConstantKind.Integer, Integer: value);
        }
    }

    private sealed class DeadTempWriteEliminationPass : IGesBinaryOptimizationPass
    {
        public static readonly DeadTempWriteEliminationPass Instance = new();

        private DeadTempWriteEliminationPass() { }

        public bool Rewrite(RewriteContext context)
        {
            EffectAnalysis? effects = null;
            var changed = false;
            var anyRemoved = true;
            while (anyRemoved)
            {
                anyRemoved = false;
                var readCounts = new int[context.RegisterCount];
                CountRegisterReads(context, readCounts);
                var remove = new bool[context.Count];
                for (var index = 0; index < context.Count; index++)
                {
                    if (context.GetInstruction(index) is not { Destination.Kind: GesOperandKind.Register } instruction || !context.IsTemporary(instruction.Destination.RegisterRef) ||
                        readCounts[instruction.Destination.RegisterRef.Id] != 0 || !EffectAnalysis.CanDiscardResult(instruction.OpCode) || HasProgramEffects(context, instruction, ref effects))
                        continue;
                    remove[index] = true;
                    anyRemoved = true;
                    changed = true;
                }

                if (anyRemoved) context.RemoveMarked(remove);
            }

            return changed;
        }

        private static bool HasProgramEffects(RewriteContext context, InstructionPlan instruction, ref EffectAnalysis? effects)
            => instruction.OpCode == GesBytecodeOpCode.CastCustom ? (effects ??= context.AnalyzeEffects()).HasEffects(instruction) : EffectAnalysis.HasDirectEffects(instruction);
    }

    private sealed class BranchSimplificationPass : IGesBinaryOptimizationPass
    {
        public static readonly BranchSimplificationPass Instance = new();

        private BranchSimplificationPass() { }

        public bool Rewrite(RewriteContext context)
        {
            var changed = false;
            var labelInstructionIndexes = BuildLabelInstructionIndexes(context);
            var remove = new bool[context.Count];
            for (var index = 0; index < context.Count; index++)
            {
                // A conditional skip over the directly following jump becomes the inverse branch to the jump's target.
                if (context.GetInstruction(index) is not { Y.Kind: GesOperandKind.Label } branch || ReadInvertedBranchOpcode(branch) is not { } invertedOpcode ||
                    context.GetInstruction(index + 1) is not { OpCode: GesBytecodeOpCode.Jump, Y.Kind: GesOperandKind.Label } jump ||
                    labelInstructionIndexes[branch.Y.LabelRef.Id] != FindNextInstructionIndex(context, index + 1))
                    continue;
                context.ReplaceInstruction(index, branch with { OpCode = invertedOpcode, Y = jump.Y });
                remove[index + 1] = true;
                changed = true;
            }

            if (changed) context.RemoveMarked(remove);
            return changed;
        }

        private static GesBytecodeOpCode? ReadInvertedBranchOpcode(InstructionPlan instruction)
            => instruction.OpCode switch
            {
                GesBytecodeOpCode.JumpIfTrue => GesBytecodeOpCode.JumpIfFalse,
                GesBytecodeOpCode.JumpIfFalse or GesBytecodeOpCode.JumpIfNotTrue => GesBytecodeOpCode.JumpIfTrue,
                _ => null
            };
    }

    private sealed class JumpCleanupPass : IGesBinaryOptimizationPass
    {
        public static readonly JumpCleanupPass Instance = new();

        private JumpCleanupPass() { }

        public bool Rewrite(RewriteContext context)
        {
            var changed = false;
            var labelInstructionIndexes = BuildLabelInstructionIndexes(context);
            var remove = new bool[context.Count];

            for (var index = 0; index < context.Count; index++)
            {
                if (context.GetInstruction(index) is not { } instruction) continue;
                if (ReadJumpTarget(instruction) is { } target)
                {
                    var targetInstructionIndex = labelInstructionIndexes[target.Id];
                    if (targetInstructionIndex == FindNextInstructionIndex(context, index))
                    {
                        remove[index] = true;
                        changed = true;
                        continue;
                    }

                    // Threads a jump to a jump one step, unless that jump targets the same label.
                    if (targetInstructionIndex >= 0 && context.GetInstruction(targetInstructionIndex) is { OpCode: GesBytecodeOpCode.Jump, Y.Kind: GesOperandKind.Label } targetJump && targetJump.Y.LabelRef.Id != target.Id)
                    {
                        context.ReplaceInstruction(index, instruction with { Y = targetJump.Y });
                        changed = true;
                    }
                }

                // Code after an unconditional stop is unreachable until the next label.
                if (!IsUnconditionalControlStop(instruction.OpCode)) continue;
                for (var deadIndex = index + 1; deadIndex < context.Count && !context.GetLabel(deadIndex).HasValue; deadIndex++)
                {
                    if (context.GetInstruction(deadIndex) is null) continue;
                    remove[deadIndex] = true;
                    changed = true;
                }
            }

            if (changed) context.RemoveMarked(remove);
            return changed;
        }

        private static bool IsUnconditionalControlStop(GesBytecodeOpCode opcode) => opcode is GesBytecodeOpCode.Jump or GesBytecodeOpCode.ReturnVoid or GesBytecodeOpCode.ReturnValue;
    }

    private sealed class RegisterPressureReductionPass : IGesBinaryOptimizationPass
    {
        public static readonly RegisterPressureReductionPass Instance = new();

        private RegisterPressureReductionPass() { }

        public bool Rewrite(RewriteContext context)
        {
            var readCounts = new int[context.RegisterCount];
            var writeCounts = new int[context.RegisterCount];
            var writeIndexes = new int[context.RegisterCount];
            Array.Fill(writeIndexes, -1);
            CountRegisterUsage(context, readCounts, writeCounts, writeIndexes);
            var changed = false;
            var remove = new bool[context.Count];
            for (var moveIndex = 0; moveIndex < context.Count; moveIndex++)
            {
                if (context.GetInstruction(moveIndex) is not { OpCode: GesBytecodeOpCode.Move, Flags: GesInstructionFlag.None, X.Kind: GesOperandKind.Register, Destination.Kind: GesOperandKind.Register } move ||
                    !context.IsTemporary(move.X.RegisterRef))
                    continue;
                var temp = move.X.RegisterRef;
                if (readCounts[temp.Id] != 1 || writeCounts[temp.Id] != 1) continue;
                var writerIndex = writeIndexes[temp.Id];
                if (writerIndex < 0 || writerIndex >= moveIndex || context.GetInstruction(writerIndex) is not { Destination.Kind: GesOperandKind.Register } writer || writer.RoutineId != move.RoutineId ||
                    writer.Destination.RegisterRef.Id != temp.Id || writer.ReadsRegister(move.Destination.RegisterRef) || !CanRetargetDestination(writer.OpCode) ||
                    !CanMoveDestinationWriteEarlier(context, writerIndex, moveIndex, move.Destination.RegisterRef))
                    continue;
                context.ReplaceInstruction(writerIndex, writer.WithDestination(move.Destination));
                remove[moveIndex] = true;
                changed = true;
            }

            if (changed) context.RemoveMarked(remove);
            return changed;
        }

        // The write can move to the move's position only within one basic block that neither reads nor writes the destination meanwhile.
        private static bool CanMoveDestinationWriteEarlier(RewriteContext context, int writerIndex, int moveIndex, GesRegisterRef destination)
        {
            for (var index = writerIndex + 1; index < moveIndex; index++)
            {
                if (context.GetLabel(index).HasValue) return false;
                if (context.GetInstruction(index) is not { } instruction) continue;
                if (IsControlFlowBoundary(instruction.OpCode) || instruction.ReadsRegister(destination) || instruction.Destination.Kind == GesOperandKind.Register && instruction.Destination.RegisterRef.Id == destination.Id) return false;
            }

            return true;
        }

        private static bool IsControlFlowBoundary(GesBytecodeOpCode opcode)
            => opcode is GesBytecodeOpCode.Jump or GesBytecodeOpCode.JumpIfTrue or GesBytecodeOpCode.JumpIfFalse or GesBytecodeOpCode.JumpIfNotTrue or GesBytecodeOpCode.JumpIfNothing or GesBytecodeOpCode.ReturnVoid or
                 GesBytecodeOpCode.ReturnValue or GesBytecodeOpCode.RandomPush or GesBytecodeOpCode.RandomPushConstant or GesBytecodeOpCode.RandomPop or GesBytecodeOpCode.IteratorCreateOrJump or GesBytecodeOpCode.IteratorNext;

        private static bool CanRetargetDestination(GesBytecodeOpCode opcode)
            => opcode switch
            {
                GesBytecodeOpCode.RegisterLocals or GesBytecodeOpCode.Jump or GesBytecodeOpCode.JumpIfTrue or GesBytecodeOpCode.JumpIfFalse or GesBytecodeOpCode.JumpIfNotTrue or GesBytecodeOpCode.JumpIfNothing or GesBytecodeOpCode.Call or
                    GesBytecodeOpCode.CallExternal or GesBytecodeOpCode.ReturnVoid or GesBytecodeOpCode.ReturnValue or GesBytecodeOpCode.EmitMessage or GesBytecodeOpCode.EmitMessageWithTags or GesBytecodeOpCode.EmitMessageValue or
                    GesBytecodeOpCode.EmitMessageValueWithTags or GesBytecodeOpCode.PublishMessage or GesBytecodeOpCode.PublishMessageWithTags or GesBytecodeOpCode.PublishMessageValue or GesBytecodeOpCode.PublishMessageValueWithTags or
                    GesBytecodeOpCode.StageRegister or GesBytecodeOpCode.StageNothing or GesBytecodeOpCode.StageTrue or GesBytecodeOpCode.StageFalse or GesBytecodeOpCode.StageInteger or GesBytecodeOpCode.StageFloat or
                    GesBytecodeOpCode.StageText or GesBytecodeOpCode.StageTag or GesBytecodeOpCode.StagePercentage or GesBytecodeOpCode.CreateRecord or GesBytecodeOpCode.RandomPush or GesBytecodeOpCode.RandomPushConstant or
                    GesBytecodeOpCode.RandomPop or GesBytecodeOpCode.IteratorCreateOrJump or GesBytecodeOpCode.IteratorNext or GesBytecodeOpCode.IteratorClose or GesBytecodeOpCode.ListBuilderAdd or GesBytecodeOpCode.MapBuilderAdd or
                    GesBytecodeOpCode.DistinctBuilderAdd or GesBytecodeOpCode.GroupBuilderAdd or GesBytecodeOpCode.OrderBuilderAdd => false,
                _ => true
            };
    }

    private sealed class PeepholeMoveEliminationPass : IGesBinaryOptimizationPass
    {
        public static readonly PeepholeMoveEliminationPass Instance = new();

        private PeepholeMoveEliminationPass() { }

        public bool Rewrite(RewriteContext context)
        {
            var changed = false;
            var readCounts = new int[context.RegisterCount];
            var writeCounts = new int[context.RegisterCount];
            CountRegisterUsage(context, readCounts, writeCounts);
            var remove = new bool[context.Count];
            for (var index = 0; index + 1 < context.Count; index++)
            {
                if (remove[index] || remove[index + 1] || context.GetInstruction(index) is not { Destination.Kind: GesOperandKind.Register } instruction ||
                    instruction.OpCode is GesBytecodeOpCode.IteratorNext or GesBytecodeOpCode.IteratorCreateOrJump ||
                    context.GetInstruction(index + 1) is not { OpCode: GesBytecodeOpCode.Move, Flags: GesInstructionFlag.None, X.Kind: GesOperandKind.Register, Destination.Kind: GesOperandKind.Register } move ||
                    instruction.Destination.RegisterRef.Id != move.X.RegisterRef.Id || !context.IsTemporary(instruction.Destination.RegisterRef) || instruction.ReadsRegister(move.Destination.RegisterRef) ||
                    readCounts[instruction.Destination.RegisterRef.Id] != 1 || writeCounts[instruction.Destination.RegisterRef.Id] != 1)
                    continue;
                var tempRegisterId = instruction.Destination.RegisterRef.Id;
                context.ReplaceInstruction(index, instruction.WithDestination(move.Destination));
                remove[index + 1] = true;
                readCounts[tempRegisterId]--;
                writeCounts[tempRegisterId]--;
                changed = true;
            }

            if (changed) context.RemoveMarked(remove);
            return changed;
        }
    }
}
