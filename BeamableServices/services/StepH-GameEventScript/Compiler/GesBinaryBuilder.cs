using System;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Api.GameEventScriptBinaryHeader;

namespace StepH.GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private readonly List<RegisterSymbol> _registers = [];
    private readonly List<LabelSymbol> _labels = [];
    private readonly List<PlanItem> _rootItems = [];
    private readonly List<BindPlan> _binds = [];
    private PlanItem[]? _rewrittenItems;
    private ushort _version = 1;
    private string _moduleName = "Unknown";
    private GameEventScriptBinaryFlags _flags = GameEventScriptBinaryFlags.None;
    private bool _optimize = true;

    public GesBinaryBuilder WithVersion(ushort version)
    {
        _version = version;
        return this;
    }

    public GesBinaryBuilder WithModuleName(string moduleName)
    {
        _moduleName = moduleName ?? string.Empty;
        return this;
    }

    public GesBinaryBuilder WithFlag(GameEventScriptBinaryFlags flag, bool enabled = true)
    {
        _flags = enabled ? _flags | flag : _flags & ~flag;
        return this;
    }

    public GesBinaryBuilder WithOptimization(bool enabled = true)
    {
        _optimize = enabled;
        return this;
    }

    public GesRegisterRef AddRegister(string name)
        => AddRegisterInRoutine(name, CurrentRoutineId);

    public GesRegisterRef AddTemporaryRegister(string? name = null)
        => AddTemporaryRegisterInRoutine(name, CurrentRoutineId);

    private GesRegisterRef AddRegisterInRoutine(string name, int routineId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Register name must be non-empty.", nameof(name));
        ValidateRoutineId(routineId);
        var register = new RegisterSymbol(_registers.Count, name, IsTemporary: false, routineId);
        _registers.Add(register);
        return new GesRegisterRef(register.Id);
    }

    private GesRegisterRef AddTemporaryRegisterInRoutine(string? name, int routineId)
    {
        ValidateRoutineId(routineId);
        var register = new RegisterSymbol(_registers.Count, string.IsNullOrWhiteSpace(name) ? null : name, IsTemporary: true, routineId);
        _registers.Add(register);
        return new GesRegisterRef(register.Id);
    }

    private List<PlanItem> CurrentPlanItems => CurrentRoutineId == NoRoutineId
        ? _rootItems
        : _routines[CurrentRoutineId].Items;

    private void AppendPlanItem(PlanItem item)
    {
        _rewrittenItems = null;
        CurrentPlanItems.Add(item);
    }

    private PlanItem[] LinearizePlanItems()
    {
        var count = _rootItems.Count;
        for (var index = 0; index < _routines.Count; index++)
        {
            count += _routines[index].Items.Count;
        }

        var result = new PlanItem[count];
        var offset = 0;
        offset = CopyPlanItems(_rootItems, result, offset);

        var roots = CollectRootRoutines();
        Array.Sort(roots, CompareRoutinePlanForOutput);
        for (var index = 0; index < roots.Length; index++)
        {
            offset = AppendRoutineAndOwnedHelpers(result, offset, roots[index]);
        }

        return result;
    }

    private RoutinePlan[] CollectRootRoutines()
    {
        var count = 0;
        for (var index = 0; index < _routines.Count; index++)
        {
            if (_routines[index].ParentRoutineId == NoRoutineId) count++;
        }

        var result = new RoutinePlan[count];
        var offset = 0;
        for (var index = 0; index < _routines.Count; index++)
        {
            var routine = _routines[index];
            if (routine.ParentRoutineId != NoRoutineId) continue;
            result[offset++] = routine;
        }

        return result;
    }

    private int AppendRoutineAndOwnedHelpers(PlanItem[] result, int offset, RoutinePlan routine)
    {
        offset = CopyPlanItems(routine.Items, result, offset);
        for (var index = 0; index < _routines.Count; index++)
        {
            var child = _routines[index];
            if (child.ParentRoutineId != routine.Id) continue;
            offset = AppendRoutineAndOwnedHelpers(result, offset, child);
        }

        return offset;
    }

    private void ValidateRoutineId(int routineId)
    {
        if (routineId == NoRoutineId) return;
        if (routineId < 0 || routineId >= _routines.Count) throw new ArgumentOutOfRangeException(nameof(routineId), "Unknown routine id.");
    }

    public GesLabelRef AddLabel(string? name = null)
    {
        var label = new LabelSymbol(_labels.Count, string.IsNullOrWhiteSpace(name) ? null : name);
        _labels.Add(label);
        return new GesLabelRef(label.Id);
    }

    public GesBinaryBuilder MarkLabel(GesLabelRef label)
    {
        RequireLabel(label);
        AppendPlanItem(PlanItem.ForLabel(label, CurrentSourceRange));
        return this;
    }

    public GesBindRef AddBind(
        GameEventScriptBinaryBindKind kind,
        string name,
        IReadOnlyList<string>? argumentNames = null,
        GesLabelRef? entryLabel = null,
        ushort? id = null,
        IReadOnlyList<string>? requiredTags = null,
        IReadOnlyList<string>? excludedTags = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bind name must be non-empty.", nameof(name));
        if (entryLabel.HasValue) RequireLabel(entryLabel.Value);
        var bind = new BindPlan(
            _binds.Count,
            kind,
            name,
            CopyStringList(argumentNames),
            entryLabel,
            id,
            CopyStringList(requiredTags),
            CopyStringList(excludedTags));
        _binds.Add(bind);
        return new GesBindRef(bind.Index);
    }

    private GesBinaryBuilder AddOpcode(
        GameEventScriptBytecodeOpCode opcode,
        GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone,
        GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None,
        GesOperand dst = default,
        GesOperand x = default,
        GesOperand y = default,
        GesOperand a = default,
        GesOperand b = default,
        GesOperand c = default,
        GesOperand d = default,
        GesOperand secondaryList = default,
        short? count = null,
        long? i64 = null,
        double? f64 = null)
    {
        ValidateOperand(dst);
        ValidateOperand(x);
        ValidateOperand(y);
        ValidateOperand(a);
        ValidateOperand(b);
        ValidateOperand(c);
        ValidateOperand(d);
        ValidateOperand(secondaryList);
        AppendPlanItem(PlanItem.ForInstruction(new InstructionPlan(CurrentRoutineId, opcode, unit, flags, dst, x, y, a, b, c, d, secondaryList, count, i64, f64), CurrentSourceRange));
        return this;
    }

    public GameEventScriptBinary Build()
    {
        EnsureScopesClosed();
        var planItems = _rewrittenItems ?? LinearizePlanItems();
        var optimizedItems = _optimize ? RunDefaultOptimizationPasses(planItems) : CopyPlanItems(planItems);
        var labelAddresses = ResolveLabelAddresses(optimizedItems);
        var registerAllocation = AllocateRegisters(optimizedItems);
        var registerMap = registerAllocation.RegisterMap;
        var items = PatchRoutineRegisterLocals(optimizedItems, registerAllocation);
        var builder = new BinaryMaterializer()
            .WithVersion(_version)
            .WithModuleName(_moduleName)
            .WithFlag(GameEventScriptBinaryFlags.Optimization, (_flags & GameEventScriptBinaryFlags.Optimization) != 0)
            .WithFlag(GameEventScriptBinaryFlags.Debug, (_flags & GameEventScriptBinaryFlags.Debug) != 0);

        ushort ResolveText(string text)
        {
            return builder.AddText(text);
        }

        ushort ResolveList(IReadOnlyList<GesRegisterRef> registers)
        {
            var values = new ushort[registers.Count];
            for (var index = 0; index < registers.Count; index++)
            {
                values[index] = ResolveRegister(registers[index], registerMap);
            }

            return builder.AddUInt16Slice(values);
        }

        ushort ResolveTextList(IReadOnlyList<string> values)
        {
            var indexes = new ushort[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                indexes[index] = ResolveText(values[index]);
            }

            return builder.AddUInt16Slice(indexes);
        }

        var bindIds = ResolveBinds(builder, ResolveText, labelAddresses);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.Instruction is null) continue;
            builder.AddInstruction(EncodeInstruction(item.Instruction, registerMap, labelAddresses, bindIds, ResolveText, ResolveList, ResolveTextList));
        }

        return builder.Build();
    }

    private ushort[] ResolveBinds(
        BinaryMaterializer builder,
        Func<string, ushort> resolveText,
        IReadOnlyDictionary<int, ushort> labelAddresses)
    {
        var nextIds = new Dictionary<GameEventScriptBinaryBindKind, ushort>();
        var result = new ushort[_binds.Count];
        var orderedBinds = new BindPlan[_binds.Count];
        for (var index = 0; index < _binds.Count; index++)
        {
            orderedBinds[index] = _binds[index];
        }

        Array.Sort(orderedBinds, CompareBindPlanForOutput);
        for (var index = 0; index < orderedBinds.Length; index++)
        {
            var bind = orderedBinds[index];
            var id = bind.Id ?? NextId(bind.Kind, nextIds);
            result[bind.Index] = id;
            var argumentNames = ResolveTextList(bind.ArgumentNames, resolveText);
            var requiredTags = ResolveTextList(bind.RequiredTags, resolveText);
            var excludedTags = ResolveTextList(bind.ExcludedTags, resolveText);
            var entryAddress = bind.EntryLabel.HasValue
                ? labelAddresses[bind.EntryLabel.Value.Id]
                : ushort.MaxValue;

            builder.AddBind(new GameEventScriptBinaryBindEntry(bind.Kind, resolveText(bind.Name), argumentNames, entryAddress, id, requiredTags, excludedTags));
        }

        return result;
    }

    private sealed class BinaryMaterializer
    {
        private ushort _version = 1;
        private string _moduleName = "Unknown";
        private GameEventScriptBinaryFlags _flags = GameEventScriptBinaryFlags.None;
        private readonly List<string> _textConstants = [];
        private readonly Dictionary<string, ushort> _textIndexes = [];
        private readonly List<ushort[]> _uint16Slices = [];
        private readonly List<GameEventScriptBinaryBindEntry> _binds = [];
        private readonly List<GameEventScriptBytecodeInstruction> _instructions = [];

        public BinaryMaterializer WithVersion(ushort version)
        {
            _version = version;
            return this;
        }

        public BinaryMaterializer WithModuleName(string moduleName)
        {
            _moduleName = moduleName ?? string.Empty;
            return this;
        }

        public BinaryMaterializer WithFlag(GameEventScriptBinaryFlags flag, bool enabled = true)
        {
            _flags = enabled ? _flags | flag : _flags & ~flag;
            return this;
        }

        public ushort AddText(string text)
        {
            _ = text ?? throw new ArgumentNullException(nameof(text));
            if (_textIndexes.TryGetValue(text, out var index))
            {
                return index;
            }

            index = checked((ushort)_textConstants.Count);
            _textConstants.Add(text);
            _textIndexes.Add(text, index);
            return index;
        }

        public ushort AddUInt16Slice(ushort[] value)
        {
            for (var sliceIndex = 0; sliceIndex < _uint16Slices.Count; sliceIndex++)
            {
                if (UInt16SliceEquals(_uint16Slices[sliceIndex], value))
                {
                    return checked((ushort)sliceIndex);
                }
            }

            var index = checked((ushort)_uint16Slices.Count);
            _uint16Slices.Add(value);
            return index;
        }

        public BinaryMaterializer AddBind(GameEventScriptBinaryBindEntry bind)
        {
            _binds.Add(bind);
            return this;
        }

        public BinaryMaterializer AddInstruction(GameEventScriptBytecodeInstruction instruction)
        {
            _instructions.Add(instruction);
            return this;
        }

        public GameEventScriptBinary Build()
            => new(
                new GameEventScriptBinaryHeader { Version = _version, Flags = _flags },
                ResolveModuleName(),
                BuildTextTable(_textConstants),
                BuildUInt16Table(_uint16Slices),
                new GameEventScriptBinaryBindTable(_binds),
                CopyInstructions(_instructions));

        private string ResolveModuleName()
            => string.IsNullOrWhiteSpace(_moduleName)
                ? FormattableString.Invariant($"AnonymousModule_{ComputeBinaryHash():X8}")
                : _moduleName;

    private uint ComputeBinaryHash()
    {
        var hash = new BinaryHashBuilder(2166136261u);

        hash.MixUShort(_version);
        hash.MixUInt((uint)_flags);

        for (var index = 0; index < _textConstants.Count; index++)
        {
            hash.MixString(_textConstants[index]);
        }

        for (var sliceIndex = 0; sliceIndex < _uint16Slices.Count; sliceIndex++)
        {
            var slice = _uint16Slices[sliceIndex];
            hash.MixInt(slice.Length);
            for (var index = 0; index < slice.Length; index++)
            {
                hash.MixUShort(slice[index]);
            }
        }

        for (var index = 0; index < _binds.Count; index++)
        {
            var bind = _binds[index];
            hash.MixUInt((uint)bind.Kind);
            hash.MixUShort(bind.Name);
            hash.MixInt(bind.ArgumentNames.Count);
            for (var argumentIndex = 0; argumentIndex < bind.ArgumentNames.Count; argumentIndex++)
            {
                hash.MixUShort(bind.ArgumentNames[argumentIndex]);
            }

            hash.MixUShort(bind.EntryAddress);
            hash.MixUShort(bind.Id);
            hash.MixInt(bind.RequiredTags.Count);
            for (var tagIndex = 0; tagIndex < bind.RequiredTags.Count; tagIndex++)
            {
                hash.MixUShort(bind.RequiredTags[tagIndex]);
            }

            hash.MixInt(bind.ExcludedTags.Count);
            for (var tagIndex = 0; tagIndex < bind.ExcludedTags.Count; tagIndex++)
            {
                hash.MixUShort(bind.ExcludedTags[tagIndex]);
            }
        }

        for (var index = 0; index < _instructions.Count; index++)
        {
            var instruction = _instructions[index];
            hash.MixByte((byte)instruction.OpCode);
            hash.MixByte(instruction.UnitAndFlags);
            hash.MixUShort(instruction.DestinationRegister);
            hash.MixUShort((ushort)instruction.ImmediateX);
            hash.MixUShort((ushort)instruction.ImmediateY);
            hash.MixULong(instruction.Payload);
        }

        return hash.Value;
    }

    private struct BinaryHashBuilder
    {
        private uint _hash;

        internal BinaryHashBuilder(uint hash)
        {
            _hash = hash;
        }

        internal readonly uint Value => _hash;

        internal void MixString(string value)
        {
            MixInt(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                MixUShort(value[index]);
            }
        }

        internal void MixInt(int value)
            => MixUInt(unchecked((uint)value));

        internal void MixUShort(ushort value)
        {
            MixByte((byte)value);
            MixByte((byte)(value >> 8));
        }

        internal void MixUInt(uint value)
        {
            MixUShort((ushort)value);
            MixUShort((ushort)(value >> 16));
        }

        internal void MixULong(ulong value)
        {
            MixUInt((uint)value);
            MixUInt((uint)(value >> 32));
        }

        internal void MixByte(byte value)
        {
            _hash ^= value;
            _hash *= 16777619u;
        }
    }

        private static GameEventScriptTextTable BuildTextTable(IReadOnlyList<string> values)
        {
            var totalLength = 0;
            var slices = new GameEventScriptTextTable.SliceEntry[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                var byteCount = Encoding.UTF8.GetByteCount(values[index]);
                slices[index] = new GameEventScriptTextTable.SliceEntry
                {
                    Start = checked((ushort)totalLength),
                    Length = checked((ushort)byteCount)
                };
                totalLength += byteCount;
            }

            var data = new byte[totalLength];
            var offset = 0;
            for (var index = 0; index < values.Count; index++)
            {
                offset += Encoding.UTF8.GetBytes(values[index], 0, values[index].Length, data, offset);
            }

            return new GameEventScriptTextTable { Slices = slices, Data = data };
        }

        private static GameEventScriptUInt16Table BuildUInt16Table(IReadOnlyList<ushort[]> values)
        {
            var totalLength = 0;
            var slices = new GameEventScriptUInt16Table.SliceEntry[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                slices[index] = new GameEventScriptUInt16Table.SliceEntry
                {
                    Start = checked((ushort)totalLength),
                    Length = checked((ushort)value.Length)
                };
                totalLength += value.Length;
            }

            var data = new ushort[totalLength];
            var offset = 0;
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                Array.Copy(value, 0, data, offset, value.Length);
                offset += value.Length;
            }

            return new GameEventScriptUInt16Table { Slices = slices, Data = data };
        }

        private static GameEventScriptBytecodeInstruction[] CopyInstructions(IReadOnlyList<GameEventScriptBytecodeInstruction> source)
        {
            if (source.Count == 0) return [];
            var result = new GameEventScriptBytecodeInstruction[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = source[index];
            }

            return result;
        }
    }

    private static int BindKindOrder(GameEventScriptBinaryBindKind kind)
        => kind switch
        {
            GameEventScriptBinaryBindKind.MessageHandler or
                GameEventScriptBinaryBindKind.MessageNameHandler => 0,
            GameEventScriptBinaryBindKind.Record => 1,
            GameEventScriptBinaryBindKind.Predicate => 2,
            GameEventScriptBinaryBindKind.Function => 3,
            GameEventScriptBinaryBindKind.OutboundMessage => 4,
            GameEventScriptBinaryBindKind.ExtensionCall => 5,
            GameEventScriptBinaryBindKind.ExternalType => 6,
            _ => 7
        };

    private static int RoutineKindOrder(GameEventScriptBinaryBindKind? kind)
        => kind.HasValue ? BindKindOrder(kind.Value) : 7;

    private static int CompareRoutinePlanForOutput(RoutinePlan left, RoutinePlan right)
    {
        if (ReferenceEquals(left, right)) return 0;
        var kindCompare = RoutineKindOrder(left.Kind).CompareTo(RoutineKindOrder(right.Kind));
        return kindCompare != 0 ? kindCompare : left.Id.CompareTo(right.Id);
    }

    private static int CompareBindPlanForOutput(BindPlan left, BindPlan right)
    {
        if (ReferenceEquals(left, right)) return 0;
        var kindCompare = BindKindOrder(left.Kind).CompareTo(BindKindOrder(right.Kind));
        return kindCompare != 0 ? kindCompare : left.Index.CompareTo(right.Index);
    }

    private static int CompareRegisterInterval(RegisterInterval left, RegisterInterval right)
    {
        if (ReferenceEquals(left, right)) return 0;
        var startCompare = left.Start.CompareTo(right.Start);
        return startCompare != 0 ? startCompare : left.RegisterId.CompareTo(right.RegisterId);
    }

    private static string[] CopyStringList(IReadOnlyList<string>? source)
    {
        if (source is null || source.Count == 0) return [];
        var result = new string[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            result[index] = source[index] ?? throw new ArgumentException("String lists cannot contain null entries.", nameof(source));
        }

        return result;
    }

    private static PlanItem[] CopyPlanItems(IReadOnlyList<PlanItem> source)
    {
        if (source.Count == 0) return [];
        var result = new PlanItem[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            result[index] = source[index];
        }

        return result;
    }

    private static int CopyPlanItems(IReadOnlyList<PlanItem> source, PlanItem[] destination, int offset)
    {
        for (var index = 0; index < source.Count; index++)
        {
            destination[offset++] = source[index];
        }

        return offset;
    }

    private static ushort[] ResolveTextList(IReadOnlyList<string> source, Func<string, ushort> resolveText)
    {
        if (source.Count == 0) return [];
        var result = new ushort[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            result[index] = resolveText(source[index]);
        }

        return result;
    }

    private static bool UInt16SliceEquals(IReadOnlyList<ushort> left, IReadOnlyList<ushort> right)
    {
        if (left.Count != right.Count) return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (left[index] != right[index]) return false;
        }

        return true;
    }

    private static ushort NextId(GameEventScriptBinaryBindKind kind, Dictionary<GameEventScriptBinaryBindKind, ushort> nextIds)
    {
        nextIds.TryGetValue(kind, out var id);
        nextIds[kind] = checked((ushort)(id + 1));
        return id;
    }

    private GameEventScriptBytecodeInstruction EncodeInstruction(
        InstructionPlan plan,
        IReadOnlyDictionary<int, ushort> registerMap,
        IReadOnlyDictionary<int, ushort> labelAddresses,
        IReadOnlyList<ushort> bindIds,
        Func<string, ushort> resolveText,
        Func<IReadOnlyList<GesRegisterRef>, ushort> resolveList,
        Func<IReadOnlyList<string>, ushort> resolveTextList)
    {
        var instruction = new GameEventScriptBytecodeInstruction
        {
            OpCode = plan.OpCode,
            UnitAndFlags = GameEventScriptBytecodeInstruction.EncodeUnitAndFlags(plan.Unit, plan.Flags)
        };

        instruction = ApplyDestination(instruction, plan.Destination, registerMap, bindIds);
        instruction = ApplyX(instruction, plan.X, registerMap, labelAddresses, bindIds, resolveText, resolveList, resolveTextList);
        instruction = ApplyY(instruction, plan.Y, registerMap, labelAddresses, resolveText, resolveList, resolveTextList);
        instruction = ApplySecondaryList(instruction, plan.SecondaryList, registerMap, resolveList, resolveTextList);
        instruction = ApplyAux(instruction, plan.A, 0, registerMap, labelAddresses, resolveList);
        instruction = ApplyAux(instruction, plan.B, 1, registerMap, labelAddresses, resolveList);
        instruction = ApplyAux(instruction, plan.C, 2, registerMap, labelAddresses, resolveList);
        instruction = ApplyAux(instruction, plan.D, 3, registerMap, labelAddresses, resolveList);

        if (plan.Count.HasValue) instruction.Count = plan.Count.Value;
        if (plan.I64.HasValue) instruction.I64 = plan.I64.Value;
        if (plan.F64.HasValue) instruction.F64 = plan.F64.Value;
        return instruction;
    }

    private static GameEventScriptBytecodeInstruction ApplyDestination(
        GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        IReadOnlyDictionary<int, ushort> registerMap,
        IReadOnlyList<ushort> bindIds)
    {
        if (operand.Kind == GesOperandKind.None) return instruction;
        switch (operand.Kind)
        {
            case GesOperandKind.Register:
                instruction.DestinationRegister = ResolveRegister(operand.RegisterRef, registerMap);
                return instruction;
            case GesOperandKind.Bind:
                instruction.MessageDestination = bindIds[operand.BindRef.Id];
                return instruction;
            default:
                throw new InvalidOperationException($"Destination operand must be a register or bind but was '{operand.Kind}'.");
        }
    }

    private static GameEventScriptBytecodeInstruction ApplyX(
        GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        IReadOnlyDictionary<int, ushort> registerMap,
        IReadOnlyDictionary<int, ushort> labelAddresses,
        IReadOnlyList<ushort> bindIds,
        Func<string, ushort> resolveText,
        Func<IReadOnlyList<GesRegisterRef>, ushort> resolveList,
        Func<IReadOnlyList<string>, ushort> resolveTextList)
    {
        switch (operand.Kind)
        {
            case GesOperandKind.None:
                return instruction;
            case GesOperandKind.Register:
                instruction.XRegister = ResolveRegister(operand.RegisterRef, registerMap);
                return instruction;
            case GesOperandKind.Text:
                instruction.StringIndex = resolveText(operand.TextValue!);
                return instruction;
            case GesOperandKind.RegisterList:
                instruction.SecondaryListIndex = resolveList(operand.RegisterListValue!);
                return instruction;
            case GesOperandKind.TextList:
                instruction.SecondaryListIndex = resolveTextList(operand.TextListValue!);
                return instruction;
            case GesOperandKind.Bind:
                instruction.BindId = bindIds[operand.BindRef.Id];
                return instruction;
            case GesOperandKind.Label:
                instruction.XRegister = labelAddresses[operand.LabelRef.Id];
                return instruction;
            case GesOperandKind.Type:
                instruction.TypeOperand = (ushort)operand.TypeKind;
                return instruction;
            case GesOperandKind.UShort:
                instruction.XRegister = operand.UShort;
                return instruction;
            case GesOperandKind.Short:
                instruction.ImmediateX = operand.Short;
                return instruction;
            default:
                throw new InvalidOperationException($"Unsupported X operand '{operand.Kind}'.");
        }
    }

    private static GameEventScriptBytecodeInstruction ApplyY(
        GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        IReadOnlyDictionary<int, ushort> registerMap,
        IReadOnlyDictionary<int, ushort> labelAddresses,
        Func<string, ushort> resolveText,
        Func<IReadOnlyList<GesRegisterRef>, ushort> resolveList,
        Func<IReadOnlyList<string>, ushort> resolveTextList)
    {
        switch (operand.Kind)
        {
            case GesOperandKind.None:
                return instruction;
            case GesOperandKind.Register:
                instruction.YRegister = ResolveRegister(operand.RegisterRef, registerMap);
                return instruction;
            case GesOperandKind.Label:
                instruction.TargetAddress = labelAddresses[operand.LabelRef.Id];
                return instruction;
            case GesOperandKind.Text:
                instruction.SecondaryStringIndex = resolveText(operand.TextValue!);
                return instruction;
            case GesOperandKind.RegisterList:
                instruction.ListIndex = resolveList(operand.RegisterListValue!);
                return instruction;
            case GesOperandKind.TextList:
                instruction.ListIndex = resolveTextList(operand.TextListValue!);
                return instruction;
            case GesOperandKind.Type:
                instruction.TypeKind = operand.TypeKind;
                return instruction;
            case GesOperandKind.UShort:
                instruction.ListIndex = operand.UShort;
                return instruction;
            case GesOperandKind.Short:
                instruction.ImmediateY = operand.Short;
                return instruction;
            default:
                throw new InvalidOperationException($"Unsupported Y operand '{operand.Kind}'.");
        }
    }

    private static GameEventScriptBytecodeInstruction ApplySecondaryList(
        GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        IReadOnlyDictionary<int, ushort> registerMap,
        Func<IReadOnlyList<GesRegisterRef>, ushort> resolveList,
        Func<IReadOnlyList<string>, ushort> resolveTextList)
    {
        if (operand.Kind == GesOperandKind.None) return instruction;
        switch (operand.Kind)
        {
            case GesOperandKind.RegisterList:
                instruction.SecondaryListIndex = resolveList(operand.RegisterListValue!);
                return instruction;
            case GesOperandKind.TextList:
                instruction.SecondaryListIndex = resolveTextList(operand.TextListValue!);
                return instruction;
            default:
                throw new InvalidOperationException($"Secondary list operand must be a register or text list but was '{operand.Kind}'.");
        }
    }

    private static GameEventScriptBytecodeInstruction ApplyAux(
        GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        int index,
        IReadOnlyDictionary<int, ushort> registerMap,
        IReadOnlyDictionary<int, ushort> labelAddresses,
        Func<IReadOnlyList<GesRegisterRef>, ushort> resolveList)
    {
        if (operand.Kind == GesOperandKind.None) return instruction;
        var value = operand.Kind switch
        {
            GesOperandKind.Register => ResolveRegister(operand.RegisterRef, registerMap),
            GesOperandKind.Label => labelAddresses[operand.LabelRef.Id],
            GesOperandKind.RegisterList => resolveList(operand.RegisterListValue!),
            GesOperandKind.UShort => operand.UShort,
            GesOperandKind.Short => unchecked((ushort)operand.Short),
            GesOperandKind.Type => (ushort)operand.TypeKind,
            _ => throw new InvalidOperationException($"Unsupported aux operand '{operand.Kind}'.")
        };

        switch (index)
        {
            case 0:
                instruction.AU = value;
                break;
            case 1:
                instruction.BU = value;
                break;
            case 2:
                instruction.CU = value;
                break;
            case 3:
                instruction.DU = value;
                break;
        }

        return instruction;
    }

    private static ushort ResolveRegister(GesRegisterRef register, IReadOnlyDictionary<int, ushort> registerMap)
        => registerMap.TryGetValue(register.Id, out var registerIndex)
            ? registerIndex
            : throw new InvalidOperationException($"Register '{register.Id}' was not allocated.");

    private RegisterAllocationResult AllocateRegisters(IReadOnlyList<PlanItem> items)
    {
        var intervals = BuildRegisterIntervals(items);
        if (_routines.Count > 0) return AllocateScopedRegisters(intervals);

        var result = new Dictionary<int, ushort>();

        ushort nextPinned = 0;
        for (var index = 0; index < _registers.Count; index++)
        {
            var register = _registers[index];
            if (register.IsTemporary) continue;
            result[register.Id] = nextPinned++;
        }

        var active = new List<(int RegisterId, int End, ushort Register)>();
        var freeRegisters = new Stack<ushort>();
        var nextTempRegister = nextPinned;
        var tempIntervals = BuildSortedTemporaryIntervals(intervals);
        for (var intervalIndex = 0; intervalIndex < tempIntervals.Length; intervalIndex++)
        {
            var interval = tempIntervals[intervalIndex];
            if (_registers[interval.RegisterId].RoutineId != NoRoutineId) continue;
            for (var index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].End >= interval.Start) continue;
                freeRegisters.Push(active[index].Register);
                active.RemoveAt(index);
            }

            var register = freeRegisters.Count > 0 ? freeRegisters.Pop() : nextTempRegister++;
            result[interval.RegisterId] = register;
            active.Add((interval.RegisterId, interval.End, register));
        }

        for (var index = 0; index < intervals.Length; index++)
        {
            var interval = intervals[index];
            if (!interval.HasValue) continue;
            if (_registers[interval.RegisterId].IsTemporary) continue;
            if (!result.ContainsKey(interval.RegisterId)) result[interval.RegisterId] = nextPinned++;
        }

        return new RegisterAllocationResult(result, []);
    }

    private RegisterInterval[] BuildRegisterIntervals(IReadOnlyList<PlanItem> items)
    {
        var intervals = new RegisterInterval[_registers.Count];
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Instruction is not { } instruction) continue;
            AddOperandRegisterIntervals(intervals, instruction.Destination, index);
            AddOperandRegisterIntervals(intervals, instruction.X, index);
            AddOperandRegisterIntervals(intervals, instruction.Y, index);
            AddOperandRegisterIntervals(intervals, instruction.A, index);
            AddOperandRegisterIntervals(intervals, instruction.B, index);
            AddOperandRegisterIntervals(intervals, instruction.C, index);
            AddOperandRegisterIntervals(intervals, instruction.D, index);
            AddOperandRegisterIntervals(intervals, instruction.SecondaryList, index);
        }

        return intervals;
    }

    private void AddOperandRegisterIntervals(RegisterInterval[] intervals, GesOperand operand, int instructionIndex)
    {
        if (operand.Kind == GesOperandKind.Register)
        {
            AddRegisterInterval(intervals, operand.RegisterRef, instructionIndex);
            return;
        }

        if (operand.Kind != GesOperandKind.RegisterList || operand.RegisterListValue is null) return;
        for (var index = 0; index < operand.RegisterListValue.Count; index++)
        {
            AddRegisterInterval(intervals, operand.RegisterListValue[index], instructionIndex);
        }
    }

    private void AddRegisterInterval(RegisterInterval[] intervals, GesRegisterRef register, int instructionIndex)
    {
        RequireRegister(register);
        ref var interval = ref intervals[register.Id];
        if (interval.HasValue)
        {
            interval.End = instructionIndex;
            return;
        }

        interval = new RegisterInterval(register.Id, instructionIndex, instructionIndex);
    }

    private IReadOnlyDictionary<int, ushort> ResolveLabelAddresses(IReadOnlyList<PlanItem> items)
    {
        var result = new Dictionary<int, ushort>();
        var address = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Label.HasValue)
            {
                result[item.Label.Value.Id] = ToUShort(address, "label address");
                continue;
            }

            if (item.Instruction is not null) address++;
        }

        for (var index = 0; index < _labels.Count; index++)
        {
            var label = _labels[index];
            if (!result.ContainsKey(label.Id)) throw new InvalidOperationException($"Label '{label.DisplayName}' was declared but never marked.");
        }

        return result;
    }

    private void ValidateOperand(GesOperand operand)
    {
        switch (operand.Kind)
        {
            case GesOperandKind.None:
                return;
            case GesOperandKind.Register:
                RequireRegister(operand.RegisterRef);
                return;
            case GesOperandKind.Label:
                RequireLabel(operand.LabelRef);
                return;
            case GesOperandKind.RegisterList:
                for (var index = 0; index < operand.RegisterListValue!.Count; index++)
                {
                    RequireRegister(operand.RegisterListValue[index]);
                }
                return;
            case GesOperandKind.TextList:
                for (var index = 0; index < operand.TextListValue!.Count; index++)
                {
                    var text = operand.TextListValue[index];
                    if (text is null) throw new ArgumentException("Text list operands cannot contain null entries.", nameof(operand));
                }
                return;
            case GesOperandKind.Bind:
                if (operand.BindRef.Id < 0 || operand.BindRef.Id >= _binds.Count) throw new ArgumentOutOfRangeException(nameof(operand), "Unknown bind reference.");
                return;
            case GesOperandKind.Text:
            case GesOperandKind.Type:
            case GesOperandKind.UShort:
            case GesOperandKind.Short:
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(operand), operand.Kind, "Unknown operand kind.");
        }
    }

    private void RequireRegister(GesRegisterRef register)
    {
        if (register.Id < 0 || register.Id >= _registers.Count) throw new ArgumentOutOfRangeException(nameof(register), "Unknown register reference.");
    }

    private void RequireLabel(GesLabelRef label)
    {
        if (label.Id < 0 || label.Id >= _labels.Count) throw new ArgumentOutOfRangeException(nameof(label), "Unknown label reference.");
    }

    private static ushort ToUShort(int value, string operand)
        => value is < 0 or > ushort.MaxValue ? throw new InvalidOperationException($"{operand} '{value}' does not fit into UInt16.") : checked((ushort)value);

    private RegisterAllocationResult AllocateScopedRegisters(RegisterInterval[] intervals)
    {
        var result = new Dictionary<int, ushort>();
        var routineLocalCounts = new short[_routines.Count];
        var routineMaxRegisterIndexes = new int[_routines.Count];
        for (var index = 0; index < routineMaxRegisterIndexes.Length; index++)
        {
            routineMaxRegisterIndexes[index] = -1;
        }

        var temporaryIntervals = BuildSortedTemporaryIntervals(intervals);
        var temporaryIntervalOffset = 0;

        for (var routineIndex = 0; routineIndex < _routines.Count; routineIndex++)
        {
            var routine = _routines[routineIndex];
            ushort nextPinned = 0;
            for (var argumentIndex = 0; argumentIndex < routine.ArgumentRegisters.Count; argumentIndex++)
            {
                var argument = routine.ArgumentRegisters[argumentIndex];
                result[argument.Id] = nextPinned;
                UpdateRoutineMaxRegisterIndex(routineMaxRegisterIndexes, routine.Id, nextPinned);
                nextPinned++;
            }

            for (var registerIndex = 0; registerIndex < _registers.Count; registerIndex++)
            {
                var register = _registers[registerIndex];
                if (register.RoutineId != routine.Id || register.IsTemporary) continue;
                if (result.ContainsKey(register.Id)) continue;
                result[register.Id] = nextPinned;
                UpdateRoutineMaxRegisterIndex(routineMaxRegisterIndexes, routine.Id, nextPinned);
                nextPinned++;
            }

            var active = new List<(int RegisterId, int End, ushort Register)>();
            var freeRegisters = new Stack<ushort>();
            var nextTempRegister = nextPinned;
            while (temporaryIntervalOffset < temporaryIntervals.Length && _registers[temporaryIntervals[temporaryIntervalOffset].RegisterId].RoutineId < routine.Id)
            {
                temporaryIntervalOffset++;
            }

            var intervalIndex = temporaryIntervalOffset;
            while (intervalIndex < temporaryIntervals.Length)
            {
                var interval = temporaryIntervals[intervalIndex];
                var intervalRoutineId = _registers[interval.RegisterId].RoutineId;
                if (intervalRoutineId != routine.Id) break;
                for (var index = active.Count - 1; index >= 0; index--)
                {
                    if (active[index].End >= interval.Start) continue;
                    freeRegisters.Push(active[index].Register);
                    active.RemoveAt(index);
                }

                var register = freeRegisters.Count > 0 ? freeRegisters.Pop() : nextTempRegister++;
                result[interval.RegisterId] = register;
                UpdateRoutineMaxRegisterIndex(routineMaxRegisterIndexes, routine.Id, register);
                active.Add((interval.RegisterId, interval.End, register));
                intervalIndex++;
            }

            temporaryIntervalOffset = intervalIndex;

            var localCount = Math.Max(0, routineMaxRegisterIndexes[routine.Id] + 1 - routine.ArgumentRegisters.Count);
            if (localCount > short.MaxValue)
            {
                throw new InvalidOperationException($"Routine '{routine.Name}' requires too many local registers: {localCount}.");
            }

            routineLocalCounts[routine.Id] = (short)localCount;
        }

        var hasGlobalIntervals = false;
        for (var index = 0; index < temporaryIntervals.Length; index++)
        {
            if (_registers[temporaryIntervals[index].RegisterId].RoutineId == NoRoutineId)
            {
                hasGlobalIntervals = true;
                break;
            }
        }

        if (hasGlobalIntervals)
        {
            ushort nextPinned = 0;
            for (var registerIndex = 0; registerIndex < _registers.Count; registerIndex++)
            {
                var register = _registers[registerIndex];
                if (register.RoutineId != NoRoutineId || register.IsTemporary) continue;
                result[register.Id] = nextPinned++;
            }

            var active = new List<(int RegisterId, int End, ushort Register)>();
            var freeRegisters = new Stack<ushort>();
            var nextTempRegister = nextPinned;
            for (var intervalIndex = 0; intervalIndex < temporaryIntervals.Length; intervalIndex++)
            {
                var interval = temporaryIntervals[intervalIndex];
                if (_registers[interval.RegisterId].RoutineId != NoRoutineId) continue;
                for (var index = active.Count - 1; index >= 0; index--)
                {
                    if (active[index].End >= interval.Start) continue;
                    freeRegisters.Push(active[index].Register);
                    active.RemoveAt(index);
                }

                var register = freeRegisters.Count > 0 ? freeRegisters.Pop() : nextTempRegister++;
                result[interval.RegisterId] = register;
                active.Add((interval.RegisterId, interval.End, register));
            }
        }

        for (var index = 0; index < intervals.Length; index++)
        {
            var interval = intervals[index];
            if (!interval.HasValue) continue;
            if (!result.ContainsKey(interval.RegisterId))
            {
                throw new InvalidOperationException($"Register '{interval.RegisterId}' was not allocated.");
            }
        }

        return new RegisterAllocationResult(result, routineLocalCounts);
    }

    private RegisterInterval[] BuildSortedTemporaryIntervals(RegisterInterval[] intervals)
    {
        var count = 0;
        for (var index = 0; index < intervals.Length; index++)
        {
            var interval = intervals[index];
            if (!interval.HasValue) continue;
            if (_registers[interval.RegisterId].IsTemporary) count++;
        }

        if (count == 0) return [];
        var result = new RegisterInterval[count];
        var offset = 0;
        for (var index = 0; index < intervals.Length; index++)
        {
            var interval = intervals[index];
            if (!interval.HasValue) continue;
            if (!_registers[interval.RegisterId].IsTemporary) continue;
            result[offset++] = interval;
        }

        Array.Sort(result, CompareRegisterIntervalByRoutineThenStart);
        return result;
    }

    private void UpdateRoutineMaxRegisterIndex(int[] routineMaxRegisterIndexes, int routineId, ushort registerIndex)
    {
        if (routineId == NoRoutineId) return;
        if (registerIndex > routineMaxRegisterIndexes[routineId])
        {
            routineMaxRegisterIndexes[routineId] = registerIndex;
        }
    }

    private int CompareRegisterIntervalByRoutineThenStart(RegisterInterval left, RegisterInterval right)
    {
        var leftRoutineId = _registers[left.RegisterId].RoutineId;
        var rightRoutineId = _registers[right.RegisterId].RoutineId;
        var routineCompare = leftRoutineId.CompareTo(rightRoutineId);
        return routineCompare != 0 ? routineCompare : CompareRegisterInterval(left, right);
    }

    private sealed record RegisterAllocationResult(
        IReadOnlyDictionary<int, ushort> RegisterMap,
        IReadOnlyList<short> RoutineLocalCounts);

    private readonly record struct RegisterSymbol(int Id, string? Name, bool IsTemporary, int RoutineId);

    private readonly record struct LabelSymbol(int Id, string? Name)
    {
        public string DisplayName => Name ?? $"L_{Id}";
    }

    private sealed record BindPlan(
        int Index,
        GameEventScriptBinaryBindKind Kind,
        string Name,
        IReadOnlyList<string> ArgumentNames,
        GesLabelRef? EntryLabel,
        ushort? Id,
        IReadOnlyList<string> RequiredTags,
        IReadOnlyList<string> ExcludedTags);

    private struct RegisterInterval(int registerId, int start, int end)
    {
        public int RegisterId { get; } = registerId;
        public int Start { get; } = start;
        public int End { get; set; } = end;
        public bool HasValue { get; } = true;
    }

    internal readonly record struct PlanItem(GesLabelRef? Label, InstructionPlan? Instruction, GameEventScriptSourceLocation? SourceRange)
    {
        public static PlanItem ForLabel(GesLabelRef label, GameEventScriptSourceLocation? sourceRange = null) => new(label, null, sourceRange);

        public static PlanItem ForInstruction(InstructionPlan instruction, GameEventScriptSourceLocation? sourceRange = null) => new(null, instruction, sourceRange);
    }

    internal sealed record InstructionPlan(
        int RoutineId,
        GameEventScriptBytecodeOpCode OpCode,
        GameEventScriptBytecodeInstructionUnit Unit,
        GameEventScriptInstructionFlag Flags,
        GesOperand Destination,
        GesOperand X,
        GesOperand Y,
        GesOperand A,
        GesOperand B,
        GesOperand C,
        GesOperand D,
        GesOperand SecondaryList,
        short? Count,
        long? I64,
        double? F64)
    {
        public InstructionPlan WithDestination(GesOperand destination)
            => this with { Destination = destination };

        public bool ReadsRegister(GesRegisterRef register)
        {
            return X.ContainsRegister(register) ||
                   Y.ContainsRegister(register) ||
                   A.ContainsRegister(register) ||
                   B.ContainsRegister(register) ||
                   C.ContainsRegister(register) ||
                   D.ContainsRegister(register) ||
                   SecondaryList.ContainsRegister(register);
        }

        public int CountRegisterReads(GesRegisterRef register)
        {
            return X.CountRegister(register) +
                   Y.CountRegister(register) +
                   A.CountRegister(register) +
                   B.CountRegister(register) +
                   C.CountRegister(register) +
                   D.CountRegister(register) +
                   SecondaryList.CountRegister(register);
        }
    }
}

