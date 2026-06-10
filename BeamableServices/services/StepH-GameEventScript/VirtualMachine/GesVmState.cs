#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.VirtualMachine.GesVmState.StateValue;

namespace StepH.GameEventScript.VirtualMachine;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal class GesVmState
{
    private const int InitialRegisterCapacity = 32;
    private const int RegisterCapacityGrowth = 32;

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
    }

    internal GesVmListObject EmptyList { get; init; }

    internal GameEventScriptBinary Binary { get; init; }

    internal StateValue State { get; set; }

    internal ushort InstructionPointer { get; private set; }
    internal ushort CallStackPointer { get; private set; }
    internal CallFrame[] CallStack { get; init; }

    internal GesVmValue[] RegisterSlots { get; private set; }
    private GesVmValue _overflowRegister;

    internal ushort RandomGeneratorsPointer { get; private set; } = 0;
    internal GameEventScriptRandomGenerator[] RandomGenerators { get; init; }
    internal GameEventScriptRandomGenerator RandomGenerator { get; private set; }

    internal ushort RegisterFrameStart = 0;
    internal ushort RegisterFrameLength = 0;
    internal ushort StageLength { get; private set; }

    internal string? ErrorMessage { get; private set; }
    internal GameEventScriptBinaryBindEntry[] OutboundMessageSignatures { get; init; }
    internal GameEventScriptBinaryBindEntry[] RecordConstructors { get; init; }

    internal readonly ushort CodeSegmentSize;
    internal readonly int MaxRegisterSlots;
    
    internal GameEventScriptMessage? ProcessingMessage { get; private set; }
    internal GesVmState(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        MaxRegisterSlots = Math.Max(InitialRegisterCapacity, (int)registerSize);
        EmptyList = new GesVmListObject(this, 0);
        Binary = binary;
        CodeSegmentSize = checked((ushort)binary.InstructionTable.Length);
        InstructionPointer = 0;
        CallStackPointer = 0;
        CallStack = new CallFrame[stackSize];
        RegisterSlots = CreateRegisterArray(InitialRegisterCapacity);
        _overflowRegister.InitRegister(this);
        RandomGenerators = new GameEventScriptRandomGenerator[16];
        RandomGeneratorsPointer = 0;
        RandomGenerator = GameEventScriptRandomGenerator.Create(); // INFO: We create one here so that we always have one. but it should be set for the message from the context!
        OutboundMessageSignatures = BuildIdIndexedBindTable(binary, GameEventScriptBinaryBindKind.OutboundMessage);
        RecordConstructors = BuildIdIndexedBindTable(binary, GameEventScriptBinaryBindKind.Record);
    }

    internal bool PrepareStateForMessage(GameEventScriptMessage message, bool callAsArguments, ushort entryAddress, GameEventScriptSession session)
    {
        if (State != Ready) return RaiseError("State not ready to receive new messages.");
        if (entryAddress >= CodeSegmentSize) return RaiseError($"Illegal entry address {entryAddress} for message.");
        InstructionPointer = entryAddress;
        RegisterFrameStart = 0;
        StageLength = 0;
        RandomGenerator = session.Random;
        RandomGeneratorsPointer = 0;
        if (callAsArguments)
        {
            var arguments = message.Arguments;
            if (!EnsureRegisterCapacity(arguments.Count)) return false;
            RegisterFrameLength = (ushort)arguments.Count;
            for (var i = 0; i < RegisterFrameLength; i++) RegisterSlots[i].BindArguments(arguments[i]);
        }
        else
        {
            if (!EnsureRegisterCapacity(1)) return false;
            RegisterFrameLength = 1;
            RegisterSlots[0].SetMessage(message);
        }
        State = Processing;
        ProcessingMessage = message;
        return true;
    }
    internal ref GesVmValue Register(ushort index) => ref RegisterSlots[index + RegisterFrameStart];
    internal ref GesVmValue ConditionalRegister(ushort index)
    {
        RegisterSlots[index + RegisterFrameStart].UpdatedTextTruthinessCache();
        return ref RegisterSlots[index + RegisterFrameStart];
    } 
    internal ref GesVmValue RegisterStaged(ushort index) => ref RegisterSlots[index + RegisterFrameStart + RegisterFrameLength];
    internal GesVmValue[] CreateRegisterArray(int size)
    {
        var values = new GesVmValue[size];
        for (var i = 0; i < values.Length; i++) values[i].InitRegister(this);
        return values;
    }
    internal GesVmListObject CreateList(int size) => size == 0 ? EmptyList : new GesVmListObject(this, size);
    internal GesVmMapObject CreateMap(IReadOnlyDictionary<string, GesVmValue> entries) => new(this, entries);
    internal GesVmValue CreateNothing()
    {
        var value = new GesVmValue();
        value.InitRegister(this);
        value.SetNothing();
        return value;
    }

    internal GesVmValue CreateBoolean(bool boolean)
    {
        var value = new GesVmValue();
        value.InitRegister(this);
        value.SetBoolean(boolean);
        return value;
    }
    internal GesVmValue CreateInteger(long integer)
    {
        var value = new GesVmValue();
        value.InitRegister(this);
        value.SetInteger(integer);
        return value;
    }
    internal GesVmValue CreateText(string text)
    {
        var value = new GesVmValue();
        value.InitRegister(this);
        value.SetText(text);
        return value;
    }
    internal GesVmValue CreateTag(string tag)
    {
        var value = new GesVmValue();
        value.InitRegister(this);
        value.SetTag(tag);
        return value;
    }
    internal bool PushRandom(GameEventScriptRandomGenerator randomGenerator)
    {
        if (RandomGeneratorsPointer >= RandomGenerators.Length) return RaiseError("Random generator stack overflow");
        RandomGenerators[RandomGeneratorsPointer++] = randomGenerator;
        RandomGenerator = randomGenerator;
        return true;
    }
    internal bool PopRandom()
    {
        if (RandomGeneratorsPointer == 0) return RaiseError("Random generator stack underflow");
        RandomGenerator = RandomGenerators[--RandomGeneratorsPointer];
        return true;
    }
    internal bool RaiseError(string message)
    {
        State = Error;
        ErrorMessage = message;
        return false;
    }
    internal void Reset()
    {
        InstructionPointer = 0;
        CallStackPointer = 0;
        RegisterFrameStart = 0;
        RegisterFrameLength = 0;
        StageLength = 0;
        RandomGeneratorsPointer = 0;
        for (var i = 0; i < RegisterSlots.Length; i++) RegisterSlots[i].SetNothing();
        ProcessingMessage = null;
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
        if (CallStackPointer >= CallStack.Length) return RaiseError("Stack overflow");
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
        if (recordId >= RecordConstructors.Length) return RaiseError($"Record constructor '{recordId}' was not found.");
        var bind = RecordConstructors[recordId];
        if (bind.Kind != GameEventScriptBinaryBindKind.Record) return RaiseError($"Record constructor '{recordId}' has invalid bind kind.");
        if (bind.EntryAddress >= CodeSegmentSize) return RaiseError($"Record constructor '{recordId}' has an invalid entry address.");
        if (StageLength != bind.ArgumentNames.Count) return RaiseError($"Record constructor '{recordId}' has the wrong number of arguments.");
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
        ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        StageLength = 0;
        if (callFrame.ResultRegisterIndex.HasValue) RegisterSlots[callFrame.ResultRegisterIndex.Value + RegisterFrameStart].SetNothing();
    }
    internal void ReturnValue(ushort registerIndex)
    {
        var result = RegisterSlots[registerIndex + RegisterFrameStart];
        if (CallStackPointer == 0)
        {
            ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
            RegisterFrameLength = 0;
            StageLength = 0;
            State = Finished;
            return;
        }

        var callFrame = CallStack[--CallStackPointer];
        ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        StageLength = 0;
        if (!callFrame.ResultRegisterIndex.HasValue) return;
        if (callFrame.NormalizeResultAsPredicate && result.Kind is not GameEventScriptBytecodeTypeKind.Boolean && !result.IsNothing) result.SetNothing();
        RegisterSlots[callFrame.ResultRegisterIndex.Value + RegisterFrameStart] = result;
    }
    internal GameEventScriptBytecodeInstruction FetchInstructionAndIncrementInstructionPointer() => InstructionPointer >= CodeSegmentSize ? throw new OverflowException() : Binary.InstructionTable[InstructionPointer++];
    internal string FetchStringByPointer(ushort index) => Binary.TextConstantTable.Resolve(index);
    internal ReadOnlySpan<ushort> FetchUInt16SliceTableByPointer(ushort index) => Binary.Uint16ConstantTable.Resolve(index);
    internal void ModifyLocalSlots(short slotCount)
    {
        switch (slotCount)
        {
            case > 0:
                var requiredTotalSlots = RegisterFrameStart + RegisterFrameLength + slotCount;
                if (EnsureRegisterCapacity(requiredTotalSlots))
                {
                    RegisterFrameLength += (ushort)slotCount;
                }

                break;
            case < 0:
                var tempSlotCount = -slotCount;
                if (RegisterFrameLength < tempSlotCount)
                {
                    RaiseError("Inconsistent register frame length. Cannot remove more slots than are available.");
                }
                else
                {
                    ClearRegisterRange(RegisterFrameStart + RegisterFrameLength - tempSlotCount, tempSlotCount);
                    RegisterFrameLength -= (ushort)tempSlotCount;
                }

                break;
        }
    }
    internal void ClearStage()
    {
        ClearRegisterRange(RegisterFrameStart + RegisterFrameLength, StageLength);
        StageLength = 0;
    }
    internal void StageRegister(ushort index) => AddStageSlot() = RegisterSlots[index + RegisterFrameStart];
    internal void StageValue(ref GesVmValue value) => AddStageSlot() = value;
    internal void StageNothing() => AddStageSlot().SetNothing();
    internal void StageBoolean(bool value) => AddStageSlot().SetBoolean(value);
    internal void StageInteger(long value, GameEventScriptBytecodeInstructionUnit unit) => AddStageSlot().SetInteger(value, unit);
    internal void StageFloat(double value, GameEventScriptBytecodeInstructionUnit unit) => AddStageSlot().SetFloat(value, unit);
    internal void StagePercentage(double value) => AddStageSlot().SetPercentage(value);
    internal void StageTextConstant(ushort constantIndex) => AddStageSlot().SetTextPointer(constantIndex);
    internal void StageTagConstant(ushort constantIndex) => AddStageSlot().SetTagPointer(constantIndex);
    private ref GesVmValue AddStageSlot()
    {
        var stageRegisterIndex = RegisterFrameStart + RegisterFrameLength + StageLength;
        if (!EnsureRegisterCapacity(stageRegisterIndex + 1)) return ref _overflowRegister;
        StageLength++;
        return ref RegisterSlots[stageRegisterIndex];
    }
    private bool EnsureRegisterCapacity(int requiredSlots)
    {
        if (requiredSlots <= RegisterSlots.Length) return true;
        if (requiredSlots > MaxRegisterSlots) return RaiseError($"Register overflow. Required {requiredSlots} slots but maximum is {MaxRegisterSlots}.");
        var newLength = RegisterSlots.Length;
        do
        {
            newLength = Math.Min(newLength + RegisterCapacityGrowth, MaxRegisterSlots);
        } while (newLength < requiredSlots);

        var oldLength = RegisterSlots.Length;
        var expanded = new GesVmValue[newLength];
        Array.Copy(RegisterSlots, expanded, oldLength);
        RegisterSlots = expanded;
        for (var i = oldLength; i < RegisterSlots.Length; i++)
        {
            RegisterSlots[i].InitRegister(this);
        }

        return true;
    }
    private void ClearRegisterRange(int start, int count)
    {
        var end = start + count;
        for (var index = start; index < end; index++)
        {
            RegisterSlots[index].SetNothing();
        }
    }
 
    private static GameEventScriptBinaryBindEntry[] BuildIdIndexedBindTable(GameEventScriptBinary binary, GameEventScriptBinaryBindKind kind)
    {
        var maxId = -1;
        foreach (var entry in binary.BindTable.Entries)
        {
            if (entry.Kind == kind && entry.Id != ushort.MaxValue && entry.Id > maxId) maxId = entry.Id;
        }

        if (maxId < 0) return [];
        var result = new GameEventScriptBinaryBindEntry[maxId + 1];
        foreach (var entry in binary.BindTable.Entries)
        {
            if (entry.Kind == kind && entry.Id != ushort.MaxValue) result[entry.Id] = entry;
        }

        return result;
    }
    
}
