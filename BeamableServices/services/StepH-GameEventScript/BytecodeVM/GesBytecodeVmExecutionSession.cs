using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed partial class GesBytecodeVmExecutionSession
{
    private const string CallableCallDepthExceededDetail = "Callable exceeded the configured call depth.";
    private const string PipelineRangeLimitDetail = "Collection access would enumerate more range items than allowed.";

    private enum PipelineIterationResult
    {
        Completed,
        Stopped,
        Failed,
        RangeLimitReached
    }

    private readonly GesBytecodeVmExecutable _compiledScript;
    private readonly GameEventScriptContext _context;
    private readonly GesRuntimeBudget _runtimeBudget;
    private readonly IReadOnlyDictionary<string, int> _slots;
    private BytecodeVmValue[] _locals;
    private bool[] _assignedSlots;
    private readonly bool _diagnosticsEnabled;
    private Dictionary<int, bool>? _linearProjectionFastSupportCache;
    private int _trackedLocalSlotCount;
    private List<LocalChange> _changes = [];
    private List<int> _scopeMarks = [];
    private readonly Stack<GameEventScriptRandomGenerator> _randomScopes = new();
    private bool _suppressLocalChangeTracking;
    private bool _halted;

    private GesBytecodeVmExecutionSession(
        GesBytecodeVmExecutable compiledScript,
        GameEventScriptContext context,
        IReadOnlyDictionary<string, int> slots,
        int localSlotCount,
        bool diagnosticsEnabled)
    {
        _compiledScript = compiledScript;
        _context = context;
        _runtimeBudget = context.RuntimeBudget;
        _slots = slots;
        _diagnosticsEnabled = diagnosticsEnabled;
        _trackedLocalSlotCount = localSlotCount;
        var frameSlotCount = Math.Max(localSlotCount, compiledScript.LinearExecutable.MaxFrameSlots);
        _locals = new BytecodeVmValue[frameSlotCount];
        _assignedSlots = new bool[frameSlotCount];
        _randomScopes.Push(context.Random);
    }

    public static void InvokeHandler(
        GesBytecodeVmExecutable compiledScript,
        GameEventScriptContext context,
        GesBytecodeVmCompiledHandler handler,
        IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        var session = new GesBytecodeVmExecutionSession(compiledScript, context, handler.Slots, handler.LocalSlotCount, handler.DiagnosticsEnabled);
        if (!session.TryInvoke(handler, args))
        {
            throw new InvalidOperationException(
                $"BytecodeVM invariant failed: handler '{handler.SignatureId}' #{handler.DeclarationOrder} could not be executed from its linear entry address.");
        }
    }

    private bool TryInvoke(GesBytecodeVmCompiledHandler handler, IReadOnlyDictionary<string, GameEventScriptValue> args)
        => TryInvokeLinearHandler(handler, args);

    private bool CanExecuteLinearInstruction(
        GameEventScriptBytecodeInstruction instruction,
        HashSet<string> visitingCallables,
        bool allowPipeline)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Pipeline:
                return allowPipeline && CanExecuteLinearPipeline(instruction.Data, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.SeededRandom:
                return CanExecuteLinearSeededRandom(instruction.Data, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.GeneratedCollection:
                return CanExecuteLinearGeneratedCollection(instruction.Data, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.GuardedChoice:
                return CanExecuteLinearGuardedChoice(instruction.Data, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.Call:
            case GameEventScriptBytecodeOpCode.PredicateTest:
                return TryGetOperationLayout(instruction.Data, out var layout) &&
                       !string.IsNullOrEmpty(layout.Name) &&
                       CanExecuteLinearCallable(layout.Name, visitingCallables, allowPipeline);

            default:
                return true;
        }
    }

    private bool CanExecuteLinearCallable(string callableName, HashSet<string> visitingCallables, bool allowPipeline)
    {
        if (!_compiledScript.BytecodeModule.Callables.TryGetValue(callableName, out var callable))
        {
            return false;
        }

        if (!visitingCallables.Add(callable.SignatureId))
        {
            return true;
        }

        try
        {
            return CanExecuteLinearEntry(callable.EntryAddress, visitingCallables, allowPipeline);
        }
        finally
        {
            visitingCallables.Remove(callable.SignatureId);
        }
    }

    private bool CanExecuteLinearPipeline(int layoutIndex, HashSet<string> visitingCallables, bool allowPipeline)
    {
        if (!TryGetPipelineLayout(layoutIndex, out var layout) ||
            !TryGetSelectorLayout(layout.TerminalSelectorLayoutIndex, out var terminal))
        {
            return false;
        }

        var prefixSelectorLayoutIndexes = layout.PrefixSelectorLayoutIndexes;
        for (var prefixIndex = 0; prefixIndex < prefixSelectorLayoutIndexes.Count; prefixIndex++)
        {
            var selectorIndex = prefixSelectorLayoutIndexes[prefixIndex];
            if (!CanExecuteLinearPipelineSelector(selectorIndex, isTerminal: false, visitingCallables, allowPipeline))
            {
                return false;
            }
        }

        return CanExecuteLinearPipelineSelector(layout.TerminalSelectorLayoutIndex, isTerminal: true, visitingCallables, allowPipeline);
    }

    private bool CanExecuteLinearPipelineSelector(
        int selectorIndex,
        bool isTerminal,
        HashSet<string> visitingCallables,
        bool allowPipeline)
    {
        if (!TryGetSelectorLayout(selectorIndex, out var selector))
        {
            return false;
        }

        return selector.Kind switch
        {
            GameEventScriptBytecodeSelectorKind.Filter or GameEventScriptBytecodeSelectorKind.Select =>
                selector.ExpressionEntryAddress >= 0 &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.Edge =>
                isTerminal &&
                IsSupportedLinearPipelineEdgeMode(selector.EdgeMode) &&
                (selector.ExpressionEntryAddress < 0 ||
                 CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline)),
            GameEventScriptBytecodeSelectorKind.Predicate =>
                isTerminal &&
                selector.ExpressionEntryAddress >= 0 &&
                (string.Equals(selector.EdgeMode, "any", StringComparison.Ordinal) ||
                 string.Equals(selector.EdgeMode, "all", StringComparison.Ordinal)) &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.Count =>
                isTerminal &&
                (selector.ExpressionEntryAddress < 0 ||
                 CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline)),
            GameEventScriptBytecodeSelectorKind.SeriesTerm =>
                isTerminal &&
                selector.ExpressionEntryAddress >= 0 &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.Sum or GameEventScriptBytecodeSelectorKind.Average =>
                isTerminal &&
                selector.ExpressionEntryAddress >= 0 &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.Min or GameEventScriptBytecodeSelectorKind.Max =>
                isTerminal &&
                selector.ExpressionEntryAddress >= 0 &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.Dictionary =>
                isTerminal &&
                selector.ExpressionEntryAddress >= 0 &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline) &&
                (selector.SecondaryExpressionEntryAddress < 0 ||
                 CanExecuteLinearEntry(selector.SecondaryExpressionEntryAddress, visitingCallables, allowPipeline)),
            GameEventScriptBytecodeSelectorKind.Distinct =>
                isTerminal &&
                (selector.ExpressionEntryAddress < 0 ||
                 (selector.IdentifierSlot >= 0 &&
                  CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline))),
            GameEventScriptBytecodeSelectorKind.GroupBy =>
                isTerminal &&
                selector.ExpressionEntryAddress >= 0 &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.OrderBy =>
                isTerminal &&
                selector.IdentifierSlot >= 0 &&
                selector.ExpressionEntryAddress >= 0 &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.SequenceSlice =>
                isTerminal &&
                selector.ExpressionEntryAddress < 0 &&
                IsSupportedLinearPipelineSequenceSliceMode(selector.EdgeMode, selector.SecondaryMode),
            GameEventScriptBytecodeSelectorKind.Pattern or GameEventScriptBytecodeSelectorKind.TakePattern =>
                isTerminal &&
                selector.DicePatternLayoutIndex >= 0 &&
                CanExecuteLinearDicePattern(selector.DicePatternLayoutIndex, visitingCallables, allowPipeline),
            GameEventScriptBytecodeSelectorKind.ObjectMatch =>
                isTerminal &&
                selector.ObjectMatchPatternLayoutIndex >= 0 &&
                CanExecuteLinearObjectMatchPattern(selector.ObjectMatchPatternLayoutIndex, visitingCallables, allowPipeline, []),
            GameEventScriptBytecodeSelectorKind.Choose =>
                isTerminal &&
                (selector.ExpressionEntryAddress < 0 ||
                 (selector.IdentifierSlot >= 0 &&
                  CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline))) &&
                (selector.SecondaryExpressionEntryAddress < 0 ||
                 (selector.SecondaryIdentifierSlot >= 0 &&
                  CanExecuteLinearEntry(selector.SecondaryExpressionEntryAddress, visitingCallables, allowPipeline))),
            GameEventScriptBytecodeSelectorKind.Draw =>
                isTerminal &&
                selector.ExpressionEntryAddress < 0,
            GameEventScriptBytecodeSelectorKind.Shuffle =>
                isTerminal &&
                selector.ExpressionEntryAddress < 0,
            GameEventScriptBytecodeSelectorKind.Sort =>
                isTerminal &&
                selector.ExpressionEntryAddress < 0,
            GameEventScriptBytecodeSelectorKind.Reverse =>
                isTerminal &&
                selector.ExpressionEntryAddress < 0,
            GameEventScriptBytecodeSelectorKind.Contains =>
                isTerminal &&
                selector.ExpressionEntryAddress >= 0 &&
                IsSupportedLinearPipelineContainsMode(selector.EdgeMode) &&
                CanExecuteLinearEntry(selector.ExpressionEntryAddress, visitingCallables, allowPipeline),
            _ => false
        };
    }

    private bool CanExecuteLinearDicePattern(int layoutIndex, HashSet<string> visitingCallables, bool allowPipeline)
    {
        if (!TryGetDicePatternLayout(layoutIndex, out var layout))
        {
            return false;
        }

        return layout.FaceEntryAddress < 0 ||
               CanExecuteLinearEntry(layout.FaceEntryAddress, visitingCallables, allowPipeline);
    }

    private bool CanExecuteLinearObjectMatchPattern(
        int layoutIndex,
        HashSet<string> visitingCallables,
        bool allowPipeline,
        HashSet<int> visitingPatterns)
    {
        if (!visitingPatterns.Add(layoutIndex) ||
            !TryGetObjectMatchPatternLayout(layoutIndex, out var layout))
        {
            return false;
        }

        foreach (var entry in layout.Entries)
        {
            switch (entry.ValueKind)
            {
                case GameEventScriptBytecodeObjectMatchValueKind.Expression:
                    if (entry.ExpressionEntryAddress < 0 ||
                        !CanExecuteLinearEntry(entry.ExpressionEntryAddress, visitingCallables, allowPipeline))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeObjectMatchValueKind.Nested:
                    if (!CanExecuteLinearObjectMatchPattern(entry.NestedPatternLayoutIndex, visitingCallables, allowPipeline, visitingPatterns))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        visitingPatterns.Remove(layoutIndex);
        return true;
    }

    private bool CanExecuteLinearGuardedChoice(int layoutIndex, HashSet<string> visitingCallables, bool allowPipeline)
    {
        if (!TryGetGuardedChoiceLayout(layoutIndex, out var layout) ||
            layout.ConditionEntryAddresses.Count != layout.ValueEntryAddresses.Count)
        {
            return false;
        }

        for (var branchIndex = 0; branchIndex < layout.ConditionEntryAddresses.Count; branchIndex++)
        {
            if (!CanExecuteLinearEntry(layout.ConditionEntryAddresses[branchIndex], visitingCallables, allowPipeline) ||
                !CanExecuteLinearEntry(layout.ValueEntryAddresses[branchIndex], visitingCallables, allowPipeline))
            {
                return false;
            }
        }

        return CanExecuteLinearEntry(layout.OtherwiseEntryAddress, visitingCallables, allowPipeline);
    }

    private bool CanExecuteLinearGeneratedCollection(int layoutIndex, HashSet<string> visitingCallables, bool allowPipeline)
    {
        if (!TryGetGeneratedCollectionLayout(layoutIndex, out var layout) ||
            layout.ProjectionEntryAddress < 0 ||
            (layout.PredicateEntryAddress >= 0 && !CanExecuteLinearEntry(layout.PredicateEntryAddress, visitingCallables, allowPipeline)))
        {
            return false;
        }

        return CanExecuteLinearEntry(layout.ProjectionEntryAddress, visitingCallables, allowPipeline);
    }

    private bool CanExecuteLinearSeededRandom(int layoutIndex, HashSet<string> visitingCallables, bool allowPipeline)
        => TryGetOperationLayout(layoutIndex, out var layout) &&
           layout.ExpressionEntryAddress >= 0 &&
           CanExecuteLinearEntry(layout.ExpressionEntryAddress, visitingCallables, allowPipeline);

    private bool CanExecuteLinearEntry(int entryAddress, HashSet<string> visitingCallables, bool allowPipeline)
    {
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)entryAddress >= (uint)code.Count)
        {
            return false;
        }

        for (var pc = entryAddress; pc < code.Count; pc++)
        {
            var instruction = code[pc];
            if (!CanExecuteLinearInstruction(instruction, visitingCallables, allowPipeline))
            {
                return false;
            }

            if (instruction.OpCode == GameEventScriptBytecodeOpCode.Return)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryInvokeLinearHandler(GesBytecodeVmCompiledHandler handler, IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        EnterScope();
        try
        {
            return TryExecuteLinearRange(
                handler.EntryAddress,
                _compiledScript.LinearExecutable.Code.Count,
                LinearArgumentSource.ForHandler(handler, args),
                out _,
                out _);
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryExecuteLinearRange(
        int startAddress,
        int endAddress,
        LinearArgumentSource? arguments,
        out bool returned,
        out BytecodeVmValue returnValue)
    {
        returned = false;
        returnValue = BytecodeVmValue.Nothing;
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)startAddress > (uint)code.Count ||
            (uint)endAddress > (uint)code.Count ||
            startAddress > endAddress)
        {
            return false;
        }

        var pc = startAddress;
        List<LinearCallFrame>? callFrames = null;
        try
        {
            while (pc < endAddress)
            {
                if (!TryConsumeExecutionStep("Linear instruction execution budget exhausted."))
                {
                    _halted = true;
                    returned = true;
                    return true;
                }

                var instructionAddress = pc;
                if (!RecordLinearHandlerInvokedIfReady(arguments))
                {
                    return false;
                }

                RecordLinearDiagnosticsBefore(instructionAddress);
                var instruction = code[instructionAddress];
                var callFrameCountBefore = callFrames?.Count ?? 0;
                if (!TryExecuteLinearInstruction(
                        instruction,
                        ref pc,
                        ref endAddress,
                        ref arguments,
                        ref callFrames,
                        out returned,
                        out returnValue))
                {
                    return false;
                }

                var callFrameCountAfter = callFrames?.Count ?? 0;
                if (callFrameCountAfter <= callFrameCountBefore ||
                    instruction.OpCode is not (GameEventScriptBytecodeOpCode.Call or GameEventScriptBytecodeOpCode.PredicateTest))
                {
                    RecordLinearDiagnosticsAfter(instructionAddress);
                }

                if (returned)
                {
                    return true;
                }
            }

            if (!RecordLinearHandlerInvokedIfReady(arguments))
            {
                return false;
            }

            return true;
        }
        finally
        {
            UnwindLinearCallFrames(callFrames);
        }
    }

    private bool TryExecuteLinearInstruction(
        GameEventScriptBytecodeInstruction instruction,
        ref int pc,
        ref int endAddress,
        ref LinearArgumentSource? arguments,
        ref List<LinearCallFrame>? callFrames,
        out bool returned,
        out BytecodeVmValue returnValue)
    {
        returned = false;
        returnValue = BytecodeVmValue.Nothing;
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Nop:
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.EnterScope:
                EnterScope();
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.ExitScope:
                ExitScope();
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.BindParameter:
                if (arguments is null ||
                    !arguments.TryGetValue(instruction.A, out var parameterValue) ||
                    !DefineSlot(instruction.Dest, parameterValue))
                {
                    return false;
                }

                RecordLinearParameterBoundFromBind(arguments, instruction.A, instruction.Dest);
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.CoerceSlot:
                if (!TryReadTypeMetadata(instruction.Data, out var typeName) ||
                    !TryConvertDeclaredType(typeName, ResolveSlot(instruction.A), out var coercedValue) ||
                    !DefineSlot(instruction.Dest, coercedValue))
                {
                    return false;
                }

                RecordLinearParameterBoundFromCoerce(arguments, instruction.Dest, coercedValue);
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.LoadConstant:
                if (!DefineSlot(instruction.Dest, LoadConstant(instruction.Data)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.LoadSlot:
            case GameEventScriptBytecodeOpCode.CopySlot:
                if (!DefineSlot(instruction.Dest, ResolveSlot(instruction.A)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.Jump:
                return TryMoveLinearPc(instruction.Target, endAddress, ref pc);

            case GameEventScriptBytecodeOpCode.JumpIfTrue:
                if (ResolveSlot(instruction.A).IsTrue())
                {
                    return TryMoveLinearPc(instruction.Target, endAddress, ref pc);
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.JumpIfFalse:
                if (ResolveSlot(instruction.A).IsFalse())
                {
                    return TryMoveLinearPc(instruction.Target, endAddress, ref pc);
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                if (!ResolveSlot(instruction.A).IsTrue())
                {
                    return TryMoveLinearPc(instruction.Target, endAddress, ref pc);
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.Return:
                returnValue = ResolveSlot(instruction.A);
                if (callFrames is null || callFrames.Count == 0)
                {
                    returned = true;
                    return true;
                }

                var frame = callFrames[^1];
                callFrames.RemoveAt(callFrames.Count - 1);
                ExitScope();
                _locals = frame.Locals;
                _assignedSlots = frame.AssignedSlots;
                _changes = frame.Changes;
                _scopeMarks = frame.ScopeMarks;
                arguments = frame.Arguments;
                endAddress = frame.EndAddress;
                _runtimeBudget.ExitCall();

                if (frame.NormalizePredicateResult &&
                    !NormalizeExtensionPredicateResult(requirePredicateResult: true, ref returnValue))
                {
                    return false;
                }

                if (!DefineSlot(frame.ReturnDestinationSlot, returnValue))
                {
                    return false;
                }

                RecordLinearDiagnosticsAfter(frame.CallInstructionAddress);
                pc = frame.ReturnAddress;
                return true;

            case GameEventScriptBytecodeOpCode.PublishValue:
                if (!TryPublishLinearLayout(instruction.Data))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.PublishMessageValue:
                if (!TryPublishLinearMessageValue(instruction.Data, ResolveSlot(instruction.A)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.ForRange:
                if (!TryExecuteLinearRangeFor(instruction))
                {
                    return false;
                }

                pc = instruction.Target2;
                return true;

            case GameEventScriptBytecodeOpCode.ForCollection:
                if (!TryExecuteLinearCollectionFor(instruction))
                {
                    return false;
                }

                pc = instruction.Target2;
                return true;

            case GameEventScriptBytecodeOpCode.SeededRandomBlock:
                if (!TryExecuteLinearSeededRandomBlock(instruction))
                {
                    return false;
                }

                pc = instruction.Target2;
                return true;

            case GameEventScriptBytecodeOpCode.Call:
            case GameEventScriptBytecodeOpCode.PredicateTest:
                return TryEnterLinearCallFrame(
                    instruction,
                    pc,
                    arguments,
                    endAddress,
                    pc + 1,
                    ref callFrames,
                    out arguments,
                    out endAddress,
                    out pc);

            default:
                if (!TryExecuteLinearValueInstruction(instruction))
                {
                    return false;
                }

                pc++;
                return true;
        }
    }

    private bool TryEnterLinearCallFrame(
        GameEventScriptBytecodeInstruction instruction,
        int callInstructionAddress,
        LinearArgumentSource? currentArguments,
        int currentEndAddress,
        int returnAddress,
        ref List<LinearCallFrame>? callFrames,
        out LinearArgumentSource? nextArguments,
        out int nextEndAddress,
        out int nextPc)
    {
        nextArguments = currentArguments;
        nextEndAddress = currentEndAddress;
        nextPc = returnAddress;

        if (!TryGetOperationLayout(instruction.Data, out var layout) ||
            string.IsNullOrEmpty(layout.Name) ||
            !_compiledScript.BytecodeModule.Callables.TryGetValue(layout.Name, out var callable))
        {
            return false;
        }

        var normalizePredicateResult = instruction.OpCode == GameEventScriptBytecodeOpCode.PredicateTest;
        var callArguments = normalizePredicateResult
            ? new[] { ResolveSlot(instruction.A) }
            : CopyLinearOperands(layout.ArgumentSlots);

        if (!_runtimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            var value = BytecodeVmValue.Nothing;
            if (normalizePredicateResult &&
                !NormalizeExtensionPredicateResult(requirePredicateResult: true, ref value))
            {
                return false;
            }

            return DefineSlot(instruction.Dest, value);
        }

        if (!RecordLinearCallableCalled(layout, callArguments, normalizePredicateResult))
        {
            _runtimeBudget.ExitCall();
            return false;
        }

        callFrames ??= [];
        callFrames.Add(new LinearCallFrame(
            _locals,
            _assignedSlots,
            _changes,
            _scopeMarks,
            _trackedLocalSlotCount,
            currentArguments,
            currentEndAddress,
            returnAddress,
            instruction.Dest,
            callInstructionAddress,
            normalizePredicateResult));

        _locals = new BytecodeVmValue[Math.Max(1, callable.LocalSlotCount)];
        _assignedSlots = new bool[_locals.Length];
        _changes = [];
        _scopeMarks = [];
        _trackedLocalSlotCount = callable.LocalSlotCount;
        nextArguments = LinearArgumentSource.ForValues(callArguments);
        nextEndAddress = _compiledScript.LinearExecutable.Code.Count;
        nextPc = callable.EntryAddress;
        EnterScope();
        return true;
    }

    private void UnwindLinearCallFrames(List<LinearCallFrame>? callFrames)
    {
        if (callFrames is null)
        {
            return;
        }

        while (callFrames.Count > 0)
        {
            var frame = callFrames[^1];
            callFrames.RemoveAt(callFrames.Count - 1);
            ExitScope();
            _locals = frame.Locals;
            _assignedSlots = frame.AssignedSlots;
            _changes = frame.Changes;
            _scopeMarks = frame.ScopeMarks;
            _trackedLocalSlotCount = frame.TrackedLocalSlotCount;
            _runtimeBudget.ExitCall();
        }
    }

    private bool TryMoveLinearPc(int target, int endAddress, ref int pc)
    {
        if (target < 0 || target > endAddress)
        {
            return false;
        }

        pc = target;
        return true;
    }

    private bool RecordLinearHandlerInvokedIfReady(LinearArgumentSource? arguments)
    {
        if (!_diagnosticsEnabled ||
            arguments is null ||
            !arguments.TryMarkHandlerInvoked(out var message, out var handlerArgs))
        {
            return true;
        }

        RecordHandlerInvoked(message, handlerArgs);
        return true;
    }

    private void RecordLinearParameterBoundFromBind(LinearArgumentSource? arguments, int parameterIndex, int slot)
    {
        if (!_diagnosticsEnabled ||
            arguments is null ||
            !arguments.TryGetHandlerParameterDiagnostic(parameterIndex, out var diagnostic))
        {
            return;
        }

        if (diagnostic.HasDeclaredType)
        {
            arguments.MarkPendingTypedParameter(slot, parameterIndex);
            return;
        }

        RecordParameterBound(diagnostic.Name, diagnostic.OriginalValue);
        arguments.MarkParameterDiagnosticRecorded(parameterIndex);
    }

    private void RecordLinearParameterBoundFromCoerce(LinearArgumentSource? arguments, int slot, BytecodeVmValue coercedValue)
    {
        if (!_diagnosticsEnabled ||
            arguments is null ||
            !arguments.TryTakePendingTypedParameter(slot, out var parameterIndex) ||
            !arguments.TryGetHandlerParameterDiagnostic(parameterIndex, out var diagnostic))
        {
            return;
        }

        RecordParameterBound(diagnostic.Name, coercedValue.ToGameEventScriptValue());
        arguments.MarkParameterDiagnosticRecorded(parameterIndex);
    }

    private bool RecordLinearCallableCalled(
        GameEventScriptBytecodeOperationLayout layout,
        IReadOnlyList<BytecodeVmValue> arguments,
        bool normalizePredicateResult)
    {
        if (!_diagnosticsEnabled)
        {
            return true;
        }

        if (normalizePredicateResult)
        {
            var predicateInput = arguments.Count > 0 ? arguments[0] : BytecodeVmValue.Nothing;
            if (!TryConvertParameterType(layout.DeclaredTypes, 0, predicateInput, out predicateInput))
            {
                return false;
            }

            RecordPredicateCalled(layout.Name ?? string.Empty, layout.ArgumentName ?? "value", predicateInput);
            return true;
        }

        RecordCallableCalled(layout.Name ?? string.Empty, layout.CallableKind, layout.Names, arguments);
        return true;
    }

    private void RecordLinearDiagnosticsBefore(int address)
        => RecordLinearDiagnostics(_compiledScript.LinearExecutable.DiagnosticsBefore, address);

    private void RecordLinearDiagnosticsAfter(int address)
        => RecordLinearDiagnostics(_compiledScript.LinearExecutable.DiagnosticsAfter, address);

    private void RecordLinearDiagnostics(
        IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] sites,
        int address)
    {
        if (!_diagnosticsEnabled ||
            (uint)address >= (uint)sites.Length)
        {
            return;
        }

        var entries = sites[address];
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var value = ResolveSlot(entry.Slot);
            switch (entry.Kind)
            {
                case GameEventScriptBytecodeDiagnosticKind.LetEvaluated:
                    RecordLetEvaluated(entry.Name, value);
                    break;

                case GameEventScriptBytecodeDiagnosticKind.ExpressionEvaluatedToNothing:
                    if (value.Kind == BytecodeVmValueKind.Nothing)
                    {
                        RecordExpressionEvaluatedToNothing(entry.Name);
                    }
                    break;
            }
        }
    }

    private bool TryExecuteLinearValueInstruction(GameEventScriptBytecodeInstruction instruction)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Or:
            case GameEventScriptBytecodeOpCode.Xor:
            case GameEventScriptBytecodeOpCode.And:
            case GameEventScriptBytecodeOpCode.Power:
            case GameEventScriptBytecodeOpCode.ShortCircuitImplies:
            case GameEventScriptBytecodeOpCode.Equal:
            case GameEventScriptBytecodeOpCode.NotEqual:
            case GameEventScriptBytecodeOpCode.ApproxEqual:
            case GameEventScriptBytecodeOpCode.Less:
            case GameEventScriptBytecodeOpCode.Greater:
            case GameEventScriptBytecodeOpCode.LessOrEqual:
            case GameEventScriptBytecodeOpCode.GreaterOrEqual:
            case GameEventScriptBytecodeOpCode.Add:
            case GameEventScriptBytecodeOpCode.Subtract:
            case GameEventScriptBytecodeOpCode.Multiply:
            case GameEventScriptBytecodeOpCode.Divide:
            case GameEventScriptBytecodeOpCode.IntegerDivide:
            case GameEventScriptBytecodeOpCode.Modulo:
            case GameEventScriptBytecodeOpCode.Remainder:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLess:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo:
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder:
            case GameEventScriptBytecodeOpCode.Default:
            case GameEventScriptBytecodeOpCode.Contains:
            case GameEventScriptBytecodeOpCode.ContainsValue:
            case GameEventScriptBytecodeOpCode.StartsWith:
            case GameEventScriptBytecodeOpCode.EndsWith:
            case GameEventScriptBytecodeOpCode.Intersect:
            case GameEventScriptBytecodeOpCode.Combine:
            case GameEventScriptBytecodeOpCode.Except:
            case GameEventScriptBytecodeOpCode.Zip:
                return DefineSlot(
                    instruction.Dest,
                    EvaluateProgramBinary(instruction.OpCode, ResolveSlot(instruction.A), ResolveSlot(instruction.B)));

            case GameEventScriptBytecodeOpCode.Clamp:
                return DefineSlot(
                    instruction.Dest,
                    EvaluateClamp(ResolveSlot(instruction.A), ResolveSlot(instruction.B), ResolveSlot(instruction.C)));

            case GameEventScriptBytecodeOpCode.Random:
                return DefineSlot(
                    instruction.Dest,
                    EvaluateRandomExpression(ResolveSlot(instruction.A), ResolveSlot(instruction.B)));

            case GameEventScriptBytecodeOpCode.Dice:
                return DefineSlot(instruction.Dest, EvaluateDiceExpression(instruction.A, instruction.B));

            case GameEventScriptBytecodeOpCode.SeededRandom:
                if (!TryEvaluateLinearSeededRandom(instruction.Data, ResolveSlot(instruction.A), out var seededValue))
                {
                    return false;
                }

                return DefineSlot(instruction.Dest, seededValue);

            case GameEventScriptBytecodeOpCode.IndexedAccess:
                return DefineSlot(instruction.Dest, EvaluateIndexedAccess(ResolveSlot(instruction.A), ResolveSlot(instruction.B)));

            case GameEventScriptBytecodeOpCode.Pipeline:
                if (!TryEvaluateLinearPipeline(instruction.Data, out var pipelineValue))
                {
                    return false;
                }

                return DefineSlot(instruction.Dest, pipelineValue);

            case GameEventScriptBytecodeOpCode.GeneratedCollection:
                if (!TryEvaluateLinearGeneratedCollection(instruction.Data, out var generatedValue))
                {
                    return false;
                }

                return DefineSlot(instruction.Dest, generatedValue);

            case GameEventScriptBytecodeOpCode.GuardedChoice:
                if (!TryEvaluateLinearGuardedChoice(instruction.Data, out var guardedValue))
                {
                    return false;
                }

                return DefineSlot(instruction.Dest, guardedValue);
        }

        if (!TryGetOperationLayout(instruction.Data, out var layout))
        {
            return false;
        }

        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Unary:
                if (!TryEvaluateUnaryOperation(layout.Name, ResolveSlot(instruction.A), out var unaryValue))
                {
                    return false;
                }

                return DefineSlot(instruction.Dest, unaryValue);

            case GameEventScriptBytecodeOpCode.Cast:
                return DefineSlot(instruction.Dest, EvaluateProgramCast(layout.CastKind, ResolveSlot(instruction.A)));

            case GameEventScriptBytecodeOpCode.TypeCheck:
                return DefineSlot(
                    instruction.Dest,
                    BytecodeVmValue.Boolean(IsValueOfType(ResolveSlot(instruction.A), layout.Name)));

            case GameEventScriptBytecodeOpCode.MemberAccess:
                return DefineSlot(instruction.Dest, EvaluateMemberAccess(ResolveSlot(instruction.A), layout.Name));

            case GameEventScriptBytecodeOpCode.Range:
                return DefineSlot(
                    instruction.Dest,
                    EvaluateRangeExpression(
                        ResolveSlot(instruction.A),
                        ResolveSlot(instruction.B),
                        instruction.C >= 0 ? ResolveSlot(instruction.C) : BytecodeVmValue.Integer(1)));
        }

        var operandCount = layout.ArgumentSlots.Count;
        var operands = operandCount == 0
            ? Array.Empty<BytecodeVmValue>()
            : ArrayPool<BytecodeVmValue>.Shared.Rent(operandCount);
        if (operandCount > 0)
        {
            CopyLinearOperands(layout.ArgumentSlots, operands);
        }

        try
        {
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Variadic:
                    if (!TryEvaluateVariadicOperation(layout.Name, operands, 0, operandCount, out var variadicValue))
                    {
                        return false;
                    }

                    return DefineSlot(instruction.Dest, variadicValue);

                case GameEventScriptBytecodeOpCode.TypeConstructor:
                    return DefineSlot(
                        instruction.Dest,
                        EvaluateTypeConstructor(layout.Name, AsArray(layout.Names), operands, 0, operandCount));

                case GameEventScriptBytecodeOpCode.BuildList:
                    return DefineSlot(instruction.Dest, BuildListValue(operands, 0, operandCount));

                case GameEventScriptBytecodeOpCode.BuildSequence:
                    return DefineSlot(instruction.Dest, BuildSequenceValue(operands, 0, operandCount));

                case GameEventScriptBytecodeOpCode.BuildSet:
                    return DefineSlot(instruction.Dest, BuildSetValue(operands, 0, operandCount));

                case GameEventScriptBytecodeOpCode.BuildDictionary:
                    return DefineSlot(instruction.Dest, BuildDictionaryValue(operands, 0, operandCount, AsArray(layout.Names)));

                case GameEventScriptBytecodeOpCode.BuildMessage:
                    return DefineSlot(
                        instruction.Dest,
                        BuildMessageValue(
                            operands,
                            0,
                            operandCount,
                            AsArray(layout.Names),
                            layout.Name,
                            layout.ArgumentName));

                case GameEventScriptBytecodeOpCode.BindHandler:
                    if (operandCount == 0)
                    {
                        return DefineSlot(instruction.Dest, BytecodeVmValue.Nothing);
                    }

                    return DefineSlot(
                        instruction.Dest,
                        BindHandlerValue(operands[0], operands, 1, operandCount - 1, AsArray(layout.Names)));

                case GameEventScriptBytecodeOpCode.CallExtension:
                    if (!TryCallExtension(
                            layout.Name,
                            layout.ArgumentName,
                            AsArray(layout.Names),
                            layout.ExternalReferenceIndex,
                            operands,
                            0,
                            operandCount,
                            layout.CallableKind == GameEventScriptBytecodeCallableKind.Predicate,
                            out var extensionValue))
                    {
                        return false;
                    }

                    return DefineSlot(instruction.Dest, extensionValue);

                default:
                    return false;
            }
        }
        finally
        {
            if (operandCount > 0)
            {
                Array.Clear(operands, 0, operandCount);
                ArrayPool<BytecodeVmValue>.Shared.Return(operands);
            }
        }
    }

    private bool TryEvaluateLinearSeededRandom(int layoutIndex, BytecodeVmValue seed, out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryGetOperationLayout(layoutIndex, out var layout) ||
            layout.ExpressionEntryAddress < 0)
        {
            return false;
        }

        PushSeededRandomScope(seed.ToGameEventScriptValue());
        try
        {
            return TryEvaluateLinearHelperExpression(layout.ExpressionEntryAddress, out value);
        }
        finally
        {
            PopSeededRandomScope();
        }
    }

    private bool TryEvaluateLinearPipeline(int layoutIndex, out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryGetPipelineLayout(layoutIndex, out var layout) ||
            !TryGetSelectorLayout(layout.TerminalSelectorLayoutIndex, out var terminal))
        {
            return false;
        }

        var sourceTarget = ResolveSlot(layout.SourceSlot).ToGameEventScriptValue();
        if (TryGetSeriesTarget(sourceTarget, out var seriesTarget))
        {
            return TryEvaluateLinearSeriesPipeline(
                seriesTarget,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value);
        }

        var checkRangeItemLimit = GesRuntimeLimitUtilities.TryGetRangeLength(sourceTarget, out var sourceRangeLength);
        if (ShouldCheckLinearPipelineSourceLengthBeforeExecution(terminal, layout.PrefixSelectorLayoutIndexes.Count) &&
            checkRangeItemLimit &&
            !_runtimeBudget.TryCheckRangeLength(sourceRangeLength, PipelineRangeLimitDetail))
        {
            return true;
        }

        if (ShouldCheckLinearPipelineSourceLengthBeforeExecution(terminal, layout.PrefixSelectorLayoutIndexes.Count))
        {
            checkRangeItemLimit = false;
        }

        if (TryGetIndexedPipelineSource(sourceTarget, out var indexedSourceItems))
        {
            switch (terminal.Kind)
            {
                case GameEventScriptBytecodeSelectorKind.Sum:
                    return TryEvaluateLinearIndexedPipelineSum(indexedSourceItems, layout.PrefixSelectorLayoutIndexes, terminal, out value);

                case GameEventScriptBytecodeSelectorKind.Average:
                    return TryEvaluateLinearIndexedPipelineAverage(indexedSourceItems, layout.PrefixSelectorLayoutIndexes, terminal, out value);

                case GameEventScriptBytecodeSelectorKind.Count:
                    return TryEvaluateLinearIndexedPipelineCount(indexedSourceItems, layout.PrefixSelectorLayoutIndexes, terminal, out value);

                case GameEventScriptBytecodeSelectorKind.Edge when IsSupportedLinearPipelineEdgeMode(terminal.EdgeMode):
                    return TryEvaluateLinearIndexedPipelineEdge(indexedSourceItems, layout.PrefixSelectorLayoutIndexes, terminal, out value);
            }
        }

        return terminal.Kind switch
        {
            GameEventScriptBytecodeSelectorKind.Select => TryEvaluateLinearPipelineSelect(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Filter => TryEvaluateLinearPipelineFilter(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Edge when IsSupportedLinearPipelineEdgeMode(terminal.EdgeMode) =>
                TryEvaluateLinearPipelineEdge(
                    EnumerateListLikeValue(sourceTarget),
                    checkRangeItemLimit,
                    layout.PrefixSelectorLayoutIndexes,
                    terminal,
                    out value),
            GameEventScriptBytecodeSelectorKind.Count => TryEvaluateLinearPipelineCount(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Predicate => TryEvaluateLinearPipelinePredicate(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Sum => TryEvaluateLinearPipelineSum(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Average => TryEvaluateLinearPipelineAverage(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Min => TryEvaluateLinearPipelineExtrema(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                isMax: false,
                out value),
            GameEventScriptBytecodeSelectorKind.Max => TryEvaluateLinearPipelineExtrema(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                isMax: true,
                out value),
            GameEventScriptBytecodeSelectorKind.Dictionary => TryEvaluateLinearPipelineDictionary(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Distinct => TryEvaluateLinearPipelineDistinct(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.GroupBy => TryEvaluateLinearPipelineGroupBy(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Reverse => TryEvaluateLinearPipelineReverse(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                out value),
            GameEventScriptBytecodeSelectorKind.Sort => TryEvaluateLinearPipelineSort(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.OrderBy => TryEvaluateLinearPipelineOrderBy(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.SequenceSlice => TryEvaluateLinearPipelineSequenceSlice(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Shuffle => TryEvaluateLinearPipelineShuffle(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                out value),
            GameEventScriptBytecodeSelectorKind.Draw => TryEvaluateLinearPipelineDraw(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Pattern or GameEventScriptBytecodeSelectorKind.ObjectMatch or GameEventScriptBytecodeSelectorKind.TakePattern =>
                TryEvaluateLinearPipelinePattern(
                    sourceTarget,
                    EnumerateListLikeValue(sourceTarget),
                    checkRangeItemLimit,
                    layout.PrefixSelectorLayoutIndexes,
                    terminal,
                    out value),
            GameEventScriptBytecodeSelectorKind.Choose => TryEvaluateLinearPipelineChoose(
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            GameEventScriptBytecodeSelectorKind.Contains => TryEvaluateLinearPipelineContains(
                sourceTarget,
                EnumerateListLikeValue(sourceTarget),
                checkRangeItemLimit,
                layout.PrefixSelectorLayoutIndexes,
                terminal,
                out value),
            _ => false
        };
    }

    private static bool IsSupportedLinearPipelineEdgeMode(string? edgeMode)
        => string.Equals(edgeMode, "first", StringComparison.Ordinal) ||
           string.Equals(edgeMode, "last", StringComparison.Ordinal) ||
           string.Equals(edgeMode, "single", StringComparison.Ordinal);

    private static bool IsSupportedLinearPipelineContainsMode(string? edgeMode)
        => string.Equals(edgeMode, "single", StringComparison.Ordinal) ||
           string.Equals(edgeMode, "all", StringComparison.Ordinal) ||
           string.Equals(edgeMode, "any", StringComparison.Ordinal);

    private bool TryEvaluateLinearSeriesPipeline(
        GameEventScriptSeriesValue source,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var series = source;
        for (var prefixIndex = 0; prefixIndex < prefixSelectorIndexes.Count; prefixIndex++)
        {
            var selectorIndex = prefixSelectorIndexes[prefixIndex];
            if (!TryGetSelectorLayout(selectorIndex, out var selector) ||
                !TryApplyLinearSeriesPrefixSelector(series, selector, out series))
            {
                value = BytecodeVmValue.Nothing;
                return true;
            }
        }

        switch (terminal.Kind)
        {
            case GameEventScriptBytecodeSelectorKind.SeriesTerm:
                if (!TryEvaluateLinearPipelineSelectorExpression(terminal, BytecodeVmValue.Nothing, out var termIndex))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (!termIndex.ToGameEventScriptValue().TryConvertToInteger(out var integerTermIndex))
                {
                    value = BytecodeVmValue.Nothing;
                    return true;
                }

                value = BytecodeVmValue.FromGameEventScriptValue(series.GetTerm(integerTermIndex.AsInteger()));
                return true;

            case GameEventScriptBytecodeSelectorKind.SequenceSlice:
                return TryEvaluateLinearSeriesSliceSelector(series, terminal, out value);

            default:
                value = BytecodeVmValue.Nothing;
                return true;
        }
    }

    private static bool TryApplyLinearSeriesPrefixSelector(
        GameEventScriptSeriesValue source,
        GameEventScriptBytecodeSelectorLayout selector,
        out GameEventScriptSeriesValue series)
    {
        if (selector.Kind == GameEventScriptBytecodeSelectorKind.SequenceSlice &&
            string.Equals(selector.EdgeMode, "drop", StringComparison.Ordinal) &&
            string.Equals(selector.SecondaryMode, "first", StringComparison.Ordinal))
        {
            series = source.Drop(selector.Count);
            return true;
        }

        series = source;
        return false;
    }

    private bool TryEvaluateLinearSeriesSliceSelector(
        GameEventScriptSeriesValue series,
        GameEventScriptBytecodeSelectorLayout selector,
        out BytecodeVmValue value)
    {
        if (!string.Equals(selector.SecondaryMode, "first", StringComparison.Ordinal))
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        if (string.Equals(selector.EdgeMode, "drop", StringComparison.Ordinal))
        {
            value = BytecodeVmValue.Reference(series.Drop(selector.Count));
            return true;
        }

        if (!string.Equals(selector.EdgeMode, "take", StringComparison.Ordinal))
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        if (!_runtimeBudget.TryCheckRangeLength(selector.Count, "Series take would materialize more items than allowed."))
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        value = BytecodeVmValue.Reference(GesList(series.Take(selector.Count)));
        return true;
    }

    private static bool IsSupportedLinearPipelineSequenceSliceMode(string? operation, string? scope)
        => (string.Equals(operation, "take", StringComparison.Ordinal) ||
            string.Equals(operation, "drop", StringComparison.Ordinal)) &&
           (string.Equals(scope, "first", StringComparison.Ordinal) ||
            string.Equals(scope, "last", StringComparison.Ordinal) ||
            string.Equals(scope, "highest", StringComparison.Ordinal) ||
            string.Equals(scope, "lowest", StringComparison.Ordinal));

    private static bool ShouldCheckLinearPipelineSourceLengthBeforeExecution(
        GameEventScriptBytecodeSelectorLayout terminal,
        int prefixSelectorCount)
        => terminal.Kind switch
        {
            GameEventScriptBytecodeSelectorKind.Edge => !string.Equals(terminal.EdgeMode, "first", StringComparison.Ordinal),
            GameEventScriptBytecodeSelectorKind.Predicate => false,
            GameEventScriptBytecodeSelectorKind.Contains => prefixSelectorCount > 0,
            _ => true
        };

    private bool TryEvaluateLinearIndexedPipelineSum(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (terminal.ExpressionEntryAddress < 0)
        {
            return false;
        }

        var hasValue = false;
        var sum = BytecodeVmValue.Float(0d);
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            if (!TryApplyLinearPipelinePrefixes(item, prefixSelectorIndexes, out item, out var include))
            {
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var projected))
            {
                return false;
            }

            sum = hasValue ? BytecodeVmValue.Add(sum, projected) : projected;
            hasValue = true;
        }

        value = hasValue ? sum : BytecodeVmValue.Float(0d);
        return true;
    }

    private bool TryEvaluateLinearIndexedPipelineAverage(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (terminal.ExpressionEntryAddress < 0)
        {
            return false;
        }

        var count = 0L;
        var sum = BytecodeVmValue.Float(0d);
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            if (!TryApplyLinearPipelinePrefixes(item, prefixSelectorIndexes, out item, out var include))
            {
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var projected))
            {
                return false;
            }

            sum = count == 0 ? projected : BytecodeVmValue.Add(sum, projected);
            count++;
        }

        value = count > 0 && sum.TryGetFiniteNumber(out var number)
            ? BytecodeVmValue.Float(number / count, sum.Unit)
            : BytecodeVmValue.Nothing;
        return true;
    }

    private bool TryEvaluateLinearIndexedPipelineCount(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var count = 0L;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            if (!TryApplyLinearPipelinePrefixes(item, prefixSelectorIndexes, out item, out var include))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (terminal.ExpressionEntryAddress >= 0)
            {
                if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var predicate))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (!predicate.IsTrue())
                {
                    continue;
                }
            }

            count++;
        }

        value = BytecodeVmValue.Integer(count);
        return true;
    }

    private bool TryEvaluateLinearIndexedPipelineEdge(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var first = BytecodeVmValue.Nothing;
        var last = BytecodeVmValue.Nothing;
        var count = 0;
        var stopAfterFirst = string.Equals(terminal.EdgeMode, "first", StringComparison.Ordinal);
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            if (!TryApplyLinearPipelinePrefixes(item, prefixSelectorIndexes, out item, out var include))
            {
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (terminal.ExpressionEntryAddress >= 0)
            {
                if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var predicate))
                {
                    return false;
                }

                if (!predicate.IsTrue())
                {
                    continue;
                }
            }

            first = count == 0 ? item : first;
            last = item;
            count++;
            if (stopAfterFirst)
            {
                break;
            }
        }

        value = terminal.EdgeMode switch
        {
            "first" => count > 0 ? first : BytecodeVmValue.Nothing,
            "last" => count > 0 ? last : BytecodeVmValue.Nothing,
            "single" => count == 1 ? first : BytecodeVmValue.Nothing,
            _ => BytecodeVmValue.Nothing
        };
        return true;
    }

    private bool TryEvaluateLinearPipelineSelect(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var result = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var selected))
            {
                return PipelineIterationResult.Failed;
            }

            result.Add(selected.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(result))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineFilter(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var result = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var predicate))
            {
                return PipelineIterationResult.Failed;
            }

            if (predicate.IsTrue())
            {
                result.Add(item.ToGameEventScriptValue());
            }

            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(result))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineEdge(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var first = BytecodeVmValue.Nothing;
        var last = BytecodeVmValue.Nothing;
        var count = 0;
        var stopAfterFirst = string.Equals(terminal.EdgeMode, "first", StringComparison.Ordinal);
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (terminal.ExpressionEntryAddress >= 0)
            {
                if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var predicate))
                {
                    return PipelineIterationResult.Failed;
                }

                if (!predicate.IsTrue())
                {
                    return PipelineIterationResult.Completed;
                }
            }

            first = count == 0 ? item : first;
            last = item;
            count++;
            return stopAfterFirst
                ? PipelineIterationResult.Stopped
                : PipelineIterationResult.Completed;
        });

        if (iterationResult is PipelineIterationResult.Completed or PipelineIterationResult.Stopped)
        {
            value = terminal.EdgeMode switch
            {
                "first" => count > 0 ? first : BytecodeVmValue.Nothing,
                "last" => count > 0 ? last : BytecodeVmValue.Nothing,
                "single" => count == 1 ? first : BytecodeVmValue.Nothing,
                _ => BytecodeVmValue.Nothing
            };
        }

        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineCount(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        long count = 0;
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (terminal.ExpressionEntryAddress >= 0)
            {
                if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var predicate))
                {
                    return PipelineIterationResult.Failed;
                }

                if (!predicate.IsTrue())
                {
                    return PipelineIterationResult.Completed;
                }
            }

            count++;
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.Integer(count)
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelinePredicate(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var isAny = string.Equals(terminal.EdgeMode, "any", StringComparison.Ordinal);
        if (!isAny && !string.Equals(terminal.EdgeMode, "all", StringComparison.Ordinal))
        {
            return false;
        }

        var result = !isAny;
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var predicate))
            {
                return PipelineIterationResult.Failed;
            }

            if (isAny && predicate.IsTrue())
            {
                result = true;
                return PipelineIterationResult.Stopped;
            }

            if (!isAny && !predicate.IsTrue())
            {
                result = false;
                return PipelineIterationResult.Stopped;
            }

            return PipelineIterationResult.Completed;
        });

        if (iterationResult is PipelineIterationResult.Completed or PipelineIterationResult.Stopped)
        {
            value = BytecodeVmValue.Boolean(result);
        }

        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineSum(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var hasValue = false;
        var sum = BytecodeVmValue.Float(0d);
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var projected))
            {
                return PipelineIterationResult.Failed;
            }

            sum = hasValue ? BytecodeVmValue.Add(sum, projected) : projected;
            hasValue = true;
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? (hasValue ? sum : BytecodeVmValue.Float(0d))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineAverage(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var count = 0L;
        var sum = BytecodeVmValue.Float(0d);
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var projected))
            {
                return PipelineIterationResult.Failed;
            }

            sum = count == 0 ? projected : BytecodeVmValue.Add(sum, projected);
            count++;
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed &&
                count > 0 &&
                sum.TryGetFiniteNumber(out var number)
            ? BytecodeVmValue.Float(number / count, sum.Unit)
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineExtrema(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        bool isMax,
        out BytecodeVmValue value)
    {
        BytecodeVmValue bestItem = BytecodeVmValue.Nothing;
        GameEventScriptValue? bestProjection = null;
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var projection))
            {
                return PipelineIterationResult.Failed;
            }

            var candidateProjection = projection.ToGameEventScriptValue();
            if (bestProjection is null)
            {
                bestProjection = candidateProjection;
                bestItem = item;
                return PipelineIterationResult.Completed;
            }

            int comparison;
            if (GesValueOperations.TryCoerceNumericForOperation(candidateProjection, out _) &&
                GesValueOperations.TryCoerceNumericForOperation(bestProjection, out _))
            {
                if (!GesValueOperations.TryCompareNumericValues(candidateProjection, bestProjection, out comparison))
                {
                    return PipelineIterationResult.Failed;
                }
            }
            else
            {
                comparison = GameEventScriptValue.StableComparer.Compare(candidateProjection, bestProjection);
            }

            if ((isMax && comparison > 0) || (!isMax && comparison < 0))
            {
                bestProjection = candidateProjection;
                bestItem = item;
            }

            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed && bestProjection is not null
            ? bestItem
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineContains(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryEvaluateLinearPipelineSelectorExpression(terminal, BytecodeVmValue.Nothing, out var needle))
        {
            return false;
        }

        var target = sourceTarget;
        if (prefixSelectorIndexes.Count != 0)
        {
            var targetItems = new List<GameEventScriptValue>();
            var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
            {
                targetItems.Add(item.ToGameEventScriptValue());
                return PipelineIterationResult.Completed;
            });
            if (iterationResult != PipelineIterationResult.Completed)
            {
                value = BytecodeVmValue.Nothing;
                return iterationResult != PipelineIterationResult.Failed;
            }

            target = GameEventScriptValueFactory.GesList(targetItems);
        }

        var boxedNeedle = needle.ToGameEventScriptValue();
        value = terminal.EdgeMode switch
        {
            "single" => BytecodeVmValue.Boolean(target.Contains(boxedNeedle)),
            "all" => BytecodeVmValue.Boolean(EnumerateListLikeValue(boxedNeedle).All(target.Contains)),
            "any" => BytecodeVmValue.Boolean(EnumerateListLikeValue(boxedNeedle).Any(target.Contains)),
            _ => BytecodeVmValue.Boolean(false)
        };
        return true;
    }

    private bool TryEvaluateLinearPipelineDictionary(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var result = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var keyValue))
            {
                return PipelineIterationResult.Failed;
            }

            var key = keyValue.ToGameEventScriptValue().AsText();
            if (string.IsNullOrEmpty(key))
            {
                return PipelineIterationResult.Completed;
            }

            if (terminal.SecondaryExpressionEntryAddress < 0)
            {
                result[key] = item.ToGameEventScriptValue();
                return PipelineIterationResult.Completed;
            }

            if (!TryEvaluateLinearPipelineSecondarySelectorExpression(terminal, item, out var projectedValue))
            {
                return PipelineIterationResult.Failed;
            }

            result[key] = projectedValue.ToGameEventScriptValue();
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.Reference(GameEventScriptValueFactory.GesDictionary(result))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineDistinct(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var distinctItems = new List<GameEventScriptValue>();
        var seenKeys = new HashSet<GameEventScriptValue>();
        var hasProjection = terminal.ExpressionEntryAddress >= 0 && terminal.IdentifierSlot >= 0;

        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            GameEventScriptValue key;
            if (hasProjection)
            {
                if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var projectedKey))
                {
                    return PipelineIterationResult.Failed;
                }

                key = projectedKey.ToGameEventScriptValue();
            }
            else
            {
                key = item.ToGameEventScriptValue();
            }

            if (seenKeys.Add(key))
            {
                distinctItems.Add(item.ToGameEventScriptValue());
            }

            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.FromGameEventScriptValue(MaterializeDistinctItems(target, distinctItems))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineOrderBy(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var pairs = new List<(GameEventScriptValue Item, GameEventScriptValue Key)>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var key))
            {
                return PipelineIterationResult.Failed;
            }

            pairs.Add((Item: item.ToGameEventScriptValue(), Key: key.ToGameEventScriptValue()));
            return PipelineIterationResult.Completed;
        });
        if (iterationResult != PipelineIterationResult.Completed)
        {
            value = BytecodeVmValue.Nothing;
            return iterationResult != PipelineIterationResult.Failed;
        }

        var comparer = string.Equals(terminal.EdgeMode, "descending", StringComparison.Ordinal)
            ? Comparer<GameEventScriptValue>.Create((left, right) => GameEventScriptValue.StableComparer.Compare(right, left))
            : GameEventScriptValue.StableComparer;
        var ordered = pairs.OrderBy(pair => pair.Key, comparer).Select(pair => pair.Item).ToArray();
        value = BytecodeVmValue.FromGameEventScriptValue(MaterializeOrderedItems(target, ordered));
        return true;
    }

    private bool TryEvaluateLinearPipelineSequenceSlice(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var items = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            items.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.FromGameEventScriptValue(EvaluateSequenceSliceSelector(
                target,
                items,
                terminal.EdgeMode,
                terminal.SecondaryMode,
                terminal.Count))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineShuffle(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var items = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            items.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.FromGameEventScriptValue(EvaluateShuffleSelector(target, items))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineDraw(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var items = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            items.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.FromGameEventScriptValue(EvaluateDrawSelector(target, items, terminal.Count))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelinePattern(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var items = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            items.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });
        if (iterationResult != PipelineIterationResult.Completed)
        {
            value = BytecodeVmValue.Nothing;
            return iterationResult != PipelineIterationResult.Failed;
        }

        switch (terminal.Kind)
        {
            case GameEventScriptBytecodeSelectorKind.Pattern:
                if (!TryEvaluateLinearSequencePattern(target, items, terminal.DicePatternLayoutIndex, out var patternMatches))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Boolean(patternMatches);
                return true;

            case GameEventScriptBytecodeSelectorKind.ObjectMatch:
                if (!TryEvaluateLinearObjectMatchSelector(target, items, terminal.ObjectMatchPatternLayoutIndex, out var objectMatches))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Boolean(objectMatches);
                return true;

            case GameEventScriptBytecodeSelectorKind.TakePattern:
                if (!TryEvaluateLinearTakePattern(target, items, terminal.DicePatternLayoutIndex, out var result))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.FromGameEventScriptValue(result);
                return true;

            default:
                value = BytecodeVmValue.Nothing;
                return false;
        }
    }

    private bool TryEvaluateLinearSequencePattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        int patternLayoutIndex,
        out bool matches)
    {
        if (!TryGetDicePatternLayout(patternLayoutIndex, out var pattern) ||
            !IsPatternSequence(target))
        {
            matches = false;
            return patternLayoutIndex >= 0;
        }

        var counts = items
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());

        switch (pattern.Kind)
        {
            case GameEventScriptBytecodeDicePatternKind.Count:
                return TryMatchLinearDiceCountPattern(counts, pattern, out matches);

            case GameEventScriptBytecodeDicePatternKind.FullHouse:
                matches = counts.Count == 2 && counts.Values.OrderByDescending(x => x).SequenceEqual(new[] { 3, 2 });
                return true;

            case GameEventScriptBytecodeDicePatternKind.Straight:
                matches = MatchStraight(items);
                return true;

            default:
                matches = false;
                return true;
        }
    }

    private bool TryMatchLinearDiceCountPattern(
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        GameEventScriptBytecodeDicePatternLayout pattern,
        out bool matches)
    {
        if (pattern.FaceEntryAddress >= 0)
        {
            if (!TryEvaluateLinearHelperExpression(pattern.FaceEntryAddress, out var face))
            {
                matches = false;
                return false;
            }

            matches = counts.TryGetValue(face.ToGameEventScriptValue(), out var count) && count >= pattern.Count;
            return true;
        }

        matches = counts.Values.Any(count => count >= pattern.Count);
        return true;
    }

    private bool TryEvaluateLinearTakePattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        int patternLayoutIndex,
        out GameEventScriptValue value)
    {
        if (!TryGetDicePatternLayout(patternLayoutIndex, out var pattern) ||
            !IsPatternSequence(target))
        {
            value = GameEventScriptNothingValue.Instance;
            return patternLayoutIndex >= 0;
        }

        if (!TryTakeLinearSequencePattern(items, pattern, out var takenItems))
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        value = target.Kind == GameEventScriptValueKind.Dice
            ? GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Create(takenItems.Select(item => (int)item.AsInteger())))
            : GameEventScriptValueFactory.GesList(takenItems);
        return true;
    }

    private bool TryTakeLinearSequencePattern(
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeDicePatternLayout pattern,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        var counts = items
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());

        switch (pattern.Kind)
        {
            case GameEventScriptBytecodeDicePatternKind.Count:
                return TryTakeLinearCountPattern(items, counts, pattern, out takenItems);

            case GameEventScriptBytecodeDicePatternKind.FullHouse:
                return TryTakeFullHouse(items, counts, out takenItems);

            case GameEventScriptBytecodeDicePatternKind.Straight:
                return TryTakeStraight(items, out takenItems);

            default:
                takenItems = Array.Empty<GameEventScriptValue>();
                return false;
        }
    }

    private bool TryTakeLinearCountPattern(
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        GameEventScriptBytecodeDicePatternLayout pattern,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        if (pattern.FaceEntryAddress >= 0)
        {
            if (!TryEvaluateLinearHelperExpression(pattern.FaceEntryAddress, out var face))
            {
                takenItems = Array.Empty<GameEventScriptValue>();
                return false;
            }

            var boxedFace = face.ToGameEventScriptValue();
            if (counts.TryGetValue(boxedFace, out var faceCount) && faceCount >= pattern.Count)
            {
                takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int> { [boxedFace] = pattern.Count });
                return true;
            }

            takenItems = Array.Empty<GameEventScriptValue>();
            return false;
        }

        foreach (var candidate in EnumerateDistinctInSourceOrder(items))
        {
            if (counts.TryGetValue(candidate, out var candidateCount) && candidateCount >= pattern.Count)
            {
                takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int> { [candidate] = pattern.Count });
                return true;
            }
        }

        takenItems = Array.Empty<GameEventScriptValue>();
        return false;
    }

    private bool TryEvaluateLinearObjectMatchSelector(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        int patternLayoutIndex,
        out bool matches)
    {
        if (!TryGetObjectMatchPatternLayout(patternLayoutIndex, out _) ||
            target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Set or GameEventScriptValueKind.Dice))
        {
            matches = false;
            return patternLayoutIndex >= 0;
        }

        foreach (var item in items)
        {
            if (!TryMatchesLinearObjectPattern(item, patternLayoutIndex, out var itemMatches))
            {
                matches = false;
                return false;
            }

            if (itemMatches)
            {
                matches = true;
                return true;
            }
        }

        matches = false;
        return true;
    }

    private bool TryMatchesLinearObjectPattern(
        GameEventScriptValue value,
        int patternLayoutIndex,
        out bool matches)
    {
        if (!TryGetObjectMatchPatternLayout(patternLayoutIndex, out var pattern))
        {
            matches = false;
            return false;
        }

        if (value.Kind != GameEventScriptValueKind.Dictionary)
        {
            matches = false;
            return true;
        }

        var dictionary = value.AsDictionary();
        foreach (var entry in pattern.Entries)
        {
            if (!dictionary.TryGetValue(entry.Key, out var actual))
            {
                matches = false;
                return true;
            }

            switch (entry.ValueKind)
            {
                case GameEventScriptBytecodeObjectMatchValueKind.Expression:
                    if (entry.ExpressionEntryAddress < 0 ||
                        !TryEvaluateLinearHelperExpression(entry.ExpressionEntryAddress, out var expected))
                    {
                        matches = false;
                        return false;
                    }

                    if (!GesValueOperations.AreEqual(actual, expected.ToGameEventScriptValue()))
                    {
                        matches = false;
                        return true;
                    }

                    break;

                case GameEventScriptBytecodeObjectMatchValueKind.Nested:
                    if (!TryMatchesLinearObjectPattern(actual, entry.NestedPatternLayoutIndex, out var nestedMatches))
                    {
                        matches = false;
                        return false;
                    }

                    if (!nestedMatches)
                    {
                        matches = false;
                        return true;
                    }

                    break;

                default:
                    matches = false;
                    return false;
            }
        }

        matches = true;
        return true;
    }

    private bool TryEvaluateLinearPipelineChoose(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var candidates = new List<GameEventScriptValue>();
        var hasPredicate = terminal.ExpressionEntryAddress >= 0 && terminal.IdentifierSlot >= 0;
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (hasPredicate)
            {
                if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var predicate))
                {
                    return PipelineIterationResult.Failed;
                }

                if (!predicate.IsTrue())
                {
                    return PipelineIterationResult.Completed;
                }
            }

            candidates.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });
        if (iterationResult != PipelineIterationResult.Completed)
        {
            value = BytecodeVmValue.Nothing;
            return iterationResult != PipelineIterationResult.Failed;
        }

        IReadOnlyList<GameEventScriptValue> chosen;
        if (terminal.SecondaryExpressionEntryAddress >= 0 && terminal.SecondaryIdentifierSlot >= 0)
        {
            if (!TryChooseWeightedLinearPipelineItems(candidates, terminal.Count, terminal, out chosen))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }
        }
        else if (terminal.Flag)
        {
            chosen = ChooseRandomItems(candidates, terminal.Count);
        }
        else
        {
            chosen = candidates.Take(terminal.Count).ToArray();
        }

        value = terminal.Count == 1
            ? chosen.Count == 0
                ? BytecodeVmValue.Nothing
                : BytecodeVmValue.FromGameEventScriptValue(chosen[0])
            : BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(chosen));
        return true;
    }

    private bool TryChooseWeightedLinearPipelineItems(
        IReadOnlyList<GameEventScriptValue> candidates,
        int count,
        GameEventScriptBytecodeSelectorLayout terminal,
        out IReadOnlyList<GameEventScriptValue> chosen)
    {
        var remaining = candidates.ToList();
        var result = new List<GameEventScriptValue>();

        while (result.Count < count && remaining.Count > 0)
        {
            var weightedItems = new List<(GameEventScriptValue Item, double Weight)>();
            double totalWeight = 0d;

            foreach (var candidate in remaining)
            {
                if (!TryEvaluateLinearPipelineSecondarySelectorExpression(
                        terminal,
                        BytecodeVmValue.FromGameEventScriptValue(candidate),
                        out var weightValue))
                {
                    chosen = [];
                    return false;
                }

                var weight = EvaluatePositiveWeight(weightValue.ToGameEventScriptValue());
                if (weight <= 0d)
                {
                    continue;
                }

                weightedItems.Add((candidate, weight));
                totalWeight += weight;
            }

            if (weightedItems.Count == 0 || totalWeight <= 0d)
            {
                break;
            }

            if (!TryNextInclusiveFloat(0d, totalWeight, out var threshold))
            {
                break;
            }

            double cumulative = 0d;
            var selected = weightedItems[^1].Item;
            foreach (var weightedItem in weightedItems)
            {
                cumulative += weightedItem.Weight;
                if (threshold < cumulative)
                {
                    selected = weightedItem.Item;
                    break;
                }
            }

            result.Add(selected);
            remaining.Remove(selected);
        }

        chosen = result;
        return true;
    }

    private IReadOnlyList<GameEventScriptValue> ChooseRandomItems(IReadOnlyList<GameEventScriptValue> candidates, int count)
    {
        if (count <= 0 || candidates.Count == 0)
        {
            return Array.Empty<GameEventScriptValue>();
        }

        var remaining = candidates.ToList();
        var result = new List<GameEventScriptValue>(Math.Min(count, candidates.Count));
        while (result.Count < count && remaining.Count > 0)
        {
            if (!TryNextInclusiveInt(0, remaining.Count - 1, out var selectedIndex))
            {
                break;
            }

            result.Add(remaining[selectedIndex]);
            remaining.RemoveAt(selectedIndex);
        }

        return result;
    }

    private static double EvaluatePositiveWeight(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped) ||
            !GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number) ||
            !number.IsFinite ||
            number.Value <= 0d)
        {
            return 0d;
        }

        return number.Value;
    }

    private bool TryEvaluateLinearPipelineSort(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var items = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            items.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.FromGameEventScriptValue(GesCollectionOperations.Sort(target, items, terminal.EdgeMode ?? "ascending"))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineReverse(
        GameEventScriptValue sourceTarget,
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        out BytecodeVmValue value)
    {
        var target = prefixSelectorIndexes.Count == 0 ? sourceTarget : GameEventScriptListValue.Empty;
        var items = new List<GameEventScriptValue>();
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            items.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.FromGameEventScriptValue(EvaluateReverseSelector(target, items))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private bool TryEvaluateLinearPipelineGroupBy(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        GameEventScriptBytecodeSelectorLayout terminal,
        out BytecodeVmValue value)
    {
        var groups = new Dictionary<string, List<GameEventScriptValue>>(StringComparer.Ordinal);
        var iterationResult = TryForEachLinearPipelineItem(sourceItems, checkRangeItemLimit, prefixSelectorIndexes, item =>
        {
            if (!TryEvaluateLinearPipelineSelectorExpression(terminal, item, out var keyValue))
            {
                return PipelineIterationResult.Failed;
            }

            var key = keyValue.ToGameEventScriptValue().AsText();
            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = [];
                groups[key] = bucket;
            }

            bucket.Add(item.ToGameEventScriptValue());
            return PipelineIterationResult.Completed;
        });

        value = iterationResult == PipelineIterationResult.Completed
            ? BytecodeVmValue.Reference(GameEventScriptValueFactory.GesDictionary(groups.ToDictionary(
                pair => pair.Key,
                pair => GameEventScriptValueFactory.GesList(pair.Value),
                StringComparer.Ordinal)))
            : BytecodeVmValue.Nothing;
        return iterationResult != PipelineIterationResult.Failed;
    }

    private PipelineIterationResult TryForEachLinearPipelineItem(
        IEnumerable<GameEventScriptValue> sourceItems,
        bool checkRangeItemLimit,
        IReadOnlyList<int> prefixSelectorIndexes,
        Func<BytecodeVmValue, PipelineIterationResult> action)
    {
        long sourceItemCount = 0;
        foreach (var sourceItem in sourceItems)
        {
            if (checkRangeItemLimit &&
                !_runtimeBudget.TryCheckRangeLength(++sourceItemCount, PipelineRangeLimitDetail))
            {
                return PipelineIterationResult.RangeLimitReached;
            }

            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItem);
            if (!TryApplyLinearPipelinePrefixes(item, prefixSelectorIndexes, out item, out var include))
            {
                return PipelineIterationResult.Failed;
            }

            if (!include)
            {
                continue;
            }

            var actionResult = action(item);
            if (actionResult != PipelineIterationResult.Completed)
            {
                return actionResult;
            }
        }

        return PipelineIterationResult.Completed;
    }

    private bool TryApplyLinearPipelinePrefixes(
        BytecodeVmValue item,
        IReadOnlyList<int> prefixSelectorIndexes,
        out BytecodeVmValue value,
        out bool include)
    {
        value = item;
        include = true;
        for (var prefixIndex = 0; prefixIndex < prefixSelectorIndexes.Count; prefixIndex++)
        {
            var selectorIndex = prefixSelectorIndexes[prefixIndex];
            if (!TryGetSelectorLayout(selectorIndex, out var selector))
            {
                return false;
            }

            switch (selector.Kind)
            {
                case GameEventScriptBytecodeSelectorKind.Filter:
                    if (!TryEvaluateLinearPipelineSelectorExpression(selector, value, out var predicate))
                    {
                        return false;
                    }

                    if (!predicate.IsTrue())
                    {
                        include = false;
                        return true;
                    }

                    break;

                case GameEventScriptBytecodeSelectorKind.Select:
                    if (!TryEvaluateLinearPipelineSelectorExpression(selector, value, out var selected))
                    {
                        return false;
                    }

                    value = selected;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private bool TryEvaluateLinearPipelineSelectorExpression(
        GameEventScriptBytecodeSelectorLayout selector,
        BytecodeVmValue item,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (selector.ExpressionEntryAddress < 0)
        {
            return false;
        }

        if (TryEvaluateLinearProjectionFast(
                selector.IdentifierSlot,
                selector.ExpressionEntryAddress,
                item,
                out value,
                out var handledFast))
        {
            return true;
        }

        if (handledFast)
        {
            return false;
        }

        return selector.IdentifierSlot >= 0
            ? TryEvaluateLinearHelperExpressionWithTemporarySlot(
                selector.IdentifierSlot,
                item,
                selector.ExpressionEntryAddress,
                out value)
            : TryEvaluateLinearHelperExpression(selector.ExpressionEntryAddress, out value);
    }

    private bool TryEvaluateLinearPipelineSecondarySelectorExpression(
        GameEventScriptBytecodeSelectorLayout selector,
        BytecodeVmValue item,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (selector.SecondaryExpressionEntryAddress < 0)
        {
            return false;
        }

        var identifierSlot = selector.SecondaryIdentifierSlot >= 0
            ? selector.SecondaryIdentifierSlot
            : selector.IdentifierSlot;

        return identifierSlot >= 0
            ? TryEvaluateLinearHelperExpressionWithTemporarySlot(
                identifierSlot,
                item,
                selector.SecondaryExpressionEntryAddress,
                out value)
            : TryEvaluateLinearHelperExpression(selector.SecondaryExpressionEntryAddress, out value);
    }

    private bool TryEvaluateLinearProjectionFast(
        int identifierSlot,
        int entryAddress,
        BytecodeVmValue item,
        out BytecodeVmValue value,
        out bool handled)
    {
        value = BytecodeVmValue.Nothing;
        handled = false;
        if (TryEvaluateLinearProjectionPatternFast(identifierSlot, item, entryAddress, out value, out handled))
        {
            return true;
        }

        if (handled)
        {
            return false;
        }

        if (!CanEvaluateLinearProjectionFast(entryAddress, allowPredicateTest: true))
        {
            return false;
        }

        handled = true;
        return TryEvaluateLinearProjectionFastRange(identifierSlot, item, entryAddress, out value);
    }

    private bool TryEvaluateLinearProjectionPatternFast(
        int identifierSlot,
        BytecodeVmValue item,
        int entryAddress,
        out BytecodeVmValue value,
        out bool handled)
    {
        value = BytecodeVmValue.Nothing;
        handled = false;
        var code = _compiledScript.LinearExecutable.Code;
        var projectionFastKinds = _compiledScript.LinearExecutable.ProjectionFastKinds;
        if ((uint)entryAddress >= (uint)code.Count ||
            (uint)entryAddress >= (uint)projectionFastKinds.Count)
        {
            return false;
        }

        switch (projectionFastKinds[entryAddress])
        {
            case GesBytecodeVmLinearProjectionFastKind.Operand:
                handled = true;
                return TryGetLinearProjectionOperand(code[entryAddress], identifierSlot, item, out value) &&
                       TryConsumeLinearProjectionPatternSteps(2, ref value);

            case GesBytecodeVmLinearProjectionFastKind.PredicateTest:
                if (!CanEvaluateLinearPredicateTestFast(code[entryAddress + 1]))
                {
                    return false;
                }

                handled = true;
                if (!TryGetLinearProjectionOperand(code[entryAddress], identifierSlot, item, out var predicateInput) ||
                    !TryConsumeLinearProjectionPatternSteps(3, ref value))
                {
                    return false;
                }

                return TryEvaluateLinearPredicateTestFast(code[entryAddress + 1], predicateInput, out value);

            case GesBytecodeVmLinearProjectionFastKind.Binary:
                handled = true;
                if (!TryGetLinearProjectionOperand(code[entryAddress], identifierSlot, item, out var left) ||
                    !TryGetLinearProjectionOperand(code[entryAddress + 1], identifierSlot, item, out var right))
                {
                    return false;
                }

                value = EvaluateProjectionBinary(code[entryAddress + 2].OpCode, left, right);
                return TryConsumeLinearProjectionPatternSteps(4, ref value);

            case GesBytecodeVmLinearProjectionFastKind.BinaryThenBinary:
                handled = true;
                if (!TryGetLinearProjectionOperand(code[entryAddress], identifierSlot, item, out var firstLeft) ||
                    !TryGetLinearProjectionOperand(code[entryAddress + 1], identifierSlot, item, out var firstRight) ||
                    !TryGetLinearProjectionOperand(code[entryAddress + 3], identifierSlot, item, out var secondRight))
                {
                    return false;
                }

                var first = EvaluateProjectionBinary(code[entryAddress + 2].OpCode, firstLeft, firstRight);
                value = EvaluateProjectionBinary(code[entryAddress + 4].OpCode, first, secondRight);
                return TryConsumeLinearProjectionPatternSteps(6, ref value);

            case GesBytecodeVmLinearProjectionFastKind.BinaryThenBinaryThenBinary:
                handled = true;
                if (!TryGetLinearProjectionOperand(code[entryAddress], identifierSlot, item, out var chainLeft) ||
                    !TryGetLinearProjectionOperand(code[entryAddress + 1], identifierSlot, item, out var chainRight) ||
                    !TryGetLinearProjectionOperand(code[entryAddress + 3], identifierSlot, item, out var chainMiddle) ||
                    !TryGetLinearProjectionOperand(code[entryAddress + 5], identifierSlot, item, out var chainFinal))
                {
                    return false;
                }

                first = EvaluateProjectionBinary(code[entryAddress + 2].OpCode, chainLeft, chainRight);
                var second = EvaluateProjectionBinary(code[entryAddress + 4].OpCode, first, chainMiddle);
                value = EvaluateProjectionBinary(code[entryAddress + 6].OpCode, second, chainFinal);
                return TryConsumeLinearProjectionPatternSteps(8, ref value);
        }

        return false;
    }

    private bool TryConsumeLinearProjectionPatternSteps(int count, ref BytecodeVmValue value)
    {
        if (TryConsumeExecutionSteps(count, "Expression evaluation budget exhausted."))
        {
            return true;
        }

        value = BytecodeVmValue.Nothing;
        return true;
    }

    private bool TryGetLinearProjectionOperand(
        GameEventScriptBytecodeInstruction instruction,
        int identifierSlot,
        BytecodeVmValue item,
        out BytecodeVmValue value)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.LoadSlot:
                value = instruction.A == identifierSlot
                    ? item
                    : ResolveSlot(instruction.A);
                return true;

            case GameEventScriptBytecodeOpCode.LoadConstant:
                value = LoadConstant(instruction.Data);
                return true;

            default:
                value = BytecodeVmValue.Nothing;
                return false;
        }
    }

    private bool CanEvaluateLinearProjectionFast(int entryAddress, bool allowPredicateTest)
    {
        if (entryAddress < 0)
        {
            return false;
        }

        var cacheKey = entryAddress * 2 + (allowPredicateTest ? 1 : 0);
        if (_linearProjectionFastSupportCache is not null &&
            _linearProjectionFastSupportCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var supported = CanEvaluateLinearProjectionFastRange(entryAddress, allowPredicateTest);
        _linearProjectionFastSupportCache ??= [];
        _linearProjectionFastSupportCache[cacheKey] = supported;
        return supported;
    }

    private bool CanEvaluateLinearProjectionFastRange(int entryAddress, bool allowPredicateTest)
    {
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)entryAddress >= (uint)code.Count)
        {
            return false;
        }

        var temp0 = -1;
        var temp1 = -1;
        var temp2 = -1;
        var temp3 = -1;
        var temp4 = -1;
        var temp5 = -1;
        var temp6 = -1;
        var temp7 = -1;

        for (var pc = entryAddress; pc < code.Count; pc++)
        {
            var instruction = code[pc];
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Nop:
                    break;

                case GameEventScriptBytecodeOpCode.Return:
                    return true;

                case GameEventScriptBytecodeOpCode.LoadSlot:
                case GameEventScriptBytecodeOpCode.LoadConstant:
                case GameEventScriptBytecodeOpCode.Cast:
                case GameEventScriptBytecodeOpCode.CoerceSlot:
                    if (!AddTemp(instruction.Dest))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.PredicateTest:
                    if (!allowPredicateTest ||
                        !CanEvaluateLinearPredicateTestFast(instruction) ||
                        !AddTemp(instruction.Dest))
                    {
                        return false;
                    }

                    break;

                default:
                    if (!IsProjectionBinaryOp(instruction.OpCode) ||
                        !AddTemp(instruction.Dest))
                    {
                        return false;
                    }

                    break;
            }
        }

        return false;

        bool AddTemp(int slot)
        {
            if (slot < 0 ||
                slot == temp0 ||
                slot == temp1 ||
                slot == temp2 ||
                slot == temp3 ||
                slot == temp4 ||
                slot == temp5 ||
                slot == temp6 ||
                slot == temp7)
            {
                return slot >= 0;
            }

            if (temp0 < 0)
            {
                temp0 = slot;
                return true;
            }

            if (temp1 < 0)
            {
                temp1 = slot;
                return true;
            }

            if (temp2 < 0)
            {
                temp2 = slot;
                return true;
            }

            if (temp3 < 0)
            {
                temp3 = slot;
                return true;
            }

            if (temp4 < 0)
            {
                temp4 = slot;
                return true;
            }

            if (temp5 < 0)
            {
                temp5 = slot;
                return true;
            }

            if (temp6 < 0)
            {
                temp6 = slot;
                return true;
            }

            if (temp7 < 0)
            {
                temp7 = slot;
                return true;
            }

            return false;
        }
    }

    private bool CanEvaluateLinearPredicateTestFast(GameEventScriptBytecodeInstruction instruction)
        => TryGetOperationLayout(instruction.Data, out var layout) &&
           !string.IsNullOrEmpty(layout.Name) &&
           _compiledScript.BytecodeModule.Callables.TryGetValue(layout.Name, out var callable) &&
           CanEvaluateLinearPredicateCallableFast(callable);

    private bool CanEvaluateLinearPredicateCallableFast(GameEventScriptBytecodeCallable callable)
    {
        var code = _compiledScript.LinearExecutable.Code;
        var pc = callable.EntryAddress;
        if ((uint)pc >= (uint)code.Count ||
            code[pc].OpCode != GameEventScriptBytecodeOpCode.BindParameter ||
            code[pc].A != 0)
        {
            return false;
        }

        var parameterSlot = code[pc].Dest;
        pc++;
        if ((uint)pc < (uint)code.Count &&
            code[pc].OpCode == GameEventScriptBytecodeOpCode.CoerceSlot &&
            code[pc].A == parameterSlot &&
            code[pc].Dest == parameterSlot)
        {
            pc++;
        }

        return CanEvaluateLinearProjectionFastRange(pc, allowPredicateTest: false);
    }

    private bool TryEvaluateLinearProjectionFastRange(
        int identifierSlot,
        BytecodeVmValue item,
        int entryAddress,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)entryAddress >= (uint)code.Count)
        {
            return false;
        }

        var tempSlot0 = -1;
        var tempSlot1 = -1;
        var tempSlot2 = -1;
        var tempSlot3 = -1;
        var tempSlot4 = -1;
        var tempSlot5 = -1;
        var tempSlot6 = -1;
        var tempSlot7 = -1;
        var tempValue0 = BytecodeVmValue.Nothing;
        var tempValue1 = BytecodeVmValue.Nothing;
        var tempValue2 = BytecodeVmValue.Nothing;
        var tempValue3 = BytecodeVmValue.Nothing;
        var tempValue4 = BytecodeVmValue.Nothing;
        var tempValue5 = BytecodeVmValue.Nothing;
        var tempValue6 = BytecodeVmValue.Nothing;
        var tempValue7 = BytecodeVmValue.Nothing;

        for (var pc = entryAddress; pc < code.Count; pc++)
        {
            if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
            {
                value = BytecodeVmValue.Nothing;
                return true;
            }

            var instruction = code[pc];
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Nop:
                    break;

                case GameEventScriptBytecodeOpCode.Return:
                    value = instruction.A >= 0
                        ? GetSlot(instruction.A)
                        : BytecodeVmValue.Nothing;
                    return true;

                case GameEventScriptBytecodeOpCode.LoadSlot:
                    if (!SetTemp(instruction.Dest, GetSlot(instruction.A)))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.LoadConstant:
                    if (!SetTemp(instruction.Dest, LoadConstant(instruction.Data)))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.Cast:
                    if (!TryGetOperationLayout(instruction.Data, out var castLayout) ||
                        !SetTemp(instruction.Dest, EvaluateProgramCast(castLayout.CastKind, GetSlot(instruction.A))))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.CoerceSlot:
                    if (!TryReadTypeMetadata(instruction.Data, out var typeName) ||
                        !TryConvertDeclaredType(typeName, GetSlot(instruction.A), out var coercedValue) ||
                        !SetTemp(instruction.Dest, coercedValue))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.PredicateTest:
                    if (!TryEvaluateLinearPredicateTestFast(instruction, GetSlot(instruction.A), out var predicateValue) ||
                        !SetTemp(instruction.Dest, predicateValue))
                    {
                        return false;
                    }

                    break;

                default:
                    if (!IsProjectionBinaryOp(instruction.OpCode) ||
                        !SetTemp(instruction.Dest, EvaluateProjectionBinary(instruction.OpCode, GetSlot(instruction.A), GetSlot(instruction.B))))
                    {
                        return false;
                    }

                    break;
            }
        }

        value = BytecodeVmValue.Nothing;
        return false;

        BytecodeVmValue GetSlot(int slot)
        {
            if (slot == identifierSlot)
            {
                return item;
            }

            if (slot == tempSlot0)
            {
                return tempValue0;
            }

            if (slot == tempSlot1)
            {
                return tempValue1;
            }

            if (slot == tempSlot2)
            {
                return tempValue2;
            }

            if (slot == tempSlot3)
            {
                return tempValue3;
            }

            if (slot == tempSlot4)
            {
                return tempValue4;
            }

            if (slot == tempSlot5)
            {
                return tempValue5;
            }

            if (slot == tempSlot6)
            {
                return tempValue6;
            }

            if (slot == tempSlot7)
            {
                return tempValue7;
            }

            return ResolveSlot(slot);
        }

        bool SetTemp(int slot, BytecodeVmValue input)
        {
            if (slot < 0)
            {
                return false;
            }

            if (slot == tempSlot0)
            {
                tempValue0 = input;
                return true;
            }

            if (slot == tempSlot1)
            {
                tempValue1 = input;
                return true;
            }

            if (slot == tempSlot2)
            {
                tempValue2 = input;
                return true;
            }

            if (slot == tempSlot3)
            {
                tempValue3 = input;
                return true;
            }

            if (slot == tempSlot4)
            {
                tempValue4 = input;
                return true;
            }

            if (slot == tempSlot5)
            {
                tempValue5 = input;
                return true;
            }

            if (slot == tempSlot6)
            {
                tempValue6 = input;
                return true;
            }

            if (slot == tempSlot7)
            {
                tempValue7 = input;
                return true;
            }

            if (tempSlot0 < 0)
            {
                tempSlot0 = slot;
                tempValue0 = input;
                return true;
            }

            if (tempSlot1 < 0)
            {
                tempSlot1 = slot;
                tempValue1 = input;
                return true;
            }

            if (tempSlot2 < 0)
            {
                tempSlot2 = slot;
                tempValue2 = input;
                return true;
            }

            if (tempSlot3 < 0)
            {
                tempSlot3 = slot;
                tempValue3 = input;
                return true;
            }

            if (tempSlot4 < 0)
            {
                tempSlot4 = slot;
                tempValue4 = input;
                return true;
            }

            if (tempSlot5 < 0)
            {
                tempSlot5 = slot;
                tempValue5 = input;
                return true;
            }

            if (tempSlot6 < 0)
            {
                tempSlot6 = slot;
                tempValue6 = input;
                return true;
            }

            if (tempSlot7 < 0)
            {
                tempSlot7 = slot;
                tempValue7 = input;
                return true;
            }

            return false;
        }
    }

    private bool TryEvaluateLinearPredicateTestFast(
        GameEventScriptBytecodeInstruction instruction,
        BytecodeVmValue input,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryGetOperationLayout(instruction.Data, out var layout) ||
            string.IsNullOrEmpty(layout.Name) ||
            !_compiledScript.BytecodeModule.Callables.TryGetValue(layout.Name, out var callable))
        {
            return false;
        }

        if (!_runtimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            return NormalizeExtensionPredicateResult(requirePredicateResult: true, ref value);
        }

        try
        {
            var diagnosticInput = input;
            if (!TryConvertParameterType(layout.DeclaredTypes, 0, input, out diagnosticInput))
            {
                return false;
            }

            RecordPredicateCalled(layout.Name, layout.ArgumentName ?? "value", diagnosticInput);
            if (!TryEvaluateLinearPredicateCallableBodyFast(callable, input, out value))
            {
                return false;
            }

            return NormalizeExtensionPredicateResult(requirePredicateResult: true, ref value);
        }
        finally
        {
            _runtimeBudget.ExitCall();
        }
    }

    private bool TryEvaluateLinearPredicateCallableBodyFast(
        GameEventScriptBytecodeCallable callable,
        BytecodeVmValue input,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var code = _compiledScript.LinearExecutable.Code;
        var pc = callable.EntryAddress;
        if ((uint)pc >= (uint)code.Count)
        {
            return false;
        }

        var bind = code[pc];
        if (bind.OpCode != GameEventScriptBytecodeOpCode.BindParameter ||
            bind.A != 0)
        {
            return false;
        }

        if (!TryConsumeExecutionStep("Linear instruction execution budget exhausted."))
        {
            return true;
        }

        var parameterSlot = bind.Dest;
        var parameterValue = input;
        pc++;
        if ((uint)pc < (uint)code.Count &&
            code[pc].OpCode == GameEventScriptBytecodeOpCode.CoerceSlot &&
            code[pc].A == parameterSlot &&
            code[pc].Dest == parameterSlot)
        {
            if (!TryConsumeExecutionStep("Linear instruction execution budget exhausted."))
            {
                return true;
            }

            if (!TryReadTypeMetadata(code[pc].Data, out var typeName) ||
                !TryConvertDeclaredType(typeName, parameterValue, out parameterValue))
            {
                return false;
            }

            pc++;
        }

        return TryEvaluateLinearProjectionFastRange(parameterSlot, parameterValue, pc, out value);
    }

    private bool TryEvaluateLinearGeneratedCollection(int layoutIndex, out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryGetGeneratedCollectionLayout(layoutIndex, out var layout) ||
            layout.ProjectionEntryAddress < 0 ||
            !TryGetLinearIterationSourceItems(layout.IterationSourceLayoutIndex, out var sourceItems))
        {
            return false;
        }

        var values = new List<GameEventScriptValue>();
        foreach (var item in sourceItems)
        {
            if (!_runtimeBudget.TryConsumeLoopIteration("Generated collection iteration budget exhausted."))
            {
                break;
            }

            var fastItem = BytecodeVmValue.FromGameEventScriptValue(item);
            if (layout.PredicateEntryAddress >= 0)
            {
                if (!TryEvaluateLinearHelperExpressionWithTemporarySlot(
                        layout.IdentifierSlot,
                        fastItem,
                        layout.PredicateEntryAddress,
                        out var predicate))
                {
                    return false;
                }

                if (!predicate.IsTrue())
                {
                    continue;
                }
            }

            if (!_runtimeBudget.TryCheckGeneratedCollectionItemCount(values.Count + 1, "Generated collection item count exceeds the configured limit."))
            {
                break;
            }

            if (!TryEvaluateLinearHelperExpressionWithTemporarySlot(
                    layout.IdentifierSlot,
                    fastItem,
                    layout.ProjectionEntryAddress,
                    out var projected))
            {
                return false;
            }

            values.Add(projected.ToGameEventScriptValue());
        }

        value = BytecodeVmValue.Reference(layout.CollectionType == "set"
            ? GameEventScriptValueFactory.GesSet(values)
            : GameEventScriptValueFactory.GesList(values));
        return true;
    }

    private bool TryEvaluateLinearGuardedChoice(int layoutIndex, out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryGetGuardedChoiceLayout(layoutIndex, out var layout) ||
            layout.ConditionEntryAddresses.Count != layout.ValueEntryAddresses.Count)
        {
            return false;
        }

        for (var branchIndex = 0; branchIndex < layout.ConditionEntryAddresses.Count; branchIndex++)
        {
            if (!TryEvaluateLinearHelperExpression(layout.ConditionEntryAddresses[branchIndex], out var condition))
            {
                return false;
            }

            if (condition.IsTrue())
            {
                return TryEvaluateLinearHelperExpression(layout.ValueEntryAddresses[branchIndex], out value);
            }
        }

        return TryEvaluateLinearHelperExpression(layout.OtherwiseEntryAddress, out value);
    }

    private bool TryEvaluateLinearHelperExpression(int entryAddress, out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if ((uint)entryAddress >= (uint)_compiledScript.LinearExecutable.Code.Count)
        {
            return false;
        }

        return TryExecuteLinearRange(
                   entryAddress,
                   _compiledScript.LinearExecutable.Code.Count,
                   null,
                   out var returned,
               out value) &&
           returned;
    }

    private bool TryEvaluateLinearHelperExpressionWithIsolatedTemporaries(
        int entryAddress,
        int temporaryBaseSlot,
        out BytecodeVmValue value)
    {
        if (temporaryBaseSlot < 0 || temporaryBaseSlot >= _locals.Length)
        {
            return TryEvaluateLinearHelperExpression(entryAddress, out value);
        }

        var temporarySlotCount = _locals.Length - temporaryBaseSlot;
        var previousValues = ArrayPool<BytecodeVmValue>.Shared.Rent(temporarySlotCount);
        var previousAssignedSlots = ArrayPool<bool>.Shared.Rent(temporarySlotCount);
        for (var index = 0; index < temporarySlotCount; index++)
        {
            var slot = temporaryBaseSlot + index;
            previousValues[index] = _locals[slot];
            previousAssignedSlots[index] = _assignedSlots[slot];
        }

        var previousSuppressLocalChangeTracking = _suppressLocalChangeTracking;
        _suppressLocalChangeTracking = true;
        try
        {
            return TryEvaluateLinearHelperExpression(entryAddress, out value);
        }
        finally
        {
            _suppressLocalChangeTracking = previousSuppressLocalChangeTracking;
            for (var index = 0; index < temporarySlotCount; index++)
            {
                var slot = temporaryBaseSlot + index;
                _locals[slot] = previousValues[index];
                _assignedSlots[slot] = previousAssignedSlots[index];
            }

            Array.Clear(previousValues, 0, temporarySlotCount);
            ArrayPool<BytecodeVmValue>.Shared.Return(previousValues);
            Array.Clear(previousAssignedSlots, 0, temporarySlotCount);
            ArrayPool<bool>.Shared.Return(previousAssignedSlots);
        }
    }

    private bool CanExecuteLinearEntryCached(int entryAddress)
    {
        if (entryAddress < 0)
        {
            return false;
        }

        if (_compiledScript.TryGetLinearEntrySupport(entryAddress, out var supported))
        {
            return supported;
        }

        supported = CanExecuteLinearEntry(entryAddress, [], allowPipeline: true);
        _compiledScript.SetLinearEntrySupport(entryAddress, supported);
        return supported;
    }

    private bool TryEvaluateLinearHelperExpressionWithTemporarySlot(
        int slot,
        BytecodeVmValue slotValue,
        int entryAddress,
        out BytecodeVmValue value)
    {
        if ((uint)slot >= (uint)_locals.Length)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var hadValue = _assignedSlots[slot];
        var previous = _locals[slot];
        _locals[slot] = slotValue;
        _assignedSlots[slot] = true;
        var success = TryEvaluateLinearHelperExpression(entryAddress, out value);
        RestoreSlot(slot, hadValue, previous);
        return success;
    }

    private bool TryGetLinearIterationSourceItems(int layoutIndex, out IEnumerable<GameEventScriptValue> items)
    {
        if (!TryGetIterationSourceLayout(layoutIndex, out var source))
        {
            items = [];
            return false;
        }

        switch (source.Kind)
        {
            case GameEventScriptBytecodeIterationSourceKind.Collection:
            {
                var boxedCollection = ResolveSlot(source.CollectionSlot).ToGameEventScriptValue();
                if (!TryCheckMaterializedValue(boxedCollection, "Iteration source would enumerate more range items than allowed."))
                {
                    items = [];
                    return true;
                }

                items = boxedCollection.AsEnumerable();
                return true;
            }

            case GameEventScriptBytecodeIterationSourceKind.Range:
            {
                var step = source.RangeStepSlot >= 0
                    ? ResolveSlot(source.RangeStepSlot)
                    : BytecodeVmValue.Integer(1);
                var rangeValue = EvaluateRangeExpression(
                    ResolveSlot(source.RangeFromSlot),
                    ResolveSlot(source.RangeToSlot),
                    step);
                var boxedRange = rangeValue.ToGameEventScriptValue();
                if (!TryCheckMaterializedValue(boxedRange, "Range item count exceeds the configured limit."))
                {
                    items = [];
                    return true;
                }

                items = boxedRange.AsEnumerable();
                return true;
            }

            default:
                items = [];
                return false;
        }
    }

    private BytecodeVmValue[] CopyLinearOperands(IReadOnlyList<int> slots)
    {
        if (slots.Count == 0)
        {
            return [];
        }

        var operands = new BytecodeVmValue[slots.Count];
        for (var index = 0; index < slots.Count; index++)
        {
            operands[index] = ResolveSlot(slots[index]);
        }

        return operands;
    }

    private void CopyLinearOperands(IReadOnlyList<int> slots, BytecodeVmValue[] operands)
    {
        for (var index = 0; index < slots.Count; index++)
        {
            operands[index] = ResolveSlot(slots[index]);
        }
    }

    private static string[] AsArray(IReadOnlyList<string> values)
        => values as string[] ?? values.ToArray();

    private bool TryPublishLinearLayout(int layoutIndex)
    {
        if (!TryGetPublishLayout(layoutIndex, out var layout) ||
            string.IsNullOrEmpty(layout.MessageName) ||
            string.IsNullOrEmpty(layout.SignatureId) ||
            layout.ArgumentNames.Count != layout.ArgumentSlots.Count)
        {
            return false;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[layout.ArgumentNames.Count];
        for (var argumentIndex = 0; argumentIndex < pairs.Length; argumentIndex++)
        {
            var argumentName = layout.ArgumentNames[argumentIndex];
            var value = ResolveSlot(layout.ArgumentSlots[argumentIndex]);
            RecordPublishArgumentEvaluatedToNothing(argumentName, value);
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                argumentName,
                value.ToGameEventScriptValue());
        }

        var message = GameEventScriptMessage.CreatePrecomputed(
            layout.MessageName,
            pairs.Length == 0
                ? GameEventScriptNamedArguments.Empty
                : GameEventScriptNamedArguments.CreateOrdered(pairs),
            layout.SignatureId);
        PublishMessage(layout.Kind, ApplyLinearTags(message, layout.TagSlots));
        return true;
    }

    private bool TryPublishLinearMessageValue(int layoutIndex, BytecodeVmValue messageValue)
    {
        if (!TryGetPublishLayout(layoutIndex, out var layout))
        {
            return false;
        }

        var boxed = messageValue.ToGameEventScriptValue();
        if (!GesMessageValueCodec.TryReadMessageValue(boxed, out var message))
        {
            return true;
        }

        PublishMessage(layout.Kind, ApplyLinearTags(message, layout.TagSlots));
        return true;
    }

    private GameEventScriptMessage ApplyLinearTags(GameEventScriptMessage message, IReadOnlyList<int> tagSlots)
    {
        if (tagSlots.Count == 0)
        {
            return message;
        }

        var builder = new MessageTagBuilder();
        foreach (var tagSlot in tagSlots)
        {
            AddTags(builder, ResolveSlot(tagSlot).ToGameEventScriptValue());
        }

        var tags = builder.ToArray();
        return tags.Count == 0 ? message : message.WithTags(tags);
    }

    private bool TryExecuteLinearRangeFor(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetLoopLayout(instruction.Data, out var loopLayout) ||
            !TryGetIterationSourceLayout(loopLayout.IterationSourceLayoutIndex, out var sourceLayout) ||
            !ResolveSlot(sourceLayout.RangeFromSlot).TryGetRangeInteger(out var from) ||
            !ResolveSlot(sourceLayout.RangeToSlot).TryGetRangeInteger(out var to))
        {
            return false;
        }

        var step = 1L;
        if (sourceLayout.RangeStepSlot >= 0 &&
            !ResolveSlot(sourceLayout.RangeStepSlot).TryGetRangeInteger(out step))
        {
            return false;
        }

        if (step == 0)
        {
            return true;
        }

        var length = GesRuntimeLimitUtilities.GetRangeLength(from, to, step);
        if (!_runtimeBudget.TryCheckRangeLength(length, "For loop range would enumerate more range items than allowed."))
        {
            return true;
        }

        if (step > 0)
        {
            for (var item = from; item <= to; item += step)
            {
                if (!TryExecuteLinearLoopIteration(loopLayout.IdentifierSlot, BytecodeVmValue.Integer(item), instruction.Target, instruction.Target2))
                {
                    return false;
                }

                if (_halted || long.MaxValue - item < step)
                {
                    break;
                }
            }
        }
        else
        {
            for (var item = from; item >= to; item += step)
            {
                if (!TryExecuteLinearLoopIteration(loopLayout.IdentifierSlot, BytecodeVmValue.Integer(item), instruction.Target, instruction.Target2))
                {
                    return false;
                }

                if (_halted || long.MinValue - item > step)
                {
                    break;
                }
            }
        }

        return true;
    }

    private bool TryExecuteLinearCollectionFor(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetLoopLayout(instruction.Data, out var loopLayout) ||
            !TryGetIterationSourceLayout(loopLayout.IterationSourceLayoutIndex, out var sourceLayout))
        {
            return false;
        }

        var sourceValue = ResolveSlot(sourceLayout.CollectionSlot).ToGameEventScriptValue();
        if (GesRuntimeLimitUtilities.TryGetRangeLength(sourceValue, out var length) &&
            !_runtimeBudget.TryCheckRangeLength(length, "Iteration source would enumerate more range items than allowed."))
        {
            return true;
        }

        foreach (var item in sourceValue.AsEnumerable())
        {
            if (!TryExecuteLinearLoopIteration(loopLayout.IdentifierSlot, BytecodeVmValue.FromGameEventScriptValue(item), instruction.Target, instruction.Target2))
            {
                return false;
            }

            if (_halted)
            {
                break;
            }
        }

        return true;
    }

    private bool TryExecuteLinearLoopIteration(int identifierSlot, BytecodeVmValue item, int bodyStart, int bodyEnd)
    {
        if (!_runtimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
        {
            _halted = true;
            return true;
        }

        EnterScope();
        try
        {
            if (!DefineSlot(identifierSlot, item))
            {
                return false;
            }

            return TryExecuteLinearRange(bodyStart, bodyEnd, null, out _, out _);
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryExecuteLinearSeededRandomBlock(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetSeededRandomBlockLayout(instruction.Data, out var layout))
        {
            return false;
        }

        PushSeededRandomScope(ResolveSlot(layout.SeedSlot).ToGameEventScriptValue());
        try
        {
            return TryExecuteLinearRange(instruction.Target, instruction.Target2, null, out _, out _);
        }
        finally
        {
            PopSeededRandomScope();
        }
    }

    private bool TryReadTypeMetadata(int index, out string typeName)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.TypeMetadata.Count)
        {
            typeName = _compiledScript.BytecodeModule.TypeMetadata[index];
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    private bool TryGetOperationLayout(int index, out GameEventScriptBytecodeOperationLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.OperationLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.OperationLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetPublishLayout(int index, out GameEventScriptBytecodePublishLayoutEntry layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.PublishLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.PublishLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetIterationSourceLayout(int index, out GameEventScriptBytecodeIterationSourceLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.IterationSourceLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.IterationSourceLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetLoopLayout(int index, out GameEventScriptBytecodeLoopLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.LoopLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.LoopLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetSeededRandomBlockLayout(int index, out GameEventScriptBytecodeSeededRandomBlockLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.SeededRandomBlockLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.SeededRandomBlockLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetSelectorLayout(int index, out GameEventScriptBytecodeSelectorLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.SelectorLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.SelectorLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetDicePatternLayout(int index, out GameEventScriptBytecodeDicePatternLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.DicePatternLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.DicePatternLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetObjectMatchPatternLayout(int index, out GameEventScriptBytecodeObjectMatchPatternLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.ObjectMatchPatternLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.ObjectMatchPatternLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetPipelineLayout(int index, out GameEventScriptBytecodePipelineLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.PipelineLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.PipelineLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetGeneratedCollectionLayout(int index, out GameEventScriptBytecodeGeneratedCollectionLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.GeneratedCollectionLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.GeneratedCollectionLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private bool TryGetGuardedChoiceLayout(int index, out GameEventScriptBytecodeGuardedChoiceLayout layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.GuardedChoiceLayouts.Count)
        {
            layout = _compiledScript.BytecodeModule.GuardedChoiceLayouts[index];
            return true;
        }

        layout = default!;
        return false;
    }

    private sealed class LinearCallFrame(
        BytecodeVmValue[] locals,
        bool[] assignedSlots,
        List<LocalChange> changes,
        List<int> scopeMarks,
        int trackedLocalSlotCount,
        LinearArgumentSource? arguments,
        int endAddress,
        int returnAddress,
        int returnDestinationSlot,
        int callInstructionAddress,
        bool normalizePredicateResult)
    {
        public BytecodeVmValue[] Locals { get; } = locals;

        public bool[] AssignedSlots { get; } = assignedSlots;

        public List<LocalChange> Changes { get; } = changes;

        public List<int> ScopeMarks { get; } = scopeMarks;

        public int TrackedLocalSlotCount { get; } = trackedLocalSlotCount;

        public LinearArgumentSource? Arguments { get; } = arguments;

        public int EndAddress { get; } = endAddress;

        public int ReturnAddress { get; } = returnAddress;

        public int ReturnDestinationSlot { get; } = returnDestinationSlot;

        public int CallInstructionAddress { get; } = callInstructionAddress;

        public bool NormalizePredicateResult { get; } = normalizePredicateResult;
    }

    private sealed class LinearArgumentSource
    {
        private readonly GesBytecodeVmCompiledHandler? _handler;
        private readonly IReadOnlyDictionary<string, GameEventScriptValue>? _handlerArgs;
        private readonly IReadOnlyList<BytecodeVmValue>? _values;
        private Dictionary<int, int>? _pendingTypedParameterSlots;
        private bool[]? _recordedParameterDiagnostics;
        private int _recordedParameterDiagnosticCount;
        private bool _handlerInvoked;

        private LinearArgumentSource(
            GesBytecodeVmCompiledHandler? handler,
            IReadOnlyDictionary<string, GameEventScriptValue>? handlerArgs,
            IReadOnlyList<BytecodeVmValue>? values)
        {
            _handler = handler;
            _handlerArgs = handlerArgs;
            _values = values;
        }

        public static LinearArgumentSource ForHandler(
            GesBytecodeVmCompiledHandler handler,
            IReadOnlyDictionary<string, GameEventScriptValue> args)
            => new(handler, args, null);

        public static LinearArgumentSource ForValues(IReadOnlyList<BytecodeVmValue> values)
            => new(null, null, values);

        public bool TryGetValue(int index, out BytecodeVmValue value)
        {
            if (_values is not null)
            {
                if ((uint)index < (uint)_values.Count)
                {
                    value = _values[index];
                    return true;
                }

                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (_handler is not null &&
                _handlerArgs is not null &&
                (uint)index < (uint)_handler.Parameters.Count &&
                TryGetArgumentValue(_handlerArgs, _handler.Parameters[index], index, out var handlerValue))
            {
                value = BytecodeVmValue.FromGameEventScriptValue(handlerValue);
                return true;
            }

            value = BytecodeVmValue.Nothing;
            return false;
        }

        public bool TryGetHandlerParameterDiagnostic(int index, out LinearParameterDiagnostic diagnostic)
        {
            if (_handler is not null &&
                _handlerArgs is not null &&
                (uint)index < (uint)_handler.Parameters.Count &&
                TryGetArgumentValue(_handlerArgs, _handler.Parameters[index], index, out var originalValue))
            {
                diagnostic = new LinearParameterDiagnostic(
                    _handler.Parameters[index],
                    originalValue,
                    HasParameterType(_handler.ParameterTypes, index));
                return true;
            }

            diagnostic = default;
            return false;
        }

        public void MarkPendingTypedParameter(int slot, int parameterIndex)
        {
            _pendingTypedParameterSlots ??= [];
            _pendingTypedParameterSlots[slot] = parameterIndex;
        }

        public bool TryTakePendingTypedParameter(int slot, out int parameterIndex)
        {
            if (_pendingTypedParameterSlots is not null &&
                _pendingTypedParameterSlots.Remove(slot, out parameterIndex))
            {
                return true;
            }

            parameterIndex = -1;
            return false;
        }

        public void MarkParameterDiagnosticRecorded(int parameterIndex)
        {
            if (_handler is null ||
                (uint)parameterIndex >= (uint)_handler.Parameters.Count)
            {
                return;
            }

            _recordedParameterDiagnostics ??= new bool[_handler.Parameters.Count];
            if (_recordedParameterDiagnostics[parameterIndex])
            {
                return;
            }

            _recordedParameterDiagnostics[parameterIndex] = true;
            _recordedParameterDiagnosticCount++;
        }

        public bool TryMarkHandlerInvoked(
            out string message,
            out IReadOnlyDictionary<string, GameEventScriptValue> args)
        {
            if (_handler is not null &&
                _handlerArgs is not null &&
                !_handlerInvoked &&
                _recordedParameterDiagnosticCount >= _handler.Parameters.Count)
            {
                _handlerInvoked = true;
                message = _handler.Message;
                args = _handlerArgs;
                return true;
            }

            message = string.Empty;
            args = GameEventScriptNamedArguments.Empty;
            return false;
        }
    }

    private readonly record struct LinearParameterDiagnostic(
        string Name,
        GameEventScriptValue OriginalValue,
        bool HasDeclaredType);

    private static bool TryGetArgumentValue(IReadOnlyDictionary<string, GameEventScriptValue> args, string parameter, int parameterIndex, out GameEventScriptValue value)
    {
        if (args is GameEventScriptNamedArguments namedArguments && parameterIndex >= 0 && parameterIndex < namedArguments.Count)
        {
            value = namedArguments[parameterIndex];
            return true;
        }

        return args.TryGetValue(parameter, out value!);
    }

    internal void PublishMessage(GameEventScriptBytecodePublishKind publishKind, GameEventScriptMessage message)
    {
        if (publishKind == GameEventScriptBytecodePublishKind.Publish)
        {
            _context.Publish(message);
        }
        else
        {
            _context.Emit(message);
        }
    }

    internal static void AddTags(MessageTagBuilder builder, GameEventScriptValue value)
    {
        if (value.IsNothing())
        {
            return;
        }

        if (value.IsList() || value.IsSet() || value.IsSequence())
        {
            foreach (var item in value.AsEnumerable())
            {
                AddTags(builder, item);
            }

            return;
        }

        builder.Add(value.AsText());
    }

    private BytecodeVmValue LoadConstant(int index)
        => (uint)index < (uint)_compiledScript.Constants.Count
            ? _compiledScript.Constants[index]
            : BytecodeVmValue.Nothing;

    private static BytecodeVmValue EvaluateMemberAccess(BytecodeVmValue target, string? member)
    {
        if (string.IsNullOrEmpty(member))
        {
            return BytecodeVmValue.Nothing;
        }

        var targetValue = target.ToGameEventScriptValue();
        if (targetValue.IsNothing())
        {
            return BytecodeVmValue.Nothing;
        }

        return targetValue.TryGetDictionaryMember(member, out var value)
            ? BytecodeVmValue.FromGameEventScriptValue(value)
            : BytecodeVmValue.Nothing;
    }

    private static BytecodeVmValue EvaluateIndexedAccess(BytecodeVmValue target, BytecodeVmValue selector)
    {
        var selectorValue = selector.ToGameEventScriptValue();
        if (selectorValue.IsNothing())
        {
            return BytecodeVmValue.Nothing;
        }

        return BytecodeVmValue.FromGameEventScriptValue(target.ToGameEventScriptValue().Lookup(selectorValue));
    }

    private static BytecodeVmValue BuildListValue(BytecodeVmValue[] stack, int start, int count)
    {
        var items = new GameEventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(items));
    }

    private static BytecodeVmValue BuildSequenceValue(BytecodeVmValue[] stack, int start, int count)
    {
        var items = new GameEventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesSequence(items));
    }

    private static BytecodeVmValue BuildSetValue(BytecodeVmValue[] stack, int start, int count)
    {
        var items = new GameEventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesSet(items));
    }

    private static BytecodeVmValue BuildDictionaryValue(BytecodeVmValue[] stack, int start, int count, string[]? names)
    {
        if (names is null || names.Length != count)
        {
            return BytecodeVmValue.Nothing;
        }

        var map = new Dictionary<string, GameEventScriptValue>(count, StringComparer.Ordinal);
        for (var entryIndex = 0; entryIndex < count; entryIndex++)
        {
            map[names[entryIndex]] = stack[start + entryIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesDictionary(map));
    }

    private static BytecodeVmValue BuildMessageValue(
        BytecodeVmValue[] stack,
        int start,
        int count,
        string[]? names,
        string? messageName,
        string? signatureId)
    {
        if (string.IsNullOrEmpty(messageName) ||
            string.IsNullOrEmpty(signatureId) ||
            names is null ||
            names.Length != count)
        {
            return BytecodeVmValue.Nothing;
        }

        if (count == 0)
        {
            return BytecodeVmValue.Reference(GesMessage(GameEventScriptMessage.CreatePrecomputed(
                messageName,
                GameEventScriptNamedArguments.Empty,
                signatureId)));
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                names[argumentIndex],
                stack[start + argumentIndex].ToGameEventScriptValue());
        }

        return BytecodeVmValue.Reference(GesMessage(GameEventScriptMessage.CreatePrecomputed(
            messageName,
            GameEventScriptNamedArguments.CreateOrdered(pairs),
            signatureId)));
    }

    private static BytecodeVmValue BindHandlerValue(
        BytecodeVmValue callee,
        BytecodeVmValue[] stack,
        int start,
        int count,
        string[]? names)
    {
        if (names is null || names.Length != count)
        {
            return BytecodeVmValue.Nothing;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                names[argumentIndex],
                stack[start + argumentIndex].ToGameEventScriptValue());
        }

        var arguments = GameEventScriptNamedArguments.CreateOrdered(pairs, names);
        return GesMessageValueCodec.TryBindHandlerValue(callee.ToGameEventScriptValue(), arguments, out var message)
            ? BytecodeVmValue.Reference(GesMessageValueCodec.CreateMessageValue(message))
            : BytecodeVmValue.Nothing;
    }

    private bool TryCallExtension(
        string? extensionName,
        string? functionName,
        string[]? labels,
        int referenceIndex,
        BytecodeVmValue[] stack,
        int start,
        int count,
        bool requirePredicateResult,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (string.IsNullOrWhiteSpace(extensionName) ||
            string.IsNullOrWhiteSpace(functionName))
        {
            return true;
        }

        var argumentLabels = labels is { Length: var labelCount } && labelCount == count
            ? labels
            : Enumerable.Repeat(GameEventScriptMessageSignature.UnlabeledParameterName, count).ToArray();
        var reference = new GameEventScriptExtensionReference(extensionName, functionName, argumentLabels);
        var arguments = new GameEventScriptFastValue[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            arguments[argumentIndex] = ToGameEventScriptFastValue(stack[start + argumentIndex]);
        }

        if (GesStandardExtensions.TryInvoke(reference, arguments, out var standardValue))
        {
            value = BytecodeVmValue.FromGameEventScriptFastValue(standardValue);
            return NormalizeExtensionPredicateResult(requirePredicateResult, ref value);
        }

        IGameEventScriptExtensionFunction function;
        if (referenceIndex >= 0)
        {
            if (!_compiledScript.TryGetBoundExtension(referenceIndex, out function))
            {
                throw new GameEventScriptDynamicLinkException($"GameEventScript extension '{reference.SignatureId}' was not dynamically bound to reference slot '{referenceIndex}'.");
            }
        }
        else
        {
            if (!_compiledScript.TryGetBoundExtension(reference, out function))
            {
                throw new GameEventScriptDynamicLinkException($"GameEventScript extension '{reference.SignatureId}' was not dynamically bound.");
            }
        }

        value = BytecodeVmValue.FromGameEventScriptFastValue(function.Invoke(new GameEventScriptExtensionContext(_context), arguments));
        return NormalizeExtensionPredicateResult(requirePredicateResult, ref value);
    }

    private static bool NormalizeExtensionPredicateResult(bool requirePredicateResult, ref BytecodeVmValue value)
    {
        if (!requirePredicateResult ||
            value.Kind == BytecodeVmValueKind.Boolean ||
            value.IsNothingLike())
        {
            return true;
        }

        value = BytecodeVmValue.Nothing;
        return true;
    }

    private static GameEventScriptFastValue ToGameEventScriptFastValue(BytecodeVmValue value)
        => value.Kind switch
        {
            BytecodeVmValueKind.Nothing => GameEventScriptFastValue.Nothing,
            BytecodeVmValueKind.Boolean => GameEventScriptFastValue.FromBoolean(value.BooleanValue),
            BytecodeVmValueKind.Integer => GameEventScriptFastValue.FromInteger(value.IntegerValue, value.Unit),
            BytecodeVmValueKind.Float => GameEventScriptFastValue.FromFloat(value.Number, value.Unit),
            BytecodeVmValueKind.Percentage => GameEventScriptFastValue.FromPercentage(value.Number),
            BytecodeVmValueKind.Reference => GameEventScriptFastValue.FromGameEventScriptValue(value.ReferenceValue ?? GameEventScriptNothingValue.Instance),
            _ => GameEventScriptFastValue.Nothing
        };

    private BytecodeVmValue EvaluateProgramBinary(GameEventScriptBytecodeOpCode opCode, in BytecodeVmValue left, in BytecodeVmValue right)
    {
        switch (opCode)
        {
            case GameEventScriptBytecodeOpCode.Or:
                return EvaluateLogicalOr(left, right);
            case GameEventScriptBytecodeOpCode.Xor:
                return EvaluateLogicalXor(left, right);
            case GameEventScriptBytecodeOpCode.And:
                return EvaluateLogicalAnd(left, right);
            case GameEventScriptBytecodeOpCode.ShortCircuitImplies:
                return EvaluateLogicalImplies(left, right);
        }

        if (opCode != GameEventScriptBytecodeOpCode.Default &&
            (left.IsNothingLike() || right.IsNothingLike()))
        {
            return BytecodeVmValue.Nothing;
        }

        switch (opCode)
        {
            case GameEventScriptBytecodeOpCode.Power:
                return BytecodeVmValue.Power(left, right);
            case GameEventScriptBytecodeOpCode.Equal:
                return BytecodeVmValue.Boolean(BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.NotEqual:
                return BytecodeVmValue.Boolean(!BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.ApproxEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.AreApproximatelyEqual(left, right));
            case GameEventScriptBytecodeOpCode.Less:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var lessComparison) && lessComparison < 0);
            case GameEventScriptBytecodeOpCode.Greater:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var greaterComparison) && greaterComparison > 0);
            case GameEventScriptBytecodeOpCode.LessOrEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var lessOrEqualComparison) && lessOrEqualComparison <= 0);
            case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var greaterOrEqualComparison) && greaterOrEqualComparison >= 0);
            case GameEventScriptBytecodeOpCode.Add:
                return BytecodeVmValue.Add(left, right);
            case GameEventScriptBytecodeOpCode.Subtract:
                return BytecodeVmValue.Subtract(left, right);
            case GameEventScriptBytecodeOpCode.Multiply:
                return BytecodeVmValue.Multiply(left, right);
            case GameEventScriptBytecodeOpCode.Divide:
                return BytecodeVmValue.Divide(left, right);
            case GameEventScriptBytecodeOpCode.IntegerDivide:
                return BytecodeVmValue.IntegerDivide(left, right);
            case GameEventScriptBytecodeOpCode.Modulo:
                return BytecodeVmValue.Modulo(left, right);
            case GameEventScriptBytecodeOpCode.Remainder:
                return BytecodeVmValue.Remainder(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var equalComparison)
                    ? BytecodeVmValue.Boolean(equalComparison == 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var notEqualComparison)
                    ? BytecodeVmValue.Boolean(notEqualComparison != 0)
                    : BytecodeVmValue.Boolean(!BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLess:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerLessComparison)
                    ? BytecodeVmValue.Boolean(integerLessComparison < 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveLessComparison) && primitiveLessComparison < 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerGreaterComparison)
                    ? BytecodeVmValue.Boolean(integerGreaterComparison > 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveGreaterComparison) && primitiveGreaterComparison > 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerLessOrEqualComparison)
                    ? BytecodeVmValue.Boolean(integerLessOrEqualComparison <= 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveLessOrEqualComparison) && primitiveLessOrEqualComparison <= 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerGreaterOrEqualComparison)
                    ? BytecodeVmValue.Boolean(integerGreaterOrEqualComparison >= 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveGreaterOrEqualComparison) && primitiveGreaterOrEqualComparison >= 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd:
                return BytecodeVmValue.TryPrimitiveIntegerAdd(left, right, out var integerAdd) ? integerAdd : BytecodeVmValue.Add(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract:
                return BytecodeVmValue.TryPrimitiveIntegerSubtract(left, right, out var integerSubtract) ? integerSubtract : BytecodeVmValue.Subtract(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply:
                return BytecodeVmValue.TryPrimitiveIntegerMultiply(left, right, out var integerMultiply) ? integerMultiply : BytecodeVmValue.Multiply(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide:
                return BytecodeVmValue.TryPrimitiveIntegerDivide(left, right, out var integerDivide) ? integerDivide : BytecodeVmValue.Divide(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide:
                return BytecodeVmValue.TryPrimitiveIntegerFloorDivide(left, right, out var integerFloorDivide) ? integerFloorDivide : BytecodeVmValue.IntegerDivide(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo:
                return BytecodeVmValue.TryPrimitiveIntegerModulo(left, right, out var integerModulo) ? integerModulo : BytecodeVmValue.Modulo(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder:
                return BytecodeVmValue.TryPrimitiveIntegerRemainder(left, right, out var integerRemainder) ? integerRemainder : BytecodeVmValue.Remainder(left, right);
            default:
                return TryEvaluateBinaryOperation(GetBinaryOperator(opCode), left, right, out var value)
                    ? value
                    : throw new InvalidOperationException($"BytecodeVM invariant failed: binary opcode '{opCode}' could not be evaluated.");
        }
    }

    private BytecodeVmValue EvaluateProjectionBinary(GameEventScriptBytecodeOpCode opCode, in BytecodeVmValue left, in BytecodeVmValue right)
        => EvaluateProgramBinary(opCode, left, right);

    private static bool IsProjectionBinaryOp(GameEventScriptBytecodeOpCode opCode)
        => opCode is
            GameEventScriptBytecodeOpCode.Default or
            GameEventScriptBytecodeOpCode.Add or
            GameEventScriptBytecodeOpCode.Subtract or
            GameEventScriptBytecodeOpCode.Multiply or
            GameEventScriptBytecodeOpCode.Divide or
            GameEventScriptBytecodeOpCode.IntegerDivide or
            GameEventScriptBytecodeOpCode.Modulo or
            GameEventScriptBytecodeOpCode.Remainder or
            GameEventScriptBytecodeOpCode.Power or
            GameEventScriptBytecodeOpCode.Equal or
            GameEventScriptBytecodeOpCode.NotEqual or
            GameEventScriptBytecodeOpCode.ApproxEqual or
            GameEventScriptBytecodeOpCode.Less or
            GameEventScriptBytecodeOpCode.Greater or
            GameEventScriptBytecodeOpCode.LessOrEqual or
            GameEventScriptBytecodeOpCode.GreaterOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLess or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder;

    private static BytecodeVmValue EvaluateLogicalAnd(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (left.IsFalse() || right.IsFalse())
        {
            return BytecodeVmValue.Boolean(false);
        }

        return left.IsNothing() || right.IsNothing()
            ? BytecodeVmValue.Nothing
            : BytecodeVmValue.Boolean(true);
    }

    private static BytecodeVmValue EvaluateLogicalOr(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (left.IsTrue() || right.IsTrue())
        {
            return BytecodeVmValue.Boolean(true);
        }

        return left.IsNothing() || right.IsNothing()
            ? BytecodeVmValue.Nothing
            : BytecodeVmValue.Boolean(false);
    }

    private static BytecodeVmValue EvaluateLogicalXor(in BytecodeVmValue left, in BytecodeVmValue right)
        => left.IsNothing() || right.IsNothing()
            ? BytecodeVmValue.Nothing
            : BytecodeVmValue.Boolean(left.IsTrue() ^ right.IsTrue());

    private static BytecodeVmValue EvaluateLogicalImplies(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (left.IsFalse() || right.IsTrue())
        {
            return BytecodeVmValue.Boolean(true);
        }

        if (left.IsNothing() || right.IsNothing())
        {
            return BytecodeVmValue.Nothing;
        }

        return BytecodeVmValue.Boolean(false);
    }

    private BytecodeVmValue EvaluateProgramCast(GameEventScriptBytecodeCastKind castKind, BytecodeVmValue input)
        => TryConvertDeclaredType(GetCastTypeName(castKind), input, out var value)
            ? value
            : throw new InvalidOperationException($"BytecodeVM invariant failed: cast '{castKind}' could not be evaluated.");

    private static bool IsValueOfType(BytecodeVmValue value, string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        return typeName switch
        {
            "nothing" => value.Kind == BytecodeVmValueKind.Nothing || (value.ReferenceValue?.IsNothing() ?? false),
            "tag" => value.ReferenceValue?.IsTag() ?? false,
            "text" => value.ReferenceValue?.IsText() ?? false,
            "percentage" => value.Kind == BytecodeVmValueKind.Percentage || (value.ReferenceValue?.IsPercentage() ?? false),
            "degree" => (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float) && value.Unit == GameEventScriptNumericUnit.Degree ||
                        (value.ReferenceValue?.IsNumericUnit(GameEventScriptNumericUnit.Degree) ?? false),
            "meter" => (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float) && value.Unit == GameEventScriptNumericUnit.Meter ||
                       (value.ReferenceValue?.IsNumericUnit(GameEventScriptNumericUnit.Meter) ?? false),
            "second" => (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float) && value.Unit == GameEventScriptNumericUnit.Second ||
                        (value.ReferenceValue?.IsNumericUnit(GameEventScriptNumericUnit.Second) ?? false),
            "vector" => value.ReferenceValue?.IsVector() ?? false,
            "point" => value.ReferenceValue?.IsPoint() ?? false,
            "float" => value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float or BytecodeVmValueKind.Percentage ||
                       (value.ReferenceValue?.IsNumber() ?? false),
            "integer" => value.Kind == BytecodeVmValueKind.Integer || (value.ReferenceValue?.IsInteger() ?? false),
            "boolean" => value.Kind == BytecodeVmValueKind.Boolean || value.ReferenceValue?.Kind == GameEventScriptValueKind.Boolean,
            "uuid" => value.ReferenceValue?.IsUuid() ?? false,
            "optional" => value.ReferenceValue?.IsOptional() ?? false,
            "sequence" => value.ReferenceValue?.IsSequence() ?? false,
            "series" => value.ReferenceValue?.IsSeries() ?? false,
            "envelope" => value.ReferenceValue is { } envelopeValue &&
                          envelopeValue.TryGetCustomTypeName(out var envelopeTypeName) &&
                          string.Equals(envelopeTypeName, GameEventScriptSystemEndpoints.EnvelopeTypeName, StringComparison.Ordinal),
            "list" => value.ReferenceValue?.IsList() ?? false,
            "range" => value.ReferenceValue?.IsRange() ?? false,
            "message" => value.ReferenceValue is { } messageValue &&
                         (messageValue.Kind == GameEventScriptValueKind.Message || GesMessageValueCodec.TryReadMessageValue(messageValue, out _)),
            "handler" => value.ReferenceValue is { } handlerValue &&
                         (handlerValue.Kind == GameEventScriptValueKind.Handler || GesMessageValueCodec.TryReadHandlerValue(handlerValue, out _)),
            "ref" => value.ReferenceValue?.IsRef() ?? false,
            "dictionary" => value.ReferenceValue?.IsDictionary() ?? false,
            "set" => value.ReferenceValue?.IsSet() ?? false,
            "dice" => value.ReferenceValue?.IsDice() ?? false,
            _ => value.ReferenceValue is { } customValue &&
                 customValue.TryGetCustomTypeName(out var customTypeName) &&
                 string.Equals(customTypeName, typeName, StringComparison.Ordinal)
        };
    }

    private BytecodeVmValue EvaluateRandomExpression(BytecodeVmValue fromValue, BytecodeVmValue toValue)
    {
        var fromRaw = fromValue.ToGameEventScriptValue();
        var toRaw = toValue.ToGameEventScriptValue();

        if (GesValueOperations.TryUnwrapOptionalForOperation(fromRaw, out var unwrappedFrom) &&
            GesValueOperations.TryUnwrapOptionalForOperation(toRaw, out var unwrappedTo) &&
            unwrappedFrom.Kind == GameEventScriptValueKind.Integer &&
            unwrappedTo.Kind == GameEventScriptValueKind.Integer)
        {
            var from = unwrappedFrom.AsInteger();
            var to = unwrappedTo.AsInteger();
            if (from > to)
            {
                (from, to) = (to, from);
            }

            return TryNextInclusiveInteger(from, to, out var next)
                ? BytecodeVmValue.Integer(next)
                : BytecodeVmValue.Nothing;
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(fromRaw, out var fromNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(toRaw, out var toNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        var lower = Math.Min(fromNumber.Value, toNumber.Value);
        var upper = Math.Max(fromNumber.Value, toNumber.Value);
        if (lower == upper)
        {
            return BytecodeVmValue.Float(lower);
        }

        return TryNextInclusiveFloat(lower, upper, out var nextFloat)
            ? BytecodeVmValue.Float(nextFloat)
            : BytecodeVmValue.Nothing;
    }

    private static BytecodeVmValue EvaluateRangeExpression(BytecodeVmValue fromValue, BytecodeVmValue toValue, BytecodeVmValue stepValue)
    {
        if (!fromValue.TryGetRangeInteger(out var from) ||
            !toValue.TryGetRangeInteger(out var to) ||
            !stepValue.TryGetRangeInteger(out var step))
        {
            return BytecodeVmValue.Nothing;
        }

        return BytecodeVmValue.Reference(GesRange(from, to, step));
    }

    private BytecodeVmValue EvaluateDiceExpression(int diceCount, int sideCount)
    {
        if (diceCount <= 0 || sideCount <= 0)
        {
            return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Empty));
        }

        if (!_runtimeBudget.TryCheckDice(diceCount, sideCount))
        {
            return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Empty));
        }

        var rolls = new int[diceCount];
        for (var i = 0; i < rolls.Length; i++)
        {
            if (!TryNextInclusiveInt(1, sideCount, out var roll))
            {
                return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Empty));
            }

            rolls[i] = roll;
        }

        return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Create(rolls)));
    }

    private bool TryEvaluateUnaryOperation(string? operation, BytecodeVmValue operand, out BytecodeVmValue value)
    {
        var boxed = operand.ToGameEventScriptValue();
        value = operation switch
        {
            "-" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNegateUnary(boxed)),
            "!" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNotUnary(boxed)),
            "has value" => BytecodeVmValue.Boolean(boxed.HasSemanticValue()),
            "empty" => BytecodeVmValue.Boolean(boxed.IsSemanticallyEmpty()),
            "len" => BytecodeVmValue.FromGameEventScriptValue(EvaluateLenUnary(boxed)),
            "chance" => BytecodeVmValue.FromGameEventScriptValue(EvaluateChanceUnary(boxed)),
            "keys" => BytecodeVmValue.Reference(GesKeys(boxed)),
            "values" => BytecodeVmValue.Reference(GesValues(boxed)),
            "entries" => BytecodeVmValue.Reference(GesEntries(boxed)),
            "abs" => BytecodeVmValue.FromGameEventScriptValue(EvaluateAbsUnary(boxed)),
            "ln" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNaturalLogUnary(boxed)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown unary operator '{operation}'.")
        };

        return true;
    }

    private bool TryEvaluateVariadicOperation(
        string? operation,
        BytecodeVmValue[] stack,
        int start,
        int count,
        out BytecodeVmValue value)
    {
        if (count == 0)
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        var boxedValues = new GameEventScriptValue[count];
        for (var i = 0; i < count; i++)
        {
            boxedValues[i] = stack[start + i].ToGameEventScriptValue();
        }

        value = operation switch
        {
            "min" => BytecodeVmValue.FromGameEventScriptValue(GesValueOperations.EvaluateMinMax(boxedValues, isMax: false)),
            "max" => BytecodeVmValue.FromGameEventScriptValue(GesValueOperations.EvaluateMinMax(boxedValues, isMax: true)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown variadic operator '{operation}'.")
        };

        return true;
    }

    private bool TryEvaluateBinaryOperation(string operation, BytecodeVmValue leftRawValue, BytecodeVmValue rightRawValue, out BytecodeVmValue value)
    {
        var leftRaw = leftRawValue.ToGameEventScriptValue();
        var rightRaw = rightRawValue.ToGameEventScriptValue();

        if (operation == "default")
        {
            value = BytecodeVmValue.FromGameEventScriptValue(EvaluateDefaultBinary(leftRaw, rightRaw));
            return true;
        }

        if (operation is "|" or "xor" or "&" or "->")
        {
            value = operation switch
            {
                "|" => EvaluateLogicalOr(leftRawValue, rightRawValue),
                "xor" => EvaluateLogicalXor(leftRawValue, rightRawValue),
                "&" => EvaluateLogicalAnd(leftRawValue, rightRawValue),
                "->" => EvaluateLogicalImplies(leftRawValue, rightRawValue),
                _ => BytecodeVmValue.Nothing
            };
            return true;
        }

        if (leftRaw.IsNothing() || rightRaw.IsNothing())
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        if (!GesValueOperations.TryUnwrapOptionalForOperation(leftRaw, out var left) ||
            !GesValueOperations.TryUnwrapOptionalForOperation(rightRaw, out var right))
        {
            value = BytecodeVmValue.Reference(GesOptionalNone());
            return true;
        }

        value = operation switch
        {
            "=" or "==" => BytecodeVmValue.Boolean(GesValueOperations.AreEqual(left, right)),
            "<>" => BytecodeVmValue.Boolean(!GesValueOperations.AreEqual(left, right)),
            "=~" => BytecodeVmValue.Boolean(GesValueOperations.AreApproximatelyEqual(left, right)),
            "in" => BytecodeVmValue.Boolean(right.Contains(left)),
            "value in" => BytecodeVmValue.Boolean(right.ContainsValue(left)),
            "starts with" => BytecodeVmValue.Boolean(left.StartsWith(right)),
            "ends with" => BytecodeVmValue.Boolean(left.EndsWith(right)),
            "<" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison < 0)),
            ">" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison > 0)),
            "<=" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison <= 0)),
            ">=" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison >= 0)),
            "+" => BytecodeVmValue.FromGameEventScriptValue(EvaluateAddBinary(left, right)),
            "-" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "-", right)),
            "*" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "*", right)),
            "/" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "/", right)),
            "div" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "div", right)),
            "mod" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "mod", right)),
            "rem" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "rem", right)),
            "^" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "^", right)),
            "intersect" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionIntersect(left, right)),
            "combine" or "merge" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionCombine(left, right)),
            "except" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionExcept(left, right)),
            "zip" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionZip(left, right)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown binary operator '{operation}'.")
        };

        return true;
    }

    private static string GetBinaryOperator(GameEventScriptBytecodeOpCode opCode)
        => opCode switch
        {
            GameEventScriptBytecodeOpCode.Or => "|",
            GameEventScriptBytecodeOpCode.Xor => "xor",
            GameEventScriptBytecodeOpCode.And => "&",
            GameEventScriptBytecodeOpCode.ShortCircuitImplies => "->",
            GameEventScriptBytecodeOpCode.Power => "^",
            GameEventScriptBytecodeOpCode.Equal => "=",
            GameEventScriptBytecodeOpCode.NotEqual => "<>",
            GameEventScriptBytecodeOpCode.ApproxEqual => "=~",
            GameEventScriptBytecodeOpCode.Less => "<",
            GameEventScriptBytecodeOpCode.Greater => ">",
            GameEventScriptBytecodeOpCode.LessOrEqual => "<=",
            GameEventScriptBytecodeOpCode.GreaterOrEqual => ">=",
            GameEventScriptBytecodeOpCode.Add => "+",
            GameEventScriptBytecodeOpCode.Subtract => "-",
            GameEventScriptBytecodeOpCode.Multiply => "*",
            GameEventScriptBytecodeOpCode.Divide => "/",
            GameEventScriptBytecodeOpCode.IntegerDivide => "div",
            GameEventScriptBytecodeOpCode.Modulo => "mod",
            GameEventScriptBytecodeOpCode.Remainder => "rem",
            GameEventScriptBytecodeOpCode.Default => "default",
            GameEventScriptBytecodeOpCode.Contains => "in",
            GameEventScriptBytecodeOpCode.ContainsValue => "value in",
            GameEventScriptBytecodeOpCode.StartsWith => "starts with",
            GameEventScriptBytecodeOpCode.EndsWith => "ends with",
            GameEventScriptBytecodeOpCode.Intersect => "intersect",
            GameEventScriptBytecodeOpCode.Combine => "combine",
            GameEventScriptBytecodeOpCode.Except => "except",
            GameEventScriptBytecodeOpCode.Zip => "zip",
            _ => string.Empty
        };

    private static GameEventScriptValue EvaluateDefaultBinary(GameEventScriptValue leftRaw, GameEventScriptValue rightRaw)
    {
        if (!leftRaw.HasSemanticValue())
        {
            return rightRaw;
        }

        if (leftRaw.IsOptional())
        {
            var optional = leftRaw.AsOptional();
            return optional.HasValue ? optional.Value : rightRaw;
        }

        return leftRaw;
    }

    private static bool TryCompare(GameEventScriptValue left, GameEventScriptValue right, Func<int, bool> predicate)
        => GesValueOperations.TryCompareNumericValues(left, right, out var comparison) &&
           predicate(comparison);

    private static GameEventScriptValue EvaluateAddBinary(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.IsUuid() || right.IsUuid())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (GesValueOperations.TryEvaluatePointBinary(left, "+", right, out var point))
        {
            return point;
        }

        if (GesValueOperations.TryEvaluateVectorBinary(left, "+", right, out var vector))
        {
            return vector;
        }

        if (GesValueOperations.TryEvaluateIntegerBinary(left, "+", right, out var integer))
        {
            return integer;
        }

        if (GesValueOperations.TryEvaluatePercentageBinary(left, "+", right, out var percentage))
        {
            return percentage;
        }

        if (GesValueOperations.TryEvaluateUnitBinary(left, "+", right, out var unit))
        {
            return unit;
        }

        if (GesValueOperations.TryCoerceNumericForOperation(left, out var leftNumeric) &&
            GesValueOperations.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return GesValueOperations.ToGameEventScriptNumericResult(left, "+", right, GesValueOperations.AddNumeric(leftNumeric, rightNumeric));
        }

        if (GesValueOperations.TryCombineWithPlus(left, right, out var combined))
        {
            return combined;
        }

        return left.IsText() || right.IsText()
            ? GesText($"{GesValueOperations.ToText(left)}{GesValueOperations.ToText(right)}")
            : GesFloatNaN();
    }

    private static GameEventScriptValue EvaluateNumericBinary(GameEventScriptValue left, string operation, GameEventScriptValue right)
    {
        if (left.IsUuid() || right.IsUuid())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (GesValueOperations.TryEvaluatePointBinary(left, operation, right, out var point))
        {
            return point;
        }

        if (GesValueOperations.TryEvaluateVectorBinary(left, operation, right, out var vector))
        {
            return vector;
        }

        if (GesValueOperations.TryEvaluateIntegerBinary(left, operation, right, out var integer))
        {
            return integer;
        }

        if (GesValueOperations.TryEvaluatePercentageBinary(left, operation, right, out var percentage))
        {
            return percentage;
        }

        if (GesValueOperations.TryEvaluateUnitBinary(left, operation, right, out var unit))
        {
            return unit;
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(left, out var leftNumeric) ||
            !GesValueOperations.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return GesFloatNaN();
        }

        var result = operation switch
        {
            "-" => GesValueOperations.SubtractNumeric(leftNumeric, rightNumeric),
            "*" => GesValueOperations.MultiplyNumeric(leftNumeric, rightNumeric),
            "/" => GesValueOperations.DivideNumeric(leftNumeric, rightNumeric),
            "div" => GesValueOperations.IntegerDivideNumeric(leftNumeric, rightNumeric),
            "mod" => GesValueOperations.ModuloNumeric(leftNumeric, rightNumeric),
            "rem" => GesValueOperations.RemainderNumeric(leftNumeric, rightNumeric),
            "^" => GesValueOperations.PowerNumeric(leftNumeric, rightNumeric),
            _ => GesValueOperations.NumericValue.NaN()
        };
        return GesValueOperations.ToGameEventScriptNumericResult(left, operation, right, result);
    }

    private static GameEventScriptValue EvaluateNegateUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (!GesValueOperations.TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return GesOptionalNone();
        }

        if (unwrapped.IsPercentage())
        {
            return GesPercentage(-unwrapped.AsNumber());
        }

        if (GesValueOperations.TryEvaluatePointUnary(unwrapped, "-", out var pointNegation))
        {
            return pointNegation;
        }

        if (GesValueOperations.TryEvaluateVectorUnary(unwrapped, "-", out var vectorNegation))
        {
            return vectorNegation;
        }

        if (unwrapped is GameEventScriptIntegerValue { Value: not long.MinValue } integer)
        {
            return GesInteger(-integer.Value, integer.Unit);
        }

        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out var unit))
        {
            return GesFloat(-unwrapped.AsNumber(), unit);
        }

        return GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number)
            ? GesValueOperations.ToGameEventScriptFloat(GesValueOperations.NegateNumeric(number))
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue EvaluateNotUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GesValueOperations.TryUnwrapOptionalForOperation(operand, out var unwrapped)
            ? GesBoolean(!unwrapped.AsBoolean())
            : GesOptionalNone();
    }

    private GameEventScriptValue EvaluateLenUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GesInteger(0);
        }

        return operand.Kind switch
        {
            GameEventScriptValueKind.Text => GesInteger(operand.AsText().Length),
            GameEventScriptValueKind.Sequence => CountEnumerableWithBudget(operand.AsEnumerable(), "Sequence length evaluation budget exhausted."),
            GameEventScriptValueKind.Range => EvaluateRangeLength(operand),
            GameEventScriptValueKind.List => GesInteger(operand.AsList().Count),
            GameEventScriptValueKind.Dictionary => GesInteger(operand.AsDictionary().Count),
            GameEventScriptValueKind.Set => GesInteger(operand.AsSet().Count),
            GameEventScriptValueKind.Dice => GesInteger(operand.AsDice().Rolls.Count),
            GameEventScriptValueKind.Optional => GesInteger(operand.AsOptional().HasValue ? 1 : 0),
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private GameEventScriptValue EvaluateRangeLength(GameEventScriptValue operand)
    {
        if (!GesRuntimeLimitUtilities.TryGetRangeLength(operand, out var length))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return _runtimeBudget.TryCheckRangeLength(length, "Range length exceeds the configured limit.")
            ? GesInteger(length)
            : GameEventScriptNothingValue.Instance;
    }

    private GameEventScriptValue CountEnumerableWithBudget(IEnumerable<GameEventScriptValue> values, string detail)
    {
        long count = 0;
        foreach (var _ in values)
        {
            if (!_runtimeBudget.TryConsumeLoopIteration(detail))
            {
                return GameEventScriptNothingValue.Instance;
            }

            count++;
        }

        return GesInteger(count);
    }

    private GameEventScriptValue EvaluateChanceUnary(GameEventScriptValue operand)
    {
        var percentage = ConvertToPercentage(operand);
        if (!percentage.IsPercentage())
        {
            return GesBoolean(false);
        }

        var ratio = percentage.AsNumber();
        if (ratio <= 0d)
        {
            return GesBoolean(false);
        }

        if (ratio >= 1d)
        {
            return GesBoolean(true);
        }

        return TryNextInclusiveFloat(0d, 1d, out var randomValue)
            ? GesBoolean(randomValue < ratio)
            : GesBoolean(false);
    }

    private static GameEventScriptValue EvaluateAbsUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (GesValueOperations.TryEvaluatePointUnary(operand, "abs", out var pointAbs))
        {
            return pointAbs;
        }

        if (GesValueOperations.TryEvaluateVectorUnary(operand, "abs", out var vectorLength))
        {
            return vectorLength;
        }

        return GesValueOperations.TryCoerceNumericForOperation(operand, out var number) && number.IsFinite
            ? GesFloat(Math.Abs(number.Value))
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue EvaluateNaturalLogUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (!GesValueOperations.TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return GesOptionalNone();
        }

        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out _))
        {
            return GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number))
        {
            return GesFloatNaN();
        }

        if (number.IsNaN || number.IsNegativeInfinity)
        {
            return GesFloatNaN();
        }

        if (number.IsPositiveInfinity)
        {
            return GesFloatInfinity();
        }

        if (number.Value < 0d)
        {
            return GesFloatNaN();
        }

        if (number.Value == 0d)
        {
            return GesFloatNegativeInfinity();
        }

        var result = Math.Log((double)number.Value);
        if (double.IsNaN(result))
        {
            return GesFloatNaN();
        }

        if (double.IsPositiveInfinity(result))
        {
            return GesFloatInfinity();
        }

        if (double.IsNegativeInfinity(result))
        {
            return GesFloatNegativeInfinity();
        }

        try
        {
            return GesFloat((double)result);
        }
        catch (OverflowException)
        {
            return result < 0d ? GesFloatNegativeInfinity() : GesFloatInfinity();
        }
    }

    private static BytecodeVmValue EvaluateClamp(BytecodeVmValue rawValue, BytecodeVmValue minimumValue, BytecodeVmValue maximumValue)
    {
        var raw = rawValue.ToGameEventScriptValue();
        var minimum = minimumValue.ToGameEventScriptValue();
        var maximum = maximumValue.ToGameEventScriptValue();

        if (!GesValueOperations.HaveCompatibleNumericUnits(raw, minimum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(raw, maximum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return BytecodeVmValue.NaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(raw, out var rawNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(maximum, out var maximumNumber) ||
            !rawNumber.IsFinite ||
            !minimumNumber.IsFinite ||
            !maximumNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        GameEventScriptValue.TryGetNumericUnit(raw, out var unit);
        return BytecodeVmValue.FromGameEventScriptValue(GesFloat(
            Math.Min(Math.Max(rawNumber.Value, lower), upper),
            raw.HasNumericUnit() ? unit : null));
    }

    private bool TryNextInclusiveFloat(double minInclusive, double maxInclusive, out double value)
    {
        try
        {
            value = _randomScopes.Peek().NextInclusiveFloat(minInclusive, maxInclusive);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private bool TryNextInclusiveInt(int minInclusive, int maxInclusive, out int value)
    {
        try
        {
            value = _randomScopes.Peek().NextInclusiveInt(minInclusive, maxInclusive);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private bool TryNextInclusiveInteger(long minInclusive, long maxInclusive, out long value)
    {
        try
        {
            value = _randomScopes.Peek().NextInclusiveInteger(minInclusive, maxInclusive);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private void PushSeededRandomScope(GameEventScriptValue seedValue) => _randomScopes.Push(GameEventScriptRandomGenerator.FromSeed(DeriveStableSeed(seedValue)));

    private void PopSeededRandomScope()
    {
        if (_randomScopes.Count > 1)
        {
            _randomScopes.Pop();
        }
    }

    private static int ToIntSaturated(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return (int)value;
    }

    private static int DeriveStableSeed(GameEventScriptValue value)
    {
        var canonical = BuildStableSeedText(value);
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in canonical)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }

    private static string BuildStableSeedText(GameEventScriptValue value)
        => value.Kind switch
        {
            GameEventScriptValueKind.Nothing => "nothing",
            GameEventScriptValueKind.Tag => $"tag:{value.AsText()}",
            GameEventScriptValueKind.Text => $"text:{value.AsText()}",
            GameEventScriptValueKind.Percentage => $"percentage:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
            GameEventScriptValueKind.Vector => BuildVectorStableSeedText((GameEventScriptVectorValue)value),
            GameEventScriptValueKind.Point => BuildPointStableSeedText((GameEventScriptPointValue)value),
            GameEventScriptValueKind.Float => value.IsNaN()
                ? "float:nan"
                : value.IsNegativeInfinity()
                    ? "float:-infinity"
                    : value.IsInfinity()
                        ? "float:infinity"
                        : value is GameEventScriptFloatValue { Unit: { } unit }
                            ? $"float:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}:{unit.ToTypeName()}"
                            : $"float:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
            GameEventScriptValueKind.Integer => $"integer:{value.AsInteger().ToString(CultureInfo.InvariantCulture)}",
            GameEventScriptValueKind.Boolean => $"boolean:{(value.AsBoolean() ? "true" : "false")}",
            GameEventScriptValueKind.Uuid => $"uuid:{value.AsText()}",
            GameEventScriptValueKind.Optional => value.AsOptional().HasValue
                ? $"optional:{BuildStableSeedText(value.AsOptional().Value)}"
                : "optional:none",
            GameEventScriptValueKind.Sequence => $"sequence:[{string.Join("|", value.AsEnumerable().Select(BuildStableSeedText))}]",
            GameEventScriptValueKind.Series => $"series:{((GameEventScriptSeriesValue)value).SignatureId}:{((GameEventScriptSeriesValue)value).Offset}",
            GameEventScriptValueKind.Range => $"range:{((GameEventScriptRangeValue)value).From}:{((GameEventScriptRangeValue)value).To}:{((GameEventScriptRangeValue)value).Step}",
            GameEventScriptValueKind.Message =>
                $"message:{((GameEventScriptMessageValue)value).Value.SignatureId}:[{string.Join("|", ((GameEventScriptMessageValue)value).Value.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
            GameEventScriptValueKind.Handler => $"handler:{((GameEventScriptHandlerValue)value).Signature.SignatureId}",
            GameEventScriptValueKind.Ref => $"ref:{((GameEventScriptRefValue)value).TypeName}:{((GameEventScriptRefValue)value).Id}",
            GameEventScriptValueKind.List => $"list:[{string.Join("|", value.AsList().Select(BuildStableSeedText))}]",
            GameEventScriptValueKind.Dictionary =>
                $"dict:[{string.Join("|", value.AsDictionary().OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
            GameEventScriptValueKind.Set => $"set:[{string.Join("|", value.AsSet().OrderBy(item => item, GameEventScriptValue.StableComparer).Select(BuildStableSeedText))}]",
            GameEventScriptValueKind.Dice => $"dice:[{string.Join("|", value.AsDice().Rolls)}]",
            _ => value.ToString()
        };

    private static string BuildVectorStableSeedText(GameEventScriptVectorValue value)
        => value.Unit.HasValue
            ? $"vector:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}:{value.Unit.Value.ToTypeName()}"
            : $"vector:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}";

    private static string BuildPointStableSeedText(GameEventScriptPointValue value)
        => value.Unit.HasValue
            ? $"point:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}:{value.Unit.Value.ToTypeName()}"
            : $"point:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}";

    private BytecodeVmValue EvaluateTypeConstructor(
        string? typeName,
        string[]? labels,
        BytecodeVmValue[] stack,
        int start,
        int count)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return BytecodeVmValue.Nothing;
        }

        if (typeName is "vector" or "point")
        {
            return EvaluateSpatialConstructor(typeName, labels, stack, start, count);
        }

        if (typeName == "ref")
        {
            return EvaluateRefConstructor(labels, stack, start, count);
        }

        if (TryEvaluateExternalTypeConstructor(typeName, labels, stack, start, count, out var externalValue))
        {
            return externalValue;
        }

        if (_compiledScript.TypeDefinitions.TryGetValue(typeName, out var typeDefinition))
        {
            var values = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var label = labels is { Length: var labelCount } && index < labelCount
                    ? labels[index]
                    : GameEventScriptMessageSignature.UnlabeledParameterName;
                if (string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
                {
                    return BytecodeVmValue.Nothing;
                }

                values[label] = stack[start + index].ToGameEventScriptValue();
            }

            return BytecodeVmValue.FromGameEventScriptValue(ConvertToCustomType(GesDictionary(values), typeDefinition));
        }

        if (count != 1 || labels is not { Length: > 0 } || !string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return BytecodeVmValue.Nothing;
        }

        return TryConvertDeclaredType(typeName, stack[start], out var converted)
            ? converted
            : BytecodeVmValue.Nothing;
    }

    private BytecodeVmValue EvaluateRefConstructor(
        string[]? labels,
        BytecodeVmValue[] stack,
        int start,
        int count)
    {
        if (count == 1 &&
            labels is { Length: > 0 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return TryConvertDeclaredType("ref", stack[start], out var converted)
                ? converted
                : BytecodeVmValue.Nothing;
        }

        if (count != 2 || labels is null || labels.Length < count)
        {
            return BytecodeVmValue.Nothing;
        }

        if (!TryGetRefConstructorArgumentIndexes(labels, count, out var typeIndex, out var idIndex))
        {
            return BytecodeVmValue.Nothing;
        }

        if (!TryReadRefTypeName(stack[start + typeIndex].ToGameEventScriptValue(), out var targetTypeName) ||
            !IsReferenceTargetType(targetTypeName))
        {
            return BytecodeVmValue.Nothing;
        }

        var idValue = NormalizeRefIdValue(stack[start + idIndex].ToGameEventScriptValue());
        return idValue.IsNothing()
            ? BytecodeVmValue.Nothing
            : BytecodeVmValue.Reference(GesRef(targetTypeName, idValue));
    }

    private static GameEventScriptValue NormalizeRefIdValue(GameEventScriptValue id)
    {
        if (!id.TryUnwrapOptional(out var unwrapped) || unwrapped.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (unwrapped.IsUuid())
        {
            return unwrapped;
        }

        var text = GesValueOperations.ToText(unwrapped).Trim();
        return text.Length == 0
            ? GameEventScriptNothingValue.Instance
            : GesText(text);
    }

    private bool IsReferenceTargetType(string typeName)
        => _compiledScript.TypeDefinitions.ContainsKey(typeName) ||
           _compiledScript.ExternalTypeRegistry.Types.ContainsKey(typeName);

    private static bool TryGetRefConstructorArgumentIndexes(
        IReadOnlyList<string> labels,
        int count,
        out int typeIndex,
        out int idIndex)
    {
        typeIndex = -1;
        idIndex = -1;
        for (var index = 0; index < count; index++)
        {
            var label = labels[index];
            if (string.Equals(label, "type", StringComparison.Ordinal))
            {
                if (typeIndex >= 0)
                {
                    return false;
                }

                typeIndex = index;
                continue;
            }

            if (string.Equals(label, "id", StringComparison.Ordinal))
            {
                if (idIndex >= 0)
                {
                    return false;
                }

                idIndex = index;
                continue;
            }

            if (string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                if (typeIndex < 0)
                {
                    typeIndex = index;
                    continue;
                }

                if (idIndex < 0)
                {
                    idIndex = index;
                    continue;
                }
            }

            return false;
        }

        return typeIndex >= 0 && idIndex >= 0 && typeIndex != idIndex;
    }

    private static bool TryReadRefTypeName(GameEventScriptValue value, out string typeName)
    {
        typeName = value.Kind switch
        {
            GameEventScriptValueKind.Tag => value.AsText(),
            GameEventScriptValueKind.Text => value.AsText(),
            _ => string.Empty
        };

        typeName = NormalizeRefTargetTypeName(typeName);
        return typeName.Length > 0;
    }

    private static string NormalizeRefTargetTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        var normalized = typeName.Trim();
        return normalized.StartsWith(":", StringComparison.Ordinal)
            ? normalized[1..]
            : normalized;
    }

    private bool TryEvaluateExternalTypeConstructor(
        string typeName,
        string[]? labels,
        BytecodeVmValue[] stack,
        int start,
        int count,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (labels is null || labels.Length < count || HasUnlabeledConstructorArgument(labels, count) ||
            !_compiledScript.TryGetBoundExternalTypeConstructor(typeName, labels, out var constructor))
        {
            return false;
        }

        if (constructor.Definition.Parameters.Count != count)
        {
            return true;
        }

        var arguments = new GameEventScriptValue[count];
        for (var parameterIndex = 0; parameterIndex < constructor.Definition.Parameters.Count; parameterIndex++)
        {
            var parameter = constructor.Definition.Parameters[parameterIndex];
            var argumentIndex = IndexOf(labels, parameter.Name, count);
            if (argumentIndex < 0 ||
                string.Equals(labels[argumentIndex], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) ||
                !TryConvertDeclaredType(parameter.TypeName, stack[start + argumentIndex], out var converted))
            {
                return true;
            }

            arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(
                converted.ToGameEventScriptValue(),
                parameter);
        }

        value = BytecodeVmValue.FromGameEventScriptValue(constructor.Invoke(arguments));
        return true;
    }

    private static bool HasUnlabeledConstructorArgument(IReadOnlyList<string> labels, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(labels[index], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(IReadOnlyList<string> labels, string label, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(labels[index], label, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private BytecodeVmValue EvaluateSpatialConstructor(
        string typeName,
        string[]? labels,
        BytecodeVmValue[] stack,
        int start,
        int count)
    {
        if (count == 1 &&
            labels is { Length: > 0 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return TryConvertDeclaredType(typeName, stack[start], out var converted)
                ? converted
                : BytecodeVmValue.Nothing;
        }

        if (count == 2 &&
            labels is { Length: >= 2 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) &&
            string.Equals(labels[1], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) &&
            TryCreateSpatialLift(
                typeName,
                stack[start].ToGameEventScriptValue(),
                stack[start + 1].ToGameEventScriptValue(),
                out var lifted))
        {
            return BytecodeVmValue.FromGameEventScriptValue(lifted);
        }

        if (count == 0 || labels is { Length: var labelCount } &&
            labelCount >= count &&
            Enumerable.Range(0, count).All(index => !string.Equals(labels[index], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal)))
        {
            var labeledComponents = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                labeledComponents[labels![index]] = stack[start + index].ToGameEventScriptValue();
            }

            return TryCreateSpatialFromLabeledComponents(typeName, labeledComponents, out var spatialValue)
                ? BytecodeVmValue.FromGameEventScriptValue(spatialValue)
                : BytecodeVmValue.Nothing;
        }

        if (count is < 1 or > 3)
        {
            return BytecodeVmValue.Nothing;
        }

        var components = new GameEventScriptValue[count];
        for (var index = 0; index < components.Length; index++)
        {
            components[index] = stack[start + index].ToGameEventScriptValue();
        }

        return TryCreateSpatialFromComponents(typeName, components, out var spatial)
            ? BytecodeVmValue.FromGameEventScriptValue(spatial)
            : BytecodeVmValue.Nothing;
    }

    private static bool TryCreateSpatialFromComponents(string typeName, IReadOnlyList<GameEventScriptValue> components, out GameEventScriptValue value)
        => typeName == "point"
            ? GesValueOperations.TryCreatePointFromComponents(components, out value)
            : GesValueOperations.TryCreateVectorFromComponents(components, out value);

    private static bool TryCreateSpatialLift(string typeName, GameEventScriptValue xy, GameEventScriptValue z, out GameEventScriptValue value)
        => typeName == "point"
            ? GesValueOperations.TryCreatePoint(xy, z, out value)
            : GesValueOperations.TryCreateVector(xy, z, out value);

    private static bool TryCreateSpatialFromLabeledComponents(string typeName, IReadOnlyDictionary<string, GameEventScriptValue> components, out GameEventScriptValue value)
        => typeName == "point"
            ? GesValueOperations.TryCreatePointFromLabeledComponents(typeName, components, out value)
            : GesValueOperations.TryCreateVectorFromLabeledComponents(typeName, components, out value);

    private bool TryConvertDeclaredType(string declaredType, BytecodeVmValue input, out BytecodeVmValue value)
    {
        if (input.ReferenceValue?.IsUuid() == true &&
            declaredType is not "uuid" and not "text" and not "optional")
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        if (TryConvertPrimitiveDeclaredType(declaredType, input, out value))
        {
            return true;
        }

        var boxed = input.ToGameEventScriptValue();
        value = declaredType switch
        {
            "tag" => BytecodeVmValue.Reference(GesTag(boxed.AsText())),
            "text" => BytecodeVmValue.Reference(GesText(GesValueOperations.ToText(boxed))),
            "percentage" => BytecodeVmValue.FromGameEventScriptValue(ConvertToPercentage(boxed)),
            "degree" => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(boxed, GameEventScriptNumericUnit.Degree)),
            "meter" => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(boxed, GameEventScriptNumericUnit.Meter)),
            "second" => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(boxed, GameEventScriptNumericUnit.Second)),
            "vector" => BytecodeVmValue.Reference(ConvertToVector(boxed)),
            "point" => BytecodeVmValue.Reference(ConvertToPoint(boxed)),
            "integer" => BytecodeVmValue.Integer(boxed.AsInteger()),
            "float" or "number" => BytecodeVmValue.FromGameEventScriptValue(ConvertToFloat(boxed)),
            "uuid" => BytecodeVmValue.FromGameEventScriptValue(ConvertToUuid(boxed)),
            "sequence" => BytecodeVmValue.Reference(boxed.IsSequence() ? boxed : GameEventScriptValueFactory.GesSequence(boxed.AsEnumerable())),
            "series" => boxed.IsSeries()
                ? input
                : BytecodeVmValue.Nothing,
            "envelope" => boxed.TryGetCustomTypeName(out var envelopeTypeName) &&
                          string.Equals(envelopeTypeName, GameEventScriptSystemEndpoints.EnvelopeTypeName, StringComparison.Ordinal)
                ? input
                : BytecodeVmValue.Nothing,
            "list" => TryCheckMaterializedValue(boxed, "List conversion would materialize more range items than allowed.")
                ? BytecodeVmValue.Reference(GesList(boxed.AsList()))
                : BytecodeVmValue.Nothing,
            "range" => BytecodeVmValue.Reference(boxed.IsRange() ? boxed : GameEventScriptNothingValue.Instance),
            "message" => boxed.Kind == GameEventScriptValueKind.Message
                ? input
                : GesMessageValueCodec.TryReadMessageValue(boxed, out var message)
                    ? BytecodeVmValue.Reference(GesMessageValueCodec.CreateMessageValue(message))
                    : BytecodeVmValue.Nothing,
            "handler" => boxed.Kind == GameEventScriptValueKind.Handler
                ? input
                : GesMessageValueCodec.TryReadHandlerValue(boxed, out var handler)
                    ? BytecodeVmValue.Reference(GesMessageValueCodec.CreateHandlerValue(handler))
                    : BytecodeVmValue.Nothing,
            "ref" => boxed.IsRef()
                ? input
                : BytecodeVmValue.Nothing,
            "dictionary" => BytecodeVmValue.Reference(GesDictionary(boxed.AsDictionary())),
            "set" => TryCheckMaterializedValue(boxed, "Set conversion would materialize more range items than allowed.")
                ? BytecodeVmValue.Reference(GesSet(boxed.AsSet()))
                : BytecodeVmValue.Nothing,
            "dice" => BytecodeVmValue.Reference(GesDice(boxed.AsDice())),
            "optional" => boxed.IsOptional()
                ? input
                : boxed.IsNothing()
                    ? BytecodeVmValue.Reference(GesOptionalNone())
                    : BytecodeVmValue.Reference(GesOptionalSome(boxed)),
            _ => _compiledScript.TypeDefinitions.TryGetValue(declaredType, out var typeDefinition)
                ? BytecodeVmValue.FromGameEventScriptValue(ConvertToCustomType(boxed, typeDefinition))
                : boxed.TryGetCustomTypeName(out var customTypeName) &&
                  string.Equals(customTypeName, declaredType, StringComparison.Ordinal)
                    ? input
                    : BytecodeVmValue.Nothing
        };

        return true;
    }

    private static bool TryConvertPrimitiveDeclaredType(string declaredType, BytecodeVmValue input, out BytecodeVmValue value)
    {
        switch (declaredType)
        {
            case "nothing":
                value = BytecodeVmValue.Nothing;
                return true;
            case "boolean":
                value = BytecodeVmValue.Boolean(input.AsBoolean());
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Integer:
                value = input.Unit.HasValue ? BytecodeVmValue.Integer(input.IntegerValue) : input;
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Boolean:
                value = BytecodeVmValue.Integer(input.BooleanValue ? 1 : 0);
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Float:
                value = BytecodeVmValue.Integer(ToLongSaturated(input.Number));
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Percentage:
                value = BytecodeVmValue.Integer(GameEventScriptValue.ToIntegerPercentage(input.Number));
                return true;
            case "float":
            case "number":
                return TryConvertPrimitiveToFloat(input, out value);
            case "percentage":
                return TryConvertPrimitiveToPercentage(input, out value);
            case "degree":
                return TryConvertPrimitiveToNumericUnit(input, GameEventScriptNumericUnit.Degree, out value);
            case "meter":
                return TryConvertPrimitiveToNumericUnit(input, GameEventScriptNumericUnit.Meter, out value);
            case "second":
                return TryConvertPrimitiveToNumericUnit(input, GameEventScriptNumericUnit.Second, out value);
            default:
                value = input;
                return false;
        }
    }

    private static bool TryConvertPrimitiveToFloat(BytecodeVmValue input, out BytecodeVmValue value)
    {
        value = input.Kind switch
        {
            BytecodeVmValueKind.Integer => BytecodeVmValue.Float(input.IntegerValue),
            BytecodeVmValueKind.Boolean => BytecodeVmValue.Float(input.BooleanValue ? 1d : 0d),
            BytecodeVmValueKind.Float => input.Unit.HasValue ? BytecodeVmValue.Float(input.Number) : input,
            BytecodeVmValueKind.Percentage => BytecodeVmValue.Float(input.Number),
            _ => input
        };

        return input.Kind is BytecodeVmValueKind.Integer
            or BytecodeVmValueKind.Boolean
            or BytecodeVmValueKind.Float
            or BytecodeVmValueKind.Percentage;
    }

    private static bool TryConvertPrimitiveToPercentage(BytecodeVmValue input, out BytecodeVmValue value)
    {
        switch (input.Kind)
        {
            case BytecodeVmValueKind.Percentage:
                value = input;
                return true;
            case BytecodeVmValueKind.Integer:
                if (input.Unit.HasValue)
                {
                    value = BytecodeVmValue.NaN();
                    return true;
                }

                value = BytecodeVmValue.Percentage(input.IntegerValue / 100d);
                return true;
            case BytecodeVmValueKind.Boolean:
                value = BytecodeVmValue.Percentage(input.BooleanValue ? 1d : 0d);
                return true;
            case BytecodeVmValueKind.Float when input.Unit.HasValue:
                value = BytecodeVmValue.NaN();
                return true;
            case BytecodeVmValueKind.Float:
                var number = input.Number;
                value = BytecodeVmValue.Percentage(number > 1d || number < -1d ? number / 100d : number);
                return true;
            default:
                value = input;
                return false;
        }
    }

    private static bool TryConvertPrimitiveToNumericUnit(BytecodeVmValue input, GameEventScriptNumericUnit unit, out BytecodeVmValue value)
    {
        switch (input.Kind)
        {
            case BytecodeVmValueKind.Integer:
                value = input.Unit.HasValue && input.Unit != unit
                    ? BytecodeVmValue.NaN()
                    : BytecodeVmValue.Integer(input.IntegerValue, unit);
                return true;
            case BytecodeVmValueKind.Float when input.Unit.HasValue && input.Unit != unit:
                value = BytecodeVmValue.NaN();
                return true;
            case BytecodeVmValueKind.Float:
                value = BytecodeVmValue.Float(input.Number, unit);
                return true;
            default:
                value = input;
                return false;
        }
    }

    private bool TryConvertParameterType(IReadOnlyList<string?>? declaredTypes, int index, BytecodeVmValue input, out BytecodeVmValue value)
    {
        value = input;
        if (!TryGetParameterType(declaredTypes, index, out var declaredType))
        {
            return true;
        }

        return TryConvertDeclaredType(declaredType, input, out value);
    }

    private static bool HasParameterType(IReadOnlyList<string?>? declaredTypes, int index)
        => TryGetParameterType(declaredTypes, index, out _);

    private static bool TryGetParameterType(IReadOnlyList<string?>? declaredTypes, int index, out string declaredType)
    {
        declaredType = string.Empty;
        if (declaredTypes is null ||
            index < 0 ||
            index >= declaredTypes.Count ||
            string.IsNullOrEmpty(declaredTypes[index]))
        {
            return false;
        }

        declaredType = declaredTypes[index]!;
        return true;
    }

    private bool TryCheckMaterializedValue(GameEventScriptValue value, string detail)
        => !GesRuntimeLimitUtilities.TryGetRangeLength(value, out var length) ||
           _runtimeBudget.TryCheckRangeLength(length, detail);

    private static string GetCastTypeName(GameEventScriptBytecodeCastKind castKind)
        => castKind switch
        {
            GameEventScriptBytecodeCastKind.Boolean => "boolean",
            GameEventScriptBytecodeCastKind.Integer => "integer",
            GameEventScriptBytecodeCastKind.Float => "float",
            GameEventScriptBytecodeCastKind.Number => "number",
            GameEventScriptBytecodeCastKind.Percentage => "percentage",
            GameEventScriptBytecodeCastKind.Degree => "degree",
            GameEventScriptBytecodeCastKind.Meter => "meter",
            GameEventScriptBytecodeCastKind.Second => "second",
            GameEventScriptBytecodeCastKind.Vector => "vector",
            GameEventScriptBytecodeCastKind.Point => "point",
            GameEventScriptBytecodeCastKind.Uuid => "uuid",
            GameEventScriptBytecodeCastKind.Sequence => "sequence",
            GameEventScriptBytecodeCastKind.Series => "series",
            GameEventScriptBytecodeCastKind.Envelope => "envelope",
            GameEventScriptBytecodeCastKind.Ref => "ref",
            _ => string.Empty
        };

    private static GameEventScriptValue ConvertToFloat(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrappedNumber))
        {
            return GesFloatNaN();
        }

        if (GesValueOperations.TryEraseVectorUnit(unwrappedNumber, out var vectorWithoutUnit))
        {
            return vectorWithoutUnit;
        }

        if (unwrappedNumber is GameEventScriptTagValue && unwrappedNumber.TryConvertToNumber(out var convertedTag))
        {
            return ConvertToFloat(convertedTag);
        }

        return GesValueOperations.TryCoerceNumericForOperation(unwrappedNumber, out var number)
            ? GesValueOperations.ToGameEventScriptFloat(number)
            : GesFloatNaN();
    }

    private static GameEventScriptValue ConvertToUuid(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (unwrapped.IsUuid())
        {
            return unwrapped;
        }

        return GameEventScriptUuidValue.TryParse(GesValueOperations.ToText(unwrapped), out var uuid)
            ? uuid
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue ConvertToPercentage(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GesFloatNaN();
        }

        if (unwrapped.IsPercentage())
        {
            return unwrapped;
        }

        if (unwrapped.HasNumericUnit())
        {
            return GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number) ||
            !number.IsFinite)
        {
            return GesFloatNaN();
        }

        var ratio = unwrapped.Kind == GameEventScriptValueKind.Integer
            ? number.Value / 100d
            : number.Value > 1d || number.Value < -1d
                ? number.Value / 100d
                : number.Value;
        return GesPercentage(ratio);
    }

    private static GameEventScriptValue ConvertToNumericUnit(GameEventScriptValue value, GameEventScriptNumericUnit unit)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GesFloatNaN();
        }

        if (GesValueOperations.TryApplyVectorUnit(unwrapped, unit, out var vectorWithUnit))
        {
            return vectorWithUnit;
        }

        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out var existingUnit) && existingUnit != unit)
        {
            return GesFloatNaN();
        }

        if (unwrapped.Kind == GameEventScriptValueKind.Integer &&
            GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var integerNumber) &&
            integerNumber.IsFinite)
        {
            return GesInteger(GesValueOperations.ToIntegerSaturated(integerNumber.Value), unit);
        }

        if (unwrapped.Kind == GameEventScriptValueKind.Float &&
            GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number) &&
            number.IsFinite)
        {
            return GesFloat(number.Value, unit);
        }

        return GesFloatNaN();
    }

    private static GameEventScriptValue ConvertToVector(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (unwrapped is GameEventScriptVectorValue vector)
        {
            return vector;
        }

        if (unwrapped is GameEventScriptPointValue)
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (TryCreateSpatialFromMembers("vector", unwrapped, out var vectorFromMembers))
        {
            return vectorFromMembers;
        }

        var items = unwrapped.AsList();
        if (items.Count is >= 1 and <= 3 &&
            TryCreateSpatialFromComponents("vector", items, out var vectorFromItems))
        {
            return vectorFromItems;
        }

        return TryCreateSpatialFromComponents("vector", [unwrapped], out var vectorFromScalar)
            ? vectorFromScalar
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue ConvertToPoint(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (unwrapped is GameEventScriptPointValue point)
        {
            return point;
        }

        if (unwrapped is GameEventScriptVectorValue)
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (TryCreateSpatialFromMembers("point", unwrapped, out var pointFromMembers))
        {
            return pointFromMembers;
        }

        var items = unwrapped.AsList();
        if (items.Count is >= 1 and <= 3 &&
            TryCreateSpatialFromComponents("point", items, out var pointFromItems))
        {
            return pointFromItems;
        }

        return TryCreateSpatialFromComponents("point", [unwrapped], out var pointFromScalar)
            ? pointFromScalar
            : GameEventScriptNothingValue.Instance;
    }

    private static bool TryCreateSpatialFromMembers(string typeName, GameEventScriptValue value, out GameEventScriptValue spatial)
    {
        var hasX = value.TryGetDictionaryMember("x", out var x);
        var hasY = value.TryGetDictionaryMember("y", out var y);
        var hasZ = value.TryGetDictionaryMember("z", out var z);

        if (!hasX && !hasY && !hasZ)
        {
            spatial = GameEventScriptNothingValue.Instance;
            return false;
        }

        var components = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        if (hasX) components["x"] = x;
        if (hasY) components["y"] = y;
        if (hasZ) components["z"] = z;
        return TryCreateSpatialFromLabeledComponents(typeName, components, out spatial);
    }

    private GameEventScriptValue ConvertToCustomType(GameEventScriptValue value, GameEventScriptBytecodeTypeDefinition typeDefinition)
    {
        if (value.TryGetCustomTypeName(out var existingTypeName) &&
            string.Equals(existingTypeName, typeDefinition.Name, StringComparison.Ordinal))
        {
            return value;
        }

        var sourceValues = value.AsDictionary().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var materializedValues = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);

        foreach (var field in typeDefinition.Fields.Where(field => !IsComputedTypeField(field)))
        {
            sourceValues.TryGetValue(field.Name, out var rawValue);
            rawValue ??= GameEventScriptNothingValue.Instance;

            var fieldValue = ConvertValueToDeclaredType(rawValue, field.TypeName);
            fieldValue = ApplyFieldClamp(typeDefinition, field, fieldValue, sourceValues, materializedValues);
            fieldValue = ConvertValueToDeclaredType(fieldValue, field.TypeName);
            materializedValues[field.Name] = fieldValue;
        }

        foreach (var field in typeDefinition.Fields.Where(IsComputedTypeField))
        {
            var computedValue = EvaluateCustomTypeExpression(
                field.ComputedEntryAddress,
                sourceValues,
                materializedValues);
            materializedValues[field.Name] = ConvertValueToDeclaredType(computedValue, field.TypeName);
        }

        return GameEventScriptValueFactory.GesCustomType(typeDefinition.Name, materializedValues);
    }

    private static bool IsComputedTypeField(GameEventScriptBytecodeTypeFieldDefinition field)
        => field.ComputedEntryAddress >= 0;

    private GameEventScriptValue ConvertValueToDeclaredType(GameEventScriptValue value, string declaredType)
        => TryConvertDeclaredType(declaredType, BytecodeVmValue.FromGameEventScriptValue(value), out var converted)
            ? converted.ToGameEventScriptValue()
            : GameEventScriptNothingValue.Instance;

    private GameEventScriptValue ApplyFieldClamp(
        GameEventScriptBytecodeTypeDefinition typeDefinition,
        GameEventScriptBytecodeTypeFieldDefinition field,
        GameEventScriptValue fieldValue,
        IReadOnlyDictionary<string, GameEventScriptValue> sourceValues,
        IReadOnlyDictionary<string, GameEventScriptValue> materializedValues)
    {
        _ = typeDefinition;
        if (!HasCustomTypeExpression(field.MinimumEntryAddress) ||
            !HasCustomTypeExpression(field.MaximumEntryAddress))
        {
            return fieldValue;
        }

        var minimum = EvaluateCustomTypeExpression(
            field.MinimumEntryAddress,
            sourceValues,
            materializedValues);
        var maximum = EvaluateCustomTypeExpression(
            field.MaximumEntryAddress,
            sourceValues,
            materializedValues);
        if (!GesValueOperations.HaveCompatibleNumericUnits(fieldValue, minimum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(fieldValue, maximum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(fieldValue, out var valueNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(maximum, out var maximumNumber))
        {
            return fieldValue;
        }

        if (!valueNumber.IsFinite || !minimumNumber.IsFinite || !maximumNumber.IsFinite)
        {
            if (maximumNumber.IsPositiveInfinity && minimumNumber.IsFinite && valueNumber.IsFinite)
            {
                return GameEventScriptValueFactory.GesFloat(Math.Max(valueNumber.Value, minimumNumber.Value));
            }

            return fieldValue;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        GameEventScriptValue.TryGetNumericUnit(fieldValue, out var unit);
        return GameEventScriptValueFactory.GesFloat(Math.Min(Math.Max(valueNumber.Value, lower), upper), fieldValue.HasNumericUnit() ? unit : null);
    }

    private static bool HasCustomTypeExpression(int entryAddress)
        => entryAddress >= 0;

    private GameEventScriptValue EvaluateCustomTypeExpression(
        int entryAddress,
        IReadOnlyDictionary<string, GameEventScriptValue> sourceValues,
        IReadOnlyDictionary<string, GameEventScriptValue> materializedValues)
        => TryEvaluateLinearCustomTypeExpression(entryAddress, sourceValues, materializedValues, out var linearValue)
            ? linearValue
            : GameEventScriptNothingValue.Instance;

    private bool TryEvaluateLinearCustomTypeExpression(
        int entryAddress,
        IReadOnlyDictionary<string, GameEventScriptValue> sourceValues,
        IReadOnlyDictionary<string, GameEventScriptValue> materializedValues,
        out GameEventScriptValue value)
    {
        value = GameEventScriptNothingValue.Instance;
        if (entryAddress < 0 ||
            !CanExecuteLinearEntry(entryAddress, [], allowPipeline: true))
        {
            return false;
        }

        EnterScope();
        try
        {
            foreach (var pair in sourceValues)
            {
                if (!Define(pair.Key, BytecodeVmValue.FromGameEventScriptValue(pair.Value)))
                {
                    return false;
                }
            }

            foreach (var pair in materializedValues)
            {
                if (!Define(pair.Key, BytecodeVmValue.FromGameEventScriptValue(pair.Value)))
                {
                    return false;
                }
            }

            if (!TryEvaluateLinearHelperExpression(entryAddress, out var linearValue))
            {
                return false;
            }

            value = linearValue.ToGameEventScriptValue();
            return true;
        }
        finally
        {
            ExitScope();
        }
    }

    private static bool TryGetIndexedPipelineSource(GameEventScriptValue value, out IReadOnlyList<GameEventScriptValue> items)
    {
        if (value.Kind == GameEventScriptValueKind.Optional)
        {
            var optional = value.AsOptional();
            if (optional.HasValue)
            {
                return TryGetIndexedPipelineSource(optional.Value, out items);
            }

            items = Array.Empty<GameEventScriptValue>();
            return true;
        }

        if (value.Kind is GameEventScriptValueKind.Range or GameEventScriptValueKind.Sequence)
        {
            items = Array.Empty<GameEventScriptValue>();
            return false;
        }

        items = value.AsList();
        return true;
    }

    private static bool TryGetSeriesTarget(GameEventScriptValue value, out GameEventScriptSeriesValue series)
    {
        if (value is GameEventScriptSeriesValue direct)
        {
            series = direct;
            return true;
        }

        if (value.Kind == GameEventScriptValueKind.Optional)
        {
            var optional = value.AsOptional();
            if (optional.HasValue && optional.Value is GameEventScriptSeriesValue optionalSeries)
            {
                series = optionalSeries;
                return true;
            }
        }

        series = default!;
        return false;
    }

    private static IEnumerable<GameEventScriptValue> EnumerateListLikeValue(GameEventScriptValue value)
    {
        if (value.Kind == GameEventScriptValueKind.Optional)
        {
            var optional = value.AsOptional();
            return optional.HasValue
                ? EnumerateListLikeValue(optional.Value)
                : Array.Empty<GameEventScriptValue>();
        }

        return value.Kind is GameEventScriptValueKind.Range or GameEventScriptValueKind.Sequence
            ? value.AsEnumerable()
            : value.AsList();
    }

    private static GameEventScriptValue EvaluateReverseSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GameEventScriptValueFactory.GesList(items.Reverse().ToArray());
    }

    private static GameEventScriptValue EvaluateDrawSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items, int count)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptNothingValue.Instance;
        }

        var drawn = items.Take(count).ToArray();
        if (count == 1)
        {
            return drawn.Length == 0 ? GameEventScriptNothingValue.Instance : drawn[0];
        }

        return target.Kind == GameEventScriptValueKind.Dice ? GesDice(GameEventScriptDiceValue.Create(drawn.Select(item => (int)item.AsInteger()))) : GesList(drawn);
    }

    private GameEventScriptValue EvaluateShuffleSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptNothingValue.Instance;
        }

        var shuffled = items.ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            if (!TryNextInclusiveInt(0, i, out var swapIndex))
            {
                return GameEventScriptValueFactory.GesList(shuffled);
            }

            (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
        }

        return GameEventScriptValueFactory.GesList(shuffled);
    }

    private static bool MatchStraight(IReadOnlyList<GameEventScriptValue> items)
    {
        var unique = items
            .Select(item => item.AsInteger())
            .Distinct()
            .OrderByDescending(x => x)
            .ToArray();
        if (unique.Length < 2)
        {
            return false;
        }

        for (var i = 0; i < unique.Length - 1; i++)
        {
            if (unique[i] - 1 != unique[i + 1])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPatternSequence(GameEventScriptValue value)
        => value.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice;

    private static bool TryTakeFullHouse(
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        foreach (var tripleCandidate in EnumerateDistinctInSourceOrder(items))
        {
            if (!counts.TryGetValue(tripleCandidate, out var tripleCount) || tripleCount < 3)
            {
                continue;
            }

            foreach (var pairCandidate in EnumerateDistinctInSourceOrder(items))
            {
                if (GesValueOperations.AreEqual(pairCandidate, tripleCandidate))
                {
                    continue;
                }

                if (counts.TryGetValue(pairCandidate, out var pairCount) && pairCount >= 2)
                {
                    takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int>
                    {
                        [tripleCandidate] = 3,
                        [pairCandidate] = 2
                    });
                    return true;
                }
            }
        }

        takenItems = Array.Empty<GameEventScriptValue>();
        return false;
    }

    private static bool TryTakeStraight(IReadOnlyList<GameEventScriptValue> items, out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        var distinctValues = new List<GameEventScriptValue>();
        var seenIntegers = new HashSet<long>();
        foreach (var item in items)
        {
            var value = item.AsInteger();
            if (seenIntegers.Add(value))
            {
                distinctValues.Add(item);
            }
        }

        var uniqueIntegers = distinctValues
            .Select(item => item.AsInteger())
            .OrderByDescending(value => value)
            .ToArray();
        if (uniqueIntegers.Length < 2)
        {
            takenItems = Array.Empty<GameEventScriptValue>();
            return false;
        }

        for (var i = 0; i < uniqueIntegers.Length - 1; i++)
        {
            if (uniqueIntegers[i] - 1 != uniqueIntegers[i + 1])
            {
                takenItems = Array.Empty<GameEventScriptValue>();
                return false;
            }
        }

        takenItems = distinctValues;
        return true;
    }

    private static IReadOnlyList<GameEventScriptValue> TakeItemsByCounts(
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> requiredCounts)
    {
        var remaining = requiredCounts.ToDictionary(pair => pair.Key, pair => pair.Value);
        var takenItems = new List<GameEventScriptValue>();

        foreach (var item in items)
        {
            if (!remaining.TryGetValue(item, out var remainingCount) || remainingCount <= 0)
            {
                continue;
            }

            takenItems.Add(item);
            remaining[item] = remainingCount - 1;
        }

        return takenItems;
    }

    private static IEnumerable<GameEventScriptValue> EnumerateDistinctInSourceOrder(IReadOnlyList<GameEventScriptValue> items)
    {
        var seen = new HashSet<GameEventScriptValue>();
        foreach (var item in items)
        {
            if (seen.Add(item))
            {
                yield return item;
            }
        }
    }

    private static GameEventScriptValue EvaluateSequenceSliceSelector(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        string? operation,
        string? scope,
        int count)
    {
        if (count <= 0)
        {
            return target.Kind == GameEventScriptValueKind.Dice
                ? GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Empty)
                : GameEventScriptValueFactory.GesList(Array.Empty<GameEventScriptValue>());
        }

        var selectedItems = scope switch
        {
            "first" => TakeFirst(items, count),
            "last" => TakeLast(items, count),
            "highest" => TakeHighest(items, count),
            "lowest" => TakeLowest(items, count),
            _ => Array.Empty<GameEventScriptValue>()
        };

        if (string.Equals(operation, "drop", StringComparison.Ordinal))
        {
            selectedItems = DropSelection(items, selectedItems);
        }

        return target.Kind switch
        {
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Create(selectedItems.Select(item => (int)item.AsInteger()))),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(selectedItems),
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GesList(selectedItems),
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private static GameEventScriptValue MaterializeDistinctItems(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        return target.Kind switch
        {
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GesSet(items),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Range => GameEventScriptValueFactory.GesList(items),
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private static GameEventScriptValue MaterializeOrderedItems(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        return target.Kind switch
        {
            GameEventScriptValueKind.Dice or
            GameEventScriptValueKind.List or
            GameEventScriptValueKind.Set or
            GameEventScriptValueKind.Range => GameEventScriptValueFactory.GesList(items),
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private static GameEventScriptValue[] TakeFirst(IReadOnlyList<GameEventScriptValue> items, int count)
        => items.Take(count).ToArray();

    private static GameEventScriptValue[] TakeLast(IReadOnlyList<GameEventScriptValue> items, int count)
        => items.Skip(Math.Max(0, items.Count - count)).ToArray();

    private static GameEventScriptValue[] TakeHighest(IReadOnlyList<GameEventScriptValue> items, int count)
        => items
            .OrderByDescending(item => item, GameEventScriptValue.StableComparer)
            .Take(count)
            .ToArray();

    private static GameEventScriptValue[] TakeLowest(IReadOnlyList<GameEventScriptValue> items, int count)
        => items
            .OrderBy(item => item, GameEventScriptValue.StableComparer)
            .Take(count)
            .ToArray();

    private static GameEventScriptValue[] DropSelection(IReadOnlyList<GameEventScriptValue> items, IReadOnlyList<GameEventScriptValue> selection)
    {
        if (selection.Count == 0)
        {
            return items.ToArray();
        }

        var remainingSelections = selection
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());
        var result = new List<GameEventScriptValue>(items.Count);

        foreach (var item in items)
        {
            if (remainingSelections.TryGetValue(item, out var remainingCount) && remainingCount > 0)
            {
                remainingSelections[item] = remainingCount - 1;
                continue;
            }

            result.Add(item);
        }

        return result.ToArray();
    }

    private void RestoreSlot(int slot, bool hadValue, BytecodeVmValue previous)
    {
        if (hadValue)
        {
            _locals[slot] = previous;
            _assignedSlots[slot] = true;
        }
        else
        {
            _locals[slot] = BytecodeVmValue.Nothing;
            _assignedSlots[slot] = false;
        }
    }

    private void EnterScope() => _scopeMarks.Add(_changes.Count);

    private void ExitScope()
    {
        var markIndex = _scopeMarks.Count - 1;
        var mark = _scopeMarks[markIndex];
        _scopeMarks.RemoveAt(markIndex);
        for (var i = _changes.Count - 1; i >= mark; i--)
        {
            var change = _changes[i];
            if (change.HadValue)
            {
                _locals[change.Slot] = change.PreviousValue;
                _assignedSlots[change.Slot] = true;
            }
            else
            {
                _locals[change.Slot] = BytecodeVmValue.Nothing;
                _assignedSlots[change.Slot] = false;
            }
        }

        _changes.RemoveRange(mark, _changes.Count - mark);
    }

    private bool Define(string name, BytecodeVmValue value)
    {
        if (!_slots.TryGetValue(name, out var slot))
        {
            return false;
        }

        return DefineSlot(slot, value);
    }

    private bool DefineSlot(int slot, BytecodeVmValue value)
    {
        if ((uint)slot >= (uint)_locals.Length)
        {
            return false;
        }

        var hadValue = _assignedSlots[slot];
        var previous = _locals[slot];
        if (!_suppressLocalChangeTracking && slot < _trackedLocalSlotCount)
        {
            _changes.Add(new LocalChange(slot, hadValue, previous));
        }

        _locals[slot] = value;
        _assignedSlots[slot] = true;
        return true;
    }

    private BytecodeVmValue Resolve(string name)
        => _slots.TryGetValue(name, out var slot) && _assignedSlots[slot]
            ? _locals[slot]
            : BytecodeVmValue.Nothing;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BytecodeVmValue ResolveSlot(int slot)
        => (uint)slot < (uint)_locals.Length && _assignedSlots[slot]
            ? _locals[slot]
            : BytecodeVmValue.Nothing;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryConsumeExecutionStep(string detail)
    {
        if (_runtimeBudget.TryConsumeExecutionStep(detail))
        {
            return true;
        }

        _halted = true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryConsumeExecutionSteps(int count, string detail)
    {
        if (_runtimeBudget.TryConsumeExecutionSteps(count, detail))
        {
            return true;
        }

        _halted = true;
        return false;
    }

    private void RecordParameterBound(string parameter, GameEventScriptValue value)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ParameterBound,
            parameter,
            CreateSingleArgument(parameter, value),
            $"Bound '{parameter}'");
    }

    private void RecordLetEvaluated(string identifier, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.LetEvaluated,
            identifier,
            CreateSingleArgument(identifier, value.ToGameEventScriptValue()),
            $"Let '{identifier}' evaluated");
    }

    private void RecordHandlerInvoked(string message, IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.HandlerInvoked,
            message,
            args,
            $"Handler '{message}' invoked");
    }

    private void RecordPredicateCalled(string predicateName, string argumentName, BytecodeVmValue input)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.PredicateCalled,
            predicateName,
            CreateSingleArgument(argumentName, input.ToGameEventScriptValue()),
            $"predicate '{predicateName}' called");
    }

    private void RecordCallableCalled(
        string callableName,
        GameEventScriptBytecodeCallableKind callableKind,
        IReadOnlyList<string> parameters,
        IReadOnlyList<BytecodeVmValue> arguments)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[Math.Min(parameters.Count, arguments.Count)];
        for (var argumentIndex = 0; argumentIndex < pairs.Length; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                parameters[argumentIndex],
                arguments[argumentIndex].ToGameEventScriptValue());
        }

        var kind = callableKind == GameEventScriptBytecodeCallableKind.Predicate
            ? GameEventScriptDiagnosticEventKind.PredicateCalled
            : GameEventScriptDiagnosticEventKind.FunctionCalled;
        var kindText = callableKind == GameEventScriptBytecodeCallableKind.Predicate ? "predicate" : "function";
        RecordDiagnostic(
            kind,
            callableName,
            GameEventScriptNamedArguments.CreateOrdered(pairs),
            $"{kindText} '{callableName}' called");
    }

    private void RecordLetExpressionEvaluatedToNothing(string identifier, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != BytecodeVmValueKind.Nothing)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            identifier,
            CreateSingleArgument(identifier, GameEventScriptNothingValue.Instance),
            $"Let '{identifier}' expression evaluated to Nothing.");
    }

    private void RecordPublishArgumentEvaluatedToNothing(string argumentName, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != BytecodeVmValueKind.Nothing)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            argumentName,
            CreateSingleArgument(argumentName, GameEventScriptNothingValue.Instance),
            $"Publish argument '{argumentName}' evaluated to Nothing.");
    }

    private void RecordExpressionStatementEvaluatedToNothing(string? diagnosticName, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != BytecodeVmValueKind.Nothing)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(diagnosticName) ? "Expression" : diagnosticName;
        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            name,
            CreateSingleArgument(name, GameEventScriptNothingValue.Instance),
            "Expression statement evaluated to Nothing.");
    }

    private void RecordExpressionEvaluatedToNothing(string name)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            name,
            CreateSingleArgument(name, GameEventScriptNothingValue.Instance),
            $"Expression '{name}' evaluated to Nothing.");
    }

    private void RecordDiagnostic(
        GameEventScriptDiagnosticEventKind kind,
        string name,
        IReadOnlyDictionary<string, GameEventScriptValue> arguments,
        string? detail = null)
        => GesInvocationKernel.RecordDiagnostic(_context, _diagnosticsEnabled, kind, name, arguments, detail);

    private static GameEventScriptNamedArguments CreateSingleArgument(string name, GameEventScriptValue value)
        => GameEventScriptNamedArguments.CreateOrdered(
        [
            new KeyValuePair<string, GameEventScriptValue>(name, value)
        ]);

    private static bool Fail(out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        return false;
    }

    private static long ToLongSaturated(double value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }

    internal sealed class MessageTagBuilder
    {
        private readonly List<string> _tags = [];
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public int Count => _tags.Count;

        public void Add(string? tag)
        {
            var normalized = GameEventScriptMessage.NormalizeTagName(tag);
            if (normalized.Length == 0 || !_seen.Add(normalized))
            {
                return;
            }

            _tags.Add(normalized);
        }

        public IReadOnlyList<string> ToArray()
            => _tags.Count == 0 ? [] : _tags.ToArray();
    }

    private readonly record struct LocalChange(int Slot, bool HadValue, BytecodeVmValue PreviousValue);
}

