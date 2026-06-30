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
        CopyPlanItems(_rootItems, result, ref offset);

        var roots = CollectRootRoutines();
        Array.Sort(roots, CompareRoutinePlanForOutput);
        for (var index = 0; index < roots.Length; index++)
        {
            AppendRoutineAndOwnedHelpers(result, ref offset, roots[index]);
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

    private void AppendRoutineAndOwnedHelpers(PlanItem[] result, ref int offset, RoutinePlan routine)
    {
        CopyPlanItems(routine.Items, result, ref offset);
        for (var index = 0; index < _routines.Count; index++)
        {
            var child = _routines[index];
            if (child.ParentRoutineId != routine.Id) continue;
            AppendRoutineAndOwnedHelpers(result, ref offset, child);
        }
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
        var registerMap = AllocateRegisters(optimizedItems);
        var items = PatchRoutineRegisterLocals(optimizedItems, registerMap);
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
            var hash = 2166136261u;

            MixUShort(ref hash, _version);
            MixUInt(ref hash, (uint)_flags);

            for (var index = 0; index < _textConstants.Count; index++)
            {
                MixString(ref hash, _textConstants[index]);
            }

            for (var sliceIndex = 0; sliceIndex < _uint16Slices.Count; sliceIndex++)
            {
                var slice = _uint16Slices[sliceIndex];
                MixInt(ref hash, slice.Length);
                for (var index = 0; index < slice.Length; index++)
                {
                    MixUShort(ref hash, slice[index]);
                }
            }

            for (var index = 0; index < _binds.Count; index++)
            {
                var bind = _binds[index];
                MixUInt(ref hash, (uint)bind.Kind);
                MixUShort(ref hash, bind.Name);
                MixInt(ref hash, bind.ArgumentNames.Count);
                for (var argumentIndex = 0; argumentIndex < bind.ArgumentNames.Count; argumentIndex++)
                {
                    MixUShort(ref hash, bind.ArgumentNames[argumentIndex]);
                }

                MixUShort(ref hash, bind.EntryAddress);
                MixUShort(ref hash, bind.Id);
                MixInt(ref hash, bind.RequiredTags.Count);
                for (var tagIndex = 0; tagIndex < bind.RequiredTags.Count; tagIndex++)
                {
                    MixUShort(ref hash, bind.RequiredTags[tagIndex]);
                }

                MixInt(ref hash, bind.ExcludedTags.Count);
                for (var tagIndex = 0; tagIndex < bind.ExcludedTags.Count; tagIndex++)
                {
                    MixUShort(ref hash, bind.ExcludedTags[tagIndex]);
                }
            }

            for (var index = 0; index < _instructions.Count; index++)
            {
                var instruction = _instructions[index];
                MixByte(ref hash, (byte)instruction.OpCode);
                MixByte(ref hash, instruction.UnitAndFlags);
                MixUShort(ref hash, instruction.DestinationRegister);
                MixUShort(ref hash, (ushort)instruction.ImmediateX);
                MixUShort(ref hash, (ushort)instruction.ImmediateY);
                MixULong(ref hash, instruction.Payload);
            }

            return hash;
        }

        private static void MixString(ref uint hash, string value)
        {
            MixInt(ref hash, value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                MixUShort(ref hash, value[index]);
            }
        }

        private static void MixInt(ref uint hash, int value)
            => MixUInt(ref hash, unchecked((uint)value));

        private static void MixUShort(ref uint hash, ushort value)
        {
            MixByte(ref hash, (byte)value);
            MixByte(ref hash, (byte)(value >> 8));
        }

        private static void MixUInt(ref uint hash, uint value)
        {
            MixUShort(ref hash, (ushort)value);
            MixUShort(ref hash, (ushort)(value >> 16));
        }

        private static void MixULong(ref uint hash, ulong value)
        {
            MixUInt(ref hash, (uint)value);
            MixUInt(ref hash, (uint)(value >> 32));
        }

        private static void MixByte(ref uint hash, byte value)
        {
            hash ^= value;
            hash *= 16777619u;
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

    private static void CopyPlanItems(IReadOnlyList<PlanItem> source, PlanItem[] destination, ref int offset)
    {
        for (var index = 0; index < source.Count; index++)
        {
            destination[offset++] = source[index];
        }
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

        ApplyDestination(ref instruction, plan.Destination, registerMap, bindIds);
        ApplyX(ref instruction, plan.X, registerMap, labelAddresses, bindIds, resolveText, resolveList, resolveTextList);
        ApplyY(ref instruction, plan.Y, registerMap, labelAddresses, resolveText, resolveList, resolveTextList);
        ApplySecondaryList(ref instruction, plan.SecondaryList, registerMap, resolveList, resolveTextList);
        ApplyAux(ref instruction, plan.A, 0, registerMap, labelAddresses, resolveList);
        ApplyAux(ref instruction, plan.B, 1, registerMap, labelAddresses, resolveList);
        ApplyAux(ref instruction, plan.C, 2, registerMap, labelAddresses, resolveList);
        ApplyAux(ref instruction, plan.D, 3, registerMap, labelAddresses, resolveList);

        if (plan.Count.HasValue) instruction.Count = plan.Count.Value;
        if (plan.I64.HasValue) instruction.I64 = plan.I64.Value;
        if (plan.F64.HasValue) instruction.F64 = plan.F64.Value;
        return instruction;
    }

    private static void ApplyDestination(
        ref GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        IReadOnlyDictionary<int, ushort> registerMap,
        IReadOnlyList<ushort> bindIds)
    {
        if (operand.Kind == GesOperandKind.None) return;
        switch (operand.Kind)
        {
            case GesOperandKind.Register:
                instruction.DestinationRegister = ResolveRegister(operand.RegisterRef, registerMap);
                return;
            case GesOperandKind.Bind:
                instruction.MessageDestination = bindIds[operand.BindRef.Id];
                return;
            default:
                throw new InvalidOperationException($"Destination operand must be a register or bind but was '{operand.Kind}'.");
        }
    }

    private static void ApplyX(
        ref GameEventScriptBytecodeInstruction instruction,
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
                return;
            case GesOperandKind.Register:
                instruction.XRegister = ResolveRegister(operand.RegisterRef, registerMap);
                return;
            case GesOperandKind.Text:
                instruction.StringIndex = resolveText(operand.TextValue!);
                return;
            case GesOperandKind.RegisterList:
                instruction.SecondaryListIndex = resolveList(operand.RegisterListValue!);
                return;
            case GesOperandKind.TextList:
                instruction.SecondaryListIndex = resolveTextList(operand.TextListValue!);
                return;
            case GesOperandKind.Bind:
                instruction.BindId = bindIds[operand.BindRef.Id];
                return;
            case GesOperandKind.Label:
                instruction.XRegister = labelAddresses[operand.LabelRef.Id];
                return;
            case GesOperandKind.Type:
                instruction.TypeOperand = (ushort)operand.TypeKind;
                return;
            case GesOperandKind.UShort:
                instruction.XRegister = operand.UShort;
                return;
            case GesOperandKind.Short:
                instruction.ImmediateX = operand.Short;
                return;
            default:
                throw new InvalidOperationException($"Unsupported X operand '{operand.Kind}'.");
        }
    }

    private static void ApplyY(
        ref GameEventScriptBytecodeInstruction instruction,
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
                return;
            case GesOperandKind.Register:
                instruction.YRegister = ResolveRegister(operand.RegisterRef, registerMap);
                return;
            case GesOperandKind.Label:
                instruction.TargetAddress = labelAddresses[operand.LabelRef.Id];
                return;
            case GesOperandKind.Text:
                instruction.SecondaryStringIndex = resolveText(operand.TextValue!);
                return;
            case GesOperandKind.RegisterList:
                instruction.ListIndex = resolveList(operand.RegisterListValue!);
                return;
            case GesOperandKind.TextList:
                instruction.ListIndex = resolveTextList(operand.TextListValue!);
                return;
            case GesOperandKind.Type:
                instruction.TypeKind = operand.TypeKind;
                return;
            case GesOperandKind.UShort:
                instruction.ListIndex = operand.UShort;
                return;
            case GesOperandKind.Short:
                instruction.ImmediateY = operand.Short;
                return;
            default:
                throw new InvalidOperationException($"Unsupported Y operand '{operand.Kind}'.");
        }
    }

    private static void ApplySecondaryList(
        ref GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        IReadOnlyDictionary<int, ushort> registerMap,
        Func<IReadOnlyList<GesRegisterRef>, ushort> resolveList,
        Func<IReadOnlyList<string>, ushort> resolveTextList)
    {
        if (operand.Kind == GesOperandKind.None) return;
        switch (operand.Kind)
        {
            case GesOperandKind.RegisterList:
                instruction.SecondaryListIndex = resolveList(operand.RegisterListValue!);
                return;
            case GesOperandKind.TextList:
                instruction.SecondaryListIndex = resolveTextList(operand.TextListValue!);
                return;
            default:
                throw new InvalidOperationException($"Secondary list operand must be a register or text list but was '{operand.Kind}'.");
        }
    }

    private static void ApplyAux(
        ref GameEventScriptBytecodeInstruction instruction,
        GesOperand operand,
        int index,
        IReadOnlyDictionary<int, ushort> registerMap,
        IReadOnlyDictionary<int, ushort> labelAddresses,
        Func<IReadOnlyList<GesRegisterRef>, ushort> resolveList)
    {
        if (operand.Kind == GesOperandKind.None) return;
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
    }

    private static ushort ResolveRegister(GesRegisterRef register, IReadOnlyDictionary<int, ushort> registerMap)
        => registerMap.TryGetValue(register.Id, out var registerIndex)
            ? registerIndex
            : throw new InvalidOperationException($"Register '{register.Id}' was not allocated.");

    private IReadOnlyDictionary<int, ushort> AllocateRegisters(IReadOnlyList<PlanItem> items)
    {
        var intervals = BuildRegisterIntervals(items);
        var result = new Dictionary<int, ushort>();
        if (_routines.Count > 0) return AllocateScopedRegisters(items);

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
        var tempIntervals = CollectIntervals(intervals, NoRoutineId, temporary: true);
        for (var intervalIndex = 0; intervalIndex < tempIntervals.Length; intervalIndex++)
        {
            var interval = tempIntervals[intervalIndex];
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

        for (var index = 0; index < intervals.Count; index++)
        {
            var interval = intervals[index];
            if (_registers[interval.RegisterId].IsTemporary) continue;
            if (!result.ContainsKey(interval.RegisterId)) result[interval.RegisterId] = nextPinned++;
        }

        return result;
    }

    private List<RegisterInterval> BuildRegisterIntervals(IReadOnlyList<PlanItem> items)
    {
        var intervals = new List<RegisterInterval>();
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

    private void AddOperandRegisterIntervals(List<RegisterInterval> intervals, GesOperand operand, int instructionIndex)
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

    private void AddRegisterInterval(List<RegisterInterval> intervals, GesRegisterRef register, int instructionIndex)
    {
        RequireRegister(register);
        for (var index = 0; index < intervals.Count; index++)
        {
            var interval = intervals[index];
            if (interval.RegisterId != register.Id) continue;
            interval.End = instructionIndex;
            intervals[index] = interval;
            return;
        }

        intervals.Add(new RegisterInterval(register.Id, instructionIndex, instructionIndex));
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

    private IReadOnlyDictionary<int, ushort> AllocateScopedRegisters(IReadOnlyList<PlanItem> items)
    {
        var intervals = BuildRegisterIntervals(items);
        var result = new Dictionary<int, ushort>();

        for (var routineIndex = 0; routineIndex < _routines.Count; routineIndex++)
        {
            var routine = _routines[routineIndex];
            ushort nextPinned = 0;
            for (var argumentIndex = 0; argumentIndex < routine.ArgumentRegisters.Count; argumentIndex++)
            {
                var argument = routine.ArgumentRegisters[argumentIndex];
                result[argument.Id] = nextPinned++;
            }

            for (var registerIndex = 0; registerIndex < _registers.Count; registerIndex++)
            {
                var register = _registers[registerIndex];
                if (register.RoutineId != routine.Id || register.IsTemporary) continue;
                if (!result.ContainsKey(register.Id)) result[register.Id] = nextPinned++;
            }

            var active = new List<(int RegisterId, int End, ushort Register)>();
            var freeRegisters = new Stack<ushort>();
            var nextTempRegister = nextPinned;
            var routineIntervals = CollectIntervals(intervals, routine.Id, temporary: true);
            for (var intervalIndex = 0; intervalIndex < routineIntervals.Length; intervalIndex++)
            {
                var interval = routineIntervals[intervalIndex];
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

        var globalIntervals = CollectIntervals(intervals, NoRoutineId, temporary: true);
        if (globalIntervals.Length > 0)
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
            for (var intervalIndex = 0; intervalIndex < globalIntervals.Length; intervalIndex++)
            {
                var interval = globalIntervals[intervalIndex];
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

        for (var index = 0; index < intervals.Count; index++)
        {
            var interval = intervals[index];
            if (!result.ContainsKey(interval.RegisterId))
            {
                throw new InvalidOperationException($"Register '{interval.RegisterId}' was not allocated.");
            }
        }

        return result;
    }

    private RegisterInterval[] CollectIntervals(IReadOnlyList<RegisterInterval> intervals, int routineId, bool temporary)
    {
        var count = 0;
        for (var index = 0; index < intervals.Count; index++)
        {
            var register = _registers[intervals[index].RegisterId];
            if (register.RoutineId == routineId && register.IsTemporary == temporary) count++;
        }

        if (count == 0) return [];
        var result = new RegisterInterval[count];
        var offset = 0;
        for (var index = 0; index < intervals.Count; index++)
        {
            var interval = intervals[index];
            var register = _registers[interval.RegisterId];
            if (register.RoutineId != routineId || register.IsTemporary != temporary) continue;
            result[offset++] = interval;
        }

        Array.Sort(result, CompareRegisterInterval);
        return result;
    }

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
