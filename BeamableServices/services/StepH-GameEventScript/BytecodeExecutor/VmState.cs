#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.BytecodeExecutor.VmState.StateValue;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal class VmState
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

    internal VmListObject EmptyList { get; init; }

    internal GameEventScriptBinary Binary { get; init; }

    internal StateValue State { get; set; }

    internal ushort InstructionPointer { get; private set; }
    internal ushort CallStackPointer { get; private set; }
    internal CallFrame[] CallStack { get; init; }

    internal VmValue[] RegisterSlots { get; private set; }
    private VmValue _overflowRegister;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmState(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        MaxRegisterSlots = Math.Max(InitialRegisterCapacity, (int)registerSize);
        EmptyList = new VmListObject(this, 0);
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
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref VmValue Register(ushort index) => ref RegisterSlots[index + RegisterFrameStart];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref VmValue RegisterStaged(ushort index) => ref RegisterSlots[index + RegisterFrameStart + RegisterFrameLength];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmValue CreateRegister() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmValue[] CreateRegisterArray(int size)
    {
        var values = new VmValue[size];
        for (var i = 0; i < values.Length; i++) values[i].InitRegister(this);
        return values;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmListObject CreateList(int size) => size == 0 ? EmptyList : new VmListObject(this, size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmMapObject CreateMap(IReadOnlyDictionary<string, VmValue> entries) => new(this, entries);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmValue CreateInteger(long integer)
    {
        var value = default(VmValue);
        value.InitRegister(this);
        value.SetInteger(integer);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmValue CreateText(string text)
    {
        var value = default(VmValue);
        value.InitRegister(this);
        value.SetText(text);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmValue CreateTag(string tag)
    {
        var value = default(VmValue);
        value.InitRegister(this);
        value.SetTag(tag);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool PushRandom(GameEventScriptRandomGenerator randomGenerator)
    {
        if (RandomGeneratorsPointer >= RandomGenerators.Length) return RaiseError("Random generator stack overflow");
        RandomGenerators[RandomGeneratorsPointer++] = randomGenerator;
        RandomGenerator = randomGenerator;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool PopRandom()
    {
        if (RandomGeneratorsPointer == 0) return RaiseError("Random generator stack underflow");
        RandomGenerator = RandomGenerators[--RandomGeneratorsPointer];
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool RaiseError(string message)
    {
        State = Error;
        ErrorMessage = message;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Reset()
    {
        InstructionPointer = 0;
        CallStackPointer = 0;
        RegisterFrameStart = 0;
        RegisterFrameLength = 0;
        StageLength = 0;
        RandomGeneratorsPointer = 0;
        for (var i = 0; i < RegisterSlots.Length; i++) RegisterSlots[i].SetNothing();
        State = Ready;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void JumpAddress(ushort address)
    {
        InstructionPointer = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool CallRecordConstructor(ushort recordId, ushort resultRegister)
    {
        if (recordId >= RecordConstructors.Length) return RaiseError($"Record constructor '{recordId}' was not found.");
        var bind = RecordConstructors[recordId];
        if (bind.Kind != GameEventScriptBinaryBindKind.Record) return RaiseError($"Record constructor '{recordId}' has invalid bind kind.");
        if (bind.EntryAddress >= CodeSegmentSize) return RaiseError($"Record constructor '{recordId}' has an invalid entry address.");
        if (StageLength != bind.ArgumentNames.Count) return RaiseError($"Record constructor '{recordId}' has the wrong number of arguments.");
        return CallAddress(bind.EntryAddress, resultRegister);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal GameEventScriptBytecodeInstruction FetchInstructionAndIncrementInstructionPointer() => InstructionPointer >= CodeSegmentSize ? throw new OverflowException() : Binary.InstructionTable[InstructionPointer++];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string FetchStringByPointer(ushort index) => Binary.TextConstantTable.Resolve(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<ushort> FetchUInt16SliceTableByPointer(ushort index) => Binary.Uint16ConstantTable.Resolve(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ClearStage()
    {
        ClearRegisterRange(RegisterFrameStart + RegisterFrameLength, StageLength);
        StageLength = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageRegister(ushort index) => AddStageSlot() = RegisterSlots[index + RegisterFrameStart];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageNothing() => AddStageSlot().SetNothing();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageBoolean(bool value) => AddStageSlot().SetBoolean(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageInteger(long value, GameEventScriptBytecodeInstructionUnit unit) => AddStageSlot().SetInteger(value, unit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageFloat(double value, GameEventScriptBytecodeInstructionUnit unit) => AddStageSlot().SetFloat(value, unit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StagePercentage(double value) => AddStageSlot().SetPercentage(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageTextConstant(ushort constantIndex) => AddStageSlot().SetTextPointer(constantIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageTagConstant(ushort constantIndex) => AddStageSlot().SetTagPointer(constantIndex);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref VmValue AddStageSlot()
    {
        var stageRegisterIndex = RegisterFrameStart + RegisterFrameLength + StageLength;
        if (!EnsureRegisterCapacity(stageRegisterIndex + 1)) return ref _overflowRegister;
        StageLength++;
        return ref RegisterSlots[stageRegisterIndex];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        var expanded = new VmValue[newLength];
        Array.Copy(RegisterSlots, expanded, oldLength);
        RegisterSlots = expanded;
        for (var i = oldLength; i < RegisterSlots.Length; i++)
        {
            RegisterSlots[i].InitRegister(this);
        }

        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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