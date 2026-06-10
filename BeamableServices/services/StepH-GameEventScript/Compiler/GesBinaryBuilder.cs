using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Api.GameEventScriptBinaryHeader;

namespace StepH.GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private readonly List<RegisterSymbol> _registers = [];
    private readonly List<LabelSymbol> _labels = [];
    private readonly List<PlanItem> _items = [];
    private readonly List<BindPlan> _binds = [];
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
        _moduleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("Module name must be non-empty.", nameof(moduleName))
            : moduleName;
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
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Register name must be non-empty.", nameof(name));
        var register = new RegisterSymbol(_registers.Count, name, IsTemporary: false);
        _registers.Add(register);
        return new GesRegisterRef(register.Id);
    }

    public GesRegisterRef AddTemporaryRegister(string? name = null)
    {
        var register = new RegisterSymbol(_registers.Count, string.IsNullOrWhiteSpace(name) ? null : name, IsTemporary: true);
        _registers.Add(register);
        return new GesRegisterRef(register.Id);
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
        _items.Add(PlanItem.ForLabel(label));
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
            argumentNames?.ToArray() ?? [],
            entryLabel,
            id,
            requiredTags?.ToArray() ?? [],
            excludedTags?.ToArray() ?? []);
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
        _items.Add(PlanItem.ForInstruction(new InstructionPlan(opcode, unit, flags, dst, x, y, a, b, c, d, secondaryList, count, i64, f64)));
        return this;
    }

    public GameEventScriptBinary Build()
    {
        var items = _optimize ? OptimizePeepholeMoves(_items) : _items.ToArray();
        var labelAddresses = ResolveLabelAddresses(items);
        var registerMap = AllocateRegisters(items);
        var builder = new GameEventScriptBinaryBuilder()
            .WithVersion(_version)
            .WithModuleName(_moduleName)
            .WithFlag(GameEventScriptBinaryFlags.Optimization, (_flags & GameEventScriptBinaryFlags.Optimization) != 0)
            .WithFlag(GameEventScriptBinaryFlags.Debug, (_flags & GameEventScriptBinaryFlags.Debug) != 0);

        var listIndexes = new Dictionary<string, ushort>(StringComparer.Ordinal);

        ushort ResolveText(string text)
        {
            builder.AddStringPoolElement(text, out var index);
            return index;
        }

        ushort ResolveList(IReadOnlyList<GesRegisterRef> registers)
        {
            var values = new ushort[registers.Count];
            for (var index = 0; index < registers.Count; index++)
            {
                values[index] = ResolveRegister(registers[index], registerMap);
            }

            var key = string.Join(",", values.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (listIndexes.TryGetValue(key, out var existing)) return existing;
            builder.AddUint16TableEntry(values, out var listIndex);
            listIndexes.Add(key, listIndex);
            return listIndex;
        }

        ushort ResolveTextList(IReadOnlyList<string> values)
        {
            var indexes = new ushort[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                indexes[index] = ResolveText(values[index]);
            }

            var key = "t:" + string.Join(",", indexes.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (listIndexes.TryGetValue(key, out var existing)) return existing;
            builder.AddUint16TableEntry(indexes, out var listIndex);
            listIndexes.Add(key, listIndex);
            return listIndex;
        }

        var bindIds = ResolveBinds(builder, ResolveText, labelAddresses);
        foreach (var item in items)
        {
            if (item.Instruction is null) continue;
            builder.AddBytecodeInstruction(EncodeInstruction(item.Instruction, registerMap, labelAddresses, bindIds, ResolveText, ResolveList, ResolveTextList));
        }

        return builder.Build();
    }

    private ushort[] ResolveBinds(
        GameEventScriptBinaryBuilder builder,
        Func<string, ushort> resolveText,
        IReadOnlyDictionary<int, ushort> labelAddresses)
    {
        var nextIds = new Dictionary<GameEventScriptBinaryBindKind, ushort>();
        var result = new ushort[_binds.Count];
        foreach (var bind in _binds)
        {
            var id = bind.Id ?? NextId(bind.Kind, nextIds);
            result[bind.Index] = id;
            var argumentNames = bind.ArgumentNames.Select(resolveText).ToArray();
            var requiredTags = bind.RequiredTags.Select(resolveText).ToArray();
            var excludedTags = bind.ExcludedTags.Select(resolveText).ToArray();
            var entryAddress = bind.EntryLabel.HasValue
                ? labelAddresses[bind.EntryLabel.Value.Id]
                : ushort.MaxValue;

            builder.AddBind(new GameEventScriptBinaryBindEntry(bind.Kind, resolveText(bind.Name), argumentNames, entryAddress, id, requiredTags, excludedTags));
        }

        return result;
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
                instruction.DestinationSlot = ResolveRegister(operand.RegisterRef, registerMap);
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
                instruction.XSlot = ResolveRegister(operand.RegisterRef, registerMap);
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
                instruction.XSlot = labelAddresses[operand.LabelRef.Id];
                return;
            case GesOperandKind.Type:
                instruction.TypeOperand = (ushort)operand.TypeKind;
                return;
            case GesOperandKind.UShort:
                instruction.XSlot = operand.UShort;
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
                instruction.YSlot = ResolveRegister(operand.RegisterRef, registerMap);
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
        => registerMap.TryGetValue(register.Id, out var slot)
            ? slot
            : throw new InvalidOperationException($"Register '{register.Id}' was not allocated.");

    private IReadOnlyDictionary<int, ushort> AllocateRegisters(IReadOnlyList<PlanItem> items)
    {
        var intervals = BuildRegisterIntervals(items);
        var result = new Dictionary<int, ushort>();
        ushort nextPinned = 0;
        foreach (var register in _registers.Where(register => !register.IsTemporary))
        {
            result[register.Id] = nextPinned++;
        }

        var active = new List<(int RegisterId, int End, ushort Slot)>();
        var freeSlots = new Stack<ushort>();
        var nextTempSlot = nextPinned;
        foreach (var interval in intervals
                     .Where(interval => _registers[interval.RegisterId].IsTemporary)
                     .OrderBy(interval => interval.Start)
                     .ThenBy(interval => interval.RegisterId))
        {
            for (var index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].End >= interval.Start) continue;
                freeSlots.Push(active[index].Slot);
                active.RemoveAt(index);
            }

            var slot = freeSlots.Count > 0 ? freeSlots.Pop() : nextTempSlot++;
            result[interval.RegisterId] = slot;
            active.Add((interval.RegisterId, interval.End, slot));
        }

        foreach (var interval in intervals.Where(interval => !_registers[interval.RegisterId].IsTemporary))
        {
            if (!result.ContainsKey(interval.RegisterId)) result[interval.RegisterId] = nextPinned++;
        }

        return result;
    }

    private List<RegisterInterval> BuildRegisterIntervals(IReadOnlyList<PlanItem> items)
    {
        var map = new Dictionary<int, RegisterInterval>();
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Instruction is not { } instruction) continue;
            foreach (var register in instruction.RegisterOperands())
            {
                RequireRegister(register);
                if (map.TryGetValue(register.Id, out var interval))
                {
                    interval.End = index;
                    map[register.Id] = interval;
                }
                else
                {
                    map.Add(register.Id, new RegisterInterval(register.Id, index, index));
                }
            }
        }

        return map.Values.ToList();
    }

    private IReadOnlyDictionary<int, ushort> ResolveLabelAddresses(IReadOnlyList<PlanItem> items)
    {
        var result = new Dictionary<int, ushort>();
        var address = 0;
        foreach (var item in items)
        {
            if (item.Label.HasValue)
            {
                result[item.Label.Value.Id] = ToUShort(address, "label address");
                continue;
            }

            if (item.Instruction is not null) address++;
        }

        foreach (var label in _labels)
        {
            if (!result.ContainsKey(label.Id)) throw new InvalidOperationException($"Label '{label.DisplayName}' was declared but never marked.");
        }

        return result;
    }

    private PlanItem[] OptimizePeepholeMoves(IReadOnlyList<PlanItem> items)
    {
        var result = new List<PlanItem>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Instruction is null ||
                index + 1 >= items.Count ||
                items[index + 1].Instruction is not { } move ||
                move.OpCode != GameEventScriptBytecodeOpCode.Move ||
                item.Instruction.Destination.Kind != GesOperandKind.Register ||
                move.X.Kind != GesOperandKind.Register ||
                move.Destination.Kind != GesOperandKind.Register ||
                item.Instruction.Destination.RegisterRef.Id != move.X.RegisterRef.Id ||
                !_registers[item.Instruction.Destination.RegisterRef.Id].IsTemporary ||
                item.Instruction.ReadRegisters().Any(register => register.Id == move.Destination.RegisterRef.Id) ||
                CountRegisterReads(items, item.Instruction.Destination.RegisterRef) != 1 ||
                CountRegisterWrites(items, item.Instruction.Destination.RegisterRef) != 1)
            {
                result.Add(item);
                continue;
            }

            result.Add(PlanItem.ForInstruction(item.Instruction.WithDestination(move.Destination)));
            index++;
        }

        return result.ToArray();
    }

    private static int CountRegisterReads(IReadOnlyList<PlanItem> items, GesRegisterRef register)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item.Instruction is not { } instruction) continue;
            count += instruction.ReadRegisters().Count(value => value.Id == register.Id);
        }

        return count;
    }

    private static int CountRegisterWrites(IReadOnlyList<PlanItem> items, GesRegisterRef register)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item.Instruction?.Destination.Kind == GesOperandKind.Register &&
                item.Instruction.Destination.RegisterRef.Id == register.Id)
            {
                count++;
            }
        }

        return count;
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
                foreach (var register in operand.RegisterListValue!) RequireRegister(register);
                return;
            case GesOperandKind.TextList:
                foreach (var text in operand.TextListValue!)
                {
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

    private readonly record struct RegisterSymbol(int Id, string? Name, bool IsTemporary);

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

    private readonly record struct PlanItem(GesLabelRef? Label, InstructionPlan? Instruction)
    {
        public static PlanItem ForLabel(GesLabelRef label) => new(label, null);

        public static PlanItem ForInstruction(InstructionPlan instruction) => new(null, instruction);
    }

    private sealed record InstructionPlan(
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

        public IEnumerable<GesRegisterRef> RegisterOperands()
            => ReadRegisters().Concat(WriteRegisters());

        public IEnumerable<GesRegisterRef> ReadRegisters()
        {
            foreach (var register in X.Registers()) yield return register;
            foreach (var register in Y.Registers()) yield return register;
            foreach (var register in A.Registers()) yield return register;
            foreach (var register in B.Registers()) yield return register;
            foreach (var register in C.Registers()) yield return register;
            foreach (var register in D.Registers()) yield return register;
            foreach (var register in SecondaryList.Registers()) yield return register;
        }

        private IEnumerable<GesRegisterRef> WriteRegisters()
        {
            if (Destination.Kind == GesOperandKind.Register) yield return Destination.RegisterRef;
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
        GameEventScriptBytecodeTypeKind typeKind = GameEventScriptBytecodeTypeKind.Invalid,
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

    public static GesOperand RegisterList(IReadOnlyList<GesRegisterRef> registers) => new(GesOperandKind.RegisterList, registerList: registers?.ToArray() ?? throw new ArgumentNullException(nameof(registers)));

    public static GesOperand TextList(IReadOnlyList<string> texts) => new(GesOperandKind.TextList, textList: texts?.ToArray() ?? throw new ArgumentNullException(nameof(texts)));

    public static GesOperand Type(GameEventScriptBytecodeTypeKind typeKind) => new(GesOperandKind.Type, typeKind: typeKind);

    public static GesOperand U16(ushort value) => new(GesOperandKind.UShort, uShort: value);

    public static GesOperand I16(short value) => new(GesOperandKind.Short, @short: value);

    internal IEnumerable<GesRegisterRef> Registers()
    {
        switch (Kind)
        {
            case GesOperandKind.Register:
                yield return RegisterRef;
                break;
            case GesOperandKind.RegisterList when RegisterListValue is not null:
                foreach (var register in RegisterListValue) yield return register;
                break;
        }
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
