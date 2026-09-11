// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed partial class GesBinaryBuilder
{
    private const int NoRoutineId = -1;

    private readonly List<RoutinePlan> _routines = [];
    private readonly Stack<int> _routineStack = [];
    private readonly List<ScopePlan> _scopeStack = [];

    private int CurrentRoutineId => _routineStack.Count == 0 ? NoRoutineId : _routineStack.Peek();

    public GesBinaryRoutineScope BeginHandler(string messageName, IReadOnlyList<string>? argumentNames = null, ushort? id = null, IReadOnlyList<string>? requiredTags = null, IReadOnlyList<string>? excludedTags = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.MessageHandler, messageName, argumentNames, id, requiredTags, excludedTags);

    public GesBinaryRoutineScope BeginMessageNameHandler(string messageName, string messageArgumentName = "message", ushort? id = null, IReadOnlyList<string>? requiredTags = null, IReadOnlyList<string>? excludedTags = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.MessageNameHandler, messageName, [messageArgumentName], id, requiredTags, excludedTags);

    public GesBinaryRoutineScope BeginFunction(string name, IReadOnlyList<string>? argumentNames = null, ushort? id = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.Function, name, argumentNames, id);

    public GesBinaryRoutineScope BeginPredicate(string name, IReadOnlyList<string>? argumentNames = null, ushort? id = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.Predicate, name, argumentNames, id);

    public GesBinaryRoutineScope BeginRecordConstructor(string typeName, IReadOnlyList<string>? argumentNames = null, ushort? id = null)
        => BeginRoutine(GameEventScriptBinaryBindKind.Record, typeName, argumentNames, id);

    public GesBinaryRoutineScope BeginHelper(string name, IReadOnlyList<string>? argumentNames = null)
        => BeginRoutine(null, name, argumentNames, id: null);

    private GesBinaryRoutineScope BeginRoutine(GameEventScriptBinaryBindKind? kind, string name, IReadOnlyList<string>? argumentNames, ushort? id, IReadOnlyList<string>? requiredTags = null, IReadOnlyList<string>? excludedTags = null)
        => BeginRoutine(DeclareRoutine(kind, name, argumentNames, id, requiredTags, excludedTags));

    public RoutinePlan DeclareRoutine(GameEventScriptBinaryBindKind? kind, string name, IReadOnlyList<string>? argumentNames = null, ushort? id = null, IReadOnlyList<string>? requiredTags = null, IReadOnlyList<string>? excludedTags = null)
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
        var routine = new RoutinePlan(routineId, parentRoutineId, kind, name, entryLabel, CopyStringList(argumentNames));
        _routines.Add(routine);
        if (kind.HasValue)
        {
            routine.Bind = AddBind(kind.Value, name, routine.ArgumentNames, entryLabel, id, requiredTags, excludedTags);
        }

        return routine;
    }

    public GesBinaryRoutineScope BeginRoutine(RoutinePlan routine)
    {
        if (routine.Id >= _routines.Count || !ReferenceEquals(_routines[routine.Id], routine)) throw new ArgumentException("Unknown routine declaration.", nameof(routine));
        if (routine.IsStarted || routine.ParentRoutineId != CurrentRoutineId) throw new InvalidOperationException("Routine must be started once in its declaring scope.");
        routine.IsStarted = true;
        _routineStack.Push(routine.Id);
        MarkLabel(routine.EntryLabel);

        var arguments = new List<GesRegisterRef>(routine.ArgumentNames.Count);
        for (var index = 0; index < routine.ArgumentNames.Count; index++)
        {
            var argumentName = routine.ArgumentNames[index];
            arguments.Add(AddRegister(argumentName));
        }

        routine.ArgumentRegisters = arguments;
        RegisterLocals(0);
        return new GesBinaryRoutineScope(this, routine);
    }

    private GesBinaryLexicalScope BeginLexicalScope(string? name)
    {
        if (_routineStack.Count == 0) throw new InvalidOperationException("Cannot begin a lexical scope outside a routine.");
        var scope = new ScopePlan(_scopeStack.Count, CurrentRoutineId, string.IsNullOrWhiteSpace(name) ? null : name);
        _scopeStack.Add(scope);
        return new GesBinaryLexicalScope(this, scope);
    }

    private void EndRoutine(int routineId)
    {
        if (_routineStack.Count == 0 || _routineStack.Peek() != routineId)
        {
            throw new InvalidOperationException("Routine scopes must be closed in reverse creation order.");
        }

        for (var index = 0; index < _scopeStack.Count; index++)
        {
            if (_scopeStack[index].RoutineId == routineId)
            {
                throw new InvalidOperationException($"Routine '{_routines[routineId].Name}' still has open lexical scopes.");
            }
        }

        _routines[routineId].IsClosed = true;
        _routineStack.Pop();
    }

    private void EndLexicalScope(ScopePlan scope)
    {
        if (_scopeStack.Count == 0 || !_scopeStack[^1].Equals(scope))
        {
            throw new InvalidOperationException("Lexical scopes must be closed in reverse creation order.");
        }

        _scopeStack.RemoveAt(_scopeStack.Count - 1);
    }

    private void EnsureScopesClosed()
    {
        if (_scopeStack.Count > 0) throw new InvalidOperationException("Cannot build binary while lexical scopes are still open.");
        if (_routineStack.Count > 0) throw new InvalidOperationException("Cannot build binary while routines are still open.");
        for (var index = 0; index < _routines.Count; index++)
        {
            var routine = _routines[index];
            if (!routine.IsClosed) throw new InvalidOperationException($"Routine '{routine.Name}' was not closed.");
        }
    }

    private PlanItem[] PatchRoutineRegisterLocals(IReadOnlyList<PlanItem> items, RegisterAllocationResult registerAllocation)
    {
        if (_routines.Count == 0) return CopyPlanItems(items);

        var result = CopyPlanItems(items);
        var registerLocalsIndexes = new int[_routines.Count];
        for (var index = 0; index < registerLocalsIndexes.Length; index++)
        {
            registerLocalsIndexes[index] = -1;
        }

        for (var itemIndex = 0; itemIndex < result.Length; itemIndex++)
        {
            var instruction = result[itemIndex].Instruction;
            if (instruction is null || instruction.OpCode != GameEventScriptBytecodeOpCode.RegisterLocals) continue;
            var routineId = instruction.RoutineId;
            if (routineId < 0 || routineId >= registerLocalsIndexes.Length) continue;
            if (registerLocalsIndexes[routineId] < 0) registerLocalsIndexes[routineId] = itemIndex;
        }

        for (var routineIndex = 0; routineIndex < _routines.Count; routineIndex++)
        {
            var routine = _routines[routineIndex];
            var registerLocalsIndex = registerLocalsIndexes[routine.Id];
            if (registerLocalsIndex < 0)
            {
                throw new InvalidOperationException($"Routine '{routine.Name}' does not have a RegisterLocals prolog.");
            }

            var localCount = registerAllocation.RoutineLocalCounts[routine.Id];
            var instruction = result[registerLocalsIndex].Instruction!;
            result[registerLocalsIndex] = PlanItem.ForInstruction(instruction with { Count = localCount }, result[registerLocalsIndex].SourceRange);
        }

        return result;
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

    internal sealed class RoutinePlan(int id, int parentRoutineId, GameEventScriptBinaryBindKind? kind, string name, GesLabelRef entryLabel, IReadOnlyList<string> argumentNames)
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
        public bool IsStarted { get; set; }
        public bool IsClosed { get; set; }
    }

    internal readonly record struct ScopePlan(int Id, int RoutineId, string? Name);
}
