#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal struct VmState
{
    internal enum StateValue
    {
        Initialized,
        Ready,
        Running,
        Halted,
        Error
    }

    internal struct CallFrame
    {
        internal ushort InstructionPointer;
        internal ushort? ResultRegisterIndex;
        internal ushort RegisterFrameStart;
        internal ushort RegisterFrameLength;
    }

    internal GameEventScriptBinary Binary { get; init; }

    internal StateValue State { get; set; }

    internal ushort InstructionPointer { get; private set; } = 0;
    internal ushort CallStackPointer { get; private set; } = 0;
    internal CallFrame[] CallStack { get; init; }

    internal VmValue[] RegisterSlots { get; init; }

    internal ushort RandomGeneratorsPointer { get; private set; } = 0;
    internal GameEventScriptRandomGenerator[] RandomGenerators { get; init; }
    internal GameEventScriptRandomGenerator RandomGenerator { get; private set; }

    internal ushort RegisterFrameStart = 0;
    internal ushort RegisterFrameLength = 0;
    internal ushort StagedArgumentCount = 0;
    
    internal string? ErrorMessage { get; private set; } = null;

    internal Dictionary<string, GameEventScriptBinaryBindEntry> InboundMessageHandlers { get; init; }
    internal List<GameEventScriptMessageSignature> OutboundMessageSignatures { get; init; }

    internal readonly ushort CodeSegmentSize; 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VmState(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        Binary = binary;
        CodeSegmentSize = checked((ushort)binary.InstructionTable.Length);
        InstructionPointer = 0;
        CallStackPointer = 0;
        CallStack = new CallFrame[stackSize];
        RegisterSlots = new VmValue[registerSize];
        RandomGenerators = new GameEventScriptRandomGenerator[16];
        RandomGeneratorsPointer = 0;
        RandomGenerator = GameEventScriptRandomGenerator.Create(); // INFO: We create one here so that we always have one. but it should be set for the message from the context!
        for (var i = 0; i < RegisterSlots.Length; i++)
        {
            RegisterSlots[i].SetNothing();
        }
        InboundMessageHandlers = binary.BindTable.Entries.Where(x => x.Kind == GameEventScriptBinaryBindKind.MessageHandler).ToDictionary(
            bind => GameEventScriptMessageSignature.CreateSignatureId(binary.TextConstantTable.Resolve(bind.Name), bind.ArgumentNames.Select(binary.TextConstantTable.Resolve)),
            x => x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool PrepareMessage(GameEventScriptMessage message, GameEventScriptContext context)
    {
        if (State != StateValue.Initialized) return RaiseError("Handler can only be loaded when the VM is initialized.");
        var signatureId = message.SignatureId;
        if (!InboundMessageHandlers.TryGetValue(signatureId, out var bind)) return RaiseError($"No handler found for message '{signatureId}'.");
        if (bind.Kind != GameEventScriptBinaryBindKind.MessageHandler) return RaiseError($"Message '{signatureId}' is not a handler.");
        if (bind.ArgumentNames.Count != message.Arguments.Count) return RaiseError($"Message '{signatureId}' has the wrong number of arguments.");
        var arguments = message.Arguments;
        if (arguments.Count > RegisterSlots.Length) return RaiseError($"Message '{signatureId}' has too many arguments for the VM register frame.");
        if (bind.EntryAddress >= CodeSegmentSize) return RaiseError($"Message '{signatureId}' has an invalid entry address.");
        InstructionPointer = bind.EntryAddress;
        RegisterFrameStart = 0;
        RegisterFrameLength = (ushort)arguments.Count;
        for (var i = 0; i < RegisterFrameLength; i++) 
        {
            RegisterSlots[i].BindArguments(arguments[i]);
        }
        RandomGenerator = context.Random;
        RandomGeneratorsPointer = 0;
        State = StateValue.Ready;
        return true;
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
        State = StateValue.Error;
        ErrorMessage = message;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Reset()
    {
        InstructionPointer = 0;
        CallStackPointer = 0;
        State = StateValue.Initialized;
        RegisterFrameStart = 0;
        RegisterFrameLength = 0;
        RandomGeneratorsPointer = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void JumpAddress(ushort address)
    {
        InstructionPointer = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool CallAddress(ushort address, ushort? resultRegister = null)
    {
        if (CallStackPointer >= CallStack.Length) return RaiseError("Stack overflow");
        CallStack[CallStackPointer++] = new CallFrame
        {
            InstructionPointer = InstructionPointer,
            ResultRegisterIndex = resultRegister,
            RegisterFrameStart = RegisterFrameStart,
            RegisterFrameLength = RegisterFrameLength
        };
        InstructionPointer = address;
        RegisterFrameStart += RegisterFrameLength;
        RegisterFrameLength = StagedArgumentCount;
        StagedArgumentCount = 0;
        return RegisterFrameStart + RegisterFrameLength <= RegisterSlots.Length || RaiseError($"Register overflow");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool CallAddressFromRegister(ushort registerIndex, ushort? resultRegister = null)
    {
        if (CallStackPointer >= CallStack.Length) return RaiseError("Stack overflow");
        if (registerIndex >= RegisterFrameLength) return RaiseError($"Register overflow");
        var register = RegisterSlots[registerIndex + RegisterFrameStart];
        CallStack[CallStackPointer++] = new CallFrame
        {
            InstructionPointer = InstructionPointer,
            ResultRegisterIndex = resultRegister,
            RegisterFrameStart = RegisterFrameStart,
            RegisterFrameLength = RegisterFrameLength
        };
        if (register.CodePointerOrNothing == null)
        {
            return RaiseError($"Illegal call: register {registerIndex} is not a code pointer.");
        }
        InstructionPointer = register.CodePointerOrNothing ?? throw new InvalidOperationException();
        RegisterFrameStart += RegisterFrameLength;
        RegisterFrameLength = StagedArgumentCount;
        StagedArgumentCount = 0;
        return RegisterFrameStart + RegisterFrameLength <= RegisterSlots.Length || RaiseError($"Register overflow");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnVoid()
    {
        if (CallStackPointer == 0)
        {
            State = StateValue.Halted;
            return;
        }

        var callFrame = CallStack[--CallStackPointer];
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        if (callFrame.ResultRegisterIndex.HasValue) RegisterSlots[callFrame.ResultRegisterIndex.Value + RegisterFrameStart].SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnValue(ushort registerIndex)
    {
        var result = RegisterSlots[registerIndex + RegisterFrameStart];
        var callFrame = CallStack[--CallStackPointer];
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        if (callFrame.ResultRegisterIndex.HasValue) RegisterSlots[callFrame.ResultRegisterIndex.Value + RegisterFrameStart] = result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal GameEventScriptBytecodeInstruction FetchInstructionAndIncrementInstructionPointer()
    {
        return InstructionPointer >= CodeSegmentSize ? throw new OverflowException() : Binary.InstructionTable[InstructionPointer++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string FetchStringByPointer(ushort index)
    {
        return Binary.TextConstantTable.Resolve(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<ushort> FetchUInt16SliceTableByPointer(ushort index)
    {
        return Binary.Uint16ConstantTable.Resolve(index);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddLocalSlots(ushort slotCount)
    {
        var requiredTotalSlots = RegisterFrameStart + slotCount;
        if (requiredTotalSlots > RegisterSlots.Length)
        {
            // FIXME: Here we might want to let the register frame grow.
            RaiseError("Not enough slots in register frame.");
        }
        else
        {
            RegisterFrameLength += slotCount;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RemoveLocalSlots(ushort slotCount)
    {
        if (RegisterFrameLength < slotCount)
        {
            // FIXME: Here we might want to let the register frame grow.
            RaiseError("Inconsistent register frame length. Cannot remove more slots than are available.");
        }
        else
        {
            RegisterFrameLength -= slotCount;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref VmValue AddStageSlot()
    {
        var stageRegisterIndex = RegisterFrameStart + RegisterFrameLength + StagedArgumentCount++;
        if (stageRegisterIndex >= RegisterSlots.Length) 
        {
            // FIXME: Here we might want to let the register frame grow.
            RaiseError("Cannot add more staged arguments than available register slots.");
        }
        return ref RegisterSlots[stageRegisterIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageRegister(ushort index)
    {
        AddStageSlot() = RegisterSlots[index + RegisterFrameStart];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageNothing()
    {
        AddStageSlot().SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageBoolean(bool value)
    {
        AddStageSlot().SetBoolean(value);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageInteger(long value, GameEventScriptBytecodeInstructionUnit unit)
    {
        AddStageSlot().SetInteger(value, unit);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageFloat(double value, GameEventScriptBytecodeInstructionUnit unit)
    {
        AddStageSlot().SetFloat(value, unit);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StagePercentage(double value)
    {
        AddStageSlot().SetPercentage(value);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageTextConstant(ushort constantIndex)
    {
        AddStageSlot().SetStringPointer(constantIndex);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StageTagConstant(ushort constantIndex)
    {
        AddStageSlot().SetTagPointer(constantIndex);
    }
    
}