internal readonly record struct GesRegisterRef(int Id);

internal readonly record struct GesLabelRef(int Id);

internal readonly record struct GesBindRef(int Id);

internal readonly struct GesOperand
{
    public readonly GesOperandKind Kind;
    public readonly GesRegisterRef RegisterRef;
    public readonly GesLabelRef LabelRef;
    public readonly GesBindRef BindRef;
    public readonly string? TextValue;
    public readonly IReadOnlyList<GesRegisterRef>? RegisterListValue;
    public readonly IReadOnlyList<string>? TextListValue;
    public readonly GameEventScriptBytecodeTypeKind TypeKind;
    public readonly ushort UShort;
    public readonly short Short;

    private GesOperand(
        GesOperandKind kind,
        GesRegisterRef register = default,
        GesLabelRef label = default,
        GesBindRef bind = default,
        string? text = null,
        IReadOnlyList<GesRegisterRef>? registerList = null,
        IReadOnlyList<string>? textList = null,
        GameEventScriptBytecodeTypeKind typeKind = GameEventScriptBytecodeTypeKind.Nothing,
        ushort uShort = 0,
        short @short = 0)
    {
        Kind = kind;
        RegisterRef = register;
        LabelRef = label;
        BindRef = bind;
        TextValue = text;
        RegisterListValue = registerList;
        TextListValue = textList;
        TypeKind = typeKind;
        UShort = uShort;
        Short = @short;
    }

    public static GesOperand Register(GesRegisterRef register) => new(GesOperandKind.Register, register);

    public static GesOperand Label(GesLabelRef label) => new(GesOperandKind.Label, label: label);

    public static GesOperand Bind(GesBindRef bind) => new(GesOperandKind.Bind, bind: bind);

    public static GesOperand Text(string text) => new(GesOperandKind.Text, text: text ?? throw new ArgumentNullException(nameof(text)));

    public static GesOperand RegisterList(IReadOnlyList<GesRegisterRef> registers) => new(GesOperandKind.RegisterList, registerList: CopyRegisterList(registers));

    public static GesOperand TextList(IReadOnlyList<string> texts) => new(GesOperandKind.TextList, textList: CopyTextList(texts));

    public static GesOperand Type(GameEventScriptBytecodeTypeKind typeKind) => new(GesOperandKind.Type, typeKind: typeKind);

    public static GesOperand U16(ushort value) => new(GesOperandKind.UShort, uShort: value);

    public static GesOperand I16(short value) => new(GesOperandKind.Short, @short: value);

    internal bool ContainsRegister(GesRegisterRef register)
    {
        switch (Kind)
        {
            case GesOperandKind.Register:
                return RegisterRef.Id == register.Id;
            case GesOperandKind.RegisterList when RegisterListValue is not null:
                for (var index = 0; index < RegisterListValue.Count; index++)
                {
                    if (RegisterListValue[index].Id == register.Id) return true;
                }

                return false;
            default:
                return false;
        }
    }

    internal int CountRegister(GesRegisterRef register)
    {
        switch (Kind)
        {
            case GesOperandKind.Register:
                return RegisterRef.Id == register.Id ? 1 : 0;
            case GesOperandKind.RegisterList when RegisterListValue is not null:
                var count = 0;
                for (var index = 0; index < RegisterListValue.Count; index++)
                {
                    if (RegisterListValue[index].Id == register.Id) count++;
                }

                return count;
            default:
                return 0;
        }
    }

    private static GesRegisterRef[] CopyRegisterList(IReadOnlyList<GesRegisterRef>? source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (source.Count == 0) return [];
        var result = new GesRegisterRef[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            result[index] = source[index];
        }

        return result;
    }

    private static string[] CopyTextList(IReadOnlyList<string>? source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (source.Count == 0) return [];
        var result = new string[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            result[index] = source[index] ?? throw new ArgumentException("Text list operands cannot contain null entries.", nameof(source));
        }

        return result;
    }
}

internal enum GesOperandKind
{
    None,
    Register,
    Label,
    Text,
    RegisterList,
    TextList,
    Bind,
    Type,
    UShort,
    Short
}
