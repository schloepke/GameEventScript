using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed partial class GesBinaryBuilder
{
    private const int NoRoutineId = -1;

    private readonly List<RoutinePlan> _routines = [];
    private readonly Stack<int> _routineStack = [];
    private readonly Stack<ScopePlan> _scopeStack = [];

    private int CurrentRoutineId => _routineStack.Count == 0 ? NoRoutineId : _routineStack.Peek();

    public GesBinaryRoutineScope BeginHandler(
        string messageName,
        IReadOnlyList<string>? argumentNames = null,
        ushort? id = null,
        IReadOnlyList<string>? requiredTags = null,
        IReadOnlyList<string>? excludedTags = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.MessageHandler, messageName, argumentNames, id, requiredTags, excludedTags);

    public GesBinaryRoutineScope BeginMessageNameHandler(
        string messageName,
        string messageArgumentName = "message",
        ushort? id = null,
        IReadOnlyList<string>? requiredTags = null,
        IReadOnlyList<string>? excludedTags = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.MessageNameHandler, messageName, [messageArgumentName], id, requiredTags, excludedTags);

    public GesBinaryRoutineScope BeginFunction(string name, IReadOnlyList<string>? argumentNames = null, ushort? id = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.Function, name, argumentNames, id);

    public GesBinaryRoutineScope BeginPredicate(string name, IReadOnlyList<string>? argumentNames = null, ushort? id = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.Predicate, name, argumentNames, id);

    public GesBinaryRoutineScope BeginRecordConstructor(string typeName, IReadOnlyList<string>? argumentNames = null, ushort? id = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.Record, typeName, argumentNames, id);

    public GesBinaryRoutineScope BeginHelper(string name, IReadOnlyList<string>? argumentNames = null)
        => BeginRoutine(null, name, argumentNames, id: null);

    private GesBinaryRoutineScope BeginRoutine(
        GameEventScriptBinaryBindKind? kind,
        string name,
        IReadOnlyList<string>? argumentNames,
        ushort? id,
        IReadOnlyList<string>? requiredTags = null,
        IReadOnlyList<string>? excludedTags = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Routine name must be non-empty.", nameof(name));
        if (kind is null && id.HasValue) throw new ArgumentException("Helper routines cannot have bind ids.", nameof(id));
        if (kind is null && ((requiredTags?.Count ?? 0) > 0 || (excludedTags?.Count ?? 0) > 0))
        {
            throw new ArgumentException("Helper routines cannot have tag filters.", nameof(requiredTags));
        }

        var parentRoutineId = CurrentRoutineId;
        var routineId = _routines.Count;
        var entryLabel = AddLabel(name);
        var routine = new RoutinePlan(routineId, parentRoutineId, kind, name, entryLabel, argumentNames?.ToArray() ?? []);
        _routines.Add(routine);
        _routineStack.Push(routineId);
        MarkLabel(entryLabel);

        var arguments = new List<GesRegisterRef>(routine.ArgumentNames.Count);
        foreach (var argumentName in routine.ArgumentNames)
        {
            arguments.Add(AddRegister(argumentName));
        }

        routine.ArgumentRegisters = arguments;
        if (kind.HasValue)
        {
            routine.Bind = AddBind(kind.Value, name, routine.ArgumentNames, entryLabel, id, requiredTags, excludedTags);
        }

        SlotLocals(0);
        return new GesBinaryRoutineScope(this, routine);
    }

    private GesBinaryLexicalScope BeginLexicalScope(string? name)
    {
        if (_routineStack.Count == 0) throw new InvalidOperationException("Cannot begin a lexical scope outside a routine.");
        var scope = new ScopePlan(_scopeStack.Count, CurrentRoutineId, string.IsNullOrWhiteSpace(name) ? null : name);
        _scopeStack.Push(scope);
        return new GesBinaryLexicalScope(this, scope);
    }

    private void EndRoutine(int routineId)
    {
        if (_routineStack.Count == 0 || _routineStack.Peek() != routineId)
        {
            throw new InvalidOperationException("Routine scopes must be closed in reverse creation order.");
        }

        if (_scopeStack.Any(scope => scope.RoutineId == routineId))
        {
            throw new InvalidOperationException($"Routine '{_routines[routineId].Name}' still has open lexical scopes.");
        }

        _routines[routineId].IsClosed = true;
        _routineStack.Pop();
    }

    private void EndLexicalScope(ScopePlan scope)
    {
        if (_scopeStack.Count == 0 || !_scopeStack.Peek().Equals(scope))
        {
            throw new InvalidOperationException("Lexical scopes must be closed in reverse creation order.");
        }

        _scopeStack.Pop();
    }

    private void EnsureScopesClosed()
    {
        if (_scopeStack.Count > 0) throw new InvalidOperationException("Cannot build binary while lexical scopes are still open.");
        if (_routineStack.Count > 0) throw new InvalidOperationException("Cannot build binary while routines are still open.");
        foreach (var routine in _routines)
        {
            if (!routine.IsClosed) throw new InvalidOperationException($"Routine '{routine.Name}' was not closed.");
        }
    }

    private PlanItem[] PatchRoutineSlotLocals(
        IReadOnlyList<PlanItem> items,
        IReadOnlyDictionary<int, ushort> registerMap)
    {
        if (_routines.Count == 0) return items.ToArray();

        var result = items.ToArray();
        foreach (var routine in _routines)
        {
            var slotLocalsIndex = FindRoutineSlotLocalsIndex(result, routine);
            var maxSlot = -1;
            foreach (var register in _registers.Where(register => register.RoutineId == routine.Id))
            {
                if (registerMap.TryGetValue(register.Id, out var slot) && slot > maxSlot)
                {
                    maxSlot = slot;
                }
            }

            var localCount = Math.Max(0, maxSlot + 1 - routine.ArgumentRegisters.Count);
            if (localCount > short.MaxValue)
            {
                throw new InvalidOperationException($"Routine '{routine.Name}' requires too many local registers: {localCount}.");
            }

            var instruction = result[slotLocalsIndex].Instruction!;
            result[slotLocalsIndex] = PlanItem.ForInstruction(instruction with { Count = (short)localCount }, result[slotLocalsIndex].SourceRange);
        }

        return result;
    }

    private static int FindRoutineSlotLocalsIndex(IReadOnlyList<PlanItem> items, RoutinePlan routine)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Label != routine.EntryLabel) continue;
            for (var instructionIndex = index + 1; instructionIndex < items.Count; instructionIndex++)
            {
                if (items[instructionIndex].Label.HasValue) break;
                if (items[instructionIndex].Instruction?.OpCode == GameEventScriptBytecodeOpCode.SlotLocals)
                {
                    return instructionIndex;
                }
            }

            break;
        }

        throw new InvalidOperationException($"Routine '{routine.Name}' does not have a SlotLocals prolog.");
    }

    internal sealed class GesBinaryRoutineScope : IDisposable
    {
        private readonly GesBinaryBuilder _builder;
        private readonly RoutinePlan _routine;
        private bool _disposed;

        internal GesBinaryRoutineScope(GesBinaryBuilder builder, RoutinePlan routine)
        {
            _builder = builder;
            _routine = routine;
        }

        public GesBinaryBuilder Builder => _builder;

        public GesLabelRef EntryLabel => _routine.EntryLabel;

        public bool HasBind => _routine.Bind.HasValue;

        public GesBindRef Bind => _routine.Bind ?? throw new InvalidOperationException($"Routine '{_routine.Name}' does not have a bind entry.");

        public GesBindRef? OptionalBind => _routine.Bind;

        public IReadOnlyList<GesRegisterRef> Arguments => _routine.ArgumentRegisters;

        public GesRegisterRef Argument(string name)
        {
            for (var index = 0; index < _routine.ArgumentNames.Count; index++)
            {
                if (string.Equals(_routine.ArgumentNames[index], name, StringComparison.Ordinal))
                {
                    return _routine.ArgumentRegisters[index];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(name), name, $"Routine '{_routine.Name}' does not define an argument named '{name}'.");
        }

        public GesRegisterRef AddRegister(string name) => _builder.AddRegister(name);

        public GesRegisterRef AddTemporaryRegister(string? name = null) => _builder.AddTemporaryRegister(name);

        public GesLabelRef AddLabel(string? name = null) => _builder.AddLabel(name);

        public GesBinaryLexicalScope BeginScope(string? name = null) => _builder.BeginLexicalScope(name);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _builder.EndRoutine(_routine.Id);
        }
    }

    internal sealed class GesBinaryLexicalScope : IDisposable
    {
        private readonly GesBinaryBuilder _builder;
        private readonly ScopePlan _scope;
        private bool _disposed;

        internal GesBinaryLexicalScope(GesBinaryBuilder builder, ScopePlan scope)
        {
            _builder = builder;
            _scope = scope;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _builder.EndLexicalScope(_scope);
        }
    }

    internal sealed class RoutinePlan(
        int id,
        int parentRoutineId,
        GameEventScriptBinaryBindKind? kind,
        string name,
        GesLabelRef entryLabel,
        IReadOnlyList<string> argumentNames)
    {
        public int Id { get; } = id;
        public int ParentRoutineId { get; } = parentRoutineId;
        public GameEventScriptBinaryBindKind? Kind { get; } = kind;
        public string Name { get; } = name;
        public GesLabelRef EntryLabel { get; } = entryLabel;
        public IReadOnlyList<string> ArgumentNames { get; } = argumentNames;
        public IReadOnlyList<GesRegisterRef> ArgumentRegisters { get; set; } = [];
        public List<PlanItem> Items { get; } = [];
        public GesBindRef? Bind { get; set; }
        public bool IsClosed { get; set; }
    }

    internal readonly record struct ScopePlan(int Id, int RoutineId, string? Name);
}
