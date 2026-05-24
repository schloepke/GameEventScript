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
    private enum PublishKind
    {
        Emit,
        Publish
    }

    private enum PipelineElementMode
    {
        First,
        Last,
        Single
    }

    private enum IteratorReduceMode
    {
        Reduce,
        ReduceOrDefault,
        Fold
    }

    private readonly GesBytecodeVmExecutable _compiledScript;
    private readonly GameEventScriptSession _context;
    private readonly GesRuntimeBudget _runtimeBudget;
    private readonly IReadOnlyDictionary<string, int> _slots;
    private BytecodeVmValue[] _locals;
    private bool[] _assignedSlots;
    private int _activeLocalSlotCount;
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
        GameEventScriptSession context,
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
        _activeLocalSlotCount = Math.Max(0, localSlotCount);
        _randomScopes.Push(context.Random);
    }

    public static void InvokeHandler(
        GesBytecodeVmExecutable compiledScript,
        GameEventScriptSession context,
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
            case GameEventScriptBytecodeOpCode.PipelineIterator:
                return allowPipeline && CanExecuteLinearEntry(instruction.EntryAddress, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.StreamReduce:
                return allowPipeline && CanExecuteLinearEntry(instruction.AU, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.StreamReduceOrDefault:
            case GameEventScriptBytecodeOpCode.StreamFold:
                return allowPipeline && CanExecuteLinearEntry(instruction.BU, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.PipelineDistinctBy:
            case GameEventScriptBytecodeOpCode.PipelineGroupBy:
            case GameEventScriptBytecodeOpCode.PipelineOrderByAscending:
            case GameEventScriptBytecodeOpCode.PipelineOrderByDescending:
            case GameEventScriptBytecodeOpCode.PipelineMap:
                return allowPipeline && CanExecuteLinearEntry(instruction.AU, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.PipelineMapValue:
                return allowPipeline &&
                       CanExecuteLinearEntry(instruction.AU, visitingCallables, allowPipeline) &&
                       CanExecuteLinearEntry(instruction.BU, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace:
                return allowPipeline && CanExecuteLinearEntry(instruction.AU, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.PipelineChooseWeighted:
                return allowPipeline && CanExecuteLinearEntry(instruction.BU, visitingCallables, allowPipeline);

            case GameEventScriptBytecodeOpCode.Call:
            case GameEventScriptBytecodeOpCode.CallPredicate:
                return CanExecuteLinearCallableAddress(instruction.EntryAddress, visitingCallables, allowPipeline);

            default:
                return true;
        }
    }

    private bool CanExecuteLinearCallableAddress(int entryAddress, HashSet<string> visitingCallables, bool allowPipeline)
    {
        var key = "@call:" + entryAddress.ToString(CultureInfo.InvariantCulture);
        if (!visitingCallables.Add(key))
        {
            return true;
        }

        try
        {
            return CanExecuteLinearEntry(entryAddress, visitingCallables, allowPipeline);
        }
        finally
        {
            visitingCallables.Remove(key);
        }
    }

    private bool CanExecuteLinearCallable(string callableName, HashSet<string> visitingCallables, bool allowPipeline)
    {
        if (!_compiledScript.BytecodeModule.Callables.TryGetValue(callableName, out var callable))
        {
            return false;
        }

        var signatureId = GameEventScriptMessageSignature.CreateSignatureId(callable.Name, callable.SignatureLabels);
        if (!visitingCallables.Add(signatureId))
        {
            return true;
        }

        try
        {
            return CanExecuteLinearEntry(callable.EntryAddress, visitingCallables, allowPipeline);
        }
        finally
        {
            visitingCallables.Remove(signatureId);
        }
    }

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

            if (instruction.OpCode is GameEventScriptBytecodeOpCode.ReturnValue or GameEventScriptBytecodeOpCode.ReturnVoid)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryInvokeLinearHandler(GesBytecodeVmCompiledHandler handler, IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        var arguments = LinearArgumentSource.ForHandler(handler, args);
        return TryInitializeLinearHandlerFrame(handler, arguments) &&
               TryExecuteLinearRange(
                   handler.EntryAddress,
                   _compiledScript.LinearExecutable.Code.Count,
                   arguments,
                   out _,
                   out _);
    }

    private bool TryInitializeLinearHandlerFrame(
        GesBytecodeVmCompiledHandler handler,
        LinearArgumentSource arguments)
    {
        var argumentSlotCount = handler.Parameters.Count;
        EnsureLocalCapacity(argumentSlotCount);
        _activeLocalSlotCount = argumentSlotCount;
        _trackedLocalSlotCount = argumentSlotCount;

        for (var index = 0; index < handler.Parameters.Count; index++)
        {
            if (!arguments.TryGetValue(index, out var parameterValue) ||
                !DefineSlot(index, parameterValue))
            {
                return false;
            }

            RecordLinearParameterInitialized(arguments, index, index);
        }

        return true;
    }

    private bool TryExecuteLinearRange(
        int startAddress,
        int endAddress,
        LinearArgumentSource? arguments,
        out bool returned,
        out BytecodeVmValue returnValue)
        => TryExecuteLinearRange(
            startAddress,
            endAddress,
            arguments,
            out returned,
            out returnValue,
            out _);

    private bool TryExecuteLinearRange(
        int startAddress,
        int endAddress,
        LinearArgumentSource? arguments,
        out bool returned,
        out BytecodeVmValue returnValue,
        out bool returnedValue)
    {
        returned = false;
        returnValue = BytecodeVmValue.Nothing;
        returnedValue = false;
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)startAddress > (uint)code.Count ||
            (uint)endAddress > (uint)code.Count ||
            startAddress > endAddress)
        {
            return false;
        }

        var pc = startAddress;
        List<LinearCallFrame>? callFrames = null;
        List<BytecodeVmValue>? stagedArguments = null;
        var randomScopeMark = _randomScopes.Count;
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
                        ref stagedArguments,
                        out returned,
                        out returnValue,
                        out returnedValue))
                {
                    return false;
                }

                var callFrameCountAfter = callFrames?.Count ?? 0;
                if (callFrameCountAfter <= callFrameCountBefore ||
                    instruction.OpCode is not (GameEventScriptBytecodeOpCode.Call or GameEventScriptBytecodeOpCode.CallPredicate))
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
            UnwindRandomScopes(randomScopeMark);
        }
    }

    private bool TryExecuteLinearInstruction(
        GameEventScriptBytecodeInstruction instruction,
        ref int pc,
        ref int endAddress,
        ref LinearArgumentSource? arguments,
        ref List<LinearCallFrame>? callFrames,
        ref List<BytecodeVmValue>? stagedArguments,
        out bool returned,
        out BytecodeVmValue returnValue,
        out bool returnedValue)
    {
        returned = false;
        returnValue = BytecodeVmValue.Nothing;
        returnedValue = false;
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Nop:
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.SlotLocals:
                if (instruction.Count > 0)
                {
                    EnterLinearScope(instruction.Count);
                }
                else if (instruction.Count < 0)
                {
                    ExitLinearScope(-instruction.Count);
                }

                pc++;
                return true;

            case var opCode when IsInlineConstantInstruction(opCode):
                if (!TryLoadInlineConstant(instruction, out var constant) ||
                    !DefineSlot(instruction.DestinationSlot, constant))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.MoveSlot:
                if (!DefineSlot(instruction.DestinationSlot, ResolveSlot(instruction.XSlot)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.StageRegister:
                stagedArguments ??= [];
                stagedArguments.Add(ResolveSlot(instruction.XSlot));
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.StageNothing:
            case GameEventScriptBytecodeOpCode.StageTrue:
            case GameEventScriptBytecodeOpCode.StageFalse:
            case GameEventScriptBytecodeOpCode.StageInteger:
            case GameEventScriptBytecodeOpCode.StageFloat:
            case GameEventScriptBytecodeOpCode.StagePercentage:
            case GameEventScriptBytecodeOpCode.StageText:
            case GameEventScriptBytecodeOpCode.StageTag:
                if (!TryLoadStageConstant(instruction, out var stagedValue))
                {
                    return false;
                }

                stagedArguments ??= [];
                stagedArguments.Add(stagedValue);
                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.CreateVector:
            case GameEventScriptBytecodeOpCode.CreatePoint:
            {
                var spatial = EvaluateStagedSpatialConstructor(
                    instruction.OpCode == GameEventScriptBytecodeOpCode.CreatePoint ? "point" : "vector",
                    instruction.ImmediateX,
                    stagedArguments);
                stagedArguments = null;
                if (!DefineSlot(instruction.DestinationSlot, spatial))
                {
                    return false;
                }

                pc++;
                return true;
            }

            case GameEventScriptBytecodeOpCode.Jump:
                return TryMoveLinearPc(instruction.TargetAddress, endAddress, ref pc);

            case GameEventScriptBytecodeOpCode.JumpIfTrue:
                if (ResolveSlot(instruction.ConditionSlot).IsTrue())
                {
                    return TryMoveLinearPc(instruction.TargetAddress, endAddress, ref pc);
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.JumpIfFalse:
                if (ResolveSlot(instruction.ConditionSlot).IsFalse())
                {
                    return TryMoveLinearPc(instruction.TargetAddress, endAddress, ref pc);
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                if (!ResolveSlot(instruction.ConditionSlot).IsTrue())
                {
                    return TryMoveLinearPc(instruction.TargetAddress, endAddress, ref pc);
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.ReturnVoid:
                returnValue = BytecodeVmValue.Nothing;
                return CompleteLinearReturn(ref pc, ref endAddress, ref arguments, ref callFrames, ref stagedArguments, ref returned, ref returnValue, hasReturnValue: false, out returnedValue);

            case GameEventScriptBytecodeOpCode.ReturnValue:
                returnValue = ResolveSlot(instruction.XSlot);
                return CompleteLinearReturn(ref pc, ref endAddress, ref arguments, ref callFrames, ref stagedArguments, ref returned, ref returnValue, hasReturnValue: true, out returnedValue);

            case GameEventScriptBytecodeOpCode.EmitMessage:
                if (!TryPublishLinearMessage(PublishKind.Emit, instruction.MessageDestination, instruction.ListIndex, tagSlotListIndex: -1))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.EmitMessageWithTags:
                if (!TryPublishLinearMessage(PublishKind.Emit, instruction.MessageDestination, instruction.ListIndex, instruction.SecondaryListIndex))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.PublishMessage:
                if (!TryPublishLinearMessage(PublishKind.Publish, instruction.MessageDestination, instruction.ListIndex, tagSlotListIndex: -1))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.PublishMessageWithTags:
                if (!TryPublishLinearMessage(PublishKind.Publish, instruction.MessageDestination, instruction.ListIndex, instruction.SecondaryListIndex))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.EmitMessageValue:
                if (!TryPublishLinearMessageValue(PublishKind.Emit, ResolveSlot(instruction.XSlot), tagSlotListIndex: -1))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.EmitMessageValueWithTags:
                if (!TryPublishLinearMessageValue(PublishKind.Emit, ResolveSlot(instruction.XSlot), instruction.ListIndex))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.PublishMessageValue:
                if (!TryPublishLinearMessageValue(PublishKind.Publish, ResolveSlot(instruction.XSlot), tagSlotListIndex: -1))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.PublishMessageValueWithTags:
                if (!TryPublishLinearMessageValue(PublishKind.Publish, ResolveSlot(instruction.XSlot), instruction.ListIndex))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.RangeIterator:
                if (!TryCreateRangeIterator(
                        ResolveSlot(instruction.XSlot),
                        ResolveSlot(instruction.YSlot),
                        BytecodeVmValue.Integer(1),
                        out var rangeIterator))
                {
                    return false;
                }

                if (!DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Iterator(rangeIterator)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.RangeIteratorWithStep:
                if (!TryCreateRangeIterator(
                        ResolveSlot(instruction.XSlot),
                        ResolveSlot(instruction.YSlot),
                        ResolveSlot(instruction.AU),
                        out var rangeIteratorWithStep))
                {
                    return false;
                }

                if (!DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Iterator(rangeIteratorWithStep)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.RangeIteratorShort:
                if (!TryCreateRangeIterator(
                        instruction.ImmediateX,
                        instruction.ImmediateY,
                        instruction.AS,
                        out var rangeIteratorShort))
                {
                    return false;
                }

                if (!DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Iterator(rangeIteratorShort)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.CollectionIterator:
                if (!TryCreateCollectionIterator(ResolveSlot(instruction.XSlot), out var collectionIterator) ||
                    !DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Iterator(collectionIterator)))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.StreamNext:
                return TryExecuteIteratorNext(instruction, ref pc);

            case GameEventScriptBytecodeOpCode.StreamClose:
                CloseIterator(ResolveSlot(instruction.XSlot));
                if (!DefineSlot(instruction.XSlot, BytecodeVmValue.Nothing))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.CollectionBuilderList:
                if (!DefineSlot(instruction.DestinationSlot, BytecodeVmValue.CollectionBuilder(BytecodeVmCollectionBuilder.List())))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.CollectionBuilderAdd:
                if (!TryExecuteCollectionBuilderAdd(instruction))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.CollectionBuilderFinish:
                if (!TryExecuteCollectionBuilderFinish(instruction))
                {
                    return false;
                }

                pc++;
                return true;

            case GameEventScriptBytecodeOpCode.Call:
            case GameEventScriptBytecodeOpCode.CallPredicate:
                if (instruction.OpCode == GameEventScriptBytecodeOpCode.CallPredicate)
                {
                    if (!TryExecuteLinearPredicateCallFast(instruction, stagedArguments, out var handledPredicateCallFast))
                    {
                        return false;
                    }

                    if (handledPredicateCallFast)
                    {
                        stagedArguments?.Clear();
                        pc++;
                        return true;
                    }
                }

                return TryEnterLinearCallFrame(
                    instruction,
                    pc,
                    arguments,
                    endAddress,
                    pc + 1,
                    ref callFrames,
                    ref stagedArguments,
                    out arguments,
                    out endAddress,
                    out pc);

            default:
                if (!TryExecuteLinearValueInstruction(instruction))
                {
                    return false;
                }

                if (IsCastInstruction(instruction.OpCode))
                {
                    RecordLinearParameterBoundFromCoerce(arguments, instruction.DestinationSlot, ResolveSlot(instruction.DestinationSlot));
                }

                pc++;
                return true;
        }
    }

    private bool CompleteLinearReturn(
        ref int pc,
        ref int endAddress,
        ref LinearArgumentSource? arguments,
        ref List<LinearCallFrame>? callFrames,
        ref List<BytecodeVmValue>? stagedArguments,
        ref bool returned,
        ref BytecodeVmValue returnValue,
        bool hasReturnValue,
        out bool returnedValue)
    {
        returnedValue = false;
        if (callFrames is null || callFrames.Count == 0)
        {
            returned = true;
            returnedValue = hasReturnValue;
            return true;
        }

        var frame = callFrames[^1];
        callFrames.RemoveAt(callFrames.Count - 1);
        _locals = frame.Locals;
        _assignedSlots = frame.AssignedSlots;
        _activeLocalSlotCount = frame.ActiveLocalSlotCount;
        _changes = frame.Changes;
        _scopeMarks = frame.ScopeMarks;
        _trackedLocalSlotCount = frame.TrackedLocalSlotCount;
        arguments = frame.Arguments;
        stagedArguments = null;
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
    }

    private bool TryEnterLinearCallFrame(
        GameEventScriptBytecodeInstruction instruction,
        int callInstructionAddress,
        LinearArgumentSource? currentArguments,
        int currentEndAddress,
        int returnAddress,
        ref List<LinearCallFrame>? callFrames,
        ref List<BytecodeVmValue>? stagedArguments,
        out LinearArgumentSource? nextArguments,
        out int nextEndAddress,
        out int nextPc)
    {
        nextArguments = currentArguments;
        nextEndAddress = currentEndAddress;
        nextPc = returnAddress;

        var normalizePredicateResult = instruction.OpCode == GameEventScriptBytecodeOpCode.CallPredicate;
        var argumentCount = stagedArguments?.Count ?? 0;
        if (!_compiledScript.LinearExecutable.CallablesByEntryAddress.TryGetValue(instruction.EntryAddress, out var callable) ||
            callable.Parameters.Count != argumentCount)
        {
            return false;
        }

        var argumentValues = argumentCount == 0
            ? []
            : stagedArguments!.ToArray();
        var callArgumentSource = argumentCount == 1
            ? LinearArgumentSource.ForSingleValue(argumentValues[0])
            : LinearArgumentSource.ForValues(argumentValues);
        stagedArguments = null;

        if (!_runtimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            var value = BytecodeVmValue.Nothing;
            if (normalizePredicateResult &&
                !NormalizeExtensionPredicateResult(requirePredicateResult: true, ref value))
            {
                return false;
            }

            return DefineSlot(instruction.DestinationSlot, value);
        }

        if (!RecordLinearCallableCalled(instruction.EntryAddress, callArgumentSource))
        {
            _runtimeBudget.ExitCall();
            return false;
        }

        callFrames ??= [];
        callFrames.Add(new LinearCallFrame(
            _locals,
            _assignedSlots,
            _activeLocalSlotCount,
            _changes,
            _scopeMarks,
            _trackedLocalSlotCount,
            currentArguments,
            currentEndAddress,
            returnAddress,
            instruction.DestinationSlot,
            callInstructionAddress,
            normalizePredicateResult));

        var localSlotCount = GetLinearEntryLocalSlotCount(instruction.EntryAddress);
        var nextFrameSlotCapacity = Math.Max(argumentCount + localSlotCount, _compiledScript.LinearExecutable.MaxFrameSlots);
        _locals = new BytecodeVmValue[nextFrameSlotCapacity];
        _assignedSlots = new bool[nextFrameSlotCapacity];
        _activeLocalSlotCount = argumentCount;
        for (var argumentIndex = 0; argumentIndex < argumentValues.Length; argumentIndex++)
        {
            _locals[argumentIndex] = argumentValues[argumentIndex];
            _assignedSlots[argumentIndex] = true;
        }

        _changes = [];
        _scopeMarks = [];
        _trackedLocalSlotCount = argumentCount;
        nextArguments = callArgumentSource;
        nextEndAddress = _compiledScript.LinearExecutable.Code.Count;
        nextPc = instruction.EntryAddress;
        return true;
    }

    private int GetLinearEntryLocalSlotCount(int entryAddress)
    {
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)entryAddress < (uint)code.Count &&
            code[entryAddress].OpCode == GameEventScriptBytecodeOpCode.SlotLocals)
        {
            var count = code[entryAddress].Count;
            return count < 0 ? 0 : count;
        }

        return 0;
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
            _locals = frame.Locals;
            _assignedSlots = frame.AssignedSlots;
            _activeLocalSlotCount = frame.ActiveLocalSlotCount;
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

    private static bool TrySkipSlotLocals(IReadOnlyList<GameEventScriptBytecodeInstruction> code, ref int pc)
    {
        if ((uint)pc >= (uint)code.Count)
        {
            return false;
        }

        if (code[pc].OpCode == GameEventScriptBytecodeOpCode.SlotLocals)
        {
            pc++;
        }

        return true;
    }

    private bool TryExecuteLinearPredicateCallFast(
        GameEventScriptBytecodeInstruction instruction,
        IReadOnlyList<BytecodeVmValue>? stagedArguments,
        out bool handled)
    {
        handled = false;
        if (_diagnosticsEnabled ||
            stagedArguments is null ||
            stagedArguments.Count != 1 ||
            !CanEvaluateLinearPredicateCallFast(instruction))
        {
            return true;
        }

        handled = true;
        return TryEvaluateLinearPredicateCallFast(instruction, stagedArguments[0], out var value) &&
               DefineSlot(instruction.DestinationSlot, value);
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

    private void RecordLinearParameterInitialized(LinearArgumentSource? arguments, int parameterIndex, int slot)
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
        int entryAddress,
        LinearArgumentSource arguments)
    {
        if (!_diagnosticsEnabled)
        {
            return true;
        }

        if (!_compiledScript.LinearExecutable.CallablesByEntryAddress.TryGetValue(entryAddress, out var callable))
        {
            return true;
        }

        RecordCallableCalled(callable.Name, callable.Kind, callable.Parameters, callable.ParameterTypes, arguments);
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
            case GameEventScriptBytecodeOpCode.Implies:
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
            case GameEventScriptBytecodeOpCode.Default:
            case GameEventScriptBytecodeOpCode.Contains:
            case GameEventScriptBytecodeOpCode.ContainsValue:
            case GameEventScriptBytecodeOpCode.StartsWith:
            case GameEventScriptBytecodeOpCode.EndsWith:
            case GameEventScriptBytecodeOpCode.Intersect:
            case GameEventScriptBytecodeOpCode.Combine:
            case GameEventScriptBytecodeOpCode.Except:
            case GameEventScriptBytecodeOpCode.Zip:
            case GameEventScriptBytecodeOpCode.Min:
            case GameEventScriptBytecodeOpCode.Max:
                return DefineSlot(
                    instruction.DestinationSlot,
                    EvaluateProgramBinary(instruction.OpCode, ResolveSlot(instruction.XSlot), ResolveSlot(instruction.YSlot)));

            case GameEventScriptBytecodeOpCode.UnaryNegate:
            case GameEventScriptBytecodeOpCode.UnaryNot:
            case GameEventScriptBytecodeOpCode.UnaryHasValue:
            case GameEventScriptBytecodeOpCode.UnaryEmpty:
            case GameEventScriptBytecodeOpCode.UnaryLength:
            case GameEventScriptBytecodeOpCode.UnaryChance:
            case GameEventScriptBytecodeOpCode.UnaryKeys:
            case GameEventScriptBytecodeOpCode.UnaryValues:
            case GameEventScriptBytecodeOpCode.UnaryEntries:
            case GameEventScriptBytecodeOpCode.UnaryAbs:
            case GameEventScriptBytecodeOpCode.UnaryNaturalLog:
                return DefineSlot(
                    instruction.DestinationSlot,
                    EvaluateUnaryOperation(instruction.OpCode, ResolveSlot(instruction.XSlot)));

            case GameEventScriptBytecodeOpCode.Clamp:
                return DefineSlot(
                    instruction.DestinationSlot,
                    EvaluateClamp(ResolveSlot(instruction.XSlot), ResolveSlot(instruction.YSlot), ResolveSlot(instruction.AU)));

            case GameEventScriptBytecodeOpCode.Random:
                return DefineSlot(
                    instruction.DestinationSlot,
                    EvaluateRandomExpression(ResolveSlot(instruction.XSlot), ResolveSlot(instruction.YSlot)));

            case GameEventScriptBytecodeOpCode.RandomPush:
                PushRandomScope(ResolveSlot(instruction.XSlot));
                return true;

            case GameEventScriptBytecodeOpCode.RandomPushConstant:
                PushRandomScope(instruction.I64);
                return true;

            case GameEventScriptBytecodeOpCode.RandomPop:
                PopRandomScope();
                return true;

            case GameEventScriptBytecodeOpCode.Range:
                return DefineSlot(
                    instruction.DestinationSlot,
                    EvaluateRangeExpression(ResolveSlot(instruction.XSlot), ResolveSlot(instruction.YSlot), BytecodeVmValue.Integer(1)));

            case GameEventScriptBytecodeOpCode.RangeWithStep:
                return DefineSlot(
                    instruction.DestinationSlot,
                    EvaluateRangeExpression(ResolveSlot(instruction.XSlot), ResolveSlot(instruction.YSlot), ResolveSlot(instruction.AU)));

            case GameEventScriptBytecodeOpCode.Dice:
                return DefineSlot(instruction.DestinationSlot, EvaluateDiceExpression(instruction.Count, instruction.ImmediateY));

            case GameEventScriptBytecodeOpCode.IndexedAccess:
                return DefineSlot(instruction.DestinationSlot, EvaluateIndexedAccess(ResolveSlot(instruction.YSlot), ResolveSlot(instruction.XSlot)));

            case GameEventScriptBytecodeOpCode.MemberAccess:
                if (!TryReadStringPool(instruction.StringIndex, out var member))
                {
                    return false;
                }

                return DefineSlot(instruction.DestinationSlot, EvaluateMemberAccess(ResolveSlot(instruction.YSlot), member));

            case GameEventScriptBytecodeOpCode.PipelineIterator:
                return TryCreatePipelineIterator(instruction);

            case GameEventScriptBytecodeOpCode.PipelineCollectList:
                return TryExecutePipelineCollect(instruction);

            case GameEventScriptBytecodeOpCode.PipelineFirst:
                return TryExecutePipelineElement(instruction, PipelineElementMode.First);

            case GameEventScriptBytecodeOpCode.PipelineLast:
                return TryExecutePipelineElement(instruction, PipelineElementMode.Last);

            case GameEventScriptBytecodeOpCode.PipelineSingle:
                return TryExecutePipelineElement(instruction, PipelineElementMode.Single);

            case GameEventScriptBytecodeOpCode.PipelineHasAny:
                return TryExecutePipelineHasAny(instruction);

            case GameEventScriptBytecodeOpCode.PipelineHasAll:
                return TryExecutePipelineHasAll(instruction);

            case GameEventScriptBytecodeOpCode.StreamReduce:
                return TryExecuteIteratorReduce(instruction, IteratorReduceMode.Reduce);

            case GameEventScriptBytecodeOpCode.StreamReduceOrDefault:
                return TryExecuteIteratorReduce(instruction, IteratorReduceMode.ReduceOrDefault);

            case GameEventScriptBytecodeOpCode.StreamFold:
                return TryExecuteIteratorReduce(instruction, IteratorReduceMode.Fold);

            case GameEventScriptBytecodeOpCode.PipelineContainsSingle:
            case GameEventScriptBytecodeOpCode.PipelineContainsAny:
            case GameEventScriptBytecodeOpCode.PipelineContainsAll:
                return TryExecutePipelineContains(instruction);

            case GameEventScriptBytecodeOpCode.PipelineMap:
            case GameEventScriptBytecodeOpCode.PipelineMapValue:
                return TryExecutePipelineMap(instruction);

            case GameEventScriptBytecodeOpCode.PipelineDistinct:
            case GameEventScriptBytecodeOpCode.PipelineDistinctBy:
                return TryExecutePipelineDistinct(instruction);

            case GameEventScriptBytecodeOpCode.PipelineGroupBy:
                return TryExecutePipelineGroupBy(instruction);

            case GameEventScriptBytecodeOpCode.PipelineReverse:
                return TryExecutePipelineReverse(instruction);

            case GameEventScriptBytecodeOpCode.PipelineSortAscending:
            case GameEventScriptBytecodeOpCode.PipelineSortDescending:
                return TryExecutePipelineSort(instruction);

            case GameEventScriptBytecodeOpCode.PipelineOrderByAscending:
            case GameEventScriptBytecodeOpCode.PipelineOrderByDescending:
                return TryExecutePipelineOrderBy(instruction);

            case GameEventScriptBytecodeOpCode.PipelineTakeFirst:
            case GameEventScriptBytecodeOpCode.PipelineTakeLast:
            case GameEventScriptBytecodeOpCode.PipelineTakeHighest:
            case GameEventScriptBytecodeOpCode.PipelineTakeLowest:
            case GameEventScriptBytecodeOpCode.PipelineDropFirst:
            case GameEventScriptBytecodeOpCode.PipelineDropLast:
            case GameEventScriptBytecodeOpCode.PipelineDropHighest:
            case GameEventScriptBytecodeOpCode.PipelineDropLowest:
                return TryExecutePipelineSequenceSlice(instruction);

            case GameEventScriptBytecodeOpCode.PipelineShuffle:
                return TryExecutePipelineShuffle(instruction);

            case GameEventScriptBytecodeOpCode.PipelineDraw:
                return TryExecutePipelineDraw(instruction);

            case GameEventScriptBytecodeOpCode.PipelineChoose:
            case GameEventScriptBytecodeOpCode.PipelineChooseRandom:
            case GameEventScriptBytecodeOpCode.PipelineChooseWeighted:
                return TryExecutePipelineChoose(instruction);

            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountAny:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternStraight:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternStraight:
                return TryExecutePipelineDicePattern(instruction);

            case GameEventScriptBytecodeOpCode.SeriesTerm:
            case GameEventScriptBytecodeOpCode.SeriesTake:
            case GameEventScriptBytecodeOpCode.SeriesDrop:
                return TryExecuteSeriesPipeline(instruction);
        }

        if (IsCastInstruction(instruction.OpCode))
        {
            return TryEvaluateCastInstruction(instruction, ResolveSlot(instruction.XSlot), out var castValue) &&
                   DefineSlot(instruction.DestinationSlot, castValue);
        }

        if (IsTypeCheckInstruction(instruction.OpCode))
        {
            return TryEvaluateTypeCheckInstruction(instruction, ResolveSlot(instruction.XSlot), out var typeCheckValue) &&
                   DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Boolean(typeCheckValue));
        }

        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.TypeConstructor:
            {
                if (!TryReadStringPool(instruction.StringIndex, out var typeName) ||
                    !TryReadStringList(instruction.ListIndex, out var constructorArgumentNames) ||
                    !TryRentLinearOperands(instruction.AU, out var constructorOperands, out var constructorOperandCount))
                {
                    return false;
                }

                try
                {
                    return DefineSlot(
                        instruction.DestinationSlot,
                        EvaluateTypeConstructor(typeName, constructorArgumentNames, constructorOperands, 0, constructorOperandCount));
                }
                finally
                {
                    ReturnLinearOperands(constructorOperands, constructorOperandCount);
                }
            }

            case GameEventScriptBytecodeOpCode.BuildList:
            {
                if (!TryRentLinearOperands(instruction.ListIndex, out var collectionOperands, out var collectionOperandCount))
                {
                    return false;
                }

                try
                {
                    return DefineSlot(instruction.DestinationSlot, BuildListValue(collectionOperands, 0, collectionOperandCount));
                }
                finally
                {
                    ReturnLinearOperands(collectionOperands, collectionOperandCount);
                }
            }

            case GameEventScriptBytecodeOpCode.BuildMap:
            {
                if (!TryReadStringList(instruction.SecondaryListIndex, out var keys) ||
                    !TryRentLinearOperands(instruction.ListIndex, out var dictionaryOperands, out var dictionaryOperandCount))
                {
                    return false;
                }

                try
                {
                    return DefineSlot(instruction.DestinationSlot, BuildMapValue(dictionaryOperands, 0, dictionaryOperandCount, keys));
                }
                finally
                {
                    ReturnLinearOperands(dictionaryOperands, dictionaryOperandCount);
                }
            }

            case GameEventScriptBytecodeOpCode.LoadMessage:
            {
                if (!TryReadMessageShape(instruction.SecondaryListIndex, out var messageName, out var messageArgumentNames) ||
                    !TryRentLinearOperands(instruction.ListIndex, out var messageOperands, out var messageOperandCount))
                {
                    return false;
                }

                try
                {
                    return DefineSlot(
                        instruction.DestinationSlot,
                        CreateMessageValue(messageOperands, 0, messageOperandCount, messageArgumentNames, messageName));
                }
                finally
                {
                    ReturnLinearOperands(messageOperands, messageOperandCount);
                }
            }

            case GameEventScriptBytecodeOpCode.BindHandler:
            {
                if (!TryRentLinearOperands(instruction.ListIndex, out var bindOperands, out var bindOperandCount))
                {
                    return false;
                }

                try
                {
                    return DefineSlot(
                        instruction.DestinationSlot,
                        BindHandlerValue(ResolveSlot(instruction.XSlot), bindOperands, bindOperandCount));
                }
                finally
                {
                    ReturnLinearOperands(bindOperands, bindOperandCount);
                }
            }

            case GameEventScriptBytecodeOpCode.CallStandard:
            case GameEventScriptBytecodeOpCode.CallStandardPredicate:
            {
                if (!TryCallStandard(
                        instruction.SecondaryListIndex,
                        instruction.ListIndex,
                        instruction.OpCode == GameEventScriptBytecodeOpCode.CallStandardPredicate,
                        out var standardValue))
                {
                    return false;
                }

                return DefineSlot(instruction.DestinationSlot, standardValue);
            }

            case GameEventScriptBytecodeOpCode.CallExternal:
            case GameEventScriptBytecodeOpCode.CallExternalPredicate:
            {
                if (!TryCallExternal(
                        instruction.ExternalReferenceIndex,
                        instruction.ListIndex,
                        instruction.OpCode == GameEventScriptBytecodeOpCode.CallExternalPredicate,
                        out var externalValue))
                {
                    return false;
                }

                return DefineSlot(instruction.DestinationSlot, externalValue);
            }
        }

        return false;
    }

    private bool TryRentLinearOperands(
        int slotListIndex,
        out BytecodeVmValue[] operands,
        out int operandCount)
    {
        if (!TryGetUShortList(slotListIndex, out var slots))
        {
            operands = [];
            operandCount = 0;
            return false;
        }

        operandCount = slots.Count;
        operands = operandCount == 0
            ? Array.Empty<BytecodeVmValue>()
            : ArrayPool<BytecodeVmValue>.Shared.Rent(operandCount);
        if (operandCount > 0)
        {
            CopyLinearOperands(slots, operands);
        }

        return true;
    }

    private static void ReturnLinearOperands(BytecodeVmValue[] operands, int operandCount)
    {
        if (operandCount == 0)
        {
            return;
        }

        Array.Clear(operands, 0, operandCount);
        ArrayPool<BytecodeVmValue>.Shared.Return(operands);
    }

    private bool CanEvaluateLinearProjectionFast(int entryAddress, bool allowPredicateCall)
    {
        if (entryAddress < 0)
        {
            return false;
        }

        var cacheKey = entryAddress * 2 + (allowPredicateCall ? 1 : 0);
        if (_linearProjectionFastSupportCache is not null &&
            _linearProjectionFastSupportCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var supported = CanEvaluateLinearProjectionFastRange(entryAddress, allowPredicateCall);
        _linearProjectionFastSupportCache ??= [];
        _linearProjectionFastSupportCache[cacheKey] = supported;
        return supported;
    }

    private bool CanEvaluateLinearProjectionFastRange(int entryAddress, bool allowPredicateCall)
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
        var stagedArgumentCount = 0;

        for (var pc = entryAddress; pc < code.Count; pc++)
        {
            var instruction = code[pc];
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Nop:
                case GameEventScriptBytecodeOpCode.SlotLocals:
                    break;

                case GameEventScriptBytecodeOpCode.ReturnVoid:
                case GameEventScriptBytecodeOpCode.ReturnValue:
                    return true;

                case GameEventScriptBytecodeOpCode.MoveSlot:
                case var opCode when IsInlineConstantInstruction(opCode):
                    if (!AddTemp(instruction.DestinationSlot))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.StageRegister:
                case GameEventScriptBytecodeOpCode.StageNothing:
                case GameEventScriptBytecodeOpCode.StageTrue:
                case GameEventScriptBytecodeOpCode.StageFalse:
                case GameEventScriptBytecodeOpCode.StageInteger:
                case GameEventScriptBytecodeOpCode.StageFloat:
                case GameEventScriptBytecodeOpCode.StagePercentage:
                case GameEventScriptBytecodeOpCode.StageText:
                case GameEventScriptBytecodeOpCode.StageTag:
                    stagedArgumentCount++;
                    break;

                case var opCode when IsCastInstruction(opCode):
                    if (!AddTemp(instruction.DestinationSlot))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.CallPredicate:
                    if (!allowPredicateCall ||
                        !CanEvaluateLinearPredicateCallFast(instruction) ||
                        stagedArgumentCount != 1 ||
                        !AddTemp(instruction.DestinationSlot))
                    {
                        return false;
                    }

                    stagedArgumentCount = 0;
                    break;

                case GameEventScriptBytecodeOpCode.CallStandard:
                case GameEventScriptBytecodeOpCode.CallStandardPredicate:
                    if (!CanEvaluateLinearStandardCallFast(instruction) ||
                        !AddTemp(instruction.DestinationSlot))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.Jump:
                case GameEventScriptBytecodeOpCode.JumpIfTrue:
                case GameEventScriptBytecodeOpCode.JumpIfFalse:
                case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                    return false;

                default:
                    if (!IsProjectionBinaryOp(instruction.OpCode) ||
                        !AddTemp(instruction.DestinationSlot))
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

    private bool CanEvaluateLinearPredicateCallFast(GameEventScriptBytecodeInstruction instruction)
        => CanEvaluateLinearPredicateCallableFast(instruction.EntryAddress);

    private bool CanEvaluateLinearPredicateCallableFast(int entryAddress)
    {
        var code = _compiledScript.LinearExecutable.Code;
        var pc = entryAddress;
        if ((uint)pc >= (uint)code.Count ||
            !TrySkipSlotLocals(code, ref pc))
        {
            return false;
        }

        const int parameterSlot = 0;
        if ((uint)pc < (uint)code.Count &&
            IsCastInstruction(code[pc].OpCode) &&
            code[pc].XSlot == parameterSlot &&
            code[pc].DestinationSlot == parameterSlot)
        {
            pc++;
        }

        return CanEvaluateLinearProjectionFastRange(pc, allowPredicateCall: false);
    }

    private bool TryEvaluateLinearProjectionFastRange(
        int identifierSlot,
        BytecodeVmValue item,
        int entryAddress,
        out BytecodeVmValue value)
    {
        var success = TryEvaluateLinearProjectionFastRange(
            identifierSlot,
            item,
            entryAddress,
            out _,
            out value);
        return success;
    }

    private bool TryEvaluateLinearProjectionFastRange(
        int identifierSlot,
        BytecodeVmValue item,
        IReadOnlyList<BytecodeVmValue> captures,
        int entryAddress,
        out bool hasValue,
        out BytecodeVmValue value)
    {
        hasValue = false;
        value = BytecodeVmValue.Nothing;
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)entryAddress >= (uint)code.Count)
        {
            return false;
        }

        var projection = new FastProjectionState(this, identifierSlot, item, captures);
        var stagedArgument = BytecodeVmValue.Nothing;
        var hasStagedArgument = false;

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
                case GameEventScriptBytecodeOpCode.SlotLocals:
                    break;

                case GameEventScriptBytecodeOpCode.ReturnVoid:
                    value = BytecodeVmValue.Nothing;
                    hasValue = false;
                    return true;

                case GameEventScriptBytecodeOpCode.ReturnValue:
                    value = projection.GetSlot(instruction.XSlot);
                    hasValue = true;
                    return true;

                case GameEventScriptBytecodeOpCode.MoveSlot:
                    if (!projection.SetTemp(instruction.DestinationSlot, projection.GetSlot(instruction.XSlot)))
                    {
                        return false;
                    }

                    break;

                case var opCode when IsInlineConstantInstruction(opCode):
                    if (!TryLoadInlineConstant(instruction, out var inlineConstant) ||
                        !projection.SetTemp(instruction.DestinationSlot, inlineConstant))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.StageRegister:
                    stagedArgument = projection.GetSlot(instruction.XSlot);
                    hasStagedArgument = true;
                    break;

                case GameEventScriptBytecodeOpCode.StageNothing:
                case GameEventScriptBytecodeOpCode.StageTrue:
                case GameEventScriptBytecodeOpCode.StageFalse:
                case GameEventScriptBytecodeOpCode.StageInteger:
                case GameEventScriptBytecodeOpCode.StageFloat:
                case GameEventScriptBytecodeOpCode.StagePercentage:
                case GameEventScriptBytecodeOpCode.StageText:
                case GameEventScriptBytecodeOpCode.StageTag:
                    if (!TryLoadStageConstant(instruction, out stagedArgument))
                    {
                        return false;
                    }

                    hasStagedArgument = true;
                    break;

                case var opCode when IsCastInstruction(opCode):
                    if (!TryEvaluateCastInstruction(instruction, projection.GetSlot(instruction.XSlot), out var castedValue) ||
                        !projection.SetTemp(instruction.DestinationSlot, castedValue))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.CallPredicate:
                    if (!hasStagedArgument ||
                        !TryEvaluateLinearPredicateCallFast(instruction, stagedArgument, out var predicateValue) ||
                        !projection.SetTemp(instruction.DestinationSlot, predicateValue))
                    {
                        return false;
                    }

                    hasStagedArgument = false;
                    break;

                case GameEventScriptBytecodeOpCode.CallStandard:
                case GameEventScriptBytecodeOpCode.CallStandardPredicate:
                    if (!TryEvaluateLinearStandardCallFast(instruction, ref projection, out var standardValue) ||
                        !projection.SetTemp(instruction.DestinationSlot, standardValue))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.Jump:
                    if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                    {
                        return false;
                    }

                    continue;

                case GameEventScriptBytecodeOpCode.JumpIfTrue:
                    if (projection.GetSlot(instruction.ConditionSlot).IsTrue())
                    {
                        if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                        {
                            return false;
                        }

                        continue;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.JumpIfFalse:
                    if (projection.GetSlot(instruction.ConditionSlot).IsFalse())
                    {
                        if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                        {
                            return false;
                        }

                        continue;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                    if (!projection.GetSlot(instruction.ConditionSlot).IsTrue())
                    {
                        if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                        {
                            return false;
                        }

                        continue;
                    }

                    break;

                default:
                    if (!IsProjectionBinaryOp(instruction.OpCode) ||
                        !projection.SetTemp(
                            instruction.DestinationSlot,
                            EvaluateProjectionBinary(instruction.OpCode, projection.GetSlot(instruction.XSlot), projection.GetSlot(instruction.YSlot))))
                    {
                        return false;
                    }

                    break;
            }
        }

        return false;
    }

    private bool TryEvaluateLinearProjectionFastRange(
        int identifierSlot,
        BytecodeVmValue item,
        int entryAddress,
        out bool hasValue,
        out BytecodeVmValue value)
    {
        hasValue = false;
        value = BytecodeVmValue.Nothing;
        var code = _compiledScript.LinearExecutable.Code;
        if ((uint)entryAddress >= (uint)code.Count)
        {
            return false;
        }

        var projection = new FastProjectionState(this, identifierSlot, item);
        var stagedArgument = BytecodeVmValue.Nothing;
        var hasStagedArgument = false;

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
                case GameEventScriptBytecodeOpCode.SlotLocals:
                    break;

                case GameEventScriptBytecodeOpCode.ReturnVoid:
                    value = BytecodeVmValue.Nothing;
                    hasValue = false;
                    return true;

                case GameEventScriptBytecodeOpCode.ReturnValue:
                    value = projection.GetSlot(instruction.XSlot);
                    hasValue = true;
                    return true;

                case GameEventScriptBytecodeOpCode.MoveSlot:
                    if (!projection.SetTemp(instruction.DestinationSlot, projection.GetSlot(instruction.XSlot)))
                    {
                        return false;
                    }

                    break;

                case var opCode when IsInlineConstantInstruction(opCode):
                    if (!TryLoadInlineConstant(instruction, out var inlineConstant) ||
                        !projection.SetTemp(instruction.DestinationSlot, inlineConstant))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.StageRegister:
                    stagedArgument = projection.GetSlot(instruction.XSlot);
                    hasStagedArgument = true;
                    break;

                case GameEventScriptBytecodeOpCode.StageNothing:
                case GameEventScriptBytecodeOpCode.StageTrue:
                case GameEventScriptBytecodeOpCode.StageFalse:
                case GameEventScriptBytecodeOpCode.StageInteger:
                case GameEventScriptBytecodeOpCode.StageFloat:
                case GameEventScriptBytecodeOpCode.StagePercentage:
                case GameEventScriptBytecodeOpCode.StageText:
                case GameEventScriptBytecodeOpCode.StageTag:
                    if (!TryLoadStageConstant(instruction, out stagedArgument))
                    {
                        return false;
                    }

                    hasStagedArgument = true;
                    break;

                case var opCode when IsCastInstruction(opCode):
                    if (!TryEvaluateCastInstruction(instruction, projection.GetSlot(instruction.XSlot), out var castedValue) ||
                        !projection.SetTemp(instruction.DestinationSlot, castedValue))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.CallPredicate:
                    if (!hasStagedArgument ||
                        !TryEvaluateLinearPredicateCallFast(instruction, stagedArgument, out var predicateValue) ||
                        !projection.SetTemp(instruction.DestinationSlot, predicateValue))
                    {
                        return false;
                    }

                    hasStagedArgument = false;
                    break;

                case GameEventScriptBytecodeOpCode.CallStandard:
                case GameEventScriptBytecodeOpCode.CallStandardPredicate:
                    if (!TryEvaluateLinearStandardCallFast(instruction, ref projection, out var standardValue) ||
                        !projection.SetTemp(instruction.DestinationSlot, standardValue))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.Jump:
                    if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                    {
                        return false;
                    }

                    continue;

                case GameEventScriptBytecodeOpCode.JumpIfTrue:
                    if (projection.GetSlot(instruction.ConditionSlot).IsTrue())
                    {
                        if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                        {
                            return false;
                        }

                        continue;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.JumpIfFalse:
                    if (projection.GetSlot(instruction.ConditionSlot).IsFalse())
                    {
                        if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                        {
                            return false;
                        }

                        continue;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                    if (!projection.GetSlot(instruction.ConditionSlot).IsTrue())
                    {
                        if (!TryMoveProjectionPc(instruction.TargetAddress, entryAddress, code.Count, ref pc))
                        {
                            return false;
                        }

                        continue;
                    }

                    break;

                default:
                    if (!IsProjectionBinaryOp(instruction.OpCode) ||
                        !projection.SetTemp(
                            instruction.DestinationSlot,
                            EvaluateProjectionBinary(instruction.OpCode, projection.GetSlot(instruction.XSlot), projection.GetSlot(instruction.YSlot))))
                    {
                        return false;
                    }

                    break;
            }
        }

        value = BytecodeVmValue.Nothing;
        return false;
    }

    private static bool TryMoveProjectionPc(int target, int entryAddress, int codeCount, ref int pc)
    {
        if (target < entryAddress || target >= codeCount)
        {
            return false;
        }

        pc = target - 1;
        return true;
    }

    private bool CanEvaluateLinearStandardCallFast(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetUShortList(instruction.SecondaryListIndex, out var shape) ||
            !TryGetUShortList(instruction.ListIndex, out var argumentSlots))
        {
            return false;
        }

        return shape.Count >= 2 &&
               argumentSlots.Count == shape.Count - 2 &&
               argumentSlots.Count <= 2;
    }

    private bool TryEvaluateLinearStandardCallFast(
        GameEventScriptBytecodeInstruction instruction,
        ref FastProjectionState projection,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryGetUShortList(instruction.SecondaryListIndex, out var shape) ||
            !TryGetUShortList(instruction.ListIndex, out var argumentSlots) ||
            argumentSlots.Count != shape.Count - 2 ||
            argumentSlots.Count > 2)
        {
            return false;
        }

        var argumentCount = argumentSlots.Count;
        var argument0 = argumentCount > 0
            ? ToGameEventScriptFastValue(projection.GetSlot(argumentSlots[0]))
            : default;
        var argument1 = argumentCount > 1
            ? ToGameEventScriptFastValue(projection.GetSlot(argumentSlots[1]))
            : default;
        if (!GesStandardExtensions.TryInvoke(_compiledScript.BytecodeModule.StringPool, shape, argument0, argument1, argumentCount, out var standardValue))
        {
            return false;
        }

        value = BytecodeVmValue.FromGameEventScriptFastValue(standardValue);
        return NormalizeExtensionPredicateResult(
            instruction.OpCode == GameEventScriptBytecodeOpCode.CallStandardPredicate,
            ref value);
    }

    private bool TryEvaluateLinearPredicateCallFast(
        GameEventScriptBytecodeInstruction instruction,
        BytecodeVmValue input,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var entryAddress = instruction.EntryAddress;

        if (!_runtimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            return NormalizeExtensionPredicateResult(requirePredicateResult: true, ref value);
        }

        try
        {
            if (_diagnosticsEnabled &&
                _compiledScript.LinearExecutable.CallablesByEntryAddress.TryGetValue(entryAddress, out var callable))
            {
                var diagnosticInput = input;
                if (!TryConvertParameterType(callable.ParameterTypes, 0, input, out diagnosticInput))
                {
                    return false;
                }

                var argumentName = callable.Parameters.Count > 0 ? callable.Parameters[0] : "value";
                RecordPredicateCalled(callable.Name, argumentName, diagnosticInput);
            }

            if (!TryEvaluateLinearPredicateCallableBodyFast(entryAddress, input, out value))
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
        int entryAddress,
        BytecodeVmValue input,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var code = _compiledScript.LinearExecutable.Code;
        var pc = entryAddress;
        if ((uint)pc >= (uint)code.Count ||
            !TrySkipSlotLocals(code, ref pc))
        {
            return false;
        }

        const int parameterSlot = 0;
        var parameterValue = input;
        if ((uint)pc < (uint)code.Count &&
            IsCastInstruction(code[pc].OpCode) &&
            code[pc].XSlot == parameterSlot &&
            code[pc].DestinationSlot == parameterSlot)
        {
            if (!TryConsumeExecutionStep("Linear instruction execution budget exhausted."))
            {
                return true;
            }

            if (!TryEvaluateCastInstruction(code[pc], parameterValue, out parameterValue))
            {
                return false;
            }

            pc++;
        }

        return TryEvaluateLinearProjectionFastRange(parameterSlot, parameterValue, pc, out value);
    }

    private bool TryEvaluateLinearHelperExpression(int entryAddress, out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if ((uint)entryAddress >= (uint)_compiledScript.LinearExecutable.Code.Count)
        {
            return false;
        }

        var previousActiveLocalSlotCount = _activeLocalSlotCount;
        try
        {
            return TryExecuteLinearRange(
                       entryAddress,
                       _compiledScript.LinearExecutable.Code.Count,
                       null,
                       out var returned,
                       out value) &&
                       returned;
        }
        finally
        {
            ClearLocalSlots(previousActiveLocalSlotCount);
        }
    }

    private bool TryEvaluateLinearHelperEntry(int entryAddress, out bool hasValue, out BytecodeVmValue value)
    {
        hasValue = false;
        value = BytecodeVmValue.Nothing;
        if ((uint)entryAddress >= (uint)_compiledScript.LinearExecutable.Code.Count)
        {
            return false;
        }

        var previousActiveLocalSlotCount = _activeLocalSlotCount;
        try
        {
            if (!TryExecuteLinearRange(
                    entryAddress,
                    _compiledScript.LinearExecutable.Code.Count,
                    null,
                    out var returned,
                    out value,
                    out hasValue))
            {
                return false;
            }

            return returned;
        }
        finally
        {
            ClearLocalSlots(previousActiveLocalSlotCount);
        }
    }

    private bool TryEvaluateLinearIsolatedHelperEntry(
        int entryAddress,
        BytecodeVmValue item,
        IReadOnlyList<BytecodeVmValue> captures,
        out bool hasValue,
        out BytecodeVmValue value)
    {
        hasValue = false;
        value = BytecodeVmValue.Nothing;
        if ((uint)entryAddress >= (uint)_compiledScript.LinearExecutable.Code.Count)
        {
            return false;
        }

        var initialSlotCount = captures.Count + 1;
        var slotCount = initialSlotCount + GetLinearEntryLocalSlotCount(entryAddress);
        if (initialSlotCount > slotCount)
        {
            return false;
        }

        var previousLocals = _locals;
        var previousAssignedSlots = _assignedSlots;
        var previousActiveLocalSlotCount = _activeLocalSlotCount;
        var previousChanges = _changes;
        var previousScopeMarks = _scopeMarks;
        var previousTrackedLocalSlotCount = _trackedLocalSlotCount;

        _locals = new BytecodeVmValue[slotCount];
        _assignedSlots = new bool[slotCount];
        _activeLocalSlotCount = initialSlotCount;
        _locals[0] = item;
        _assignedSlots[0] = true;
        for (var index = 0; index < captures.Count; index++)
        {
            var slot = index + 1;
            _locals[slot] = captures[index];
            _assignedSlots[slot] = true;
        }

        _changes = [];
        _scopeMarks = [];
        _trackedLocalSlotCount = initialSlotCount;
        try
        {
            if (!TryExecuteLinearRange(
                    entryAddress,
                    _compiledScript.LinearExecutable.Code.Count,
                    null,
                    out var returned,
                    out value,
                    out hasValue))
            {
                return false;
            }

            return returned;
        }
        finally
        {
            _locals = previousLocals;
            _assignedSlots = previousAssignedSlots;
            _activeLocalSlotCount = previousActiveLocalSlotCount;
            _changes = previousChanges;
            _scopeMarks = previousScopeMarks;
            _trackedLocalSlotCount = previousTrackedLocalSlotCount;
        }
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

    private BytecodeVmValue[] CopyLinearOperands(IReadOnlyList<ushort> slots)
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

    private void CopyLinearOperands(IReadOnlyList<ushort> slots, BytecodeVmValue[] operands)
    {
        for (var index = 0; index < slots.Count; index++)
        {
            operands[index] = ResolveSlot(slots[index]);
        }
    }

    private bool TryPublishLinearMessage(
        PublishKind publishKind,
        int messageShapeListIndex,
        int argumentSlotListIndex,
        int tagSlotListIndex)
    {
        if (!TryReadMessageShape(messageShapeListIndex, out var messageName, out var argumentNames) ||
            !TryGetUShortListOrEmpty(argumentSlotListIndex, out var argumentSlots) ||
            argumentNames.Length != argumentSlots.Count)
        {
            return false;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentNames.Length];
        for (var argumentIndex = 0; argumentIndex < pairs.Length; argumentIndex++)
        {
            var argumentName = argumentNames[argumentIndex];
            var value = ResolveSlot(argumentSlots[argumentIndex]);
            RecordPublishArgumentEvaluatedToNothing(argumentName, value);
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                argumentName,
                value.ToGameEventScriptValue());
        }

        var signatureId = GameEventScriptMessageSignature.CreateSignatureId(messageName, argumentNames);
        var message = GameEventScriptMessage.CreatePrecomputed(
            messageName,
            pairs.Length == 0
                ? GameEventScriptNamedArguments.Empty
                : GameEventScriptNamedArguments.CreateOrdered(pairs),
            signatureId);
        PublishMessage(publishKind, ApplyLinearTags(message, tagSlotListIndex));
        return true;
    }

    private bool TryPublishLinearMessageValue(
        PublishKind publishKind,
        BytecodeVmValue messageValue,
        int tagSlotListIndex)
    {
        var boxed = messageValue.ToGameEventScriptValue();
        if (!GesMessageValueCodec.TryReadMessageValue(boxed, out var message))
        {
            return true;
        }

        PublishMessage(publishKind, ApplyLinearTags(message, tagSlotListIndex));
        return true;
    }

    private GameEventScriptMessage ApplyLinearTags(GameEventScriptMessage message, int tagSlotListIndex)
    {
        if (!TryGetUShortListOrEmpty(tagSlotListIndex, out var tagSlots))
        {
            return message;
        }

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

    private bool TryCreateRangeIterator(BytecodeVmValue fromValue, BytecodeVmValue toValue, BytecodeVmValue stepValue, out BytecodeVmIterator iterator)
    {
        if (!fromValue.TryGetRangeInteger(out var from) ||
            !toValue.TryGetRangeInteger(out var to) ||
            !stepValue.TryGetRangeInteger(out var step))
        {
            iterator = BytecodeVmIterator.Empty;
            return false;
        }

        return TryCreateRangeIterator(from, to, step, out iterator);
    }

    private bool TryCreateRangeIterator(long from, long to, long step, out BytecodeVmIterator iterator)
    {
        if (step == 0)
        {
            iterator = BytecodeVmIterator.Empty;
            return true;
        }

        var length = GesRuntimeLimitUtilities.GetRangeLength(from, to, step);
        if (!_runtimeBudget.TryCheckRangeLength(length, "For loop range would enumerate more range items than allowed."))
        {
            iterator = BytecodeVmIterator.Empty;
            return true;
        }

        iterator = new BytecodeVmRangeIterator(from, to, step);
        return true;
    }

    private bool TryCreateCollectionIterator(BytecodeVmValue source, out BytecodeVmIterator iterator)
    {
        var sourceValue = source.ToGameEventScriptValue();
        iterator = new BytecodeVmCollectionIterator(sourceValue, sourceValue.AsEnumerable().GetEnumerator());
        return true;
    }

    private bool TryCreatePipelineIterator(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetIterator(instruction.XSlot, out var sourceIterator) ||
            !TryGetUShortList(instruction.BU, out var captureSlots))
        {
            return false;
        }

        var captures = captureSlots.Count == 0
            ? Array.Empty<BytecodeVmValue>()
            : new BytecodeVmValue[captureSlots.Count];
        for (var index = 0; index < captureSlots.Count; index++)
        {
            captures[index] = ResolveSlot(captureSlots[index]);
        }

        return DefineSlot(
            instruction.DestinationSlot,
            BytecodeVmValue.Iterator(new BytecodeVmPipelineIterator(
                this,
                sourceIterator,
                instruction.EntryAddress,
                instruction.AU,
                captures)));
    }

    private bool TryGetIterator(int slot, out BytecodeVmIterator iterator)
    {
        var iteratorValue = ResolveSlot(slot);
        if (iteratorValue.Kind == BytecodeVmValueKind.Iterator &&
            iteratorValue.IteratorValue is { } resolved)
        {
            iterator = resolved;
            return true;
        }

        iterator = BytecodeVmIterator.Empty;
        return false;
    }

    internal bool TryEvaluatePipelineIteratorEntry(
        int entryAddress,
        int itemSlot,
        BytecodeVmValue item,
        IReadOnlyList<BytecodeVmValue> captures,
        out bool yielded,
        out BytecodeVmValue value)
    {
        yielded = false;
        value = BytecodeVmValue.Nothing;
        if (itemSlot != 0)
        {
            return false;
        }

        if (!_diagnosticsEnabled &&
            _runtimeBudget.Limits.MaxExecutionSteps <= 0 &&
            CanEvaluateLinearProjectionFast(entryAddress, allowPredicateCall: true))
        {
            return TryEvaluateLinearProjectionFastRange(itemSlot, item, captures, entryAddress, out yielded, out value);
        }

        return TryEvaluateLinearIsolatedHelperEntry(entryAddress, item, captures, out yielded, out value);
    }

    internal bool TryEvaluatePipelineIteratorEntry(
        int entryAddress,
        int itemSlot,
        BytecodeVmValue item,
        out bool yielded,
        out BytecodeVmValue value)
    {
        yielded = false;
        value = BytecodeVmValue.Nothing;
        if ((uint)itemSlot >= (uint)_locals.Length)
        {
            return false;
        }

        var hadValue = _assignedSlots[itemSlot];
        var previous = _locals[itemSlot];
        _locals[itemSlot] = item;
        _assignedSlots[itemSlot] = true;
        try
        {
            if (!_diagnosticsEnabled &&
                _runtimeBudget.Limits.MaxExecutionSteps <= 0 &&
                CanEvaluateLinearProjectionFast(entryAddress, allowPredicateCall: true))
            {
                return TryEvaluateLinearProjectionFastRange(itemSlot, item, entryAddress, out yielded, out value);
            }

            return TryEvaluateLinearHelperEntry(entryAddress, out yielded, out value);
        }
        finally
        {
            RestoreSlot(itemSlot, hadValue, previous);
        }
    }

    private bool TryExecuteIteratorNext(GameEventScriptBytecodeInstruction instruction, ref int pc)
    {
        var iteratorValue = ResolveSlot(instruction.XSlot);
        if (iteratorValue.Kind != BytecodeVmValueKind.Iterator ||
            iteratorValue.IteratorValue is not { } iterator)
        {
            return false;
        }

        if (!iterator.TryMoveNext(out var item))
        {
            pc = instruction.TargetAddress;
            return true;
        }

        if (!_runtimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
        {
            _halted = true;
            pc = instruction.TargetAddress;
            return true;
        }

        if (!DefineSlot(instruction.DestinationSlot, item))
        {
            return false;
        }

        pc++;
        return true;
    }

    private bool TryExecuteCollectionBuilderAdd(GameEventScriptBytecodeInstruction instruction)
    {
        var builderValue = ResolveSlot(instruction.XSlot);
        if (builderValue.Kind != BytecodeVmValueKind.CollectionBuilder ||
            builderValue.CollectionBuilderValue is not { } builder)
        {
            return false;
        }

        if (!_runtimeBudget.TryCheckGeneratedCollectionItemCount(
                builder.Count + 1,
                "Generated collection item count exceeds the configured limit."))
        {
            return true;
        }

        builder.Add(ResolveSlot(instruction.YSlot));
        return true;
    }

    private bool TryExecuteCollectionBuilderFinish(GameEventScriptBytecodeInstruction instruction)
    {
        var builderValue = ResolveSlot(instruction.XSlot);
        if (builderValue.Kind != BytecodeVmValueKind.CollectionBuilder ||
            builderValue.CollectionBuilderValue is not { } builder)
        {
            return false;
        }

        return DefineSlot(instruction.DestinationSlot, builder.Finish());
    }

    private bool TryMaterializeIterator(
        int iteratorSlot,
        out BytecodeVmIterator iterator,
        out GameEventScriptValue target,
        out List<GameEventScriptValue> items)
    {
        items = [];
        target = GameEventScriptListValue.Empty;
        if (!TryGetIterator(iteratorSlot, out iterator))
        {
            return false;
        }

        target = iterator.EffectiveTarget;
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return true;
        }

        while (iterator.TryMoveNext(out var item))
        {
            items.Add(item.ToGameEventScriptValue());
        }

        return true;
    }

    private bool TryExecutePipelineCollect(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out _, out var items))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        iterator.Dispose();
        return DefineSlot(
            instruction.DestinationSlot,
            BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(items)));
    }

    private bool TryExecutePipelineElement(GameEventScriptBytecodeInstruction instruction, PipelineElementMode mode)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        try
        {
            var first = BytecodeVmValue.Nothing;
            var last = BytecodeVmValue.Nothing;
            var count = 0;
            while (iterator.TryMoveNext(out var item))
            {
                if (count == 0)
                {
                    first = item;
                }

                last = item;
                count++;
                if (mode == PipelineElementMode.First)
                {
                    break;
                }
            }

            return DefineSlot(
                instruction.DestinationSlot,
                mode switch
                {
                    PipelineElementMode.First => count > 0 ? first : BytecodeVmValue.Nothing,
                    PipelineElementMode.Last => count > 0 ? last : BytecodeVmValue.Nothing,
                    PipelineElementMode.Single => count == 1 ? first : BytecodeVmValue.Nothing,
                    _ => BytecodeVmValue.Nothing
                });
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecutePipelineHasAny(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        try
        {
            while (iterator.TryMoveNext(out var item))
            {
                if (item.IsTrue())
                {
                    return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Boolean(true));
                }
            }

            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Boolean(false));
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecutePipelineHasAll(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        try
        {
            while (iterator.TryMoveNext(out var item))
            {
                if (!item.IsTrue())
                {
                    return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Boolean(false));
                }
            }

            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Boolean(true));
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecuteIteratorReduce(GameEventScriptBytecodeInstruction instruction, IteratorReduceMode mode)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        var itemSlot = mode == IteratorReduceMode.Reduce
            ? instruction.YSlot
            : instruction.AU;
        var reducerEntry = mode == IteratorReduceMode.Reduce
            ? instruction.AU
            : instruction.BU;

        try
        {
            BytecodeVmValue accumulator;
            if (mode == IteratorReduceMode.Fold)
            {
                accumulator = ResolveSlot(instruction.YSlot);
            }
            else if (iterator.TryMoveNext(out var first))
            {
                accumulator = first;
            }
            else
            {
                accumulator = mode == IteratorReduceMode.ReduceOrDefault
                    ? ResolveSlot(instruction.YSlot)
                    : BytecodeVmValue.Nothing;
                return DefineSlot(instruction.DestinationSlot, accumulator);
            }

            while (iterator.TryMoveNext(out var item))
            {
                if (!DefineSlot(instruction.DestinationSlot, accumulator))
                {
                    return false;
                }

                if (!TryEvaluatePipelineIteratorEntry(reducerEntry, itemSlot, item, out var hasValue, out var reduced))
                {
                    return false;
                }

                accumulator = hasValue ? reduced : BytecodeVmValue.Nothing;
            }

            return DefineSlot(instruction.DestinationSlot, accumulator);
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecutePipelineContains(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out var target, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        if (target.IsNothing() || target.Kind == GameEventScriptValueKind.List && ReferenceEquals(target, GameEventScriptListValue.Empty))
        {
            target = GameEventScriptValueFactory.GesList(items);
        }

        var needle = ResolveSlot(instruction.YSlot).ToGameEventScriptValue();
        var result = instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.PipelineContainsAll => EnumerateListLikeValue(needle).All(target.Contains),
            GameEventScriptBytecodeOpCode.PipelineContainsAny => EnumerateListLikeValue(needle).Any(target.Contains),
            _ => target.Contains(needle)
        };
        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Boolean(result));
    }

    private bool TryEvaluatePipelineEntryValue(int entryAddress, int itemSlot, BytecodeVmValue item, out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryEvaluatePipelineIteratorEntry(entryAddress, itemSlot, item, out var hasValue, out value))
        {
            return false;
        }

        if (!hasValue)
        {
            value = BytecodeVmValue.Nothing;
        }

        return true;
    }

    private bool TryExecutePipelineMap(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        try
        {
            var result = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
            while (iterator.TryMoveNext(out var item))
            {
                if (!TryEvaluatePipelineEntryValue(instruction.AU, instruction.YSlot, item, out var keyValue))
                {
                    return false;
                }

                var key = keyValue.ToGameEventScriptValue().AsText();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineMapValue)
                {
                    if (!TryEvaluatePipelineEntryValue(instruction.BU, instruction.YSlot, item, out var projectedValue))
                    {
                        return false;
                    }

                    result[key] = projectedValue.ToGameEventScriptValue();
                }
                else
                {
                    result[key] = item.ToGameEventScriptValue();
                }
            }

            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Reference(GameEventScriptValueFactory.GesMap(result)));
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecutePipelineDistinct(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        try
        {
            var target = iterator.EffectiveTarget;
            var distinctItems = new List<GameEventScriptValue>();
            var seenKeys = new HashSet<GameEventScriptValue>();
            while (iterator.TryMoveNext(out var item))
            {
                GameEventScriptValue key;
                if (instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineDistinctBy)
                {
                    if (!TryEvaluatePipelineEntryValue(instruction.AU, instruction.YSlot, item, out var keyValue))
                    {
                        return false;
                    }

                    key = keyValue.ToGameEventScriptValue();
                }
                else
                {
                    key = item.ToGameEventScriptValue();
                }

                if (seenKeys.Add(key))
                {
                    distinctItems.Add(item.ToGameEventScriptValue());
                }
            }

            return DefineSlot(
                instruction.DestinationSlot,
                BytecodeVmValue.FromGameEventScriptValue(MaterializeDistinctItems(target, distinctItems)));
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecutePipelineGroupBy(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        try
        {
            var groups = new Dictionary<string, List<GameEventScriptValue>>(StringComparer.Ordinal);
            while (iterator.TryMoveNext(out var item))
            {
                if (!TryEvaluatePipelineEntryValue(instruction.AU, instruction.YSlot, item, out var keyValue))
                {
                    return false;
                }

                var key = keyValue.ToGameEventScriptValue().AsText();
                if (!groups.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    groups[key] = bucket;
                }

                bucket.Add(item.ToGameEventScriptValue());
            }

            return DefineSlot(
                instruction.DestinationSlot,
                BytecodeVmValue.Reference(GameEventScriptValueFactory.GesMap(groups.ToDictionary(
                    pair => pair.Key,
                    pair => GameEventScriptValueFactory.GesList(pair.Value),
                    StringComparer.Ordinal))));
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecutePipelineReverse(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out var target, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.FromGameEventScriptValue(EvaluateReverseSelector(target, items)));
    }

    private bool TryExecutePipelineSort(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out var target, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        return DefineSlot(
            instruction.DestinationSlot,
            BytecodeVmValue.FromGameEventScriptValue(GesCollectionOperations.Sort(
                target,
                items,
                instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineSortDescending ? "descending" : "ascending")));
    }

    private bool TryExecutePipelineOrderBy(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryGetIterator(instruction.XSlot, out var iterator))
        {
            return false;
        }

        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            iterator.Dispose();
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        try
        {
            var target = iterator.EffectiveTarget;
            var pairs = new List<(GameEventScriptValue Item, GameEventScriptValue Key)>();
            while (iterator.TryMoveNext(out var item))
            {
                if (!TryEvaluatePipelineEntryValue(instruction.AU, instruction.YSlot, item, out var key))
                {
                    return false;
                }

                pairs.Add((item.ToGameEventScriptValue(), key.ToGameEventScriptValue()));
            }

            var comparer = instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineOrderByDescending
                ? Comparer<GameEventScriptValue>.Create((left, right) => GameEventScriptValue.StableComparer.Compare(right, left))
                : GameEventScriptValue.StableComparer;
            var ordered = pairs.OrderBy(pair => pair.Key, comparer).Select(pair => pair.Item).ToArray();
            return DefineSlot(
                instruction.DestinationSlot,
                BytecodeVmValue.FromGameEventScriptValue(MaterializeOrderedItems(target, ordered)));
        }
        finally
        {
            iterator.Dispose();
        }
    }

    private bool TryExecutePipelineSequenceSlice(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out var target, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        var (operation, scope) = instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.PipelineTakeLast => ("take", "last"),
            GameEventScriptBytecodeOpCode.PipelineTakeHighest => ("take", "highest"),
            GameEventScriptBytecodeOpCode.PipelineTakeLowest => ("take", "lowest"),
            GameEventScriptBytecodeOpCode.PipelineDropFirst => ("drop", "first"),
            GameEventScriptBytecodeOpCode.PipelineDropLast => ("drop", "last"),
            GameEventScriptBytecodeOpCode.PipelineDropHighest => ("drop", "highest"),
            GameEventScriptBytecodeOpCode.PipelineDropLowest => ("drop", "lowest"),
            _ => ("take", "first")
        };
        return DefineSlot(
            instruction.DestinationSlot,
            BytecodeVmValue.FromGameEventScriptValue(EvaluateSequenceSliceSelector(target, items, operation, scope, instruction.ImmediateY)));
    }

    private bool TryExecutePipelineShuffle(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out var target, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.FromGameEventScriptValue(EvaluateShuffleSelector(target, items)));
    }

    private bool TryExecutePipelineDraw(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out var target, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.FromGameEventScriptValue(EvaluateDrawSelector(target, items, instruction.ImmediateY)));
    }

    private bool TryExecutePipelineChoose(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out _, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        var count = instruction.ImmediateY;
        IReadOnlyList<GameEventScriptValue> chosen;
        if (instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineChooseWeighted)
        {
            if (!TryChooseWeightedPipelineItems(items, count, instruction.AU, instruction.BU, out chosen))
            {
                return false;
            }
        }
        else if (instruction.OpCode == GameEventScriptBytecodeOpCode.PipelineChooseRandom)
        {
            chosen = ChooseRandomItems(items, count);
        }
        else
        {
            chosen = items.Take(count).ToArray();
        }

        var value = count == 1
            ? chosen.Count == 0
                ? BytecodeVmValue.Nothing
                : BytecodeVmValue.FromGameEventScriptValue(chosen[0])
            : BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(chosen));
        return DefineSlot(instruction.DestinationSlot, value);
    }

    private bool TryChooseWeightedPipelineItems(
        IReadOnlyList<GameEventScriptValue> candidates,
        int count,
        int itemSlot,
        int weightEntryAddress,
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
                if (!TryEvaluatePipelineEntryValue(weightEntryAddress, itemSlot, BytecodeVmValue.FromGameEventScriptValue(candidate), out var weightValue))
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
        if (!GesValueOperations.TryCoerceNumericForOperation(value, out var number) ||
            !number.IsFinite ||
            number.Value <= 0d)
        {
            return 0d;
        }

        return number.Value;
    }

    private bool TryExecutePipelineDicePattern(GameEventScriptBytecodeInstruction instruction)
    {
        if (!TryMaterializeIterator(instruction.XSlot, out var iterator, out var target, out var items))
        {
            return false;
        }

        iterator.Dispose();
        if (iterator.SourceTarget.Kind == GameEventScriptValueKind.Series)
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        if (!IsPatternSequence(target))
        {
            var isTakePattern = instruction.OpCode is
                GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny or
                GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace or
                GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse or
                GameEventScriptBytecodeOpCode.PipelineTakePatternStraight;
            return DefineSlot(instruction.DestinationSlot, isTakePattern ? BytecodeVmValue.Nothing : BytecodeVmValue.Boolean(false));
        }

        var counts = items.GroupBy(item => item).ToDictionary(group => group.Key, group => group.Count());
        var isTake = instruction.OpCode is
            GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny or
            GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace or
            GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse or
            GameEventScriptBytecodeOpCode.PipelineTakePatternStraight;

        if (!isTake)
        {
            var matches = instruction.OpCode switch
            {
                GameEventScriptBytecodeOpCode.PipelineDicePatternCountAny => counts.Values.Any(count => count >= instruction.ImmediateY),
                GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace => TryEvaluatePatternFaceCount(counts, instruction.AU, instruction.ImmediateY, out var faceMatches) && faceMatches,
                GameEventScriptBytecodeOpCode.PipelineDicePatternFullHouse => counts.Count == 2 && counts.Values.OrderByDescending(x => x).SequenceEqual(new[] { 3, 2 }),
                GameEventScriptBytecodeOpCode.PipelineDicePatternStraight => MatchStraight(items),
                _ => false
            };
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Boolean(matches));
        }

        if (!TryTakePipelinePatternItems(instruction, items, counts, out var takenItems))
        {
            return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
        }

        var takenValue = target.Kind == GameEventScriptValueKind.Dice
            ? GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Create(takenItems.Select(item => (int)item.AsInteger())))
            : GameEventScriptValueFactory.GesList(takenItems);
        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.FromGameEventScriptValue(takenValue));
    }

    private bool TryEvaluatePatternFaceCount(
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        int faceEntryAddress,
        int requiredCount,
        out bool matches)
    {
        matches = false;
        if (!TryEvaluateLinearHelperExpression(faceEntryAddress, out var face))
        {
            return false;
        }

        matches = counts.TryGetValue(face.ToGameEventScriptValue(), out var count) && count >= requiredCount;
        return true;
    }

    private bool TryTakePipelinePatternItems(
        GameEventScriptBytecodeInstruction instruction,
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace:
                if (!TryEvaluateLinearHelperExpression(instruction.AU, out var face))
                {
                    takenItems = [];
                    return false;
                }

                var boxedFace = face.ToGameEventScriptValue();
                if (counts.TryGetValue(boxedFace, out var faceCount) && faceCount >= instruction.ImmediateY)
                {
                    takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int> { [boxedFace] = instruction.ImmediateY });
                    return true;
                }

                takenItems = [];
                return false;

            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny:
                foreach (var candidate in EnumerateDistinctInSourceOrder(items))
                {
                    if (counts.TryGetValue(candidate, out var candidateCount) && candidateCount >= instruction.ImmediateY)
                    {
                        takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int> { [candidate] = instruction.ImmediateY });
                        return true;
                    }
                }

                takenItems = [];
                return false;

            case GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse:
                return TryTakeFullHouse(items, counts, out takenItems);

            case GameEventScriptBytecodeOpCode.PipelineTakePatternStraight:
                return TryTakeStraight(items, out takenItems);

            default:
                takenItems = [];
                return false;
        }
    }

    private bool TryExecuteSeriesPipeline(GameEventScriptBytecodeInstruction instruction)
    {
        var source = ResolveSlot(instruction.XSlot).ToGameEventScriptValue();
        if (TryGetSeriesTarget(source, out var series))
        {
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.SeriesTerm:
                    if (!ResolveSlot(instruction.YSlot).ToGameEventScriptValue().TryConvertToInteger(out var termIndex))
                    {
                        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
                    }

                    return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.FromGameEventScriptValue(series.GetTerm(termIndex.AsInteger())));

                case GameEventScriptBytecodeOpCode.SeriesDrop:
                    return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Reference(series.Drop(instruction.ImmediateY)));

                case GameEventScriptBytecodeOpCode.SeriesTake:
                    if (!_runtimeBudget.TryCheckRangeLength(instruction.ImmediateY, "Series take would materialize more items than allowed."))
                    {
                        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Nothing);
                    }

                    return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(series.Take(instruction.ImmediateY))));
            }
        }

        var items = EnumerateListLikeValue(source).ToList();
        var value = instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.SeriesTake => EvaluateSequenceSliceSelector(source, items, "take", "first", instruction.ImmediateY),
            GameEventScriptBytecodeOpCode.SeriesDrop => EvaluateSequenceSliceSelector(source, items, "drop", "first", instruction.ImmediateY),
            _ => GameEventScriptNothingValue.Instance
        };
        return DefineSlot(instruction.DestinationSlot, BytecodeVmValue.FromGameEventScriptValue(value));
    }

    private static void CloseIterator(BytecodeVmValue value)
    {
        if (value.Kind == BytecodeVmValueKind.Iterator)
        {
            value.IteratorValue?.Dispose();
        }
    }

    private bool TryReadStringPool(int index, out string value)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.StringPool.Count)
        {
            value = _compiledScript.BytecodeModule.StringPool[index];
            return true;
        }

        value = string.Empty;
        return false;
    }

    private bool TryReadStringList(int index, out string[] values)
    {
        if (!TryGetUShortList(index, out var indexes))
        {
            values = [];
            return false;
        }

        if (indexes.Count == 0)
        {
            values = [];
            return true;
        }

        values = new string[indexes.Count];
        for (var itemIndex = 0; itemIndex < indexes.Count; itemIndex++)
        {
            if (!TryReadStringPool(indexes[itemIndex], out var value))
            {
                values = [];
                return false;
            }

            values[itemIndex] = value;
        }

        return true;
    }

    private bool TryGetUShortList(int index, out IReadOnlyList<ushort> layout)
    {
        if ((uint)index < (uint)_compiledScript.BytecodeModule.UShortListPool.Count)
        {
            layout = _compiledScript.BytecodeModule.UShortListPool[index];
            return true;
        }

        layout = [];
        return false;
    }

    private bool TryGetUShortListOrEmpty(int index, out IReadOnlyList<ushort> layout)
    {
        if (index < 0)
        {
            layout = [];
            return true;
        }

        return TryGetUShortList(index, out layout);
    }

    private bool TryGetSingleSlot(int index, out ushort slot)
    {
        if (TryGetUShortList(index, out var slots) &&
            slots.Count == 1)
        {
            slot = slots[0];
            return true;
        }

        slot = 0;
        return false;
    }

    private bool TryReadMessageShape(int index, out string messageName, out string[] argumentNames)
    {
        if ((uint)index >= (uint)_compiledScript.BytecodeModule.UShortListPool.Count)
        {
            messageName = string.Empty;
            argumentNames = [];
            return false;
        }

        var shape = _compiledScript.BytecodeModule.UShortListPool[index];
        if (shape.Count == 0 || !TryReadStringPool(shape[0], out messageName))
        {
            messageName = string.Empty;
            argumentNames = [];
            return false;
        }

        argumentNames = new string[shape.Count - 1];
        for (var argumentIndex = 0; argumentIndex < argumentNames.Length; argumentIndex++)
        {
            if (!TryReadStringPool(shape[argumentIndex + 1], out var argumentName))
            {
                messageName = string.Empty;
                argumentNames = [];
                return false;
            }

            argumentNames[argumentIndex] = argumentName;
        }

        return true;
    }

    private sealed class LinearCallFrame(
        BytecodeVmValue[] locals,
        bool[] assignedSlots,
        int activeLocalSlotCount,
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

        public int ActiveLocalSlotCount { get; } = activeLocalSlotCount;

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

    private struct FastProjectionState
    {
        private readonly GesBytecodeVmExecutionSession _session;
        private readonly int _identifierSlot;
        private readonly BytecodeVmValue _item;
        private readonly IReadOnlyList<BytecodeVmValue> _captures;
        private int _tempSlot0;
        private int _tempSlot1;
        private int _tempSlot2;
        private int _tempSlot3;
        private int _tempSlot4;
        private int _tempSlot5;
        private int _tempSlot6;
        private int _tempSlot7;
        private BytecodeVmValue _tempValue0;
        private BytecodeVmValue _tempValue1;
        private BytecodeVmValue _tempValue2;
        private BytecodeVmValue _tempValue3;
        private BytecodeVmValue _tempValue4;
        private BytecodeVmValue _tempValue5;
        private BytecodeVmValue _tempValue6;
        private BytecodeVmValue _tempValue7;

        public FastProjectionState(GesBytecodeVmExecutionSession session, int identifierSlot, BytecodeVmValue item)
            : this(session, identifierSlot, item, [])
        {
        }

        public FastProjectionState(
            GesBytecodeVmExecutionSession session,
            int identifierSlot,
            BytecodeVmValue item,
            IReadOnlyList<BytecodeVmValue> captures)
        {
            _session = session;
            _identifierSlot = identifierSlot;
            _item = item;
            _captures = captures;
            _tempSlot0 = -1;
            _tempSlot1 = -1;
            _tempSlot2 = -1;
            _tempSlot3 = -1;
            _tempSlot4 = -1;
            _tempSlot5 = -1;
            _tempSlot6 = -1;
            _tempSlot7 = -1;
            _tempValue0 = BytecodeVmValue.Nothing;
            _tempValue1 = BytecodeVmValue.Nothing;
            _tempValue2 = BytecodeVmValue.Nothing;
            _tempValue3 = BytecodeVmValue.Nothing;
            _tempValue4 = BytecodeVmValue.Nothing;
            _tempValue5 = BytecodeVmValue.Nothing;
            _tempValue6 = BytecodeVmValue.Nothing;
            _tempValue7 = BytecodeVmValue.Nothing;
        }

        public BytecodeVmValue GetSlot(int slot)
        {
            if (slot == _identifierSlot)
            {
                return _item;
            }

            var captureIndex = slot - 1;
            if ((uint)captureIndex < (uint)_captures.Count)
            {
                return _captures[captureIndex];
            }

            if (slot == _tempSlot0) return _tempValue0;
            if (slot == _tempSlot1) return _tempValue1;
            if (slot == _tempSlot2) return _tempValue2;
            if (slot == _tempSlot3) return _tempValue3;
            if (slot == _tempSlot4) return _tempValue4;
            if (slot == _tempSlot5) return _tempValue5;
            if (slot == _tempSlot6) return _tempValue6;
            if (slot == _tempSlot7) return _tempValue7;

            return _session.ResolveSlot(slot);
        }

        public bool SetTemp(int slot, BytecodeVmValue input)
        {
            if (slot < 0)
            {
                return false;
            }

            if (slot == _tempSlot0)
            {
                _tempValue0 = input;
                return true;
            }

            if (slot == _tempSlot1)
            {
                _tempValue1 = input;
                return true;
            }

            if (slot == _tempSlot2)
            {
                _tempValue2 = input;
                return true;
            }

            if (slot == _tempSlot3)
            {
                _tempValue3 = input;
                return true;
            }

            if (slot == _tempSlot4)
            {
                _tempValue4 = input;
                return true;
            }

            if (slot == _tempSlot5)
            {
                _tempValue5 = input;
                return true;
            }

            if (slot == _tempSlot6)
            {
                _tempValue6 = input;
                return true;
            }

            if (slot == _tempSlot7)
            {
                _tempValue7 = input;
                return true;
            }

            if (_tempSlot0 < 0)
            {
                _tempSlot0 = slot;
                _tempValue0 = input;
                return true;
            }

            if (_tempSlot1 < 0)
            {
                _tempSlot1 = slot;
                _tempValue1 = input;
                return true;
            }

            if (_tempSlot2 < 0)
            {
                _tempSlot2 = slot;
                _tempValue2 = input;
                return true;
            }

            if (_tempSlot3 < 0)
            {
                _tempSlot3 = slot;
                _tempValue3 = input;
                return true;
            }

            if (_tempSlot4 < 0)
            {
                _tempSlot4 = slot;
                _tempValue4 = input;
                return true;
            }

            if (_tempSlot5 < 0)
            {
                _tempSlot5 = slot;
                _tempValue5 = input;
                return true;
            }

            if (_tempSlot6 < 0)
            {
                _tempSlot6 = slot;
                _tempValue6 = input;
                return true;
            }

            if (_tempSlot7 < 0)
            {
                _tempSlot7 = slot;
                _tempValue7 = input;
                return true;
            }

            return false;
        }
    }

    private sealed class LinearArgumentSource
    {
        private readonly GesBytecodeVmCompiledHandler? _handler;
        private readonly IReadOnlyDictionary<string, GameEventScriptValue>? _handlerArgs;
        private readonly IReadOnlyList<BytecodeVmValue>? _values;
        private readonly BytecodeVmValue _singleValue;
        private readonly bool _hasSingleValue;
        private Dictionary<int, int>? _pendingTypedParameterSlots;
        private bool[]? _recordedParameterDiagnostics;
        private int _recordedParameterDiagnosticCount;
        private bool _handlerInvoked;

        private LinearArgumentSource(
            GesBytecodeVmCompiledHandler? handler,
            IReadOnlyDictionary<string, GameEventScriptValue>? handlerArgs,
            IReadOnlyList<BytecodeVmValue>? values,
            BytecodeVmValue singleValue = default,
            bool hasSingleValue = false)
        {
            _handler = handler;
            _handlerArgs = handlerArgs;
            _values = values;
            _singleValue = singleValue;
            _hasSingleValue = hasSingleValue;
        }

        public static LinearArgumentSource ForHandler(
            GesBytecodeVmCompiledHandler handler,
            IReadOnlyDictionary<string, GameEventScriptValue> args)
            => new(handler, args, null);

        public static LinearArgumentSource ForValues(IReadOnlyList<BytecodeVmValue> values)
            => new(null, null, values);

        public static LinearArgumentSource ForSingleValue(BytecodeVmValue value)
            => new(null, null, null, value, hasSingleValue: true);

        public int Count
            => _hasSingleValue
                ? 1
                : _values?.Count ?? _handler?.Parameters.Count ?? 0;

        public bool TryGetValue(int index, out BytecodeVmValue value)
        {
            if (_hasSingleValue)
            {
                if (index == 0)
                {
                    value = _singleValue;
                    return true;
                }

                value = BytecodeVmValue.Nothing;
                return false;
            }

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

    private void PublishMessage(PublishKind publishKind, GameEventScriptMessage message)
    {
        if (publishKind == PublishKind.Publish)
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

        if (value.IsList())
        {
            foreach (var item in value.AsEnumerable())
            {
                AddTags(builder, item);
            }

            return;
        }

        builder.Add(value.AsText());
    }

    private bool TryLoadInlineConstant(GameEventScriptBytecodeInstruction instruction, out BytecodeVmValue value)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.LoadNothing:
                value = BytecodeVmValue.Nothing;
                return true;

            case GameEventScriptBytecodeOpCode.LoadTrue:
                value = BytecodeVmValue.Boolean(true);
                return true;

            case GameEventScriptBytecodeOpCode.LoadFalse:
                value = BytecodeVmValue.Boolean(false);
                return true;

            case GameEventScriptBytecodeOpCode.LoadInteger:
                value = BytecodeVmValue.Integer(instruction.I64, DecodeNumericUnit(instruction.UnitAndFlags));
                return true;

            case GameEventScriptBytecodeOpCode.LoadFloat:
            {
                var number = instruction.F64;
                value = double.IsNaN(number)
                    ? BytecodeVmValue.Reference(GesFloatNaN())
                    : double.IsPositiveInfinity(number)
                        ? BytecodeVmValue.Reference(GesFloatInfinity())
                        : double.IsNegativeInfinity(number)
                            ? BytecodeVmValue.Reference(GesFloatNegativeInfinity())
                            : BytecodeVmValue.Float(number, DecodeNumericUnit(instruction.UnitAndFlags));
                return true;
            }

            case GameEventScriptBytecodeOpCode.LoadPercentage:
                value = BytecodeVmValue.Percentage(instruction.F64);
                return true;

            case GameEventScriptBytecodeOpCode.LoadText:
                if (!TryReadStringPool(instruction.StringIndex, out var text))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Reference(GesText(text));
                return true;

            case GameEventScriptBytecodeOpCode.LoadTag:
                if (!TryReadStringPool(instruction.StringIndex, out var tag))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Reference(GesTag(tag));
                return true;

            case GameEventScriptBytecodeOpCode.LoadHandler:
                if (!TryReadMessageShape(instruction.ListIndex, out var messageName, out var labels))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Reference(GesHandler(GameEventScriptMessageSignature.Create(messageName, labels)));
                return true;

            default:
                value = BytecodeVmValue.Nothing;
                return false;
        }
    }

    private bool TryLoadStageConstant(GameEventScriptBytecodeInstruction instruction, out BytecodeVmValue value)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.StageNothing:
                value = BytecodeVmValue.Nothing;
                return true;

            case GameEventScriptBytecodeOpCode.StageTrue:
                value = BytecodeVmValue.Boolean(true);
                return true;

            case GameEventScriptBytecodeOpCode.StageFalse:
                value = BytecodeVmValue.Boolean(false);
                return true;

            case GameEventScriptBytecodeOpCode.StageInteger:
                value = BytecodeVmValue.Integer(instruction.I64, DecodeNumericUnit(instruction.UnitAndFlags));
                return true;

            case GameEventScriptBytecodeOpCode.StageFloat:
            {
                var number = instruction.F64;
                value = double.IsNaN(number)
                    ? BytecodeVmValue.Reference(GesFloatNaN())
                    : double.IsPositiveInfinity(number)
                        ? BytecodeVmValue.Reference(GesFloatInfinity())
                        : double.IsNegativeInfinity(number)
                            ? BytecodeVmValue.Reference(GesFloatNegativeInfinity())
                            : BytecodeVmValue.Float(number, DecodeNumericUnit(instruction.UnitAndFlags));
                return true;
            }

            case GameEventScriptBytecodeOpCode.StagePercentage:
            {
                var number = instruction.F64;
                value = BytecodeVmValue.Percentage(number);
                return true;
            }

            case GameEventScriptBytecodeOpCode.StageText:
                if (!TryReadStringPool(instruction.StringIndex, out var text))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Reference(GesText(text));
                return true;

            case GameEventScriptBytecodeOpCode.StageTag:
                if (!TryReadStringPool(instruction.StringIndex, out var tag))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Reference(GesTag(tag));
                return true;

            default:
                value = BytecodeVmValue.Nothing;
                return false;
        }
    }

    private static GameEventScriptNumericUnit? DecodeNumericUnit(byte value)
        => (GameEventScriptBytecodeInstructionUnit)value switch
        {
            GameEventScriptBytecodeInstructionUnit.UnitDegree => GameEventScriptNumericUnit.Degree,
            GameEventScriptBytecodeInstructionUnit.UnitMeter => GameEventScriptNumericUnit.Meter,
            GameEventScriptBytecodeInstructionUnit.UnitSecond => GameEventScriptNumericUnit.Second,
            _ => null
        };

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

        return targetValue.TryGetMapMember(member, out var value)
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

    private static BytecodeVmValue BuildListValue(BytecodeVmValue[] inputs, int start, int count)
    {
        var items = new GameEventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = inputs[start + itemIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(items));
    }

    private static BytecodeVmValue BuildMapValue(BytecodeVmValue[] inputs, int start, int count, string[]? names)
    {
        if (names is null || names.Length != count)
        {
            return BytecodeVmValue.Nothing;
        }

        var map = new Dictionary<string, GameEventScriptValue>(count, StringComparer.Ordinal);
        for (var entryIndex = 0; entryIndex < count; entryIndex++)
        {
            map[names[entryIndex]] = inputs[start + entryIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesMap(map));
    }

    private static BytecodeVmValue CreateMessageValue(
        BytecodeVmValue[] inputs,
        int start,
        int count,
        string[]? names,
        string? messageName)
    {
        if (string.IsNullOrEmpty(messageName) ||
            names is null ||
            names.Length != count)
        {
            return BytecodeVmValue.Nothing;
        }

        var signatureId = GameEventScriptMessageSignature.CreateSignatureId(messageName, names);

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
                inputs[start + argumentIndex].ToGameEventScriptValue());
        }

        return BytecodeVmValue.Reference(GesMessage(GameEventScriptMessage.CreatePrecomputed(
            messageName,
            GameEventScriptNamedArguments.CreateOrdered(pairs),
            signatureId)));
    }

    private static BytecodeVmValue BindHandlerValue(
        BytecodeVmValue callee,
        BytecodeVmValue[] inputs,
        int count)
    {
        if (!GesMessageValueCodec.TryReadHandlerValue(callee.ToGameEventScriptValue(), out var signature))
        {
            return BytecodeVmValue.Nothing;
        }

        var arguments = new GameEventScriptValue[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            arguments[argumentIndex] = inputs[argumentIndex].ToGameEventScriptValue();
        }

        return signature.TryCreateMessage(arguments, out var message)
            ? BytecodeVmValue.Reference(GesMessageValueCodec.CreateMessageValue(message))
            : BytecodeVmValue.Nothing;
    }

    private bool TryCallStandard(
        int shapeIndex,
        int argumentSlotListIndex,
        bool requirePredicateResult,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (!TryGetUShortList(shapeIndex, out var shape) ||
            shape.Count < 2 ||
            !TryGetUShortList(argumentSlotListIndex, out var argumentSlots) ||
            argumentSlots.Count != shape.Count - 2)
        {
            return false;
        }

        var argumentCount = argumentSlots.Count;
        if (argumentCount > 2)
        {
            return false;
        }

        var argument0 = argumentCount > 0
            ? ToGameEventScriptFastValue(ResolveSlot(argumentSlots[0]))
            : default;
        var argument1 = argumentCount > 1
            ? ToGameEventScriptFastValue(ResolveSlot(argumentSlots[1]))
            : default;
        if (!GesStandardExtensions.TryInvoke(_compiledScript.BytecodeModule.StringPool, shape, argument0, argument1, argumentCount, out var standardValue))
        {
            return false;
        }

        value = BytecodeVmValue.FromGameEventScriptFastValue(standardValue);
        return NormalizeExtensionPredicateResult(requirePredicateResult, ref value);
    }

    private bool TryCallExternal(
        int referenceIndex,
        int argumentSlotListIndex,
        bool requirePredicateResult,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if ((uint)referenceIndex >= (uint)_compiledScript.BytecodeModule.ExternalReferences.Count ||
            !TryGetUShortList(argumentSlotListIndex, out var argumentSlots))
        {
            return false;
        }

        var reference = _compiledScript.BytecodeModule.ExternalReferences[referenceIndex];
        if (reference.ArgumentLabels.Count != argumentSlots.Count)
        {
            return false;
        }

        if (!_compiledScript.TryGetBoundExtension(referenceIndex, out var function))
        {
            throw new GameEventScriptDynamicLinkException($"GameEventScript extension '{reference.SignatureId}' was not dynamically bound to reference slot '{referenceIndex}'.");
        }

        var argumentCount = argumentSlots.Count;
        var arguments = argumentCount == 0
            ? Array.Empty<GameEventScriptFastValue>()
            : ArrayPool<GameEventScriptFastValue>.Shared.Rent(argumentCount);
        try
        {
            for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                arguments[argumentIndex] = ToGameEventScriptFastValue(ResolveSlot(argumentSlots[argumentIndex]));
            }

            value = BytecodeVmValue.FromGameEventScriptFastValue(function.Invoke(new GameEventScriptExtensionContext(_context), arguments.AsSpan(0, argumentCount)));
            return NormalizeExtensionPredicateResult(requirePredicateResult, ref value);
        }
        finally
        {
            if (argumentCount > 0)
            {
                Array.Clear(arguments, 0, argumentCount);
                ArrayPool<GameEventScriptFastValue>.Shared.Return(arguments);
            }
        }
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
            case GameEventScriptBytecodeOpCode.Implies:
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
            case GameEventScriptBytecodeOpCode.Min:
                return EvaluateMinMaxBinary(left, right, isMax: false);
            case GameEventScriptBytecodeOpCode.Max:
                return EvaluateMinMaxBinary(left, right, isMax: true);
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
            GameEventScriptBytecodeOpCode.Min or
            GameEventScriptBytecodeOpCode.Max or
            GameEventScriptBytecodeOpCode.Power or
            GameEventScriptBytecodeOpCode.Equal or
            GameEventScriptBytecodeOpCode.NotEqual or
            GameEventScriptBytecodeOpCode.ApproxEqual or
            GameEventScriptBytecodeOpCode.Less or
            GameEventScriptBytecodeOpCode.Greater or
            GameEventScriptBytecodeOpCode.LessOrEqual or
            GameEventScriptBytecodeOpCode.GreaterOrEqual or
            GameEventScriptBytecodeOpCode.GreaterOrEqual;

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

    private bool TryEvaluateCastInstruction(
        GameEventScriptBytecodeInstruction instruction,
        BytecodeVmValue input,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CastUnit)
        {
            if (!TryDecodeRequiredNumericUnit(instruction.UnitAndFlags, out var unit))
            {
                return false;
            }

            value = BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(input.ToGameEventScriptValue(), unit));
            return true;
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CastNumeric)
        {
            value = BytecodeVmValue.FromGameEventScriptValue(ConvertToNumber(input.ToGameEventScriptValue()));
            return true;
        }

        if (instruction.OpCode is not (GameEventScriptBytecodeOpCode.Cast or GameEventScriptBytecodeOpCode.CastCustom) ||
            !TryGetDeclaredTypeName(instruction, out var typeName))
        {
            return false;
        }

        return TryConvertDeclaredType(typeName, input, out value);
    }

    private bool TryEvaluateTypeCheckInstruction(
        GameEventScriptBytecodeInstruction instruction,
        BytecodeVmValue input,
        out bool value)
    {
        value = false;
        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CheckUnit)
        {
            if (!TryDecodeRequiredNumericUnit(instruction.UnitAndFlags, out var unit))
            {
                return false;
            }

            value = IsValueOfUnit(input, unit);
            return true;
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CheckNumeric)
        {
            value = IsValueNumeric(input);
            return true;
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CheckInteger)
        {
            value = IsValueInteger(input);
            return true;
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CheckFractional)
        {
            value = IsValueFractional(input);
            return true;
        }

        if (instruction.OpCode is not (GameEventScriptBytecodeOpCode.CheckType or GameEventScriptBytecodeOpCode.CheckCustomType) ||
            !TryGetDeclaredTypeName(instruction, out var typeName))
        {
            return false;
        }

        value = IsValueOfType(input, typeName);
        return true;
    }

    private bool TryGetDeclaredTypeName(GameEventScriptBytecodeInstruction instruction, out string typeName)
    {
        if (instruction.OpCode is GameEventScriptBytecodeOpCode.CastCustom or GameEventScriptBytecodeOpCode.CheckCustomType)
        {
            return TryReadStringPool(instruction.TypeOperand, out typeName!);
        }

        var typeKind = (GameEventScriptBytecodeTypeKind)instruction.TypeOperand;
        typeName = ToDeclaredTypeName(typeKind);
        return !string.IsNullOrEmpty(typeName);
    }

    private static string ToDeclaredTypeName(GameEventScriptBytecodeTypeKind typeKind)
        => typeKind switch
        {
            GameEventScriptBytecodeTypeKind.Nothing => "nothing",
            GameEventScriptBytecodeTypeKind.Boolean => "boolean",
            GameEventScriptBytecodeTypeKind.Integer => "integer",
            GameEventScriptBytecodeTypeKind.Float => "float",
            GameEventScriptBytecodeTypeKind.Percentage => "percentage",
            GameEventScriptBytecodeTypeKind.Vector => "vector",
            GameEventScriptBytecodeTypeKind.Point => "point",
            GameEventScriptBytecodeTypeKind.Series => "series",
            GameEventScriptBytecodeTypeKind.Envelope => "envelope",
            GameEventScriptBytecodeTypeKind.Tag => "tag",
            GameEventScriptBytecodeTypeKind.Text => "text",
            GameEventScriptBytecodeTypeKind.List => "list",
            GameEventScriptBytecodeTypeKind.Range => "range",
            GameEventScriptBytecodeTypeKind.Message => "message",
            GameEventScriptBytecodeTypeKind.Handler => "handler",
            GameEventScriptBytecodeTypeKind.Map => "map",
            GameEventScriptBytecodeTypeKind.Dice => "dice",
            _ => string.Empty
        };

    private static bool TryDecodeRequiredNumericUnit(byte unitAndFlags, out GameEventScriptNumericUnit unit)
    {
        var decoded = DecodeNumericUnit(unitAndFlags);
        if (decoded.HasValue)
        {
            unit = decoded.Value;
            return true;
        }

        unit = default;
        return false;
    }

    private static bool IsValueOfUnit(BytecodeVmValue value, GameEventScriptNumericUnit unit)
        => (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float) && value.Unit == unit ||
           (value.ReferenceValue?.IsNumericUnit(unit) ?? false);

    private static bool IsValueOfType(BytecodeVmValue value, string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        if (GameEventScriptNumericUnits.TryParseQuantityTypeName(typeName, out var quantityUnit))
        {
            return IsValueOfUnit(value, quantityUnit);
        }

        return typeName switch
        {
            "nothing" => value.Kind == BytecodeVmValueKind.Nothing || (value.ReferenceValue?.IsNothing() ?? false),
            "tag" => value.ReferenceValue?.IsTag() ?? false,
            "text" => value.ReferenceValue?.IsText() ?? false,
            "percentage" => value.Kind == BytecodeVmValueKind.Percentage || (value.ReferenceValue?.IsPercentage() ?? false),
            "degree" => IsValueOfUnit(value, GameEventScriptNumericUnit.Degree),
            "meter" => IsValueOfUnit(value, GameEventScriptNumericUnit.Meter),
            "second" => IsValueOfUnit(value, GameEventScriptNumericUnit.Second),
            "vector" => value.ReferenceValue?.IsVector() ?? false,
            "point" => value.ReferenceValue?.IsPoint() ?? false,
            "boolean" => value.Kind == BytecodeVmValueKind.Boolean || value.ReferenceValue?.Kind == GameEventScriptValueKind.Boolean,
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
            "map" => value.ReferenceValue?.IsMap() ?? false,
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

        if (fromValue.Kind == BytecodeVmValueKind.Integer &&
            toValue.Kind == BytecodeVmValueKind.Integer)
        {
            if (fromValue.Unit != toValue.Unit)
            {
                return BytecodeVmValue.NaN();
            }

            var from = fromRaw.AsInteger();
            var to = toRaw.AsInteger();
            if (from > to)
            {
                (from, to) = (to, from);
            }

            return TryNextInclusiveInteger(from, to, out var next)
                ? BytecodeVmValue.Integer(next, fromValue.Unit)
                : BytecodeVmValue.Nothing;
        }

        if (fromValue.IsNothingLike() || toValue.IsNothingLike())
        {
            return BytecodeVmValue.Nothing;
        }

        if (!GesValueOperations.HaveCompatibleNumericUnits(fromRaw, toRaw))
        {
            return BytecodeVmValue.NaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(fromRaw, out var fromNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(toRaw, out var toNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite)
        {
            return BytecodeVmValue.NaN();
        }

        var lower = Math.Min(fromNumber.Value, toNumber.Value);
        var upper = Math.Max(fromNumber.Value, toNumber.Value);
        if (lower == upper)
        {
            return BytecodeVmValue.Float(lower);
        }

        return TryNextInclusiveFloat(lower, upper, out var nextFloat)
            ? BytecodeVmValue.Float(nextFloat, fromValue.Unit)
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

    private BytecodeVmValue EvaluateUnaryOperation(GameEventScriptBytecodeOpCode opCode, BytecodeVmValue operand)
    {
        var boxed = operand.ToGameEventScriptValue();
        return opCode switch
        {
            GameEventScriptBytecodeOpCode.UnaryNegate => BytecodeVmValue.FromGameEventScriptValue(EvaluateNegateUnary(boxed)),
            GameEventScriptBytecodeOpCode.UnaryNot => BytecodeVmValue.FromGameEventScriptValue(EvaluateNotUnary(boxed)),
            GameEventScriptBytecodeOpCode.UnaryHasValue => BytecodeVmValue.Boolean(boxed.HasSemanticValue()),
            GameEventScriptBytecodeOpCode.UnaryEmpty => BytecodeVmValue.Boolean(boxed.IsSemanticallyEmpty()),
            GameEventScriptBytecodeOpCode.UnaryLength => BytecodeVmValue.FromGameEventScriptValue(EvaluateLenUnary(boxed)),
            GameEventScriptBytecodeOpCode.UnaryChance => BytecodeVmValue.FromGameEventScriptValue(EvaluateChanceUnary(boxed)),
            GameEventScriptBytecodeOpCode.UnaryKeys => BytecodeVmValue.Reference(GesKeys(boxed)),
            GameEventScriptBytecodeOpCode.UnaryValues => BytecodeVmValue.Reference(GesValues(boxed)),
            GameEventScriptBytecodeOpCode.UnaryEntries => BytecodeVmValue.Reference(GesEntries(boxed)),
            GameEventScriptBytecodeOpCode.UnaryAbs => BytecodeVmValue.FromGameEventScriptValue(EvaluateAbsUnary(boxed)),
            GameEventScriptBytecodeOpCode.UnaryNaturalLog => BytecodeVmValue.FromGameEventScriptValue(EvaluateNaturalLogUnary(boxed)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown unary opcode '{opCode}'.")
        };

        GameEventScriptValue EvaluateChanceUnary(GameEventScriptValue operand)
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
    }

    private static BytecodeVmValue EvaluateMinMaxBinary(BytecodeVmValue left, BytecodeVmValue right, bool isMax)
        => BytecodeVmValue.FromGameEventScriptValue(GesValueOperations.EvaluateMinMax(
            left.ToGameEventScriptValue(),
            right.ToGameEventScriptValue(),
            isMax));

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

        var left = leftRaw;
        var right = rightRaw;

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
            GameEventScriptBytecodeOpCode.Implies => "->",
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

        return leftRaw;
    }

    private static bool TryCompare(GameEventScriptValue left, GameEventScriptValue right, Func<int, bool> predicate)
        => GesValueOperations.TryCompareNumericValues(left, right, out var comparison) &&
           predicate(comparison);

    private static GameEventScriptValue EvaluateAddBinary(GameEventScriptValue left, GameEventScriptValue right)
    {
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

        return GesFloatNaN();
    }

    private static GameEventScriptValue EvaluateNumericBinary(GameEventScriptValue left, string operation, GameEventScriptValue right)
    {
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

        if (operand.IsPercentage())
        {
            return GesPercentage(-operand.AsNumber());
        }

        if (GesValueOperations.TryEvaluatePointUnary(operand, "-", out var pointNegation))
        {
            return pointNegation;
        }

        if (GesValueOperations.TryEvaluateVectorUnary(operand, "-", out var vectorNegation))
        {
            return vectorNegation;
        }

        if (operand is GameEventScriptNumberValue { IsIntegerValue: true, IntegerValue: not long.MinValue } integer)
        {
            return GesInteger(-integer.IntegerValue, integer.Unit);
        }

        if (GameEventScriptValue.TryGetNumericUnit(operand, out var unit))
        {
            return GesFloat(-operand.AsNumber(), unit);
        }

        return GesValueOperations.TryCoerceNumericForOperation(operand, out var number)
            ? GesValueOperations.ToGameEventScriptNumber(GesValueOperations.NegateNumeric(number))
            : GesFloatNaN();
    }

    private static GameEventScriptValue EvaluateNotUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GesBoolean(!operand.AsBoolean());
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
            GameEventScriptValueKind.Range => EvaluateRangeLength(operand),
            GameEventScriptValueKind.List => GesInteger(operand.AsList().Count),
            GameEventScriptValueKind.Map => GesInteger(operand.AsMap().Count),
            GameEventScriptValueKind.Dice => GesInteger(operand.AsDice().Rolls.Count),
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

    private static GameEventScriptValue EvaluateAbsUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (operand.IsPercentage())
        {
            return GesPercentage(Math.Abs(operand.AsNumber()));
        }

        if (GesValueOperations.TryEvaluatePointUnary(operand, "abs", out var pointAbs))
        {
            return pointAbs;
        }

        if (GesValueOperations.TryEvaluateVectorUnary(operand, "abs", out var vectorLength))
        {
            return vectorLength;
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(operand, out var number))
        {
            return GesFloatNaN();
        }

        if (number.IsNaN)
        {
            return GesFloatNaN();
        }

        if (number.IsInfinity)
        {
            return GesFloatInfinity();
        }

        GameEventScriptValue.TryGetNumericUnit(operand, out var unit);
        return GesValueOperations.ToGameEventScriptNumber(
            GesValueOperations.NumericValue.Finite(Math.Abs(number.Value)),
            operand.HasNumericUnit() ? unit : null);
    }

    private static GameEventScriptValue EvaluateNaturalLogUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (GameEventScriptValue.TryGetNumericUnit(operand, out _))
        {
            return GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(operand, out var number))
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

        if (rawValue.IsNothingLike() || minimumValue.IsNothingLike() || maximumValue.IsNothingLike())
        {
            return BytecodeVmValue.Nothing;
        }

        if (!GesValueOperations.HaveCompatibleNumericUnits(raw, minimum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(raw, maximum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return BytecodeVmValue.NaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(raw, out var rawNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(maximum, out var maximumNumber) ||
            rawNumber.IsNaN ||
            minimumNumber.IsNaN ||
            maximumNumber.IsNaN)
        {
            return BytecodeVmValue.NaN();
        }

        var rawDouble = NumericValueToDouble(rawNumber);
        var lower = Math.Min(NumericValueToDouble(minimumNumber), NumericValueToDouble(maximumNumber));
        var upper = Math.Max(NumericValueToDouble(minimumNumber), NumericValueToDouble(maximumNumber));
        GameEventScriptValue.TryGetNumericUnit(raw, out var unit);
        return BytecodeVmValue.FromGameEventScriptValue(CreateFloatResult(
            Math.Min(Math.Max(rawDouble, lower), upper),
            raw.HasNumericUnit() ? unit : null));
    }

    private static double NumericValueToDouble(GesValueOperations.NumericValue number)
    {
        if (number.IsPositiveInfinity) return double.PositiveInfinity;
        if (number.IsNegativeInfinity) return double.NegativeInfinity;
        return number.IsNaN ? double.NaN : number.Value;
    }

    private static GameEventScriptValue CreateFloatResult(double value, GameEventScriptNumericUnit? unit = null)
    {
        if (double.IsNaN(value)) return GesFloatNaN();
        if (double.IsPositiveInfinity(value)) return GesFloatInfinity();
        if (double.IsNegativeInfinity(value)) return GesFloatNegativeInfinity();
        return GesFloat(value, unit);
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

    private void PushRandomScope(BytecodeVmValue seedValue)
    {
        PushRandomScope(TryResolveRandomSeed(seedValue, out var seed)
            ? seed
            : DeriveChildRandomSeed());
    }

    private void PushRandomScope(long seed)
        => _randomScopes.Push(GameEventScriptRandomGenerator.FromSeed(FoldRandomSeed(seed)));

    private void PopRandomScope()
    {
        if (_randomScopes.Count > 1)
        {
            _randomScopes.Pop();
        }
    }

    private void UnwindRandomScopes(int targetDepth)
    {
        var normalizedTargetDepth = Math.Max(1, targetDepth);
        while (_randomScopes.Count > normalizedTargetDepth)
        {
            _randomScopes.Pop();
        }
    }

    private long DeriveChildRandomSeed()
    {
        return TryNextInclusiveInteger(long.MinValue, long.MaxValue, out var seed)
            ? seed
            : 0L;
    }

    private static bool TryResolveRandomSeed(BytecodeVmValue seedValue, out long seed)
    {
        if (seedValue.Kind == BytecodeVmValueKind.Integer)
        {
            seed = seedValue.IntegerValue;
            return true;
        }

        var boxed = seedValue.ToGameEventScriptValue();
        if (boxed.TryConvertToInteger(out var integerValue))
        {
            seed = integerValue.AsInteger();
            return true;
        }

        seed = default;
        return false;
    }

    private static int FoldRandomSeed(long signedSeed)
    {
        unchecked
        {
            var seed = (ulong)signedSeed;
            seed ^= seed >> 32;
            seed ^= seed >> 16;
            return (int)seed;
        }
    }

    private static int ToIntSaturated(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return (int)value;
    }

    private BytecodeVmValue EvaluateTypeConstructor(
        string? typeName,
        string[]? labels,
        BytecodeVmValue[] inputs,
        int start,
        int count)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return BytecodeVmValue.Nothing;
        }

        if (typeName is "vector" or "point")
        {
            return EvaluateSpatialConstructor(typeName, labels, inputs, start, count);
        }

        if (TryEvaluateExternalTypeConstructor(typeName, labels, inputs, start, count, out var externalValue))
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

                values[label] = inputs[start + index].ToGameEventScriptValue();
            }

            return BytecodeVmValue.FromGameEventScriptValue(ConvertToCustomType(GesMap(values), typeDefinition));
        }

        if (count != 1 || labels is not { Length: > 0 } || !string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return BytecodeVmValue.Nothing;
        }

        return TryConvertDeclaredType(typeName, inputs[start], out var converted)
            ? converted
            : BytecodeVmValue.Nothing;
    }

    private bool TryEvaluateExternalTypeConstructor(
        string typeName,
        string[]? labels,
        BytecodeVmValue[] inputs,
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
                !TryConvertDeclaredType(parameter.TypeName, inputs[start + argumentIndex], out var converted))
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
        BytecodeVmValue[] inputs,
        int start,
        int count)
    {
        if (count == 1 &&
            labels is { Length: > 0 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return TryConvertDeclaredType(typeName, inputs[start], out var converted)
                ? converted
                : BytecodeVmValue.Nothing;
        }

        if (count == 2 &&
            labels is { Length: >= 2 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) &&
            string.Equals(labels[1], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) &&
            TryCreateSpatialLift(
                typeName,
                inputs[start].ToGameEventScriptValue(),
                inputs[start + 1].ToGameEventScriptValue(),
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
                labeledComponents[labels![index]] = inputs[start + index].ToGameEventScriptValue();
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
            components[index] = inputs[start + index].ToGameEventScriptValue();
        }

        return TryCreateSpatialFromComponents(typeName, components, out var spatial)
            ? BytecodeVmValue.FromGameEventScriptValue(spatial)
            : BytecodeVmValue.Nothing;
    }

    private BytecodeVmValue EvaluateStagedSpatialConstructor(
        string typeName,
        int startComponent,
        IReadOnlyList<BytecodeVmValue>? stagedArguments)
    {
        if (startComponent is < 0 or > 2)
        {
            return BytecodeVmValue.Nothing;
        }

        var count = stagedArguments?.Count ?? 0;
        if (count > 3 - startComponent)
        {
            return BytecodeVmValue.Nothing;
        }

        if (startComponent == 0 && count == 1)
        {
            return TryConvertDeclaredType(typeName, stagedArguments![0], out var converted)
                ? converted
                : BytecodeVmValue.Nothing;
        }

        if (startComponent == 0 &&
            count == 2 &&
            TryCreateSpatialLift(
                typeName,
                stagedArguments![0].ToGameEventScriptValue(),
                stagedArguments[1].ToGameEventScriptValue(),
                out var lifted))
        {
            return BytecodeVmValue.FromGameEventScriptValue(lifted);
        }

        if (startComponent == 0)
        {
            var components = new GameEventScriptValue[count];
            for (var index = 0; index < components.Length; index++)
            {
                components[index] = stagedArguments![index].ToGameEventScriptValue();
            }

            return TryCreateSpatialFromComponents(typeName, components, out var spatial)
                ? BytecodeVmValue.FromGameEventScriptValue(spatial)
                : BytecodeVmValue.Nothing;
        }

        var labeledComponents = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            labeledComponents[GetSpatialComponentLabel(startComponent + index)] = stagedArguments![index].ToGameEventScriptValue();
        }

        return TryCreateSpatialFromLabeledComponents(typeName, labeledComponents, out var labeledSpatial)
            ? BytecodeVmValue.FromGameEventScriptValue(labeledSpatial)
            : BytecodeVmValue.Nothing;
    }

    private static string GetSpatialComponentLabel(int componentIndex)
        => componentIndex switch
        {
            0 => "x",
            1 => "y",
            2 => "z",
            _ => string.Empty
        };

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
        if (GameEventScriptNumericUnits.TryParseQuantityTypeName(declaredType, out var quantityUnit))
        {
            value = BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(input.ToGameEventScriptValue(), quantityUnit));
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
            "number" or "numeric" => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumber(boxed)),
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
            "map" => BytecodeVmValue.Reference(GesMap(boxed.AsMap())),
            "dice" => BytecodeVmValue.Reference(GesDice(boxed.AsDice())),
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
            case "number":
            case "numeric":
                value = input.Kind switch
                {
                    BytecodeVmValueKind.Integer => input.Unit.HasValue ? BytecodeVmValue.Integer(input.IntegerValue) : input,
                    BytecodeVmValueKind.Float => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumber(input.ToGameEventScriptValue())),
                    BytecodeVmValueKind.Boolean => BytecodeVmValue.Integer(input.BooleanValue ? 1 : 0),
                    BytecodeVmValueKind.Percentage => BytecodeVmValue.Percentage(input.Number),
                    _ => input
                };
                return input.Kind is BytecodeVmValueKind.Integer
                    or BytecodeVmValueKind.Boolean
                    or BytecodeVmValueKind.Float
                    or BytecodeVmValueKind.Percentage;
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

    private static bool IsCastInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.Cast or
            GameEventScriptBytecodeOpCode.CastNumeric or
            GameEventScriptBytecodeOpCode.CastCustom or
            GameEventScriptBytecodeOpCode.CastUnit;

    private static bool IsInlineConstantInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.LoadNothing or
            GameEventScriptBytecodeOpCode.LoadTrue or
            GameEventScriptBytecodeOpCode.LoadFalse or
            GameEventScriptBytecodeOpCode.LoadInteger or
            GameEventScriptBytecodeOpCode.LoadFloat or
            GameEventScriptBytecodeOpCode.LoadPercentage or
            GameEventScriptBytecodeOpCode.LoadText or
            GameEventScriptBytecodeOpCode.LoadTag or
            GameEventScriptBytecodeOpCode.LoadHandler;

    private static bool IsTypeCheckInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.CheckType or
            GameEventScriptBytecodeOpCode.CheckNumeric or
            GameEventScriptBytecodeOpCode.CheckInteger or
            GameEventScriptBytecodeOpCode.CheckFractional or
            GameEventScriptBytecodeOpCode.CheckCustomType or
            GameEventScriptBytecodeOpCode.CheckUnit;

    private static bool IsValueNumeric(BytecodeVmValue value)
    {
        if (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float or BytecodeVmValueKind.Percentage)
        {
            return true;
        }

        return value.ReferenceValue is { } reference &&
               GesValueOperations.TryCoerceNumericForOperation(reference, out _);
    }

    private static bool IsValueInteger(BytecodeVmValue value)
    {
        if (!TryReadNumericValue(value, out var number))
        {
            return false;
        }

        return number.IsFinite &&
               number.Value >= long.MinValue &&
               number.Value <= long.MaxValue &&
               number.Value == Math.Truncate(number.Value);
    }

    private static bool IsValueFractional(BytecodeVmValue value)
    {
        if (!TryReadNumericValue(value, out var number))
        {
            return false;
        }

        return number.IsFinite && number.Value != Math.Truncate(number.Value);
    }

    private static bool TryReadNumericValue(BytecodeVmValue value, out GesValueOperations.NumericValue number)
    {
        if (value.Kind == BytecodeVmValueKind.Integer)
        {
            number = GesValueOperations.NumericValue.Finite(value.IntegerValue);
            return true;
        }

        if (value.Kind is BytecodeVmValueKind.Float or BytecodeVmValueKind.Percentage)
        {
            number = GesValueOperations.NumericValue.Finite(value.Number);
            return true;
        }

        if (value.ReferenceValue is { } reference)
        {
            return GesValueOperations.TryCoerceNumericForOperation(reference, out number);
        }

        number = default;
        return false;
    }

    private static GameEventScriptValue ConvertToNumber(GameEventScriptValue value)
    {
        if (value.IsInteger())
        {
            return value;
        }

        if (value.IsSeries() && value.TryConvertToNumber(out var convertedSeries))
        {
            return ConvertToNumber(convertedSeries);
        }

        var unit = GameEventScriptValue.TryGetNumericUnit(value, out var numericUnit)
            ? numericUnit
            : (GameEventScriptNumericUnit?)null;

        if (!GesValueOperations.TryCoerceNumericForOperation(value, out var number))
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (number.IsNaN)
        {
            return GesFloatNaN();
        }

        if (number.IsPositiveInfinity)
        {
            return GesFloatInfinity();
        }

        if (number.IsNegativeInfinity)
        {
            return GesFloatNegativeInfinity();
        }

        if (number.Value >= long.MinValue &&
            number.Value <= long.MaxValue &&
            number.Value == Math.Truncate(number.Value))
        {
            return GesInteger((long)number.Value, unit);
        }

        return GesFloat(number.Value, unit);
    }

    private static GameEventScriptValue ConvertToFloat(GameEventScriptValue value)
    {
        if (GesValueOperations.TryEraseVectorUnit(value, out var vectorWithoutUnit))
        {
            return vectorWithoutUnit;
        }

        if (value is GameEventScriptTagValue && value.TryConvertToNumber(out var convertedTag))
        {
            return ConvertToFloat(convertedTag);
        }

        return GesValueOperations.TryCoerceNumericForOperation(value, out var number)
            ? GesValueOperations.ToGameEventScriptFloat(number)
            : GesFloatNaN();
    }

    private static GameEventScriptValue ConvertToPercentage(GameEventScriptValue value)
    {
        if (value.IsPercentage())
        {
            return value;
        }

        if (value.HasNumericUnit())
        {
            return GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(value, out var number) ||
            !number.IsFinite)
        {
            return GesFloatNaN();
        }

        var ratio = value.IsInteger()
            ? number.Value / 100d
            : number.Value > 1d || number.Value < -1d
                ? number.Value / 100d
                : number.Value;
        return GesPercentage(ratio);
    }

    private static GameEventScriptValue ConvertToNumericUnit(GameEventScriptValue value, GameEventScriptNumericUnit unit)
    {
        if (GesValueOperations.TryApplyVectorUnit(value, unit, out var vectorWithUnit))
        {
            return vectorWithUnit;
        }

        if (GameEventScriptValue.TryGetNumericUnit(value, out var existingUnit) && existingUnit != unit)
        {
            return GesFloatNaN();
        }

        if (value is GameEventScriptNumberValue { IsIntegerValue: true } &&
            GesValueOperations.TryCoerceNumericForOperation(value, out var integerNumber) &&
            integerNumber.IsFinite)
        {
            return GesInteger(GesValueOperations.ToIntegerSaturated(integerNumber.Value), unit);
        }

        if (value.Kind == GameEventScriptValueKind.Number &&
            GesValueOperations.TryCoerceNumericForOperation(value, out var number) &&
            number.IsFinite)
        {
            return GesFloat(number.Value, unit);
        }

        return GesFloatNaN();
    }

    private static GameEventScriptValue ConvertToVector(GameEventScriptValue value)
    {
        if (value is GameEventScriptVectorValue vector)
        {
            return vector;
        }

        if (value is GameEventScriptPointValue)
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (TryCreateSpatialFromMembers("vector", value, out var vectorFromMembers))
        {
            return vectorFromMembers;
        }

        var items = value.AsList();
        if (items.Count is >= 1 and <= 3 &&
            TryCreateSpatialFromComponents("vector", items, out var vectorFromItems))
        {
            return vectorFromItems;
        }

        return TryCreateSpatialFromComponents("vector", [value], out var vectorFromScalar)
            ? vectorFromScalar
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue ConvertToPoint(GameEventScriptValue value)
    {
        if (value is GameEventScriptPointValue point)
        {
            return point;
        }

        if (value is GameEventScriptVectorValue)
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (TryCreateSpatialFromMembers("point", value, out var pointFromMembers))
        {
            return pointFromMembers;
        }

        var items = value.AsList();
        if (items.Count is >= 1 and <= 3 &&
            TryCreateSpatialFromComponents("point", items, out var pointFromItems))
        {
            return pointFromItems;
        }

        return TryCreateSpatialFromComponents("point", [value], out var pointFromScalar)
            ? pointFromScalar
            : GameEventScriptNothingValue.Instance;
    }

    private static bool TryCreateSpatialFromMembers(string typeName, GameEventScriptValue value, out GameEventScriptValue spatial)
    {
        var hasX = value.TryGetMapMember("x", out var x);
        var hasY = value.TryGetMapMember("y", out var y);
        var hasZ = value.TryGetMapMember("z", out var z);

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

        var sourceValues = value.AsMap().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
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

        var temporaryNames = sourceValues.Keys.Concat(materializedValues.Keys).Distinct(StringComparer.Ordinal).ToArray();
        var temporarySlots = new List<int>(temporaryNames.Length);
        foreach (var name in temporaryNames)
        {
            if (_slots.TryGetValue(name, out var slot) &&
                (uint)slot < (uint)_activeLocalSlotCount)
            {
                temporarySlots.Add(slot);
            }
        }

        var previousValues = new BytecodeVmValue[temporarySlots.Count];
        var previousAssigned = new bool[temporarySlots.Count];
        for (var index = 0; index < temporarySlots.Count; index++)
        {
            var slot = temporarySlots[index];
            previousValues[index] = _locals[slot];
            previousAssigned[index] = _assignedSlots[slot];
        }

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

            var temporaryBaseSlot = temporarySlots.Count == 0
                ? 0
                : temporarySlots.Max() + 1;
            if (!TryEvaluateLinearHelperExpressionWithIsolatedTemporaries(entryAddress, temporaryBaseSlot, out var linearValue))
            {
                return false;
            }

            value = linearValue.ToGameEventScriptValue();
            return true;
        }
        finally
        {
            for (var index = 0; index < temporarySlots.Count; index++)
            {
                var slot = temporarySlots[index];
                _locals[slot] = previousValues[index];
                _assignedSlots[slot] = previousAssigned[index];
            }
        }
    }

    private static bool TryGetSeriesTarget(GameEventScriptValue value, out GameEventScriptSeriesValue series)
    {
        if (value is GameEventScriptSeriesValue direct)
        {
            series = direct;
            return true;
        }

        series = default!;
        return false;
    }

    private static IEnumerable<GameEventScriptValue> EnumerateListLikeValue(GameEventScriptValue value)
    {
        return value.Kind is GameEventScriptValueKind.Range
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
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private static GameEventScriptValue MaterializeDistinctItems(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        return target.Kind switch
        {
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

    private void EnterLinearScope(int additionalLocalSlots)
    {
        if (additionalLocalSlots <= 0)
        {
            return;
        }

        checked
        {
            _activeLocalSlotCount += additionalLocalSlots;
        }

        EnsureLocalCapacity(_activeLocalSlotCount);
    }

    private void ExitLinearScope(int localSlotCount)
    {
        if (localSlotCount <= 0)
        {
            return;
        }

        var previousActiveLocalSlotCount = Math.Max(0, _activeLocalSlotCount - localSlotCount);
        ClearLocalSlots(previousActiveLocalSlotCount);
    }

    private void ClearLocalSlots(int previousActiveLocalSlotCount)
    {
        if (previousActiveLocalSlotCount < _activeLocalSlotCount)
        {
            for (var slot = previousActiveLocalSlotCount; slot < _activeLocalSlotCount && slot < _locals.Length; slot++)
            {
                _assignedSlots[slot] = false;
                _locals[slot] = BytecodeVmValue.Nothing;
            }
        }

        _activeLocalSlotCount = previousActiveLocalSlotCount;
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
        if ((uint)slot >= (uint)_activeLocalSlotCount)
        {
            return false;
        }

        _locals[slot] = value;
        _assignedSlots[slot] = true;
        return true;
    }

    private BytecodeVmValue Resolve(string name)
        => _slots.TryGetValue(name, out var slot) &&
           (uint)slot < (uint)_activeLocalSlotCount &&
           _assignedSlots[slot]
            ? _locals[slot]
            : BytecodeVmValue.Nothing;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BytecodeVmValue ResolveSlot(int slot)
        => (uint)slot < (uint)_activeLocalSlotCount && _assignedSlots[slot]
            ? _locals[slot]
            : BytecodeVmValue.Nothing;

    private void EnsureLocalCapacity(int slotCount)
    {
        if (slotCount <= _locals.Length)
        {
            return;
        }

        Array.Resize(ref _locals, slotCount);
        Array.Resize(ref _assignedSlots, slotCount);
    }

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
        IReadOnlyList<string?> parameterTypes,
        LinearArgumentSource arguments)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[Math.Min(parameters.Count, arguments.Count)];
        for (var argumentIndex = 0; argumentIndex < pairs.Length; argumentIndex++)
        {
            if (!arguments.TryGetValue(argumentIndex, out var argumentValue))
            {
                argumentValue = BytecodeVmValue.Nothing;
            }

            if (callableKind == GameEventScriptBytecodeCallableKind.Predicate &&
                !TryConvertParameterType(parameterTypes, argumentIndex, argumentValue, out argumentValue))
            {
                argumentValue = BytecodeVmValue.Nothing;
            }

            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                parameters[argumentIndex],
                argumentValue.ToGameEventScriptValue());
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
    Reference,
    Iterator,
    CollectionBuilder
}

internal readonly record struct BytecodeVmValue(
    BytecodeVmValueKind Kind,
    double Number,
    long IntegerValue,
    bool BooleanValue,
    GameEventScriptNumericUnit? Unit,
    GameEventScriptValue? ReferenceValue,
    BytecodeVmIterator? IteratorValue,
    BytecodeVmCollectionBuilder? CollectionBuilderValue)
{
    public static BytecodeVmValue Nothing { get; } = new(BytecodeVmValueKind.Nothing, 0d, 0, false, null, null, null, null);

    public static BytecodeVmValue Boolean(bool value)
        => new(BytecodeVmValueKind.Boolean, value ? 1d : 0d, value ? 1 : 0, value, null, null, null, null);

    public static BytecodeVmValue Integer(long value, GameEventScriptNumericUnit? unit = null)
        => new(BytecodeVmValueKind.Integer, value, value, value != 0, unit, null, null, null);

    public static BytecodeVmValue Float(double value, GameEventScriptNumericUnit? unit = null)
        => new(BytecodeVmValueKind.Float, value, ToLongSaturated(value), value != 0d, unit, null, null, null);

    public static BytecodeVmValue Numeric(double value, GameEventScriptNumericUnit? unit = null)
        => double.IsFinite(value) &&
           value >= long.MinValue &&
           value <= long.MaxValue &&
           value == Math.Truncate(value)
            ? Integer((long)value, unit)
            : Float(value, unit);

    public static BytecodeVmValue Percentage(double ratio)
        => new(BytecodeVmValueKind.Percentage, ratio, ToLongSaturated(ratio * 100d), ratio != 0d, null, null, null, null);

    public static BytecodeVmValue Reference(GameEventScriptValue value)
        => new(BytecodeVmValueKind.Reference, 0d, 0, value.AsBoolean(), null, value, null, null);

    public static BytecodeVmValue Iterator(BytecodeVmIterator iterator)
        => new(BytecodeVmValueKind.Iterator, 0d, 0, false, null, null, iterator, null);

    public static BytecodeVmValue CollectionBuilder(BytecodeVmCollectionBuilder builder)
        => new(BytecodeVmValueKind.CollectionBuilder, 0d, 0, false, null, null, null, builder);

    public static BytecodeVmValue NaN()
        => Reference(GesFloatNaN());

    public static BytecodeVmValue FromGameEventScriptValue(GameEventScriptValue value)
        => value switch
        {
            GameEventScriptBooleanValue boolean => Boolean(boolean.Value),
            GameEventScriptNumberValue { IsIntegerValue: true } integer => Integer(integer.IntegerValue, integer.Unit),
            GameEventScriptNumberValue floatValue when floatValue.HasSemanticValue() => Float(floatValue.NumberValue, floatValue.Unit),
            GameEventScriptPercentageValue percentage => Percentage(percentage.Ratio),
            _ => Reference(value)
        };

    public static BytecodeVmValue FromGameEventScriptFastValue(GameEventScriptFastValue value)
        => value.Kind switch
        {
            GameEventScriptValueKind.Nothing => Nothing,
            GameEventScriptValueKind.Boolean => Boolean(value.Boolean),
            GameEventScriptValueKind.Number when !value.IsReferenceBacked && value.IsIntegerNumber => Integer(value.Integer, value.Unit),
            GameEventScriptValueKind.Number when !value.IsReferenceBacked => Float(value.Number, value.Unit),
            GameEventScriptValueKind.Percentage when !value.IsReferenceBacked => Percentage(value.Number),
            _ => FromGameEventScriptValue(value.ToGameEventScriptValue())
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
                if (reference is GameEventScriptNumberValue { IsIntegerValue: true } integer)
                {
                    value = integer.IntegerValue;
                    return true;
                }

                if (GesValueOperations.TryCoerceNumericForOperation(reference, out var number) && number.IsFinite)
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
            BytecodeVmValueKind.Iterator => GameEventScriptNothingValue.Instance,
            BytecodeVmValueKind.CollectionBuilder => GameEventScriptNothingValue.Instance,
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

    public static BytecodeVmValue Add(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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

            return NaN();
        }

        if (leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.AddNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Subtract(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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
        if (TryPropagateNothing(left, right, out var nothingResult))
        {
            return nothingResult;
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

    private static bool TryPropagateNothing(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (left.IsNothingLike() || right.IsNothingLike())
        {
            value = Nothing;
            return true;
        }

        value = default;
        return false;
    }

    private static BytecodeVmValue FromFloatNumeric(GesValueOperations.NumericValue number, GameEventScriptNumericUnit? unit = null)
        => number.IsFinite
            ? Numeric(number.Value, unit)
            : Reference(GesValueOperations.ToGameEventScriptNumber(number));

    private static BytecodeVmValue FromFinitePrimitiveNumericResult(
        BytecodeVmValue left,
        string operation,
        BytecodeVmValue right,
        double value,
        GameEventScriptNumericUnit? unit = null)
        => double.IsFinite(value) &&
           value >= long.MinValue &&
           value <= long.MaxValue &&
           value == Math.Truncate(value)
            ? Integer((long)value, unit)
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
                    value = Numeric((double)leftInteger / rightInteger, divideUnit);
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

internal abstract class BytecodeVmIterator : IDisposable
{
    public static BytecodeVmIterator Empty { get; } = new EmptyIterator();

    public virtual GameEventScriptValue SourceTarget => GameEventScriptListValue.Empty;

    public virtual bool IsTransformed => false;

    public GameEventScriptValue EffectiveTarget => IsTransformed ? GameEventScriptListValue.Empty : SourceTarget;

    public abstract bool TryMoveNext(out BytecodeVmValue value);

    public virtual void Dispose()
    {
    }

    private sealed class EmptyIterator : BytecodeVmIterator
    {
        public override bool TryMoveNext(out BytecodeVmValue value)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }
    }
}

internal sealed class BytecodeVmRangeIterator(long from, long to, long step) : BytecodeVmIterator
{
    private long _current = from;
    private bool _completed;

    public override bool TryMoveNext(out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (_completed || (step > 0 ? _current > to : _current < to))
        {
            return false;
        }

        value = BytecodeVmValue.Integer(_current);
        Advance();
        return true;
    }

    private void Advance()
    {
        if (step > 0)
        {
            if (long.MaxValue - _current < step)
            {
                _completed = true;
                return;
            }
        }
        else if (long.MinValue - _current > step)
        {
            _completed = true;
            return;
        }

        _current += step;
    }
}

internal sealed class BytecodeVmCollectionIterator(GameEventScriptValue sourceTarget, IEnumerator<GameEventScriptValue> items) : BytecodeVmIterator
{
    private bool _disposed;

    public override GameEventScriptValue SourceTarget { get; } = sourceTarget;

    public override bool TryMoveNext(out BytecodeVmValue value)
    {
        if (_disposed || !items.MoveNext())
        {
            value = BytecodeVmValue.Nothing;
            Dispose();
            return false;
        }

        value = BytecodeVmValue.FromGameEventScriptValue(items.Current);
        return true;
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        items.Dispose();
        _disposed = true;
    }
}

internal sealed class BytecodeVmPipelineIterator(
    GesBytecodeVmExecutionSession session,
    BytecodeVmIterator source,
    int entryAddress,
    int itemSlot,
    IReadOnlyList<BytecodeVmValue> captures) : BytecodeVmIterator
{
    private bool _disposed;

    public override GameEventScriptValue SourceTarget => source.SourceTarget;

    public override bool IsTransformed => true;

    public override bool TryMoveNext(out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (_disposed)
        {
            return false;
        }

        while (source.TryMoveNext(out var item))
        {
            if (!session.TryEvaluatePipelineIteratorEntry(entryAddress, itemSlot, item, captures, out var yielded, out value))
            {
                Dispose();
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (yielded)
            {
                return true;
            }
        }

        Dispose();
        return false;
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        source.Dispose();
        _disposed = true;
    }
}

internal sealed class BytecodeVmCollectionBuilder
{
    private readonly List<GameEventScriptValue> _items = [];

    public static BytecodeVmCollectionBuilder List() => new();

    public int Count => _items.Count;

    public void Add(BytecodeVmValue value)
        => _items.Add(value.ToGameEventScriptValue());

    public BytecodeVmValue Finish()
        => BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(_items));
}
