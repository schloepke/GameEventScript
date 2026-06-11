using System;
using System.Collections.Generic;
using System.Linq;
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
        PeepholeMoveEliminationPass.Instance
    ];

    public GesBinaryBuilder Optimize(IGesBinaryOptimizationPass pass)
    {
        _ = pass ?? throw new ArgumentNullException(nameof(pass));
        EnsureScopesClosed();
        var context = new RewriteContext(this, LinearizePlanItems());
        pass.Rewrite(context);
        _rewrittenItems = context.ToArray();
        return this;
    }

    public GesBinaryBuilder Optimize(Action<RewriteContext> rewrite)
    {
        _ = rewrite ?? throw new ArgumentNullException(nameof(rewrite));
        EnsureScopesClosed();
        var context = new RewriteContext(this, LinearizePlanItems());
        rewrite(context);
        _rewrittenItems = context.ToArray();
        return this;
    }

    private PlanItem[] RunDefaultOptimizationPasses(IReadOnlyList<PlanItem> items)
    {
        var context = new RewriteContext(this, items);
        foreach (var pass in DefaultOptimizationPasses)
        {
            pass.Rewrite(context);
        }

        return context.ToArray();
    }

    internal sealed class RewriteContext
    {
        private readonly GesBinaryBuilder _builder;
        private readonly List<PlanItem> _items;

        internal RewriteContext(GesBinaryBuilder builder, IReadOnlyList<PlanItem> items)
        {
            _builder = builder;
            _items = items.ToList();
        }

        internal IReadOnlyList<PlanItem> Items => _items;

        public int Count => _items.Count;

        public GesRegisterRef AddRegister(string name) => _builder.AddRegister(name);

        public GesRegisterRef AddTemporaryRegister(string? name = null) => _builder.AddTemporaryRegister(name);

        public GesRegisterRef AddRegisterNear(int index, string name)
            => _builder.AddRegisterInRoutine(name, ResolveRoutineIdNear(index));

        public GesRegisterRef AddTemporaryRegisterNear(int index, string? name = null)
            => _builder.AddTemporaryRegisterInRoutine(name, ResolveRoutineIdNear(index));

        public GesLabelRef AddLabel(string? name = null) => _builder.AddLabel(name);

        public bool TryGetInstruction(int index, out InstructionPlan instruction)
        {
            if ((uint)index < (uint)_items.Count && _items[index].Instruction is { } plan)
            {
                instruction = plan;
                return true;
            }

            instruction = default!;
            return false;
        }

        public bool TryGetLabel(int index, out GesLabelRef label)
        {
            if ((uint)index < (uint)_items.Count && _items[index].Label is { } value)
            {
                label = value;
                return true;
            }

            label = default;
            return false;
        }

        public bool TryGetSourceRange(int index, out GameEventScriptSourceLocation? sourceRange)
        {
            if ((uint)index < (uint)_items.Count)
            {
                sourceRange = _items[index].SourceRange;
                return true;
            }

            sourceRange = null;
            return false;
        }

        public InstructionCursor GetInstructionCursor(int index)
        {
            if (!TryGetInstruction(index, out var instruction))
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
            foreach (var item in _items)
            {
                if (item.Instruction is not { } instruction) continue;
                count += instruction.ReadRegisters().Count(value => value.Id == register.Id);
            }

            return count;
        }

        public int CountRegisterWrites(GesRegisterRef register)
        {
            _builder.RequireRegister(register);
            var count = 0;
            foreach (var item in _items)
            {
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

        internal PlanItem[] ToArray() => _items.ToArray();

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

    private sealed class PeepholeMoveEliminationPass : IGesBinaryOptimizationPass
    {
        public static readonly PeepholeMoveEliminationPass Instance = new();

        private PeepholeMoveEliminationPass()
        {
        }

        public bool Rewrite(RewriteContext context)
        {
            var changed = false;
            for (var index = 0; index + 1 < context.Count; index++)
            {
                if (!context.TryGetInstruction(index, out var instruction) ||
                    !context.TryGetInstruction(index + 1, out var move) ||
                    move.OpCode != GameEventScriptBytecodeOpCode.Move ||
                    instruction.Destination.Kind != GesOperandKind.Register ||
                    move.X.Kind != GesOperandKind.Register ||
                    move.Destination.Kind != GesOperandKind.Register ||
                    instruction.Destination.RegisterRef.Id != move.X.RegisterRef.Id ||
                    !context.IsTemporary(instruction.Destination.RegisterRef) ||
                    instruction.ReadRegisters().Any(register => register.Id == move.Destination.RegisterRef.Id) ||
                    context.CountRegisterReads(instruction.Destination.RegisterRef) != 1 ||
                    context.CountRegisterWrites(instruction.Destination.RegisterRef) != 1)
                {
                    continue;
                }

                context.ReplaceInstruction(index, instruction.WithDestination(move.Destination));
                context.RemoveAt(index + 1);
                changed = true;
            }

            return changed;
        }
    }
}
