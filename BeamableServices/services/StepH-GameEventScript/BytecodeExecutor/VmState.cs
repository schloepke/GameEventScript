#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public struct VmState
{
    public enum StateValue
    {
        Initialized,
        Ready,
        Running,
        Halted,
        Error
    }

    public struct CallFrame
    {
        public ushort InstructionPointer;
        public ushort? ResultRegisterIndex;
        public ushort RegisterFrameStart;
        public ushort RegisterFrameLength;
    }

    public GameEventScriptBinary Binary { get; init; }

    public StateValue State { get; internal set; }

    public ushort InstructionPointer { get; private set; } = 0;
    public ushort CallStackPointer { get; private set; } = 0;
    public CallFrame[] CallStack { get; init; }

    public VmValue[] RegisterSlots { get; init; }

    public ushort RegisterFrameStart = 0;
    public ushort RegisterFrameLength = 0;
    
    public string? ErrorMessage { get; private set; } = null;

    public Dictionary<string, GameEventScriptBinaryBindEntry> InboundMessageHandlers { get; init; }
    public List<GameEventScriptMessageSignature> OutboundMessageSignatures { get; init; }

    public readonly ushort CodeSegmentSize; 

    public VmState(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        Binary = binary;
        CodeSegmentSize = checked((ushort)binary.InstructionTable.Length);
        InstructionPointer = 0;
        CallStackPointer = 0;
        CallStack = new CallFrame[stackSize];
        RegisterSlots = new VmValue[registerSize];
        for (var i = 0; i < RegisterSlots.Length; i++)
        {
            RegisterSlots[i].SetNothing();
        }
        InboundMessageHandlers = binary.BindTable.Entries.Where(x => x.Kind == GameEventScriptBinaryBindKind.MessageHandler).ToDictionary(
            bind => GameEventScriptMessageSignature.CreateSignatureId(binary.TextConstantTable.Resolve(bind.Name), bind.ArgumentNames.Select(binary.TextConstantTable.Resolve)),
            x => x);
    }

    public bool PrepareMessage(GameEventScriptMessage message)
    {
        if (State != StateValue.Initialized) return RaiseError("Handler can only be loaded when the VM is initialized.");
        var signatureId = message.SignatureId;
        if (!InboundMessageHandlers.TryGetValue(signatureId, out var bind)) return RaiseError($"No handler found for message '{signatureId}'.");
        InstructionPointer = bind.EntryAddress;
        State = StateValue.Ready;
        return true;

    }

    public bool RaiseError(string message)
    {
        State = StateValue.Error;
        ErrorMessage = message;
        return false;
    }


    public void Reset()
    {
        InstructionPointer = 0;
        CallStackPointer = 0;
        State = StateValue.Initialized;
        RegisterFrameStart = 0;
        RegisterFrameLength = 0;
    }

    public void JumpAddress(ushort address)
    {
        InstructionPointer = address;
    }

    public bool CallAddress(ushort address, ushort? resultRegister = null)
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
        RegisterFrameLength = 0;
        return true;
    }

    public bool CallAddressFromRegister(ushort registerIndex, ushort? resultRegister = null)
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
        RegisterFrameLength = 0;
        return true;
    }

    public void ReturnVoid()
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

    public void ReturnValue(ushort registerIndex)
    {
        var result = RegisterSlots[registerIndex + RegisterFrameStart];
        var callFrame = CallStack[--CallStackPointer];
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        if (callFrame.ResultRegisterIndex.HasValue) RegisterSlots[callFrame.ResultRegisterIndex.Value + RegisterFrameStart] = result;
    }

    public GameEventScriptBytecodeInstruction FetchInstructionAndIncrementInstructionPointer()
    {
        return InstructionPointer >= CodeSegmentSize ? throw new OverflowException() : Binary.InstructionTable[InstructionPointer++];
    }

    public string FetchStringByPointer(ushort index)
    {
        return Binary.TextConstantTable.Resolve(index);
    }

    public ReadOnlySpan<ushort> FetchUInt16SliceTableByPointer(ushort index)
    {
        return Binary.Uint16ConstantTable.Resolve(index);
    }
}