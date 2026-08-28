using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

internal interface IGesBinaryOptimizationPass
{
    bool Rewrite(GesBinaryBuilder.RewriteContext context);
}

internal sealed partial class GesBinaryBuilder
{
    private static readonly IGesBinaryOptimizationPass[] DefaultOptimizationPasses =
    [
        LocalConstantFoldingPass.Instance,
        DeadTempWriteEliminationPass.Instance,
        PeepholeMoveEliminationPass.Instance,
        BranchSimplificationPass.Instance,
        JumpCleanupPass.Instance,
        LocalConstantFoldingPass.Instance,
        DeadTempWriteEliminationPass.Instance,
        PeepholeMoveEliminationPass.Instance
    ];

    public GesBinaryBuilder Optimize(IGesBinaryOptimizationPass pass)
    {
        _ = pass ?? throw new ArgumentNullException(nameof(pass));
        EnsureScopesClosed();
        var context = new RewriteContext(this, LinearizePlanItems());
        pass.Rewrite(context);
        _rewrittenItems = context.CopyItems();
        return this;
    }

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
        var context = new RewriteContext(this, items);
        for (var index = 0; index < DefaultOptimizationPasses.Length; index++)
        {
            var pass = DefaultOptimizationPasses[index];
            pass.Rewrite(context);
        }

