#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public struct VmState
{
    public enum StateValue
    {
        Initialized, Ready, Running, Halted, Error
    }

    public struct CallFrame
    {
        public ushort InstructionPointer;
        public ushort? ResultRegisterIndex;
    }
    
    public GameEventScriptBinary Binary { get; init; }
    
    public StateValue State { get; internal set; }

    public ushort InstructionPointer { get; private set; } = 0;
    public ushort CallStackPointer { get; private set; } = 0;
    public CallFrame[] CallStack { get; init; }

    public VmRegister[] GlobalRegisters { get; init; }
    public List<VmRegister[]> LocalRegisters { get; init; }
    
    public Dictionary<string, GameEventScriptBinaryBindEntry> MessageHandlerBindings { get; init; }

    public VmState(GameEventScriptBinary binary, ushort stackSize, ushort globalRegisterCount)
    {
        Binary = binary;
        InstructionPointer = 0;
        CallStackPointer = 0;
        CallStack = new CallFrame[stackSize];
        GlobalRegisters = new VmRegister[globalRegisterCount];
        LocalRegisters = [];
        for (var i = 0; i < GlobalRegisters.Length; i++)
        {
            GlobalRegisters[i].SetNothing();
        }
        MessageHandlerBindings = binary.BindTable.Entries.Where(x => x.Kind == GameEventScriptBinaryBindKind.MessageHandler).ToDictionary(
            bind => GameEventScriptMessageSignature.CreateSignatureId(binary.StringTable.Resolve(bind.Name), bind.ArgumentNames.Select(binary.StringTable.Resolve)),
            x => x);
    }

    public bool PrepareMessage(GameEventScriptMessage message)
    {
        if(State != StateValue.Initialized) throw new InvalidOperationException("Handler can only be loaded when the VM is initialized.");
        var signatureId = message.SignatureId;
        if (MessageHandlerBindings.TryGetValue(signatureId, out var bind))
        {
            InstructionPointer = bind.EntryAddress;
            State = StateValue.Ready;
            return true;
        }
        State = StateValue.Error;
        return false;
    }

    public void Reset()
    {
        InstructionPointer = 0;
        CallStackPointer = 0;
        State = StateValue.Initialized;
    }

    public void JumpAddress(ushort address)
    {
        InstructionPointer = address;
    }

    public void CallAddress(ushort address, ushort? resultRegister = null)
    {
        if (CallStackPointer >= CallStack.Length) throw new StackOverflowException();
        CallStack[CallStackPointer++] = new CallFrame { InstructionPointer = InstructionPointer, ResultRegisterIndex = resultRegister };
        InstructionPointer = address;
    }

    public void CallAddressFromRegister(ushort registerIndex, ushort? resultRegister = null)
    {
        if (CallStackPointer >= CallStack.Length) throw new StackOverflowException();
        var register = GlobalRegisters[registerIndex];
        CallStack[CallStackPointer++] = new CallFrame { InstructionPointer = InstructionPointer, ResultRegisterIndex = resultRegister };
        InstructionPointer = register.CodePointerOrNothing ?? throw new ArgumentException($"Register {registerIndex} is not a code pointer.");
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
        if (callFrame.ResultRegisterIndex.HasValue) GlobalRegisters[callFrame.ResultRegisterIndex.Value].SetNothing();
    }

    public void ReturnValue(ushort registerIndex)
    {
        var callFrame = CallStack[--CallStackPointer];
        InstructionPointer = callFrame.InstructionPointer; 
        if (callFrame.ResultRegisterIndex.HasValue) GlobalRegisters[callFrame.ResultRegisterIndex.Value] = GlobalRegisters[registerIndex];
    }

    public GameEventScriptBytecodeInstruction FetchInstructionAndIncrementInstructionPointer()
    {
        return InstructionPointer >= ushort.MaxValue ? throw new OverflowException() : Binary.InstructionTable[InstructionPointer++];
    }

    public string FetchStringByPointer(ushort index)
    {
        return Binary.StringTable.Resolve(index);
    }

    public ReadOnlySpan<ushort> FetchUInt16SliceTableByPointer(ushort index)
    {
        return Binary.UInt16SliceTable.Resolve(index);
    }
}