internal enum BytecodeVmValueKind
{
    Nothing,
    Boolean,
    Integer,
    Float,
    Percentage,
    Reference
}

internal readonly record struct BytecodeVmValue(
    BytecodeVmValueKind Kind,
    double Number,
    long IntegerValue,
    bool BooleanValue,
    GameEventScriptNumericUnit? Unit,
    GameEventScriptValue? ReferenceValue)
{
    public static BytecodeVmValue Nothing { get; } = new(BytecodeVmValueKind.Nothing, 0d, 0, false, null, null);

    public static BytecodeVmValue Boolean(bool value)
        => new(BytecodeVmValueKind.Boolean, value ? 1d : 0d, value ? 1 : 0, value, null, null);

    public static BytecodeVmValue Integer(long value, GameEventScriptNumericUnit? unit = null)
        => new(BytecodeVmValueKind.Integer, value, value, value != 0, unit, null);

    public static BytecodeVmValue Float(double value, GameEventScriptNumericUnit? unit = null)
        => new(BytecodeVmValueKind.Float, value, ToLongSaturated(value), value != 0d, unit, null);

    public static BytecodeVmValue Percentage(double ratio)
        => new(BytecodeVmValueKind.Percentage, ratio, ToLongSaturated(ratio * 100d), ratio != 0d, null, null);

    public static BytecodeVmValue Reference(GameEventScriptValue value)
        => new(BytecodeVmValueKind.Reference, 0d, 0, value.AsBoolean(), null, value);

    public static BytecodeVmValue NaN()
        => Reference(GesFloatNaN());

    public static BytecodeVmValue FromGameEventScriptValue(GameEventScriptValue value)
        => value switch
        {
            GameEventScriptBooleanValue boolean => Boolean(boolean.Value),
            GameEventScriptIntegerValue integer => Integer(integer.Value, integer.Unit),
            GameEventScriptFloatValue floatValue when floatValue.HasSemanticValue() => Float(floatValue.Value, floatValue.Unit),
            GameEventScriptPercentageValue percentage => Percentage(percentage.Ratio),
            _ => Reference(value)
        };

    public static BytecodeVmValue FromGameEventScriptFastValue(GameEventScriptFastValue value)
        => value.Kind switch
        {
            GameEventScriptValueKind.Nothing => Nothing,
            GameEventScriptValueKind.Boolean => Boolean(value.Boolean),
            GameEventScriptValueKind.Integer => Integer(value.Integer, value.Unit),
            GameEventScriptValueKind.Float when !value.IsReferenceBacked => Float(value.Number, value.Unit),
            GameEventScriptValueKind.Percentage when !value.IsReferenceBacked => Percentage(value.Number),
            _ => FromGameEventScriptValue(value.ToGameEventScriptValue())
        };

    public static BytecodeVmValue FromBytecodeConstant(GameEventScriptBytecodeConstant constant)
        => constant.Kind switch
        {
            GameEventScriptBytecodeConstantKind.Nothing => Nothing,
            GameEventScriptBytecodeConstantKind.Boolean => Boolean(constant.Boolean),
            GameEventScriptBytecodeConstantKind.Integer => Integer(constant.Integer, constant.Unit),
            GameEventScriptBytecodeConstantKind.Float when constant.IsNaN => Reference(GesFloatNaN()),
            GameEventScriptBytecodeConstantKind.Float when constant.IsInfinity => Reference(constant.IsNegativeInfinity
                ? GesFloatNegativeInfinity()
                : GesFloatInfinity()),
            GameEventScriptBytecodeConstantKind.Float => Float(constant.Number, constant.Unit),
            GameEventScriptBytecodeConstantKind.Percentage => Percentage(constant.Number),
            GameEventScriptBytecodeConstantKind.Text => Reference(GesText(constant.Text ?? string.Empty)),
            GameEventScriptBytecodeConstantKind.Tag => Reference(GesTag(constant.Text ?? string.Empty)),
            GameEventScriptBytecodeConstantKind.Handler => Reference(GesHandler(GameEventScriptMessageSignature.Create(constant.Text ?? string.Empty, constant.Labels))),
            _ => Nothing
        };

    public bool AsBoolean()
        => Kind switch
        {
            BytecodeVmValueKind.Boolean => BooleanValue,
            BytecodeVmValueKind.Integer => IntegerValue != 0,
            BytecodeVmValueKind.Float => Number != 0d,
            BytecodeVmValueKind.Percentage => Number != 0d,
            BytecodeVmValueKind.Reference => ReferenceValue?.AsBoolean() ?? false,
            _ => false
        };

    public bool IsTrue()
        => !IsNothing() && AsBoolean();

    public bool IsFalse()
        => !IsNothing() && !AsBoolean();

    public bool IsNothing()
        => IsNothingLike();

    public bool TryGetFiniteNumber(out double value)
    {
        if (TryGetPrimitiveFiniteNumber(out value) ||
            TryGetNumeric(out var number, out _, out _) && number.IsFinite && SetNumber(number.Value, out value))
        {
            return true;
        }

        value = 0d;
        return false;
    }

    public bool TryGetRangeInteger(out long value)
    {
        switch (Kind)
        {
            case BytecodeVmValueKind.Boolean:
                value = BooleanValue ? 1L : 0L;
                return true;
            case BytecodeVmValueKind.Integer:
                value = IntegerValue;
                return true;
            case BytecodeVmValueKind.Float:
            case BytecodeVmValueKind.Percentage:
                return TryFiniteDoubleToLong(Number, out value);
            case BytecodeVmValueKind.Reference when ReferenceValue is { } reference:
                if (!GesValueOperations.TryUnwrapOptionalForOperation(reference, out var unwrapped))
                {
                    value = 0L;
                    return false;
                }

                if (unwrapped is GameEventScriptIntegerValue integer)
                {
                    value = integer.Value;
                    return true;
                }

                if (GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number) && number.IsFinite)
                {
                    return TryFiniteDoubleToLong(number.Value, out value);
                }

                break;
        }

        value = 0L;
        return false;
    }

    public GameEventScriptValue ToGameEventScriptValue()
        => Kind switch
        {
            BytecodeVmValueKind.Nothing => GameEventScriptNothingValue.Instance,
            BytecodeVmValueKind.Boolean => GameEventScriptValueFactory.GesBoolean(BooleanValue),
            BytecodeVmValueKind.Integer => GameEventScriptValueFactory.GesInteger(IntegerValue, Unit),
            BytecodeVmValueKind.Float => GameEventScriptValueFactory.GesFloat(Number, Unit),
            BytecodeVmValueKind.Percentage => GameEventScriptValueFactory.GesPercentage(Number),
            BytecodeVmValueKind.Reference => ReferenceValue ?? GameEventScriptNothingValue.Instance,
            _ => GameEventScriptNothingValue.Instance
        };

    public static bool AreEqual(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            return left.IntegerValue == right.IntegerValue;
        }

        if (left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) &&
            right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            if (leftUnit != rightUnit)
            {
                return false;
            }

            if (leftNumber.IsNaN || rightNumber.IsNaN)
            {
                return false;
            }

            if (leftNumber.IsInfinity || rightNumber.IsInfinity)
            {
                return leftNumber.Kind == rightNumber.Kind;
            }

            return leftNumber.Value == rightNumber.Value;
        }

        return left.ToGameEventScriptValue().Equals(right.ToGameEventScriptValue());
    }

    public static bool AreApproximatelyEqual(in BytecodeVmValue left, in BytecodeVmValue right)
        => GesValueOperations.AreApproximatelyEqual(left.ToGameEventScriptValue(), right.ToGameEventScriptValue());

    public static int CompareNumeric(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        return TryCompareNumeric(left, right, out var comparison) ? comparison : 0;
    }

    public static bool TryCompareNumeric(in BytecodeVmValue left, in BytecodeVmValue right, out int comparison)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            if (left.Unit != right.Unit)
            {
                comparison = default;
                return false;
            }

            comparison = left.IntegerValue.CompareTo(right.IntegerValue);
            return true;
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit != right.Unit)
            {
                comparison = default;
                return false;
            }

            comparison = leftPrimitive.CompareTo(rightPrimitive);
            return true;
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _) ||
            leftUnit != rightUnit)
        {
            comparison = default;
            return false;
        }

        return GesValueOperations.TryCompareNumeric(leftNumber, rightNumber, out comparison);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryComparePrimitiveIntegers(in BytecodeVmValue left, in BytecodeVmValue right, out int comparison)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            if (left.Unit != right.Unit)
            {
                comparison = default;
                return false;
            }

            comparison = left.IntegerValue.CompareTo(right.IntegerValue);
            return true;
        }

        comparison = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerAdd(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit == right.Unit &&
            TryAddInteger(leftInteger, rightInteger, out var result))
        {
            value = Integer(result, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerSubtract(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit == right.Unit &&
            TrySubtractInteger(leftInteger, rightInteger, out var result))
        {
            value = Integer(result, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerMultiply(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            !(left.Unit.HasValue && right.Unit.HasValue) &&
            TryMultiplyInteger(leftInteger, rightInteger, out var result))
        {
            value = Integer(result, left.Unit ?? right.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerDivide(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            TryGetDivideResultUnit(left.Unit, right.Unit, out var resultUnit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            value = Float((double)leftInteger / rightInteger, resultUnit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerFloorDivide(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            TryGetDivideResultUnit(left.Unit, right.Unit, out var resultUnit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            var quotient = leftInteger / rightInteger;
            var remainder = leftInteger % rightInteger;
            if (remainder != 0 && (remainder > 0) != (rightInteger > 0))
            {
                quotient--;
            }

            value = Integer(quotient, resultUnit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerModulo(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit.HasValue == right.Unit.HasValue &&
            (!left.Unit.HasValue || left.Unit == right.Unit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            var modulo = leftInteger % rightInteger;
            if (modulo != 0 &&
                (modulo < 0 && rightInteger > 0 || modulo > 0 && rightInteger < 0))
            {
                modulo += rightInteger;
            }

            value = Integer(modulo, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerRemainder(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit.HasValue == right.Unit.HasValue &&
            (!left.Unit.HasValue || left.Unit == right.Unit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            value = Integer(leftInteger % rightInteger, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetPrimitiveIntegerOperands(in BytecodeVmValue left, in BytecodeVmValue right, out long leftInteger, out long rightInteger)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            leftInteger = left.IntegerValue;
            rightInteger = right.IntegerValue;
            return true;
        }

        leftInteger = default;
        rightInteger = default;
        return false;
    }

    public static BytecodeVmValue Add(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (TryEvaluateIntegerBinary(left, "+", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "+", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "+", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "+", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return AddPercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit != right.Unit)
            {
                return NaN();
            }

            return GesValueOperations.TryAddFinite(leftPrimitive, rightPrimitive, out var sum)
                ? FromFinitePrimitiveNumericResult(left, "+", right, sum, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.AddNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            var leftValue = left.ToGameEventScriptValue();
            var rightValue = right.ToGameEventScriptValue();
            if (GesValueOperations.TryCombineWithPlus(leftValue, rightValue, out var combined))
            {
                return FromGameEventScriptValue(combined);
            }

            return leftValue.IsText() || rightValue.IsText()
                ? Reference(GesText($"{GesValueOperations.ToText(leftValue)}{GesValueOperations.ToText(rightValue)}"))
                : NaN();
        }

        if (leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.AddNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Subtract(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (TryEvaluateIntegerBinary(left, "-", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "-", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "-", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "-", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return SubtractPercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit != right.Unit)
            {
                return NaN();
            }

            return GesValueOperations.TryNegateFinite(rightPrimitive, out var negatedRight) &&
                   GesValueOperations.TryAddFinite(leftPrimitive, negatedRight, out var difference)
                ? FromFinitePrimitiveNumericResult(left, "-", right, difference, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.SubtractNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.SubtractNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Multiply(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (TryEvaluateIntegerBinary(left, "*", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "*", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "*", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "*", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return MultiplyPercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit.HasValue && right.Unit.HasValue)
            {
                return NaN();
            }

            return GesValueOperations.TryMultiplyFinite(leftPrimitive, rightPrimitive, out var product)
                ? FromFinitePrimitiveNumericResult(left, "*", right, product, left.Unit ?? right.Unit)
                : FromFloatNumeric(
                    GesValueOperations.MultiplyNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    left.Unit ?? right.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit.HasValue && rightUnit.HasValue)
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.MultiplyNumeric(leftNumber, rightNumber), leftUnit ?? rightUnit);
    }

    public static BytecodeVmValue Divide(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (TryEvaluateIntegerBinary(left, "/", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "/", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "/", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "/", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return DividePercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (!TryGetDivideResultUnit(left.Unit, right.Unit, out var primitiveResultUnit))
            {
                return NaN();
            }

            return rightPrimitive != 0d && GesValueOperations.TryDivideFinite(leftPrimitive, rightPrimitive, out var quotient)
                ? Float(quotient, primitiveResultUnit)
                : FromFloatNumeric(
                    GesValueOperations.DivideNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    primitiveResultUnit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (!TryGetDivideResultUnit(leftUnit, rightUnit, out var resultUnit))
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.DivideNumeric(leftNumber, rightNumber), resultUnit);
    }

    public static BytecodeVmValue IntegerDivide(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (TryEvaluateIntegerBinary(left, "div", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "div", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "div", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (!TryGetDivideResultUnit(left.Unit, right.Unit, out var primitiveResultUnit))
            {
                return NaN();
            }

            return rightPrimitive != 0d && GesValueOperations.TryDivideFinite(leftPrimitive, rightPrimitive, out var quotient)
                ? FromFinitePrimitiveNumericResult(left, "div", right, Math.Floor(quotient), primitiveResultUnit)
                : FromFloatNumeric(
                    GesValueOperations.IntegerDivideNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    primitiveResultUnit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (!TryGetDivideResultUnit(leftUnit, rightUnit, out var resultUnit))
        {
            return NaN();
        }

        var result = GesValueOperations.IntegerDivideNumeric(leftNumber, rightNumber);
        return resultUnit is null && result.IsFinite && TryToInteger(result.Value, out var integer)
            ? Integer(integer)
            : FromFloatNumeric(result, resultUnit);
    }

    public static BytecodeVmValue Modulo(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (TryEvaluateIntegerBinary(left, "mod", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "mod", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "mod", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit.HasValue != right.Unit.HasValue ||
                left.Unit.HasValue && right.Unit.HasValue && left.Unit != right.Unit)
            {
                return NaN();
            }

            return rightPrimitive != 0d && GesValueOperations.TryModuloFinite(leftPrimitive, rightPrimitive, out var modulo)
                ? FromFinitePrimitiveNumericResult(left, "mod", right, modulo, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.ModuloNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit.HasValue != rightUnit.HasValue ||
            leftUnit.HasValue && rightUnit.HasValue && leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.ModuloNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Remainder(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (TryEvaluateIntegerBinary(left, "rem", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "rem", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "rem", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit.HasValue != right.Unit.HasValue ||
                left.Unit.HasValue && right.Unit.HasValue && left.Unit != right.Unit)
            {
                return NaN();
            }

            return rightPrimitive != 0d && GesValueOperations.TryRemainderFinite(leftPrimitive, rightPrimitive, out var remainder)
                ? FromFinitePrimitiveNumericResult(left, "rem", right, remainder, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.RemainderNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit.HasValue != rightUnit.HasValue ||
            leftUnit.HasValue && rightUnit.HasValue && leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.RemainderNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Power(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (HasUuidOperand(left, right))
        {
            return Nothing;
        }

        if (GesValueOperations.TryEvaluateUnitBinary(left.ToGameEventScriptValue(), "^", right.ToGameEventScriptValue(), out var unitResult))
        {
            return FromGameEventScriptValue(unitResult);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit.HasValue || rightUnit.HasValue)
        {
            return NaN();
        }

        var result = GesValueOperations.PowerNumeric(leftNumber, rightNumber);
        return result.IsFinite &&
               left.Kind == BytecodeVmValueKind.Integer &&
               right.Kind == BytecodeVmValueKind.Integer &&
               TryToInteger(result.Value, out var integer)
            ? Integer(integer)
            : FromFloatNumeric(result);
    }

    private static bool TryGetDivideResultUnit(
        GameEventScriptNumericUnit? leftUnit,
        GameEventScriptNumericUnit? rightUnit,
        out GameEventScriptNumericUnit? resultUnit)
    {
        if (!leftUnit.HasValue && !rightUnit.HasValue)
        {
            resultUnit = null;
            return true;
        }

        if (leftUnit.HasValue && !rightUnit.HasValue)
        {
            resultUnit = leftUnit;
            return true;
        }

        if (leftUnit.HasValue && rightUnit.HasValue && leftUnit == rightUnit)
        {
            resultUnit = null;
            return true;
        }

        resultUnit = null;
        return false;
    }

    private static bool TryEvaluatePrimitivePercentage(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        value = default;
        if (!TryGetPrimitiveNumeric(left, out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !TryGetPrimitiveNumeric(right, out var rightNumber, out var rightUnit, out var rightIsPercentage) ||
            !leftIsPercentage && !rightIsPercentage)
        {
            return false;
        }

        switch (operation)
        {
            case "+":
                if (leftIsPercentage && rightIsPercentage)
                {
                    value = GesValueOperations.TryAddFinite(leftNumber, rightNumber, out var percentageSum)
                        ? Percentage(percentageSum)
                        : AddPercentage(left, right);
                    return true;
                }

                if (leftIsPercentage)
                {
                    value = NaN();
                    return true;
                }

                if (GesValueOperations.TryMultiplyFinite(leftNumber, rightNumber, out var addDelta) &&
                    GesValueOperations.TryAddFinite(leftNumber, addDelta, out var addResult))
                {
                    value = Float(addResult, leftUnit);
                    return true;
                }

                value = AddPercentage(left, right);
                return true;

            case "-":
                if (leftIsPercentage && rightIsPercentage)
                {
                    value = GesValueOperations.TryNegateFinite(rightNumber, out var negatedRight) &&
                            GesValueOperations.TryAddFinite(leftNumber, negatedRight, out var percentageDifference)
                        ? Percentage(percentageDifference)
                        : SubtractPercentage(left, right);
                    return true;
                }

                if (leftIsPercentage)
                {
                    value = NaN();
                    return true;
                }

                if (GesValueOperations.TryMultiplyFinite(leftNumber, rightNumber, out var subtractDelta) &&
                    GesValueOperations.TryNegateFinite(subtractDelta, out var negatedDelta) &&
                    GesValueOperations.TryAddFinite(leftNumber, negatedDelta, out var subtractResult))
                {
                    value = Float(subtractResult, leftUnit);
                    return true;
                }

                value = SubtractPercentage(left, right);
                return true;

            case "*":
                if (GesValueOperations.TryMultiplyFinite(leftNumber, rightNumber, out var product))
                {
                    value = leftIsPercentage && rightIsPercentage || leftIsPercentage && rightUnit is null
                        ? Percentage(product)
                        : Float(product, leftIsPercentage ? rightUnit : leftUnit);
                    return true;
                }

                value = MultiplyPercentage(left, right);
                return true;

            case "/":
                if (leftIsPercentage && rightUnit.HasValue)
                {
                    value = NaN();
                    return true;
                }

                if (rightNumber != 0d &&
                    GesValueOperations.TryDivideFinite(leftNumber, rightNumber, out var quotient))
                {
                    value = leftIsPercentage && rightIsPercentage
                        ? Float(quotient)
                        : leftIsPercentage
                            ? Percentage(quotient)
                            : Float(quotient, leftUnit);
                    return true;
                }

                value = DividePercentage(left, right);
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetPrimitiveNumeric(
        in BytecodeVmValue input,
        out double number,
        out GameEventScriptNumericUnit? unit,
        out bool isPercentage)
    {
        switch (input.Kind)
        {
            case BytecodeVmValueKind.Boolean:
                number = input.BooleanValue ? 1d : 0d;
                unit = null;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Integer:
                number = input.IntegerValue;
                unit = input.Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Float:
                number = input.Number;
                unit = input.Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Percentage:
                number = input.Number;
                unit = null;
                isPercentage = true;
                return true;
            default:
                number = default;
                unit = null;
                isPercentage = false;
                return false;
        }
    }

    private static BytecodeVmValue AddPercentage(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(GesValueOperations.AddNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = GesValueOperations.MultiplyNumeric(leftNumber, rightNumber);
        return FromFloatNumeric(GesValueOperations.AddNumeric(leftNumber, delta), leftUnit);
    }

    private static BytecodeVmValue SubtractPercentage(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(GesValueOperations.SubtractNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = GesValueOperations.MultiplyNumeric(leftNumber, rightNumber);
        return FromFloatNumeric(GesValueOperations.SubtractNumeric(leftNumber, delta), leftUnit);
    }

    private static BytecodeVmValue MultiplyPercentage(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out var rightIsPercentage))
        {
            return NaN();
        }

        var result = GesValueOperations.MultiplyNumeric(leftNumber, rightNumber);
        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(result);
        }

        if (leftIsPercentage)
        {
            return rightUnit is { } unit
                ? FromFloatNumeric(result, unit)
                : FromPercentageNumeric(result);
        }

        return FromFloatNumeric(result, leftUnit);
    }

    private static BytecodeVmValue DividePercentage(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightUnit.HasValue)
        {
            return NaN();
        }

        var result = GesValueOperations.DivideNumeric(leftNumber, rightNumber);
        if (leftIsPercentage && rightIsPercentage)
        {
            return FromFloatNumeric(result);
        }

        if (leftIsPercentage)
        {
            return FromPercentageNumeric(result);
        }

        return FromFloatNumeric(result, leftUnit);
    }

    private bool TryGetNumeric(
        out GesValueOperations.NumericValue number,
        out GameEventScriptNumericUnit? unit,
        out bool isPercentage)
    {
        switch (Kind)
        {
            case BytecodeVmValueKind.Boolean:
                number = GesValueOperations.NumericValue.Finite(BooleanValue ? 1d : 0d);
                unit = null;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Integer:
                number = GesValueOperations.NumericValue.Finite(IntegerValue);
                unit = Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Float:
                number = GesValueOperations.NumericValue.Finite(Number);
                unit = Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Percentage:
                number = GesValueOperations.NumericValue.Finite(Number);
                unit = null;
                isPercentage = true;
                return true;
            case BytecodeVmValueKind.Reference when ReferenceValue is { } reference &&
                                                    GesValueOperations.TryCoerceNumericForOperation(reference, out var referenceNumber):
                number = referenceNumber;
                unit = GameEventScriptValue.TryGetNumericUnit(reference, out var referenceUnit) ? referenceUnit : null;
                isPercentage = reference.IsPercentage();
                return true;
            default:
                number = default;
                unit = null;
                isPercentage = false;
                return false;
        }
    }

    private bool TryGetPrimitiveFiniteNumber(out double number)
    {
        switch (Kind)
        {
            case BytecodeVmValueKind.Boolean:
                number = BooleanValue ? 1d : 0d;
                return true;
            case BytecodeVmValueKind.Integer:
                number = IntegerValue;
                return true;
            case BytecodeVmValueKind.Float:
            case BytecodeVmValueKind.Percentage:
                number = Number;
                return true;
            default:
                number = default;
                return false;
        }
    }

    private bool IsPercentageLike()
        => Kind == BytecodeVmValueKind.Percentage ||
           ReferenceValue is { } reference && reference.IsPercentage();

    private static bool HasUuidOperand(in BytecodeVmValue left, in BytecodeVmValue right)
        => left.ReferenceValue?.IsUuid() == true || right.ReferenceValue?.IsUuid() == true;

    private bool IsVectorLike()
        => ReferenceValue is GameEventScriptVectorValue;

    private bool IsPointLike()
        => ReferenceValue is GameEventScriptPointValue;

    private static bool TryEvaluatePointBinary(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (!left.IsPointLike() && !right.IsPointLike())
        {
            value = default;
            return false;
        }

        if (GesValueOperations.TryEvaluatePointBinary(left.ToGameEventScriptValue(), operation, right.ToGameEventScriptValue(), out var pointValue))
        {
            value = FromGameEventScriptValue(pointValue);
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryEvaluateVectorBinary(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (!left.IsVectorLike() && !right.IsVectorLike())
        {
            value = default;
            return false;
        }

        if (GesValueOperations.TryEvaluateVectorBinary(left.ToGameEventScriptValue(), operation, right.ToGameEventScriptValue(), out var vectorValue))
        {
            value = FromGameEventScriptValue(vectorValue);
            return true;
        }

        value = default;
        return false;
    }

    public bool IsNothingLike()
        => Kind == BytecodeVmValueKind.Nothing ||
           ReferenceValue is { } reference && reference.IsNothing();

    private static BytecodeVmValue FromFloatNumeric(GesValueOperations.NumericValue number, GameEventScriptNumericUnit? unit = null)
        => number.IsFinite
            ? Float(number.Value, unit)
            : Reference(GesValueOperations.ToGameEventScriptFloat(number));

    private static BytecodeVmValue FromFinitePrimitiveNumericResult(
        BytecodeVmValue left,
        string operation,
        BytecodeVmValue right,
        double value,
        GameEventScriptNumericUnit? unit = null)
        => operation == "div" &&
           TryToInteger(value, out var quotient)
            ? Integer(quotient, unit)
            : operation is "+" or "-" or "*" or "mod" or "rem" &&
              left.Kind == BytecodeVmValueKind.Integer &&
              right.Kind == BytecodeVmValueKind.Integer &&
              TryToInteger(value, out var integer)
                ? Integer(integer, unit)
                : Float(value, unit);

    private static BytecodeVmValue FromPercentageNumeric(GesValueOperations.NumericValue number)
        => number.IsFinite
            ? Percentage(number.Value)
            : FromFloatNumeric(number);

    private static bool TryEvaluateIntegerBinary(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (left.Kind != BytecodeVmValueKind.Integer || right.Kind != BytecodeVmValueKind.Integer)
        {
            value = default;
            return false;
        }

        var leftInteger = left.IntegerValue;
        var rightInteger = right.IntegerValue;
        switch (operation)
        {
            case "+":
                if (left.Unit == right.Unit &&
                    TryAddInteger(leftInteger, rightInteger, out var sum))
                {
                    value = Integer(sum, left.Unit);
                    return true;
                }

                break;

            case "-":
                if (left.Unit == right.Unit &&
                    TrySubtractInteger(leftInteger, rightInteger, out var difference))
                {
                    value = Integer(difference, left.Unit);
                    return true;
                }

                break;

            case "*":
                if (!(left.Unit.HasValue && right.Unit.HasValue) && TryMultiplyInteger(leftInteger, rightInteger, out var product))
                {
                    value = Integer(product, left.Unit ?? right.Unit);
                    return true;
                }

                break;

            case "/":
                if (TryGetDivideResultUnit(left.Unit, right.Unit, out var divideUnit) && rightInteger != 0 && !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    value = Float((double)leftInteger / rightInteger, divideUnit);
                    return true;
                }

                break;

            case "div":
                if (TryGetDivideResultUnit(left.Unit, right.Unit, out var integerDivideUnit) && rightInteger != 0 && !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    var quotient = leftInteger / rightInteger;
                    var remainder = leftInteger % rightInteger;
                    if (remainder != 0 && (remainder > 0) != (rightInteger > 0)) quotient--;
                    value = Integer(quotient, integerDivideUnit);
                    return true;
                }

                break;

            case "mod":
                if (left.Unit.HasValue == right.Unit.HasValue && (!left.Unit.HasValue || left.Unit == right.Unit) && rightInteger != 0 && !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    var modulo = leftInteger % rightInteger;
                    if (modulo != 0 && (modulo < 0 && rightInteger > 0 || modulo > 0 && rightInteger < 0)) modulo += rightInteger;
                    value = Integer(modulo, left.Unit);
                    return true;
                }

                break;

            case "rem":
                if (left.Unit.HasValue == right.Unit.HasValue && (!left.Unit.HasValue || left.Unit == right.Unit) && rightInteger != 0 && !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    value = Integer(leftInteger % rightInteger, left.Unit);
                    return true;
                }

                break;
        }

        value = default;
        return false;
    }

    private static bool SetNumber(double input, out double output)
    {
        output = input;
        return true;
    }

    private static bool TryAddInteger(long left, long right, out long value)
    {
        value = left + right;
        return ((left ^ value) & (right ^ value)) >= 0;
    }

    private static bool TrySubtractInteger(long left, long right, out long value)
    {
        value = left - right;
        return ((left ^ right) & (left ^ value)) >= 0;
    }

    private static bool TryMultiplyInteger(long left, long right, out long value)
    {
        if (left == 0 || right == 0)
        {
            value = 0;
            return true;
        }

        if (left == -1)
        {
            if (right == long.MinValue)
            {
                value = default;
                return false;
            }

            value = -right;
            return true;
        }

        if (right == -1)
        {
            if (left == long.MinValue)
            {
                value = default;
                return false;
            }

            value = -left;
            return true;
        }

        value = left * right;
        return value / right == left;
    }

    private static bool TryToInteger(double value, out long integer)
    {
        if (value != Math.Truncate(value) ||
            value > long.MaxValue ||
            value < long.MinValue)
        {
            integer = default;
            return false;
        }

        integer = (long)value;
        return true;
    }

    private static long ToLongSaturated(double value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }

    private static bool TryFiniteDoubleToLong(double input, out long value)
    {
        if (double.IsNaN(input) || double.IsInfinity(input))
        {
            value = 0L;
            return false;
        }

        value = ToLongSaturated(input);
        return true;
    }
}
