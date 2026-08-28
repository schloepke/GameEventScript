using System;
using System.Diagnostics.CodeAnalysis;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Runtime.Values.GesValue.GesValueFlags;
using static StepH.GameEventScript.Runtime.VM.GesVmState.StateValue;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

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

    internal readonly struct OutboundMessageSignature
    {
        internal OutboundMessageSignature(string name, string[] argumentNames, string signatureId)
        {
            Name = name;
            ArgumentNames = argumentNames;
            SignatureId = signatureId;
            IsValid = true;
        }

        internal bool IsValid { get; }
        internal string Name { get; }
        internal string[] ArgumentNames { get; }
        internal string SignatureId { get; }
    }

    internal GesValue[] EmptyList { get; init; }

    internal GameEventScriptBinary Binary { get; init; }
    internal string[] StringPool { get; init; }

    internal StateValue State { get; set; }

    internal ushort InstructionPointer { get; private set; }
    internal ushort CallStackPointer { get; private set; }
    internal CallFrame[] CallStack { get; init; }

    internal GesValue[] RegisterValues { get; private set; }

    internal ushort RandomGeneratorsPointer { get; private set; } = 0;
    internal GameEventScriptRandomGenerator[] RandomGenerators { get; init; }
    internal GameEventScriptRandomGenerator RandomGenerator { get; private set; }

    internal ushort RegisterFrameStart = 0;
    internal ushort RegisterFrameLength = 0;
    internal ushort StageLength { get; private set; }

    internal string? ErrorMessage { get; private set; }
    internal OutboundMessageSignature[] OutboundMessageSignatures { get; init; }
    internal GameEventScriptBinaryBindEntry[] RecordConstructors { get; init; }
    internal GameEventScriptBinaryBindEntry[] ExtensionCallBinds { get; init; }
    internal GameEventScriptBinaryBindEntry[] ExternalTypeBinds { get; init; }
    internal IGameEventScriptExtensionFunction?[] BoundExtensionCalls { get; private set; }
    internal IGameEventScriptExternalTypeConstructor?[] BoundExternalTypeConstructors { get; private set; }

    internal readonly ushort CodeSegmentSize;
    internal readonly int MaxRegisterCount;
    
    internal GameEventScriptMessage? ProcessingMessage { get; private set; }
    internal GesVmState(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        MaxRegisterCount = Math.Max(InitialRegisterCapacity, (int)registerSize);
        EmptyList = [];
        Binary = binary;
        StringPool = BuildStringPool(binary.TextConstantTable);
        CodeSegmentSize = checked((ushort)binary.InstructionTable.Length);
        InstructionPointer = 0;
        CallStackPointer = 0;
        CallStack = new CallFrame[stackSize];
        RegisterValues = new GesValue[InitialRegisterCapacity];
        RandomGenerators = new GameEventScriptRandomGenerator[16];
        RandomGeneratorsPointer = 0;
        RandomGenerator = GameEventScriptRandomGenerator.FromSeed(0L);
        OutboundMessageSignatures = BuildOutboundMessageSignatures(binary);
        RecordConstructors = BuildIdIndexedBindTable(binary, GameEventScriptBinaryBindKind.Record);
        ExtensionCallBinds = BuildIdIndexedBindTable(binary, GameEventScriptBinaryBindKind.ExtensionCall);
        ExternalTypeBinds = BuildIdIndexedBindTable(binary, GameEventScriptBinaryBindKind.ExternalType);
        BoundExtensionCalls = [];
        BoundExternalTypeConstructors = [];
    }

    internal void BindDynamicReferences(IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        _ = extensionRegistry ?? throw new ArgumentNullException(nameof(extensionRegistry));
        _ = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));

        var boundExtensionCalls = new IGameEventScriptExtensionFunction?[ExtensionCallBinds.Length];
        for (ushort bindId = 0; bindId < ExtensionCallBinds.Length; bindId++)
        {
            var bind = ExtensionCallBinds[bindId];
            if (bind.Kind != GameEventScriptBinaryBindKind.ExtensionCall || bind.Id != bindId) continue;
            var fullName = FetchStringByPointer(bind.Name);
            var separator = fullName.IndexOf('.');
            if (separator <= 0 || separator >= fullName.Length - 1)
            {
                throw new GameEventScriptDynamicLinkException($"External extension reference '{fullName}' has an invalid name.");
            }

            var labels = bind.ArgumentNames.Count == 0 ? [] : new string[bind.ArgumentNames.Count];
            for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                labels[labelIndex] = FetchStringByPointer(bind.ArgumentNames[labelIndex]);
            }

            var reference = new GameEventScriptExtensionReference(fullName[..separator], fullName[(separator + 1)..], labels);
            var function = extensionRegistry.Resolve(reference);
            if (function is null)
            {
                throw new GameEventScriptDynamicLinkException(
                    $"GameEventScript extension '{reference.SignatureId}' was not dynamically bound to external bind id '{bindId}'.");
            }

            boundExtensionCalls[bindId] = function;
        }

        var boundExternalTypeConstructors = new IGameEventScriptExternalTypeConstructor?[ExternalTypeBinds.Length];
        for (ushort bindId = 0; bindId < ExternalTypeBinds.Length; bindId++)
        {
            var bind = ExternalTypeBinds[bindId];
            if (bind.Kind != GameEventScriptBinaryBindKind.ExternalType || bind.Id != bindId) continue;
            var typeName = FetchStringByPointer(bind.Name);
            var labels = bind.ArgumentNames.Count == 0 ? [] : new string[bind.ArgumentNames.Count];
            for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                labels[labelIndex] = FetchStringByPointer(bind.ArgumentNames[labelIndex]);
            }

            var reference = new GameEventScriptExternalTypeConstructorReference(typeName, labels);
            var constructor = typeRegistry.Resolve(reference);
            if (constructor is null)
            {
                throw new GameEventScriptDynamicLinkException(
                    $"GameEventScript external type constructor ':{reference.SignatureId}' was not dynamically bound to external bind id '{bindId}'.");
            }

            boundExternalTypeConstructors[bindId] = constructor;
        }

        BoundExtensionCalls = boundExtensionCalls;
        BoundExternalTypeConstructors = boundExternalTypeConstructors;
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
    internal bool IsRegisterNothing(ushort index) => RegisterValues[index + RegisterFrameStart].Kind is GameEventScriptBytecodeTypeKind.Nothing;
    internal ref readonly GesValue RegisterStaged(ushort index) => ref RegisterValues[index + RegisterFrameStart + RegisterFrameLength];
    internal void SetNothing(ushort index) => RegisterValues[index + RegisterFrameStart].SetNothing();
    internal void SetValue(ushort index, in GesValue value) => RegisterValues[index + RegisterFrameStart] = value;
    internal void SetBoolean(ushort index, bool value) => RegisterValues[index + RegisterFrameStart].SetBoolean(value);
    internal void SetInteger(ushort index, long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetInteger(value, unit);
    internal void SetFloat(ushort index, double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetFloat(value, unit);
    internal void SetPercentage(ushort index, double ratio) => RegisterValues[index + RegisterFrameStart].SetPercentage(ratio);
    internal void SetTextPointer(ushort index, ushort pointer) => RegisterValues[index + RegisterFrameStart].SetText(FetchStringByPointer(pointer));
    internal void SetText(ushort index, string text) => RegisterValues[index + RegisterFrameStart].SetText(text);
    internal void SetTagPointer(ushort index, ushort pointer) => RegisterValues[index + RegisterFrameStart].SetTag(FetchStringByPointer(pointer));
    internal void SetTag(ushort index, string tag) => RegisterValues[index + RegisterFrameStart].SetTag(tag);
    internal void SetVector(ushort index, double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetVector(x, y, z, unit);
    internal void SetVector(ushort index, GesValueVectorPoint vector, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetVector(vector, unit);
    internal void SetPoint(ushort index, double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetPoint(x, y, z, unit);
    internal void SetPoint(ushort index, GesValueVectorPoint point, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone) => RegisterValues[index + RegisterFrameStart].SetPoint(point, unit);
    internal void SetDice(ushort index, int[] values) => RegisterValues[index + RegisterFrameStart].SetDice(values);
    internal void SetList(ushort index, GesValue[] list) => RegisterValues[index + RegisterFrameStart].SetList(list);
    internal void SetMap(ushort index, GesValueMap valueMap) => RegisterValues[index + RegisterFrameStart].SetMap(valueMap);
    internal void SetRecord(ushort index, string typeName, GesValueMap record) => RegisterValues[index + RegisterFrameStart].SetRecord(typeName, record);
    internal void SetExternalCustomType(ushort index, GesExternalObject value) => RegisterValues[index + RegisterFrameStart].SetExternalCustomType(value);
    internal void SetRange(ushort index, long from, long to, long step) => RegisterValues[index + RegisterFrameStart].SetRange(from, to, step);
    internal void SetRange(ushort index, double from, double to, double step) => RegisterValues[index + RegisterFrameStart].SetRange(from, to, step);
    internal void SetMessageHandler(ushort index, GameEventScriptMessageSignature handler) => RegisterValues[index + RegisterFrameStart].SetMessageHandler(handler);
    internal void SetMessage(ushort index, GameEventScriptMessage message) => RegisterValues[index + RegisterFrameStart].SetMessage(message);
    internal void SetSeries(ushort index, GesSeries series) => RegisterValues[index + RegisterFrameStart].SetSeries(series);
    internal void SetIterator(ushort index, IGesIterator value) => RegisterValues[index + RegisterFrameStart].SetIterator(value);
    internal void CreateListBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetListBuilder(new GesVmListBuilder());
    internal void CreateMapBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetMapBuilder(new GesVmMapBuilder());
    internal void CreateDistinctBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetDistinctBuilder(new GesVmDistinctBuilder());
    internal void CreateGroupBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetGroupBuilder(new GesVmGroupBuilder(this));
    internal void CreateOrderBuilder(ushort index) => RegisterValues[index + RegisterFrameStart].SetOrderBuilder(new GesVmOrderBuilder());
    
    internal bool PushRandom(GameEventScriptRandomGenerator randomGenerator)
    {
        if (RandomGeneratorsPointer >= RandomGenerators.Length) return RaiseError("Random generator stack overflow");
        RandomGenerators[RandomGeneratorsPointer++] = RandomGenerator;
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
        for (var i = 0; i < RegisterValues.Length; i++) RegisterValues[i].SetNothing();
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
        if (callFrame.ResultRegisterIndex.HasValue) RegisterValues[callFrame.ResultRegisterIndex.Value + RegisterFrameStart].SetNothing();
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
        ClearRegisterRange(RegisterFrameStart, RegisterFrameLength + StageLength);
        InstructionPointer = callFrame.InstructionPointer;
        RegisterFrameStart = callFrame.RegisterFrameStart;
        RegisterFrameLength = callFrame.RegisterFrameLength;
        StageLength = 0;
        if (!callFrame.ResultRegisterIndex.HasValue) return;
        if (callFrame.NormalizeResultAsPredicate && result.Kind is not GameEventScriptBytecodeTypeKind.Boolean && !result.IsNothing) result.SetNothing();
        RegisterValues[callFrame.ResultRegisterIndex.Value + RegisterFrameStart] = result;
    }
    internal GameEventScriptBytecodeInstruction FetchInstructionAndIncrementInstructionPointer() => InstructionPointer >= CodeSegmentSize ? throw new OverflowException() : Binary.InstructionTable[InstructionPointer++];
    internal string FetchStringByPointer(ushort index) => StringPool[index];
    internal GameEventScriptUInt16Slice FetchUInt16SliceTableByPointer(ushort index) => Binary.Uint16ConstantTable.Resolve(index);
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
                    RaiseError("Inconsistent register frame length. Cannot remove more registers than are available.");
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
    internal void StageInteger(long value, GameEventScriptBytecodeInstructionUnit unit)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetInteger(value, unit);
    }
    internal void StageFloat(double value, GameEventScriptBytecodeInstructionUnit unit)
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
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetText(FetchStringByPointer(constantIndex));
    }
    internal void StageTagConstant(ushort constantIndex)
    {
        var stageRegisterIndex = AddStageRegister();
        if (stageRegisterIndex >= 0) RegisterValues[stageRegisterIndex].SetTag(FetchStringByPointer(constantIndex));
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
        if (requiredRegisters > MaxRegisterCount) return RaiseError($"Register overflow. Required {requiredRegisters} registers but maximum is {MaxRegisterCount}.");
        var newLength = RegisterValues.Length;
        do
        {
            newLength = Math.Min(newLength + RegisterCapacityGrowth, MaxRegisterCount);
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

    private OutboundMessageSignature[] BuildOutboundMessageSignatures(GameEventScriptBinary binary)
    {
        var binds = BuildIdIndexedBindTable(binary, GameEventScriptBinaryBindKind.OutboundMessage);
        if (binds.Length == 0)
        {
            return [];
        }

        var signatures = new OutboundMessageSignature[binds.Length];
        for (var bindIndex = 0; bindIndex < binds.Length; bindIndex++)
        {
            var bind = binds[bindIndex];
            if (bind.Kind != GameEventScriptBinaryBindKind.OutboundMessage || bind.Id != bindIndex)
            {
                continue;
            }

            var name = FetchStringByPointer(bind.Name);
            var argumentNames = new string[bind.ArgumentNames.Count];
            for (var argumentIndex = 0; argumentIndex < argumentNames.Length; argumentIndex++)
            {
                argumentNames[argumentIndex] = FetchStringByPointer(bind.ArgumentNames[argumentIndex]);
            }

            signatures[bindIndex] = new OutboundMessageSignature(
                name,
                argumentNames,
                GameEventScriptMessageSignature.CreateSignatureId(name, argumentNames));
        }

        return signatures;
    }

    private static string[] BuildStringPool(GameEventScriptTextTable textTable)
    {
        var strings = new string[textTable.Slices.Length];
        for (var i = 0; i < strings.Length; i++)
        {
            strings[i] = textTable.Resolve((ushort)i);
        }

        return strings;
    }
    
}