        return context.CopyItems();
    }

    internal sealed class RewriteContext
    {
        private readonly GesBinaryBuilder _builder;
        private readonly List<PlanItem> _items;

        internal RewriteContext(GesBinaryBuilder builder, IReadOnlyList<PlanItem> items)
        {
            _builder = builder;
            _items = new List<PlanItem>(items.Count);
            for (var index = 0; index < items.Count; index++)
            {
                _items.Add(items[index]);
            }
        }

        internal IReadOnlyList<PlanItem> Items => _items;

        public int Count => _items.Count;

        internal int RegisterCount => _builder._registers.Count;

        public GesRegisterRef AddRegister(string name) => _builder.AddRegister(name);

        public GesRegisterRef AddTemporaryRegister(string? name = null) => _builder.AddTemporaryRegister(name);

        public GesRegisterRef AddRegisterNear(int index, string name)
            => _builder.AddRegisterInRoutine(name, ResolveRoutineIdNear(index));

        public GesRegisterRef AddTemporaryRegisterNear(int index, string? name = null)
            => _builder.AddTemporaryRegisterInRoutine(name, ResolveRoutineIdNear(index));

        public GesLabelRef AddLabel(string? name = null) => _builder.AddLabel(name);

        public InstructionPlan? GetInstruction(int index)
        {
            if ((uint)index < (uint)_items.Count && _items[index].Instruction is { } plan)
            {
                return plan;
            }

            return null;
        }

        public GesLabelRef? GetLabel(int index)
        {
            if ((uint)index < (uint)_items.Count && _items[index].Label is { } value)
            {
                return value;
            }

            return null;
        }

        public GameEventScriptSourceLocation? GetSourceRange(int index)
        {
            if ((uint)index < (uint)_items.Count)
            {
                return _items[index].SourceRange;
            }

            return null;
        }

        public InstructionCursor GetInstructionCursor(int index)
        {
            if (GetInstruction(index) is not { } instruction)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Plan item is not an instruction.");
            }

            return new InstructionCursor(
                index,
                instruction,
                FindPreviousInstruction(index),
                FindNextInstruction(index));
        }

        public InstructionPlan CreateInstructionNear(
            int index,
            GameEventScriptBytecodeOpCode opcode,
            GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone,
            GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None,
            GesOperand dst = default,
            GesOperand x = default,
            GesOperand y = default,
            GesOperand a = default,
            GesOperand b = default,
            GesOperand c = default,
            GesOperand d = default,
            GesOperand secondaryList = default,
            short? count = null,
            long? i64 = null,
            double? f64 = null)
            => CreateInstruction(ResolveRoutineIdNear(index), opcode, unit, flags, dst, x, y, a, b, c, d, secondaryList, count, i64, f64);

        public InstructionPlan CreateInstruction(
            int routineId,
            GameEventScriptBytecodeOpCode opcode,
            GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone,
            GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None,
            GesOperand dst = default,
            GesOperand x = default,
            GesOperand y = default,
            GesOperand a = default,
            GesOperand b = default,
            GesOperand c = default,
            GesOperand d = default,
            GesOperand secondaryList = default,
            short? count = null,
            long? i64 = null,
            double? f64 = null)
        {
            var instruction = new InstructionPlan(routineId, opcode, unit, flags, dst, x, y, a, b, c, d, secondaryList, count, i64, f64);
            ValidateInstruction(instruction);
            return instruction;
        }

        public bool IsTemporary(GesRegisterRef register)
        {
            _builder.RequireRegister(register);
            return _builder._registers[register.Id].IsTemporary;
        }

        public int CountRegisterReads(GesRegisterRef register)
        {
            _builder.RequireRegister(register);
            var count = 0;
            for (var index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                if (item.Instruction is not { } instruction) continue;
                count += instruction.CountRegisterReads(register);
            }

            return count;
        }

        public int CountRegisterWrites(GesRegisterRef register)
        {
            _builder.RequireRegister(register);
            var count = 0;
            for (var index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                if (item.Instruction?.Destination.Kind == GesOperandKind.Register &&
                    item.Instruction.Destination.RegisterRef.Id == register.Id)
                {
                    count++;
                }
            }

            return count;
        }

        public void ReplaceInstruction(int index, InstructionPlan instruction)
        {
            RequireIndex(index);
            ValidateInstruction(instruction);
            _items[index] = PlanItem.ForInstruction(instruction, _items[index].SourceRange);
        }

        public void ReplaceInstruction(int index, InstructionPlan instruction, GameEventScriptSourceLocation? sourceRange)
        {
            RequireIndex(index);
            ValidateInstruction(instruction);
            _items[index] = PlanItem.ForInstruction(instruction, sourceRange);
        }

        public void ReplaceWithLabel(int index, GesLabelRef label)
        {
            RequireIndex(index);
            _builder.RequireLabel(label);
            _items[index] = PlanItem.ForLabel(label, _items[index].SourceRange);
        }

        public void ReplaceWithLabel(int index, GesLabelRef label, GameEventScriptSourceLocation? sourceRange)
        {
            RequireIndex(index);
            _builder.RequireLabel(label);
            _items[index] = PlanItem.ForLabel(label, sourceRange);
        }

        public void InsertInstructionBefore(int index, InstructionPlan instruction, GameEventScriptSourceLocation? sourceRange = null)
        {
            RequireInsertIndex(index);
            ValidateInstruction(instruction);
            _items.Insert(index, PlanItem.ForInstruction(instruction, sourceRange));
        }

        public void InsertInstructionAfter(int index, InstructionPlan instruction, GameEventScriptSourceLocation? sourceRange = null)
        {
            RequireIndex(index);
            ValidateInstruction(instruction);
            _items.Insert(index + 1, PlanItem.ForInstruction(instruction, sourceRange));
        }

        public void InsertLabelBefore(int index, GesLabelRef label, GameEventScriptSourceLocation? sourceRange = null)
        {
            RequireInsertIndex(index);
            _builder.RequireLabel(label);
            _items.Insert(index, PlanItem.ForLabel(label, sourceRange));
        }

        public void InsertLabelAfter(int index, GesLabelRef label, GameEventScriptSourceLocation? sourceRange = null)
        {
            RequireIndex(index);
            _builder.RequireLabel(label);
            _items.Insert(index + 1, PlanItem.ForLabel(label, sourceRange));
        }

        public void RemoveAt(int index)
        {
            RequireIndex(index);
            _items.RemoveAt(index);
        }

        internal void RemoveMarked(bool[] remove)
        {
            if (remove.Length != _items.Count)
            {
                throw new ArgumentException("Removal marker length must match the current rewrite item count.", nameof(remove));
            }

            var writeIndex = 0;
            for (var readIndex = 0; readIndex < _items.Count; readIndex++)
            {
                if (remove[readIndex])
                {
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    _items[writeIndex] = _items[readIndex];
                }

                writeIndex++;
            }

            if (writeIndex < _items.Count)
            {
                _items.RemoveRange(writeIndex, _items.Count - writeIndex);
            }
        }

        internal PlanItem[] CopyItems()
        {
            if (_items.Count == 0) return [];
            var result = new PlanItem[_items.Count];
            for (var index = 0; index < _items.Count; index++)
            {
                result[index] = _items[index];
            }

            return result;
        }

        private InstructionPlan? FindPreviousInstruction(int index)
        {
            for (var i = index - 1; i >= 0; i--)
            {
                if (_items[i].Instruction is { } instruction) return instruction;
            }

            return null;
        }

        private InstructionPlan? FindNextInstruction(int index)
        {
            for (var i = index + 1; i < _items.Count; i++)
            {
                if (_items[i].Instruction is { } instruction) return instruction;
            }

            return null;
        }

        private int ResolveRoutineIdNear(int index)
        {
            if (_items.Count == 0) return NoRoutineId;
            if (index < 0 || index > _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < _items.Count && _items[index].Instruction is { } current) return current.RoutineId;

            for (var i = Math.Min(index - 1, _items.Count - 1); i >= 0; i--)
            {
                if (_items[i].Instruction is { } previous) return previous.RoutineId;
            }

            for (var i = index; i < _items.Count; i++)
            {
                if (_items[i].Instruction is { } next) return next.RoutineId;
            }

            return NoRoutineId;
        }

        private void ValidateInstruction(InstructionPlan instruction)
        {
            if (instruction.RoutineId != NoRoutineId &&
                (instruction.RoutineId < 0 || instruction.RoutineId >= _builder._routines.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(instruction), "Instruction references an unknown routine.");
            }

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

        private void RequireInsertIndex(int index)
        {
            if ((uint)index > (uint)_items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    internal readonly record struct InstructionCursor(
        int Index,
        InstructionPlan Current,
        InstructionPlan? PreviousInstruction,
        InstructionPlan? NextInstruction);

    private static void CountRegisterReads(RewriteContext context, int[] readCounts)
    {
        var items = context.Items;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Instruction is not { } instruction) continue;
            CountOperandReads(readCounts, instruction.X);
            CountOperandReads(readCounts, instruction.Y);
            CountOperandReads(readCounts, instruction.A);
            CountOperandReads(readCounts, instruction.B);
            CountOperandReads(readCounts, instruction.C);
            CountOperandReads(readCounts, instruction.D);
            CountOperandReads(readCounts, instruction.SecondaryList);
        }
    }

    private static void CountOperandReads(int[] readCounts, GesOperand operand)
    {
        switch (operand.Kind)
        {
            case GesOperandKind.Register:
                readCounts[operand.RegisterRef.Id]++;
                return;
            case GesOperandKind.RegisterList when operand.RegisterListValue is not null:
                for (var index = 0; index < operand.RegisterListValue.Count; index++)
                {
                    readCounts[operand.RegisterListValue[index].Id]++;
                }

                return;
        }
    }

    private static int[] BuildLabelInstructionIndexes(RewriteContext context)
    {
        var maxLabelId = -1;
        for (var index = 0; index < context.Count; index++)
        {
            if (context.GetLabel(index) is { } label && label.Id > maxLabelId)
            {
                maxLabelId = label.Id;
            }
        }

        if (maxLabelId < 0)
        {
            return [];
        }

        var result = new int[maxLabelId + 1];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = -1;
        }

        for (var index = 0; index < context.Count; index++)
        {
            if (context.GetLabel(index) is not { } label) continue;
            result[label.Id] = FindNextInstructionIndex(context, index);
        }

        return result;
    }

    private static int FindNextInstructionIndex(RewriteContext context, int index)
    {
        for (var candidate = index + 1; candidate < context.Count; candidate++)
        {
            if (context.GetInstruction(candidate) is not null)
            {
                return candidate;
            }
        }

        return -1;
    }

    private static bool TryGetJumpTarget(InstructionPlan instruction, out GesLabelRef target)
    {
        if ((instruction.OpCode is GameEventScriptBytecodeOpCode.Jump or
                GameEventScriptBytecodeOpCode.JumpIfTrue or
                GameEventScriptBytecodeOpCode.JumpIfFalse or
                GameEventScriptBytecodeOpCode.JumpIfNotTrue or
                GameEventScriptBytecodeOpCode.JumpIfNothing) &&
            instruction.Y.Kind == GesOperandKind.Label)
        {
            target = instruction.Y.LabelRef;
            return true;
        }

        target = default;
        return false;
    }

    private sealed class LocalConstantFoldingPass : IGesBinaryOptimizationPass
    {
        public static readonly LocalConstantFoldingPass Instance = new();

        private LocalConstantFoldingPass()
        {
        }

        public bool Rewrite(RewriteContext context)
        {
            var readCounts = new int[context.RegisterCount];
            var writeCounts = new int[context.RegisterCount];
            var writeIndexes = new int[context.RegisterCount];
            for (var index = 0; index < writeIndexes.Length; index++)
            {
                writeIndexes[index] = -1;
            }

            CountRegisterUsage(context, readCounts, writeCounts, writeIndexes);

            var changed = false;
            for (var index = 0; index < context.Count; index++)
            {
                if (context.GetInstruction(index) is not { } instruction ||
                    instruction.Destination.Kind != GesOperandKind.Register)
                {
                    continue;
                }

                if (TryFoldUnary(context, index, instruction, readCounts, writeCounts, writeIndexes, out var replacement) ||
                    TryFoldBinary(context, index, instruction, readCounts, writeCounts, writeIndexes, out replacement))
                {
                    context.ReplaceInstruction(index, replacement);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool TryFoldUnary(
            RewriteContext context,
            int index,
            InstructionPlan instruction,
            int[] readCounts,
            int[] writeCounts,
            int[] writeIndexes,
            out InstructionPlan replacement)
        {
            replacement = instruction;
            if (instruction.X.Kind != GesOperandKind.Register ||
                !TryGetLocalConstant(context, index, instruction.X.RegisterRef, readCounts, writeCounts, writeIndexes, out var value))
            {
                return false;
            }

            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Not when value.Kind == ConstantKind.Boolean:
                    replacement = CreateLoadBoolean(instruction, !value.Boolean);
                    return true;
                case GameEventScriptBytecodeOpCode.HasValue:
                    replacement = CreateLoadBoolean(instruction, value.Kind != ConstantKind.Nothing);
                    return true;
                case GameEventScriptBytecodeOpCode.Negate when value.Kind == ConstantKind.Integer && value.Integer != long.MinValue:
                    replacement = CreateLoadInteger(instruction, -value.Integer);
                    return true;
                case GameEventScriptBytecodeOpCode.Abs when value.Kind == ConstantKind.Integer && value.Integer != long.MinValue:
                    replacement = CreateLoadInteger(instruction, value.Integer < 0 ? -value.Integer : value.Integer);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryFoldBinary(
            RewriteContext context,
            int index,
            InstructionPlan instruction,
            int[] readCounts,
            int[] writeCounts,
            int[] writeIndexes,
            out InstructionPlan replacement)
        {
            replacement = instruction;
            if (instruction.X.Kind != GesOperandKind.Register ||
                instruction.Y.Kind != GesOperandKind.Register ||
                !TryGetLocalConstant(context, index, instruction.X.RegisterRef, readCounts, writeCounts, writeIndexes, out var left) ||
                !TryGetLocalConstant(context, index, instruction.Y.RegisterRef, readCounts, writeCounts, writeIndexes, out var right))
            {
                return false;
            }

            if (left.Kind == ConstantKind.Integer && right.Kind == ConstantKind.Integer)
            {
                switch (instruction.OpCode)
                {
                    case GameEventScriptBytecodeOpCode.Equal:
                        replacement = CreateLoadBoolean(instruction, left.Integer == right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.NotEqual:
                        replacement = CreateLoadBoolean(instruction, left.Integer != right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.Less:
                        replacement = CreateLoadBoolean(instruction, left.Integer < right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.Greater:
                        replacement = CreateLoadBoolean(instruction, left.Integer > right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.LessOrEqual:
                        replacement = CreateLoadBoolean(instruction, left.Integer <= right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                        replacement = CreateLoadBoolean(instruction, left.Integer >= right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.Add when CanAdd(left.Integer, right.Integer):
                        replacement = CreateLoadInteger(instruction, left.Integer + right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.Subtract when CanSubtract(left.Integer, right.Integer):
                        replacement = CreateLoadInteger(instruction, left.Integer - right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.Multiply when CanMultiply(left.Integer, right.Integer):
                        replacement = CreateLoadInteger(instruction, left.Integer * right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.IntegerDivide when right.Integer != 0 && !(left.Integer == long.MinValue && right.Integer == -1):
                        replacement = CreateLoadInteger(instruction, left.Integer / right.Integer);
                        return true;
                    case GameEventScriptBytecodeOpCode.Modulo when right.Integer != 0 && !(left.Integer == long.MinValue && right.Integer == -1):
                        var modulo = left.Integer % right.Integer;
                        if (modulo != 0 && (modulo < 0 && right.Integer > 0 || modulo > 0 && right.Integer < 0)) modulo += right.Integer;
                        replacement = CreateLoadInteger(instruction, modulo);
                        return true;
                }
            }

            if (left.Kind == ConstantKind.Boolean && right.Kind == ConstantKind.Boolean)
            {
                switch (instruction.OpCode)
                {
                    case GameEventScriptBytecodeOpCode.Equal:
                        replacement = CreateLoadBoolean(instruction, left.Boolean == right.Boolean);
                        return true;
                    case GameEventScriptBytecodeOpCode.NotEqual:
                        replacement = CreateLoadBoolean(instruction, left.Boolean != right.Boolean);
                        return true;
                    case GameEventScriptBytecodeOpCode.And:
                        replacement = CreateLoadBoolean(instruction, left.Boolean && right.Boolean);
                        return true;
                    case GameEventScriptBytecodeOpCode.Or:
                        replacement = CreateLoadBoolean(instruction, left.Boolean || right.Boolean);
                        return true;
                    case GameEventScriptBytecodeOpCode.Xor:
                        replacement = CreateLoadBoolean(instruction, left.Boolean ^ right.Boolean);
                        return true;
                    case GameEventScriptBytecodeOpCode.Implies:
                        replacement = CreateLoadBoolean(instruction, !left.Boolean || right.Boolean);
                        return true;
                }
            }

            if (left.Kind == ConstantKind.Nothing || right.Kind == ConstantKind.Nothing)
            {
                switch (instruction.OpCode)
                {
                    case GameEventScriptBytecodeOpCode.Equal:
                        replacement = CreateLoadBoolean(instruction, left.Kind == ConstantKind.Nothing && right.Kind == ConstantKind.Nothing);
                        return true;
                    case GameEventScriptBytecodeOpCode.NotEqual:
                        replacement = CreateLoadBoolean(instruction, left.Kind != ConstantKind.Nothing || right.Kind != ConstantKind.Nothing);
                        return true;
                }
            }

            return false;
        }

        private static bool TryGetLocalConstant(
            RewriteContext context,
            int consumerIndex,
            GesRegisterRef register,
            int[] readCounts,
            int[] writeCounts,
            int[] writeIndexes,
            out ConstantValue value)
        {
            value = default;
            if (!context.IsTemporary(register) ||
                readCounts[register.Id] != 1 ||
                writeCounts[register.Id] != 1)
            {
                return false;
            }

            var writerIndex = writeIndexes[register.Id];
            if (writerIndex < 0 || writerIndex >= consumerIndex || !IsSameBasicBlock(context, writerIndex, consumerIndex))
            {
                return false;
            }

            if (context.GetInstruction(writerIndex) is not { } writer ||
                writer.Destination.Kind != GesOperandKind.Register ||
                writer.Destination.RegisterRef.Id != register.Id)
            {
                return false;
            }

            switch (writer.OpCode)
            {
                case GameEventScriptBytecodeOpCode.LoadInteger when writer.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone && writer.I64.HasValue:
                    value = ConstantValue.IntegerValue(writer.I64.Value);
                    return true;
                case GameEventScriptBytecodeOpCode.LoadTrue:
                    value = ConstantValue.BooleanValue(true);
                    return true;
                case GameEventScriptBytecodeOpCode.LoadFalse:
                    value = ConstantValue.BooleanValue(false);
                    return true;
                case GameEventScriptBytecodeOpCode.LoadNothing:
                    value = ConstantValue.NothingValue;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSameBasicBlock(RewriteContext context, int start, int end)
        {
            for (var index = start + 1; index < end; index++)
            {
                if (context.GetLabel(index).HasValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static InstructionPlan CreateLoadInteger(InstructionPlan source, long value)
            => source with
            {
                OpCode = GameEventScriptBytecodeOpCode.LoadInteger,
                Unit = GameEventScriptBytecodeInstructionUnit.UnitNone,
                Flags = GameEventScriptInstructionFlag.None,
                X = default,
                Y = default,
                A = default,
                B = default,
                C = default,
                D = default,
                SecondaryList = default,
                Count = null,
                I64 = value,
                F64 = null
            };

        private static InstructionPlan CreateLoadBoolean(InstructionPlan source, bool value)
            => source with
            {
                OpCode = value ? GameEventScriptBytecodeOpCode.LoadTrue : GameEventScriptBytecodeOpCode.LoadFalse,
                Unit = GameEventScriptBytecodeInstructionUnit.UnitNone,
                Flags = GameEventScriptInstructionFlag.None,
                X = default,
                Y = default,
                A = default,
                B = default,
                C = default,
                D = default,
                SecondaryList = default,
                Count = null,
                I64 = null,
                F64 = null
            };

        private static bool CanAdd(long left, long right)
            => right > 0 ? left <= long.MaxValue - right : left >= long.MinValue - right;

        private static bool CanSubtract(long left, long right)
            => right < 0 ? left <= long.MaxValue + right : left >= long.MinValue + right;

        private static bool CanMultiply(long left, long right)
        {
            if (left == 0 || right == 0) return true;
            if (left == -1) return right != long.MinValue;
            if (right == -1) return left != long.MinValue;
            var result = left * right;
            return result / right == left;
        }

        private static void CountRegisterUsage(RewriteContext context, int[] readCounts, int[] writeCounts, int[] writeIndexes)
        {
            var items = context.Items;
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index].Instruction is not { } instruction) continue;
                if (instruction.Destination.Kind == GesOperandKind.Register)
                {
                    var registerId = instruction.Destination.RegisterRef.Id;
                    writeCounts[registerId]++;
                    writeIndexes[registerId] = index;
                }

                CountOperandReads(readCounts, instruction.X);
                CountOperandReads(readCounts, instruction.Y);
                CountOperandReads(readCounts, instruction.A);
                CountOperandReads(readCounts, instruction.B);
                CountOperandReads(readCounts, instruction.C);
                CountOperandReads(readCounts, instruction.D);
                CountOperandReads(readCounts, instruction.SecondaryList);
            }
        }

        private enum ConstantKind
        {
            None,
            Nothing,
            Boolean,
            Integer
        }

        private readonly struct ConstantValue
        {
            public static readonly ConstantValue NothingValue = new(ConstantKind.Nothing);

            private ConstantValue(ConstantKind kind, bool boolean = false, long integer = 0)
            {
                Kind = kind;
                Boolean = boolean;
                Integer = integer;
            }

            public ConstantKind Kind { get; }
            public bool Boolean { get; }
            public long Integer { get; }

            public static ConstantValue BooleanValue(bool value) => new(ConstantKind.Boolean, boolean: value);

            public static ConstantValue IntegerValue(long value) => new(ConstantKind.Integer, integer: value);
        }
    }

    private sealed class DeadTempWriteEliminationPass : IGesBinaryOptimizationPass
    {
        public static readonly DeadTempWriteEliminationPass Instance = new();

        private DeadTempWriteEliminationPass()
        {
        }

        public bool Rewrite(RewriteContext context)
        {
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
                    if (context.GetInstruction(index) is not { } instruction ||
                        instruction.Destination.Kind != GesOperandKind.Register ||
                        !context.IsTemporary(instruction.Destination.RegisterRef) ||
                        readCounts[instruction.Destination.RegisterRef.Id] != 0 ||
                        !CanRemoveDeadWrite(instruction.OpCode))
                    {
                        continue;
                    }

                    remove[index] = true;
                    anyRemoved = true;
                    changed = true;
                }

                if (anyRemoved)
                {
                    context.RemoveMarked(remove);
                }
            }

            return changed;
        }

        private static bool CanRemoveDeadWrite(GameEventScriptBytecodeOpCode opcode)
        {
            return opcode switch
            {
                GameEventScriptBytecodeOpCode.Cast or
                    GameEventScriptBytecodeOpCode.CastCustom or
                    GameEventScriptBytecodeOpCode.CastUnit or
                    GameEventScriptBytecodeOpCode.CastNumeric or
                    GameEventScriptBytecodeOpCode.CheckType or
                    GameEventScriptBytecodeOpCode.CheckCustomType or
                    GameEventScriptBytecodeOpCode.CheckUnit or
                    GameEventScriptBytecodeOpCode.CheckNumeric or
                    GameEventScriptBytecodeOpCode.CheckInteger or
                    GameEventScriptBytecodeOpCode.CheckFractional or
                    GameEventScriptBytecodeOpCode.Move or
                    GameEventScriptBytecodeOpCode.MemberAccess or
                    GameEventScriptBytecodeOpCode.IndexAccess or
                    GameEventScriptBytecodeOpCode.PropertyAccess or
                    GameEventScriptBytecodeOpCode.BindHandler or
                    GameEventScriptBytecodeOpCode.LoadNothing or
                    GameEventScriptBytecodeOpCode.LoadTrue or
                    GameEventScriptBytecodeOpCode.LoadFalse or
                    GameEventScriptBytecodeOpCode.LoadInteger or
                    GameEventScriptBytecodeOpCode.LoadFloat or
                    GameEventScriptBytecodeOpCode.LoadPercentage or
                    GameEventScriptBytecodeOpCode.LoadText or
                    GameEventScriptBytecodeOpCode.LoadTag or
                    GameEventScriptBytecodeOpCode.LoadHandler or
                    GameEventScriptBytecodeOpCode.LoadMessage or
                    GameEventScriptBytecodeOpCode.CreateSeries or
                    GameEventScriptBytecodeOpCode.CreateRange or
                    GameEventScriptBytecodeOpCode.CreateRangeWithStep or
                    GameEventScriptBytecodeOpCode.CreateRangeIterator or
                    GameEventScriptBytecodeOpCode.CreateRangeIteratorWithStep or
                    GameEventScriptBytecodeOpCode.CreateRangeIteratorShort or
                    GameEventScriptBytecodeOpCode.HasValue or
                    GameEventScriptBytecodeOpCode.IsEmpty or
                    GameEventScriptBytecodeOpCode.Default or
                    GameEventScriptBytecodeOpCode.Or or
                    GameEventScriptBytecodeOpCode.And or
                    GameEventScriptBytecodeOpCode.Xor or
                    GameEventScriptBytecodeOpCode.Implies or
                    GameEventScriptBytecodeOpCode.Not or
                    GameEventScriptBytecodeOpCode.Equal or
                    GameEventScriptBytecodeOpCode.NotEqual or
                    GameEventScriptBytecodeOpCode.Less or
                    GameEventScriptBytecodeOpCode.Greater or
                    GameEventScriptBytecodeOpCode.LessOrEqual or
                    GameEventScriptBytecodeOpCode.GreaterOrEqual or
                    GameEventScriptBytecodeOpCode.Add or
                    GameEventScriptBytecodeOpCode.Subtract or
                    GameEventScriptBytecodeOpCode.Multiply or
                    GameEventScriptBytecodeOpCode.Divide or
                    GameEventScriptBytecodeOpCode.Power or
                    GameEventScriptBytecodeOpCode.IntegerDivide or
                    GameEventScriptBytecodeOpCode.Modulo or
                    GameEventScriptBytecodeOpCode.Remainder or
                    GameEventScriptBytecodeOpCode.Min or
                    GameEventScriptBytecodeOpCode.Max or
                    GameEventScriptBytecodeOpCode.Negate or
                    GameEventScriptBytecodeOpCode.Abs or
                    GameEventScriptBytecodeOpCode.LogN or
                    GameEventScriptBytecodeOpCode.Clamp or
                    GameEventScriptBytecodeOpCode.Term or
                    GameEventScriptBytecodeOpCode.Exp or
                    GameEventScriptBytecodeOpCode.Floor or
                    GameEventScriptBytecodeOpCode.Ceil or
                    GameEventScriptBytecodeOpCode.Truncate or
                    GameEventScriptBytecodeOpCode.RoundHalfEven or
                    GameEventScriptBytecodeOpCode.RoundHalfUp or
                    GameEventScriptBytecodeOpCode.RoundHalfDown or
                    GameEventScriptBytecodeOpCode.DegreeToRadians or
                    GameEventScriptBytecodeOpCode.DegreeFromRadians or
                    GameEventScriptBytecodeOpCode.WrapDegree or
                    GameEventScriptBytecodeOpCode.Sin or
                    GameEventScriptBytecodeOpCode.Cos or
                    GameEventScriptBytecodeOpCode.Tan or
                    GameEventScriptBytecodeOpCode.Asin or
                    GameEventScriptBytecodeOpCode.Acos or
                    GameEventScriptBytecodeOpCode.Atan or
                    GameEventScriptBytecodeOpCode.Atan2 or
                    GameEventScriptBytecodeOpCode.Hypot2D or
                    GameEventScriptBytecodeOpCode.Hypot3D or
                    GameEventScriptBytecodeOpCode.Distance or
                    GameEventScriptBytecodeOpCode.Distance2D or
                    GameEventScriptBytecodeOpCode.Distance3D or
                    GameEventScriptBytecodeOpCode.DistanceSquared or
                    GameEventScriptBytecodeOpCode.DistanceSquared2D or
                    GameEventScriptBytecodeOpCode.DistanceSquared3D or
                    GameEventScriptBytecodeOpCode.LengthSquared or
                    GameEventScriptBytecodeOpCode.LengthSquared2D or
                    GameEventScriptBytecodeOpCode.LengthSquared3D or
                    GameEventScriptBytecodeOpCode.Normalize or
                    GameEventScriptBytecodeOpCode.Normalize2D or
                    GameEventScriptBytecodeOpCode.Normalize3D or
                    GameEventScriptBytecodeOpCode.Dot or
                    GameEventScriptBytecodeOpCode.Dot2D or
                    GameEventScriptBytecodeOpCode.Dot3D or
                    GameEventScriptBytecodeOpCode.Cross or
                    GameEventScriptBytecodeOpCode.Cross2D or
                    GameEventScriptBytecodeOpCode.Cross3D or
                    GameEventScriptBytecodeOpCode.AngleBetween or
                    GameEventScriptBytecodeOpCode.AngleBetween2D or
                    GameEventScriptBytecodeOpCode.AngleBetween3D or
                    GameEventScriptBytecodeOpCode.TakeFirst or
                    GameEventScriptBytecodeOpCode.DropFirst or
                    GameEventScriptBytecodeOpCode.TakeLast or
                    GameEventScriptBytecodeOpCode.DropLast or
                    GameEventScriptBytecodeOpCode.TakeHighest or
                    GameEventScriptBytecodeOpCode.TakeLowest or
                    GameEventScriptBytecodeOpCode.DropHighest or
                    GameEventScriptBytecodeOpCode.DropLowest or
                    GameEventScriptBytecodeOpCode.Count or
                    GameEventScriptBytecodeOpCode.StartsWith or
                    GameEventScriptBytecodeOpCode.EndsWith or
                    GameEventScriptBytecodeOpCode.Contains or
                    GameEventScriptBytecodeOpCode.ContainsAny or
                    GameEventScriptBytecodeOpCode.ContainsAll or
                    GameEventScriptBytecodeOpCode.HasAny or
                    GameEventScriptBytecodeOpCode.HasAll or
                    GameEventScriptBytecodeOpCode.ContainsValue or
                    GameEventScriptBytecodeOpCode.Union or
                    GameEventScriptBytecodeOpCode.Intersect or
                    GameEventScriptBytecodeOpCode.Zip or
                    GameEventScriptBytecodeOpCode.KeysOfMap or
                    GameEventScriptBytecodeOpCode.ValuesOfMap or
                    GameEventScriptBytecodeOpCode.EntriesOfMap or
                    GameEventScriptBytecodeOpCode.First or
                    GameEventScriptBytecodeOpCode.Last or
                    GameEventScriptBytecodeOpCode.Single or
                    GameEventScriptBytecodeOpCode.IteratorCreate or
                    GameEventScriptBytecodeOpCode.Distinct or
                    GameEventScriptBytecodeOpCode.SortAscending or
                    GameEventScriptBytecodeOpCode.SortDescending or
                    GameEventScriptBytecodeOpCode.Reverse or
                    GameEventScriptBytecodeOpCode.HasPattern or
                    GameEventScriptBytecodeOpCode.TakePattern => true,
                _ => false
            };
        }
    }

    private sealed class BranchSimplificationPass : IGesBinaryOptimizationPass
    {
        public static readonly BranchSimplificationPass Instance = new();

        private BranchSimplificationPass()
        {
        }

        public bool Rewrite(RewriteContext context)
        {
            var changed = false;
            var labelInstructionIndexes = BuildLabelInstructionIndexes(context);
            var remove = new bool[context.Count];

            for (var index = 0; index < context.Count; index++)
            {
                if (context.GetInstruction(index) is not { } branch ||
                    !TryInvertBranch(branch, out var invertedOpcode) ||
                    branch.Y.Kind != GesOperandKind.Label ||
                    index + 1 >= context.Count ||
                    context.GetInstruction(index + 1) is not { } jump ||
                    jump.OpCode != GameEventScriptBytecodeOpCode.Jump ||
                    jump.Y.Kind != GesOperandKind.Label)
                {
                    continue;
                }

                var branchTargetInstructionIndex = labelInstructionIndexes[branch.Y.LabelRef.Id];
                if (branchTargetInstructionIndex != FindNextInstructionIndex(context, index + 1))
                {
                    continue;
                }

                context.ReplaceInstruction(index, branch with { OpCode = invertedOpcode, Y = jump.Y });
                remove[index + 1] = true;
                changed = true;
            }

            if (changed)
            {
                context.RemoveMarked(remove);
            }

            return changed;
        }

        private static bool TryInvertBranch(InstructionPlan instruction, out GameEventScriptBytecodeOpCode invertedOpcode)
        {
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.JumpIfTrue:
                    invertedOpcode = GameEventScriptBytecodeOpCode.JumpIfFalse;
                    return true;
                case GameEventScriptBytecodeOpCode.JumpIfFalse:
                    invertedOpcode = GameEventScriptBytecodeOpCode.JumpIfTrue;
                    return true;
                case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                    invertedOpcode = GameEventScriptBytecodeOpCode.JumpIfTrue;
                    return true;
                default:
                    invertedOpcode = default;
                    return false;
            }
        }
    }

    private sealed class JumpCleanupPass : IGesBinaryOptimizationPass
    {
        public static readonly JumpCleanupPass Instance = new();

        private JumpCleanupPass()
        {
        }

        public bool Rewrite(RewriteContext context)
        {
            var changed = false;
            var labelInstructionIndexes = BuildLabelInstructionIndexes(context);
            var remove = new bool[context.Count];

            for (var index = 0; index < context.Count; index++)
            {
                if (context.GetInstruction(index) is not { } instruction) continue;
                if (TryGetJumpTarget(instruction, out var target))
                {
                    var targetInstructionIndex = labelInstructionIndexes[target.Id];
                    var nextInstructionIndex = FindNextInstructionIndex(context, index);
                    if (targetInstructionIndex == nextInstructionIndex)
                    {
                        remove[index] = true;
                        changed = true;
                        continue;
                    }

                    if (targetInstructionIndex >= 0 &&
                        context.GetInstruction(targetInstructionIndex) is { OpCode: GameEventScriptBytecodeOpCode.Jump, Y.Kind: GesOperandKind.Label } targetJump &&
                        targetJump.Y.LabelRef.Id != target.Id)
                    {
                        context.ReplaceInstruction(index, instruction with { Y = targetJump.Y });
                        changed = true;
                    }
                }

                if (IsUnconditionalControlStop(instruction.OpCode))
                {
                    for (var deadIndex = index + 1; deadIndex < context.Count; deadIndex++)
                    {
                        if (context.GetLabel(deadIndex).HasValue) break;
                        if (context.GetInstruction(deadIndex) is null) continue;
                        remove[deadIndex] = true;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                context.RemoveMarked(remove);
            }

            return changed;
        }

        private static bool IsUnconditionalControlStop(GameEventScriptBytecodeOpCode opcode)
            => opcode is GameEventScriptBytecodeOpCode.Jump or GameEventScriptBytecodeOpCode.ReturnVoid or GameEventScriptBytecodeOpCode.ReturnValue;
    }

    private sealed class PeepholeMoveEliminationPass : IGesBinaryOptimizationPass
    {
        public static readonly PeepholeMoveEliminationPass Instance = new();

        private PeepholeMoveEliminationPass()
        {
        }

        public bool Rewrite(RewriteContext context)
        {
            var changed = false;
            var readCounts = new int[context.RegisterCount];
            var writeCounts = new int[context.RegisterCount];
            CountRegisterUsage(context, readCounts, writeCounts);
            var remove = new bool[context.Count];

            for (var index = 0; index + 1 < context.Count; index++)
            {
                if (remove[index] ||
                    remove[index + 1] ||
                    context.GetInstruction(index) is not { } instruction ||
                    context.GetInstruction(index + 1) is not { } move ||
                    move.OpCode != GameEventScriptBytecodeOpCode.Move ||
                    instruction.Destination.Kind != GesOperandKind.Register ||
                    move.X.Kind != GesOperandKind.Register ||
                    move.Destination.Kind != GesOperandKind.Register ||
                    instruction.Destination.RegisterRef.Id != move.X.RegisterRef.Id ||
                    !context.IsTemporary(instruction.Destination.RegisterRef) ||
                    instruction.ReadsRegister(move.Destination.RegisterRef) ||
                    readCounts[instruction.Destination.RegisterRef.Id] != 1 ||
                    writeCounts[instruction.Destination.RegisterRef.Id] != 1)
                {
                    continue;
                }

                var tempRegisterId = instruction.Destination.RegisterRef.Id;
                context.ReplaceInstruction(index, instruction.WithDestination(move.Destination));
                remove[index + 1] = true;
                readCounts[tempRegisterId]--;
                writeCounts[tempRegisterId]--;
                changed = true;
            }

            if (changed)
            {
                context.RemoveMarked(remove);
            }

            return changed;
        }

        private static void CountRegisterUsage(RewriteContext context, int[] readCounts, int[] writeCounts)
        {
            var items = context.Items;
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index].Instruction is not { } instruction) continue;
                if (instruction.Destination.Kind == GesOperandKind.Register)
                {
                    writeCounts[instruction.Destination.RegisterRef.Id]++;
                }

                CountOperandReads(readCounts, instruction.X);
                CountOperandReads(readCounts, instruction.Y);
                CountOperandReads(readCounts, instruction.A);
                CountOperandReads(readCounts, instruction.B);
                CountOperandReads(readCounts, instruction.C);
                CountOperandReads(readCounts, instruction.D);
                CountOperandReads(readCounts, instruction.SecondaryList);
            }
        }

        private static void CountOperandReads(int[] readCounts, GesOperand operand)
        {
            switch (operand.Kind)
            {
                case GesOperandKind.Register:
                    readCounts[operand.RegisterRef.Id]++;
                    return;
                case GesOperandKind.RegisterList when operand.RegisterListValue is not null:
                    for (var index = 0; index < operand.RegisterListValue.Count; index++)
                    {
                        readCounts[operand.RegisterListValue[index].Id]++;
                    }

                    return;
            }
        }
    }
}
