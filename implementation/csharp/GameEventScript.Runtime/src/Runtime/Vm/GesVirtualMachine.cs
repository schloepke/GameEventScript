// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBindingSegment;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Values.GesValue.GesValueFlags;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal partial class GesVirtualMachine
{
    private const int InitialRegisterCapacity = 32;

    internal enum StateValue
    {
        Ready,
        Processing,
        Finished,
        Error
    }

    internal struct CallFrame
    {
        internal ushort InstructionPointer;
        internal ushort? ResultRegisterIndex;
        internal ushort RegisterFrameStart;
        internal ushort RegisterFrameLength;
        internal bool NormalizeResultAsPredicate;
        internal GesLiteralEvaluation? LiteralContinuation;
    }

    internal GesValue[] EmptyList { get; init; }
    internal GesLinkedProgram? ActiveProgram { get; private set; }
    internal GameEventScriptProgram Program => ActiveProgram!.Program;
    internal string[] StringPool => ActiveProgram!.StringPool;

    internal StateValue State { get; set; }

    internal ushort InstructionPointer { get; private set; }
    internal ushort CallStackPointer { get; private set; }
    internal CallFrame[] CallStack { get; init; }

    internal GesValue[] RegisterValues { get; private set; }

    internal GesRandomGenerator RandomGenerator { get; private set; }
    internal int RandomScopeDepth => RandomGenerator is null ? 0 : RandomGenerator.ScopeDepth;

    internal ushort RegisterFrameStart = 0;
    internal ushort RegisterFrameLength = 0;
    internal ushort StageLength { get; private set; }

    internal string? ErrorMessage { get; private set; }
    internal GesDiagnostic? ErrorDiagnostic { get; private set; }
    internal string? ActiveHandlerName { get; private set; }
    internal GesLinkedProgram.OutboundMessageSignature[] OutboundMessageSignatures => ActiveProgram!.OutboundMessageSignatures;
    internal GesBindingSegment.Entry[] RecordConstructors => ActiveProgram!.RecordConstructors;
    internal GesBindingSegment.Entry[] ExtensionCallBinds => ActiveProgram!.ExtensionCallBinds;
    internal GesBindingSegment.Entry[] ExternalTypeBinds => ActiveProgram!.ExternalTypeBinds;
    internal IGesExtensionFunction?[] BoundExtensionCalls => ActiveProgram!.BoundExtensionCalls;
    internal IGesExternalTypeConstructor?[] BoundExternalTypeConstructors => ActiveProgram!.BoundExternalTypeConstructors;
    internal GesExtensionCall ExtensionCall { get; }
    private GesExternalTypeConstructorCall? _externalTypeConstructorCall;
    internal GesExternalTypeConstructorCall ExternalTypeConstructorCall => _externalTypeConstructorCall ??= new GesExternalTypeConstructorCall();

    internal ushort CodeSegmentSize => ActiveProgram!.CodeSegmentSize;
    internal readonly int MaxRegisterCount;

    internal GameEventScriptMessage? ProcessingMessage { get; private set; }
    internal GesVirtualMachine(ushort registerSize, ushort stackSize)
    {
        MaxRegisterCount = registerSize;
        EmptyList = [];
        InstructionPointer = 0;
        CallStackPointer = 0;
        CallStack = new CallFrame[stackSize];
        RegisterValues = new GesValue[Math.Min(InitialRegisterCapacity, MaxRegisterCount)];
        RandomGenerator = null!;
        ExtensionCall = new GesExtensionCall();
    }

    internal bool PrepareCapacity(GesLinkedProgram program)
        => PrepareCapacity(program.RequiredRegisterCapacity);

    internal bool PrepareCapacity(int required)
    {
        if (State != StateValue.Ready) return false;

        if (!EnsureRegisterCapacity(required))
            throw new GameEventScriptVmException(ErrorDiagnostic ?? new GesDiagnostic(
                GesDiagnosticPhase.Runtime,
                GesDiagnosticCodes.RuntimeRegisterOverflow,
                ErrorMessage ?? "VM register capacity could not be prepared."));
        return true;
    }


    internal bool PrepareStateForMessage(GesLinkedProgram program, GameEventScriptMessage message, bool callAsArguments, ushort entryAddress, string handlerName, GesContext context)
    {
        if (State != Ready) return RaiseError(GesDiagnosticCodes.RuntimeVmStateConflict, "State not ready to receive new messages.");
        ActiveProgram = program ?? throw new ArgumentNullException(nameof(program));
        ActiveHandlerName = handlerName;
        if (entryAddress >= CodeSegmentSize) return RaiseError(GesDiagnosticCodes.RuntimePreparationFailed, $"Illegal entry address {entryAddress} for message.");
        InstructionPointer = entryAddress;
        RegisterFrameStart = 0;
        StageLength = 0;
        RandomGenerator = context.Random;
        if (callAsArguments)
        {
            var arguments = message.Arguments;
            if (!EnsureRegisterCapacity(arguments.Count)) return false;
            RegisterFrameLength = (ushort)arguments.Count;
            for (var i = 0; i < RegisterFrameLength; i++)
            {
                SetValue((ushort)i, in arguments.VmValueAt(i));
            }
        }
        else
        {
            if (!EnsureRegisterCapacity(1)) return false;
            RegisterFrameLength = 1;
            RegisterValues[0].SetMessage(message);
        }
        State = Processing;
        ProcessingMessage = message;
        return true;
    }
    internal ref readonly GesValue Register(ushort index) => ref RegisterValues[index + RegisterFrameStart];
    internal bool IsRegisterTrue(ushort index) => RegisterValues[index + RegisterFrameStart].IsTrue;
    internal bool IsRegisterFalse(ushort index) => RegisterValues[index + RegisterFrameStart].IsFalse;
    internal bool IsRegisterNotTrue(ushort index) => RegisterValues[index + RegisterFrameStart].IsNotTrue;
    internal bool IsRegisterNothing(ushort index) => RegisterValues[index + RegisterFrameStart].Kind is GesBytecodeTypeKind.Nothing;
    internal ref readonly GesValue RegisterStaged(ushort index) => ref RegisterValues[index + RegisterFrameStart + RegisterFrameLength];
    internal void SetNothing(ushort index) => RegisterValues[index + RegisterFrameStart].SetNothing();
    internal void SetValue(ushort index, in GesValue value) => RegisterValues[index + RegisterFrameStart] = value;
    internal void SetBoolean(ushort index, bool value) => RegisterValues[index + RegisterFrameStart].SetBoolean(value);
    internal void SetInteger(ushort index, long value, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetInteger(value, unit);
    internal void SetFloat(ushort index, double value, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetFloat(value, unit);
    internal void SetPercentage(ushort index, double ratio) => RegisterValues[index + RegisterFrameStart].SetPercentage(ratio);
    internal void SetTextPointer(ushort index, ushort pointer) => RegisterValues[index + RegisterFrameStart].SetTextConstant(ActiveProgram!.TextConstants[pointer]);
    internal void SetText(ushort index, string text) => RegisterValues[index + RegisterFrameStart].SetText(text);
    internal void SetTagPointer(ushort index, ushort pointer) => RegisterValues[index + RegisterFrameStart].SetTextConstant(ActiveProgram!.TextConstants[pointer], tag: true);
    internal void SetTag(ushort index, string tag) => RegisterValues[index + RegisterFrameStart].SetTag(tag);
    internal void SetVector(ushort index, double x, double y, double z, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetVector(x, y, z, unit);
    internal void SetVector(ushort index, GesValueVectorPoint vector, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetVector(vector, unit);
    internal void SetPoint(ushort index, double x, double y, double z, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetPoint(x, y, z, unit);
    internal void SetPoint(ushort index, GesValueVectorPoint point, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetPoint(point, unit);
    internal void SetDice(ushort index, int[] values) => RegisterValues[index + RegisterFrameStart].SetDice(values);
    internal void SetList(ushort index, GesValue[] list) => RegisterValues[index + RegisterFrameStart].SetList(list);
    internal void SetMap(ushort index, GesValueMap valueMap) => RegisterValues[index + RegisterFrameStart].SetMap(valueMap);
    internal void SetRecord(ushort index, string typeName, GesValueMap record) => RegisterValues[index + RegisterFrameStart].SetRecord(typeName, record);
    internal void SetExternalType(ushort index, IGesExternalValue value) => RegisterValues[index + RegisterFrameStart].SetExternalType(value);
    internal void SetRange(ushort index, long from, long to, long step) => RegisterValues[index + RegisterFrameStart].SetRange(from, to, step);
    internal void SetRange(ushort index, double from, double to, double step) => RegisterValues[index + RegisterFrameStart].SetRange(from, to, step);
    internal void SetMessageHandler(ushort index, GesMessageSignature handler) => RegisterValues[index + RegisterFrameStart].SetMessageHandler(handler);
    internal void SetMessage(ushort index, GameEventScriptMessage message) => RegisterValues[index + RegisterFrameStart].SetMessage(message);
    internal void SetSeries(ushort index, GesSeries series) => RegisterValues[index + RegisterFrameStart].SetSeries(series);
    internal void SetIterator(ushort index, IGesIterator value) => RegisterValues[index + RegisterFrameStart].SetIterator(value);
    internal void CreateListBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetListBuilder(new GesVmListBuilder());
    internal void CreateMapBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetMapBuilder(new GesVmMapBuilder());
    internal void CreateDistinctBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetDistinctBuilder(new GesVmDistinctBuilder());
    internal void CreateGroupBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetGroupBuilder(new GesVmGroupBuilder(this));
    internal void CreateOrderBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetOrderBuilder(new GesVmOrderBuilder());

    internal bool RaiseError(string code, string message, string? technicalDetails = null, string? symbol = null)
    {
        State = Error;
        ErrorMessage = message;
        ErrorDiagnostic = new GesDiagnostic(
            GesDiagnosticPhase.Runtime,
            code,
            message,
            Symbol: symbol,
            ProgramName: ActiveProgram?.Program.ModuleName,
            HandlerName: ActiveHandlerName,
            TechnicalDetails: technicalDetails);
        return false;
    }
    internal bool RaiseError(GesDiagnostic diagnostic, string? technicalDetails = null)
    {
        State = Error;
        ErrorMessage = diagnostic.Message;
        ErrorDiagnostic = diagnostic with
        {
            ProgramName = diagnostic.ProgramName ?? ActiveProgram?.Program.ModuleName,
            HandlerName = diagnostic.HandlerName ?? ActiveHandlerName,
            TechnicalDetails = technicalDetails ?? diagnostic.TechnicalDetails
        };
        return false;
    }
    internal void Reset()
    {
        System.Array.Clear(CallStack, 0, CallStackPointer);
        InstructionPointer = 0;
        CallStackPointer = 0;
        RegisterFrameStart = 0;
        RegisterFrameLength = 0;
        StageLength = 0;
        for (var i = 0; i < RegisterValues.Length; i++) RegisterValues[i].SetNothing();
        RandomGenerator = null!;
        ExtensionCall.EndCall();
        _externalTypeConstructorCall?.EndCall();
        ProcessingMessage = null;
        ActiveProgram = null;
        ActiveHandlerName = null;
        ErrorMessage = null;
        ErrorDiagnostic = null;
        State = Ready;
    }
    internal void JumpAddress(ushort address)
    {
        InstructionPointer = address;
    }
    internal bool CallAddress(ushort address, ushort? resultRegister = null, bool normalizeResultAsPredicate = false)
    {
        var nextRegisterFrameStart = RegisterFrameStart + RegisterFrameLength;
        var nextRegisterFrameLength = StageLength;
        if (!EnsureRegisterCapacity(nextRegisterFrameStart + nextRegisterFrameLength)) return false;
        if (CallStackPointer >= CallStack.Length)
            return RaiseError(GesDiagnosticCodes.RuntimeCallStackOverflow, "Call stack overflow.");
        CallStack[CallStackPointer++] = new CallFrame
        {
            InstructionPointer = InstructionPointer,
            ResultRegisterIndex = resultRegister,
            RegisterFrameStart = RegisterFrameStart,
            RegisterFrameLength = RegisterFrameLength,
            NormalizeResultAsPredicate = normalizeResultAsPredicate
        };
        InstructionPointer = address;
        RegisterFrameStart = checked((ushort)nextRegisterFrameStart);
        RegisterFrameLength = nextRegisterFrameLength;
        StageLength = 0;
        return true;
    }
    internal bool CallRecordConstructor(ushort recordId, ushort resultRegister)
    {
        if (recordId >= RecordConstructors.Length)
            return RaiseError(GesDiagnosticCodes.RuntimeInvalidRecordConstructor, $"Record constructor '{recordId}' was not found.");
        var bind = RecordConstructors[recordId];
        if (bind.Kind != GesBinaryBindKind.Record)
            return RaiseError(GesDiagnosticCodes.RuntimeInvalidRecordConstructor, $"Record constructor '{recordId}' has invalid bind kind.");
        if (bind.EntryAddress >= CodeSegmentSize)
            return RaiseError(GesDiagnosticCodes.RuntimeInvalidRecordConstructor, $"Record constructor '{recordId}' has an invalid entry address.");
        if (StageLength != bind.ArgumentNames.Count)
            return RaiseError(GesDiagnosticCodes.RuntimeInvalidRecordConstructor, $"Record constructor '{recordId}' has the wrong number of arguments.");
        return CallAddress(bind.EntryAddress, resultRegister);
    }
    internal void ReturnVoid()
    {
        if (CallStackPointer == 0)
        {
            ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
            RegisterFrameLength = 0;
            StageLength = 0;
            State = Finished;
            return;
        }

        var callFrame = CallStack[--CallStackPointer];
        CallStack[CallStackPointer] = default;
        ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        StageLength = 0;
        if (callFrame.ResultRegisterIndex.HasValue) RegisterValues[callFrame.ResultRegisterIndex.Value + RegisterFrameStart].SetNothing();
        callFrame.LiteralContinuation?.Resume(default);
    }
    internal void ReturnValue(ushort registerIndex)
    {
        var result = RegisterValues[registerIndex + RegisterFrameStart];
        if (CallStackPointer == 0)
        {
            ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
            RegisterFrameLength = 0;
            StageLength = 0;
            State = Finished;
            return;
        }

        var callFrame = CallStack[--CallStackPointer];
        CallStack[CallStackPointer] = default;
        ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        StageLength = 0;
        if (!callFrame.ResultRegisterIndex.HasValue) return;
        if (callFrame.NormalizeResultAsPredicate && result.Kind is not GesBytecodeTypeKind.Boolean && !result.IsNothing) result.SetNothing();
        RegisterValues[callFrame.ResultRegisterIndex.Value + RegisterFrameStart] = result;
        callFrame.LiteralContinuation?.Resume(result);
    }
    internal GesBytecodeInstruction FetchInstructionAndIncrementInstructionPointer()
    {
        if (InstructionPointer < CodeSegmentSize) return Program.Code[InstructionPointer++];
        throw new GameEventScriptVmException(new GesDiagnostic(
            GesDiagnosticPhase.Runtime,
            GesDiagnosticCodes.RuntimeInstructionPointerOutOfRange,
            "Instruction pointer is outside the code segment.",
            ProgramName: ActiveProgram?.Program.ModuleName,
            HandlerName: ActiveHandlerName));
    }
    internal string FetchStringByPointer(ushort index) => StringPool[index];
    internal GesUInt16IndexList FetchUInt16SliceTableByPointer(ushort index) => Program.UInt16IndexLists.Resolve(index);
    internal void ModifyLocalRegisters(short registerCount)
    {
        switch (registerCount)
        {
            case > 0:
                var requiredTotalRegisters = RegisterFrameStart + RegisterFrameLength + registerCount;
                if (EnsureRegisterCapacity(requiredTotalRegisters))
                {
                    RegisterFrameLength += (ushort)registerCount;
                }

                break;
            case < 0:
                var tempRegisterCount = -registerCount;
                if (RegisterFrameLength < tempRegisterCount)
                {
                    RaiseError(GesDiagnosticCodes.RuntimePreparationFailed,
                        "Inconsistent register frame length. Cannot remove more registers than are available.");
                }
                else
                {
                    ClearRegisterRange(RegisterFrameStart + RegisterFrameLength - tempRegisterCount, tempRegisterCount);
                    RegisterFrameLength -= (ushort)tempRegisterCount;
                }

                break;
        }
    }
    internal void ClearStage()
    {
        ClearRegisterRange(RegisterFrameStart + RegisterFrameLength, StageLength);
        StageLength = 0;
    }
    internal void StageRegister(ushort index)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex] = RegisterValues[index + RegisterFrameStart];
    }
    internal void StageValue(in GesValue value)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex] = value;
    }
    internal void StageNothing()
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetNothing();
    }
    internal void StageBoolean(bool value)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetBoolean(value);
    }
    internal void StageInteger(long value, GesQuantityUnit unit)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetInteger(value, unit);
    }
    internal void StageFloat(double value, GesQuantityUnit unit)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetFloat(value, unit);
    }
    internal void StagePercentage(double value)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetPercentage(value);
    }
    internal void StageTextConstant(ushort constantIndex)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetTextConstant(ActiveProgram!.TextConstants[constantIndex]);
    }
    internal void StageTagConstant(ushort constantIndex)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetTextConstant(ActiveProgram!.TextConstants[constantIndex], tag: true);
    }
    private int AddStageRegister()
    {
        var stageRegisterIndex = RegisterFrameStart + RegisterFrameLength + StageLength;
        if (!EnsureRegisterCapacity(stageRegisterIndex + 1)) return -1;
        StageLength++;
        return stageRegisterIndex;
    }
    private bool EnsureRegisterCapacity(int requiredRegisters)
    {
        if (requiredRegisters <= RegisterValues.Length) return true;
        if (requiredRegisters > MaxRegisterCount)
            return RaiseError(GesDiagnosticCodes.RuntimeRegisterOverflow,
                $"Register overflow. Required {requiredRegisters} registers but maximum is {MaxRegisterCount}.");
        var newLength = RegisterValues.Length;
        do
        {
            newLength = Math.Min(checked(newLength * 2), MaxRegisterCount);
        } while (newLength < requiredRegisters);

        var oldLength = RegisterValues.Length;
        var expanded = new GesValue[newLength];
        Array.Copy(RegisterValues, expanded, oldLength);
        RegisterValues = expanded;

        return true;
    }
    private void ClearRegisterRange(int start, int count)
    {
        var end = start + count;
        for (var index = start; index < end; index++)
        {
            RegisterValues[index].SetNothing();
        }
    }

}
