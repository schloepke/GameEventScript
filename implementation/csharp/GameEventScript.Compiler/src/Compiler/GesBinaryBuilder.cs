// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Api;
using static GameEventScript.Api.GesBindingSegment;

namespace GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private readonly List<RegisterSymbol> _registers = [];
    private readonly List<LabelSymbol> _labels = [];
    private readonly List<PlanItem> _rootItems = [];
    private readonly List<BindPlan> _binds = [];
    private PlanItem[]? _rewrittenItems;
    private ushort _version = 1;
    private string _moduleName = "Unknown";
    private ulong _programVersion;
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

    public GesBinaryBuilder WithProgramVersion(ulong programVersion)
    {
        _programVersion = programVersion;
        return this;
    }

    public GesBinaryBuilder WithOptimization(bool enabled = true)
    {
        _optimize = enabled;
        return this;
    }

    public GesRegisterRef AddRegister(string name) => AddRegisterInRoutine(name, CurrentRoutineId);

    internal GesRegisterRef AddCompilerRegister(string name) => AddRegisterInRoutine(name, CurrentRoutineId, debugVisible: false);

    public GesRegisterRef AddTemporaryRegister(string? name = null) => AddTemporaryRegisterInRoutine(name, CurrentRoutineId);

    private GesRegisterRef AddRegisterInRoutine(string name, int routineId, bool debugVisible = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Register name must be non-empty.", nameof(name));
        ValidateRoutineId(routineId);
        var register = new RegisterSymbol(_registers.Count, name, IsTemporary: false, routineId, debugVisible);
        _registers.Add(register);
        return new GesRegisterRef(register.Id);
    }

    private GesRegisterRef AddTemporaryRegisterInRoutine(string? name, int routineId)
    {
        ValidateRoutineId(routineId);
        var register = new RegisterSymbol(_registers.Count, string.IsNullOrWhiteSpace(name) ? null : name, IsTemporary: true, routineId, IsDebugVisible: false);
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
        foreach (var routine in _routines) count += routine.Items.Count;
        var result = new PlanItem[count];
        var offset = CopyPlanItems(_rootItems, result, 0);
        var roots = CollectRootRoutines();
        Array.Sort(roots, CompareRoutinePlanForOutput);
        foreach (var root in roots) offset = AppendRoutineAndOwnedHelpers(result, offset, root);
        return result;
    }

    private RoutinePlan[] CollectRootRoutines()
    {
        var count = 0;
        foreach (var routine in _routines)
            if (routine.ParentRoutineId == NoRoutineId)
                count++;
        var result = new RoutinePlan[count];
        var offset = 0;
        foreach (var routine in _routines)
            if (routine.ParentRoutineId == NoRoutineId)
                result[offset++] = routine;
        return result;
    }

    private int AppendRoutineAndOwnedHelpers(PlanItem[] result, int offset, RoutinePlan routine)
    {
        offset = CopyPlanItems(routine.Items, result, offset);
        foreach (var child in _routines)
            if (child.ParentRoutineId == routine.Id)
                offset = AppendRoutineAndOwnedHelpers(result, offset, child);
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

    public GesBindRef AddBind(GesBinaryBindKind kind, string name, IReadOnlyList<string>? argumentNames = null, GesLabelRef? entryLabel = null, ushort? id = null, IReadOnlyList<string>? requiredTags = null,
        IReadOnlyList<string>? excludedTags = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bind name must be non-empty.", nameof(name));
        if (entryLabel.HasValue) RequireLabel(entryLabel.Value);
        var bind = new BindPlan(_binds.Count, kind, name, CopyStringList(argumentNames), entryLabel, id, CopyStringList(requiredTags), CopyStringList(excludedTags));
        _binds.Add(bind);
        return new GesBindRef(bind.Index);
    }

    internal GesBinaryBuilder AddOpcode(
        GesBytecodeOpCode opcode,
        GesQuantityUnit unit = GesQuantityUnit.UnitNone,
        GesInstructionFlag flags = GesInstructionFlag.None,
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

    public GameEventScriptProgram Build()
    {
        EnsureScopesClosed();
        var planItems = _rewrittenItems ?? LinearizePlanItems();
        var optimizedItems = _optimize ? RunDefaultOptimizationPasses(planItems) : CopyPlanItems(planItems);
        var labelAddresses = ResolveLabelAddresses(optimizedItems);
        var registerAllocation = AllocateRegisters(optimizedItems, labelAddresses);
        var registerMap = registerAllocation.RegisterMap;
        var items = PatchRoutineRegisterLocals(optimizedItems, registerAllocation);
        var resourceAnalysis = AnalyzeProgramResources(items, registerAllocation);
        var debugSegments = BuildDebugSegments(items, registerMap);
        var builder = new BinaryMaterializer()
            .WithVersion(_version)
            .WithModuleName(_moduleName)
            .WithProgramVersion(_programVersion)
            .WithResourceRequirements(resourceAnalysis.RequiredRegisterCount, resourceAnalysis.RequiredCallStackDepth)
            .WithDebugSegments(debugSegments.Symbols, debugSegments.SourceMap, debugSegments.SourceArchive);

        var bindIds = ResolveBinds(builder, builder.AddText, labelAddresses, resourceAnalysis);
        var resolver = new OperandResolver(registerMap, labelAddresses, bindIds, builder);
        foreach (var item in items)
            if (item.Instruction is { } instruction)
                builder.AddInstruction(EncodeInstruction(instruction, resolver));

        return builder.Build();
    }

    private ushort[] ResolveBinds(BinaryMaterializer builder, Func<string, ushort> resolveText, IReadOnlyDictionary<int, ushort> labelAddresses, ProgramResourceAnalysis resourceAnalysis)
    {
        var nextIds = new Dictionary<GesBinaryBindKind, ushort>();
        var result = new ushort[_binds.Count];
        BindPlan[] orderedBinds = [.. _binds];
        Array.Sort(orderedBinds, CompareBindPlanForOutput);
        foreach (var bind in orderedBinds)
        {
            var id = bind.Id ?? NextId(bind.Kind, nextIds);
            result[bind.Index] = id;
            var argumentNames = ResolveTextList(bind.ArgumentNames, resolveText);
            var requiredTags = ResolveTextList(bind.RequiredTags, resolveText);
            var excludedTags = ResolveTextList(bind.ExcludedTags, resolveText);
            var entryAddress = bind.EntryLabel.HasValue ? labelAddresses[bind.EntryLabel.Value.Id] : ushort.MaxValue;
            var requirements = resourceAnalysis.BindRequirements[bind.Index];
            builder.AddUInt16Slice(argumentNames);
            builder.AddUInt16Slice(requiredTags);
            builder.AddUInt16Slice(excludedTags);
            builder.AddBind(new GesBindingSegment.Entry(bind.Kind, resolveText(bind.Name), argumentNames, entryAddress, id, requiredTags, excludedTags, requirements.RequiredRegisterCount, requirements.RequiredCallStackDepth));
        }

        return result;
    }

    private sealed class BinaryMaterializer
    {
        private ushort _version = 1;
        private string _moduleName = "Unknown";
        private ulong _programVersion;
        private ushort _requiredRegisterCount;
        private ushort _requiredCallStackDepth;
        private GesDebugSymbolsSegment? _debugSymbols;
        private GesSourceMapSegment? _sourceMap;
        private GesSourceArchiveSegment? _sourceArchive;
        private readonly List<string> _textConstants = [];
        private readonly Dictionary<string, ushort> _textIndexes = [];
        private readonly List<ushort[]> _uint16Slices = [];
        private readonly List<GesBindingSegment.Entry> _binds = [];
        private readonly List<GesBytecodeInstruction> _instructions = [];

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

        public BinaryMaterializer WithProgramVersion(ulong programVersion)
        {
            _programVersion = programVersion;
            return this;
        }

        public BinaryMaterializer WithResourceRequirements(ushort requiredRegisterCount, ushort requiredCallStackDepth)
        {
            _requiredRegisterCount = requiredRegisterCount;
            _requiredCallStackDepth = requiredCallStackDepth;
            return this;
        }

        public BinaryMaterializer WithDebugSegments(GesDebugSymbolsSegment? debugSymbols, GesSourceMapSegment? sourceMap, GesSourceArchiveSegment? sourceArchive)
        {
            _debugSymbols = debugSymbols;
            _sourceMap = sourceMap;
            _sourceArchive = sourceArchive;
            return this;
        }

        public ushort AddText(string text)
        {
            _ = text ?? throw new ArgumentNullException(nameof(text));
            if (_textIndexes.TryGetValue(text, out var index)) return index;
            index = checked((ushort)_textConstants.Count);
            _textConstants.Add(text);
            _textIndexes.Add(text, index);
            return index;
        }

        public ushort AddUInt16Slice(ushort[] value)
        {
            for (var sliceIndex = 0; sliceIndex < _uint16Slices.Count; sliceIndex++)
                if (_uint16Slices[sliceIndex].AsSpan().SequenceEqual(value))
                    return checked((ushort)sliceIndex);
            var index = checked((ushort)_uint16Slices.Count);
            _uint16Slices.Add(value);
            return index;
        }

        public BinaryMaterializer AddBind(GesBindingSegment.Entry bind)
        {
            _binds.Add(bind);
            return this;
        }

        public BinaryMaterializer AddInstruction(GesBytecodeInstruction instruction)
        {
            _instructions.Add(instruction);
            return this;
        }

        public GameEventScriptProgram Build()
        {
            var moduleName = ResolveModuleName();
            AddText(moduleName);
            return new GameEventScriptProgram(
                _version,
                moduleName,
                _programVersion,
                _requiredRegisterCount,
                _requiredCallStackDepth,
                BuildTextTable(_textConstants),
                BuildUInt16Table(_uint16Slices),
                new GesBindingSegment(_binds),
                new GesCodeSegment([.. _instructions]),
                _debugSymbols,
                _sourceMap,
                _sourceArchive,
                buildMetadataSegment: new GesBuildMetadataSegment(GesCompilerMetadata.CompilerId, GesCompilerMetadata.CompilerVersion));
        }

        private string ResolveModuleName()
            => string.IsNullOrWhiteSpace(_moduleName)
                ? FormattableString.Invariant($"anonymous.m{ComputeBinaryHash():x8}")
                : _moduleName;

        private uint ComputeBinaryHash()
        {
            var hash = new BinaryHashBuilder(2166136261u);
            hash.MixUShort(_version);
            hash.MixULong(_programVersion);
            hash.MixUShort(_requiredRegisterCount);
            hash.MixUShort(_requiredCallStackDepth);
            foreach (var text in _textConstants) hash.MixString(text);
            foreach (var slice in _uint16Slices) hash.MixList(slice);
            foreach (var bind in _binds)
            {
                hash.MixUInt((uint)bind.Kind);
                hash.MixUShort(bind.Name);
                hash.MixList(bind.ArgumentNames);
                hash.MixUShort(bind.EntryAddress);
                hash.MixUShort(bind.Id);
                hash.MixUShort(bind.RequiredRegisterCount);
                hash.MixUShort(bind.RequiredCallStackDepth);
                hash.MixList(bind.RequiredTags);
                hash.MixList(bind.ExcludedTags);
            }

            foreach (var instruction in _instructions)
            {
                hash.MixByte((byte)instruction.OpCode);
                hash.MixByte(instruction.UnitAndFlags);
                hash.MixUShort(instruction.DestinationRegister);
                hash.MixUShort((ushort)instruction.ImmediateX);
                hash.MixUShort((ushort)instruction.ImmediateY);
                hash.MixULong(instruction.Payload);
            }

            return hash.Value;
        }

        // FNV-1a over little-endian fields; lists and strings mix their length first.
        private struct BinaryHashBuilder(uint hash)
        {
            internal readonly uint Value => hash;

            internal void MixString(string value)
            {
                MixInt(value.Length);
                foreach (var character in value) MixUShort(character);
            }

            internal void MixList(IReadOnlyList<ushort> values)
            {
                MixInt(values.Count);
                // Indexed, so mixing an interface-typed list allocates no enumerator.
                for (var index = 0; index < values.Count; index++) MixUShort(values[index]);
            }

            internal void MixInt(int value) => MixUInt(unchecked((uint)value));

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

            internal void MixByte(byte value) => hash = (hash ^ value) * 16777619u;
        }

        private static GesStringConstantSegment BuildTextTable(IReadOnlyList<string> values)
        {
            var totalLength = 0;
            var slices = new GesStringConstantSegment.SliceEntry[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                var byteCount = Encoding.UTF8.GetByteCount(values[index]);
                slices[index] = new GesStringConstantSegment.SliceEntry { Start = totalLength, Length = byteCount };
                totalLength += byteCount;
            }

            var data = new byte[totalLength];
            var offset = 0;
            foreach (var value in values) offset += Encoding.UTF8.GetBytes(value, 0, value.Length, data, offset);
            return new GesStringConstantSegment(slices, data);
        }

        private static GesUInt16IndexListSegment BuildUInt16Table(IReadOnlyList<ushort[]> values)
        {
            var totalLength = 0;
            var slices = new GesUInt16IndexListSegment.SliceEntry[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                slices[index] = new GesUInt16IndexListSegment.SliceEntry { Start = totalLength, Length = values[index].Length };
                totalLength += values[index].Length;
            }

            var data = new ushort[totalLength];
            var offset = 0;
            foreach (var value in values)
            {
                Array.Copy(value, 0, data, offset, value.Length);
                offset += value.Length;
            }

            return new GesUInt16IndexListSegment(slices, data);
        }

    }

    private static int BindKindOrder(GesBinaryBindKind kind)
        => kind switch
        {
            GesBinaryBindKind.MessageHandler or
                GesBinaryBindKind.MessageNameHandler => 0,
            GesBinaryBindKind.Record => 1,
            GesBinaryBindKind.Predicate => 2,
            GesBinaryBindKind.Function => 3,
            GesBinaryBindKind.OutboundMessage => 4,
            GesBinaryBindKind.ExtensionCall => 5,
            GesBinaryBindKind.ExternalType => 6,
            _ => 7
        };

    private static int RoutineKindOrder(GesBinaryBindKind? kind) => kind.HasValue ? BindKindOrder(kind.Value) : 7;

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
        var startCompare = left.Start.CompareTo(right.Start);
        return startCompare != 0 ? startCompare : left.RegisterId.CompareTo(right.RegisterId);
    }

    private static string[] CopyStringList(IReadOnlyList<string>? source)
    {
        if (source is null || source.Count == 0) return [];
        var result = new string[source.Count];
        for (var index = 0; index < source.Count; index++) result[index] = source[index] ?? throw new ArgumentException("String lists cannot contain null entries.", nameof(source));
        return result;
    }

    private static PlanItem[] CopyPlanItems(IReadOnlyList<PlanItem> source) => source.Count == 0 ? [] : [.. source];

    private static int CopyPlanItems(IReadOnlyList<PlanItem> source, PlanItem[] destination, int offset)
    {
        for (var index = 0; index < source.Count; index++) destination[offset++] = source[index];
        return offset;
    }

    private static ushort[] ResolveTextList(IReadOnlyList<string> source, Func<string, ushort> resolveText)
    {
        if (source.Count == 0) return [];
        var result = new ushort[source.Count];
        for (var index = 0; index < source.Count; index++) result[index] = resolveText(source[index]);
        return result;
    }

    private static ushort NextId(GesBinaryBindKind kind, Dictionary<GesBinaryBindKind, ushort> nextIds)
    {
        nextIds.TryGetValue(kind, out var id);
        nextIds[kind] = checked((ushort)(id + 1));
        return id;
    }

    private GesBytecodeInstruction EncodeInstruction(InstructionPlan plan, OperandResolver resolver)
    {
        // Every named operand field aliases one of the three words or a payload part; slots accept only their operand kinds.
        var instruction = new GesBytecodeInstruction { OpCode = plan.OpCode, UnitAndFlags = GesBytecodeInstruction.EncodeUnitAndFlags(plan.Unit, plan.Flags) };
        if (plan.Destination.Kind != GesOperandKind.None) instruction.DestinationRegister = resolver.Resolve(plan.Destination, DestinationOperands, "Destination");
        if (plan.X.Kind != GesOperandKind.None) instruction.XRegister = resolver.Resolve(plan.X, XOperands, "X");
        if (plan.Y.Kind != GesOperandKind.None) instruction.YRegister = resolver.Resolve(plan.Y, YOperands, "Y");
        if (plan.SecondaryList.Kind != GesOperandKind.None) instruction.SecondaryListIndex = resolver.Resolve(plan.SecondaryList, ListOperands, "Secondary list");
        if (plan.A.Kind != GesOperandKind.None) instruction.AU = resolver.Resolve(plan.A, AuxOperands, "Aux");
        if (plan.B.Kind != GesOperandKind.None) instruction.BU = resolver.Resolve(plan.B, AuxOperands, "Aux");
        if (plan.C.Kind != GesOperandKind.None) instruction.CU = resolver.Resolve(plan.C, AuxOperands, "Aux");
        if (plan.D.Kind != GesOperandKind.None) instruction.DU = resolver.Resolve(plan.D, AuxOperands, "Aux");
        if (plan.Count.HasValue) instruction.Count = plan.Count.Value;
        if (plan.I64.HasValue) instruction.I64 = plan.I64.Value;
        if (plan.F64.HasValue) instruction.F64 = plan.F64.Value;
        return instruction;
    }

    private const int DestinationOperands = 1 << (int)GesOperandKind.Register | 1 << (int)GesOperandKind.RegisterList | 1 << (int)GesOperandKind.Bind;
    private const int ListOperands = 1 << (int)GesOperandKind.RegisterList | 1 << (int)GesOperandKind.TextList;
    private const int AuxOperands = ListOperands | 1 << (int)GesOperandKind.Register | 1 << (int)GesOperandKind.Label | 1 << (int)GesOperandKind.Type | 1 << (int)GesOperandKind.UShort | 1 << (int)GesOperandKind.Short;
    private const int YOperands = AuxOperands | 1 << (int)GesOperandKind.Text;
    private const int XOperands = YOperands | 1 << (int)GesOperandKind.Bind;

    // Turns symbolic operands into their encoded word: register numbers, label addresses, pool indexes and bind IDs.
    private sealed class OperandResolver(IReadOnlyDictionary<int, ushort> registerMap, IReadOnlyDictionary<int, ushort> labelAddresses, IReadOnlyList<ushort> bindIds, BinaryMaterializer materializer)
    {
        public ushort Resolve(GesOperand operand, int allowedKinds, string slot)
        {
            if ((allowedKinds & 1 << (int)operand.Kind) == 0) throw new InvalidOperationException($"Unsupported {slot} operand '{operand.Kind}'.");
            return operand.Kind switch
            {
                GesOperandKind.Register => ResolveRegister(operand.RegisterRef, registerMap),
                GesOperandKind.Label => labelAddresses[operand.LabelRef.Id],
                GesOperandKind.Text => materializer.AddText(operand.TextValue!),
                GesOperandKind.RegisterList => ResolveList(operand.RegisterListValue!),
                GesOperandKind.TextList => ResolveTextList(operand.TextListValue!),
                GesOperandKind.Bind => bindIds[operand.BindRef.Id],
                GesOperandKind.Type => (ushort)operand.TypeKind,
                GesOperandKind.UShort => operand.UShort,
                _ => unchecked((ushort)operand.Short)
            };
        }

        private ushort ResolveList(IReadOnlyList<GesRegisterRef> registers)
        {
            var values = new ushort[registers.Count];
            for (var index = 0; index < registers.Count; index++) values[index] = ResolveRegister(registers[index], registerMap);
            return materializer.AddUInt16Slice(values);
        }

        private ushort ResolveTextList(IReadOnlyList<string> texts)
        {
            var indexes = new ushort[texts.Count];
            for (var index = 0; index < texts.Count; index++) indexes[index] = materializer.AddText(texts[index]);
            return materializer.AddUInt16Slice(indexes);
        }
    }

    private static ushort ResolveRegister(GesRegisterRef register, IReadOnlyDictionary<int, ushort> registerMap)
        => registerMap.TryGetValue(register.Id, out var registerIndex)
            ? registerIndex
            : throw new InvalidOperationException($"Register '{register.Id}' was not allocated.");

    private RegisterAllocationResult AllocateRegisters(IReadOnlyList<PlanItem> items, IReadOnlyDictionary<int, ushort> labelAddresses)
    {
        var intervals = BuildRegisterIntervals(items, labelAddresses);
        if (_routines.Count > 0) return AllocateScopedRegisters(intervals);

        var result = new Dictionary<int, ushort>();
        ushort nextPinned = 0;
        foreach (var register in _registers)
            if (!register.IsTemporary)
                result[register.Id] = nextPinned++;
        AssignTemporaries(BuildSortedTemporaryIntervals(intervals), NoRoutineId, result, nextPinned, null);
        foreach (var interval in intervals)
            if (interval.HasValue && !_registers[interval.RegisterId].IsTemporary && !result.ContainsKey(interval.RegisterId))
                result[interval.RegisterId] = nextPinned++;
        return new RegisterAllocationResult(result, []);
    }

    private RegisterInterval[] BuildRegisterIntervals(IReadOnlyList<PlanItem> items, IReadOnlyDictionary<int, ushort> labelAddresses)
    {
        var intervals = new RegisterInterval[_registers.Count];
        var address = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Instruction is not { } instruction) continue;
            AddOperandRegisterIntervals(intervals, instruction.Destination, address);
            AddOperandRegisterIntervals(intervals, instruction.X, address);
            AddOperandRegisterIntervals(intervals, instruction.Y, address);
            AddOperandRegisterIntervals(intervals, instruction.A, address);
            AddOperandRegisterIntervals(intervals, instruction.B, address);
            AddOperandRegisterIntervals(intervals, instruction.C, address);
            AddOperandRegisterIntervals(intervals, instruction.D, address);
            AddOperandRegisterIntervals(intervals, instruction.SecondaryList, address);
            address++;
        }

        // Values entering a loop remain live through its back edge, even when their
        // last textual use occurs earlier in the body. Inner iterations may read them again.
        address = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Instruction is not { } instruction) continue;
            if (ReadJumpTarget(instruction) is { } target && labelAddresses[target.Id] <= address)
            {
                var start = labelAddresses[target.Id];
                for (var register = 0; register < intervals.Length; register++)
                {
                    ref var interval = ref intervals[register];
                    if (interval.HasValue && interval.Start < start && interval.End >= start && interval.End < address)
                        interval.End = address;
                }
            }
            address++;
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
        for (var index = 0; index < operand.RegisterListValue.Count; index++) AddRegisterInterval(intervals, operand.RegisterListValue[index], instructionIndex);
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
            if (items[index].Label is { } label) result[label.Id] = ToUShort(address, "label address");
            else if (items[index].Instruction is not null) address++;
        }

        foreach (var label in _labels)
            if (!result.ContainsKey(label.Id))
                throw new InvalidOperationException($"Label '{label.DisplayName}' was declared but never marked.");
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
                for (var index = 0; index < operand.RegisterListValue!.Count; index++) RequireRegister(operand.RegisterListValue[index]);
                return;
            case GesOperandKind.TextList:
                for (var index = 0; index < operand.TextListValue!.Count; index++)
                    if (operand.TextListValue[index] is null)
                        throw new ArgumentException("Text list operands cannot contain null entries.", nameof(operand));
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

    private static ushort ToUShort(int value, string operand) => value is < 0 or > ushort.MaxValue ? throw new InvalidOperationException($"{operand} '{value}' does not fit into UInt16.") : checked((ushort)value);

    private RegisterAllocationResult AllocateScopedRegisters(RegisterInterval[] intervals)
    {
        var result = new Dictionary<int, ushort>();
        var routineLocalCounts = new short[_routines.Count];
        var routineMaxRegisterIndexes = new int[_routines.Count];
        Array.Fill(routineMaxRegisterIndexes, -1);
        var temporaryIntervals = BuildSortedTemporaryIntervals(intervals);
        var temporaryIntervalOffset = 0;
        foreach (var routine in _routines)
        {
            // Arguments come first, then the routine's other pinned registers, then its temporaries.
            ushort nextPinned = 0;
            foreach (var argument in routine.ArgumentRegisters)
            {
                result[argument.Id] = nextPinned;
                UpdateRoutineMaxRegisterIndex(routineMaxRegisterIndexes, routine.Id, nextPinned++);
            }

            foreach (var register in _registers)
            {
                if (register.RoutineId != routine.Id || register.IsTemporary || result.ContainsKey(register.Id)) continue;
                result[register.Id] = nextPinned;
                UpdateRoutineMaxRegisterIndex(routineMaxRegisterIndexes, routine.Id, nextPinned++);
            }

            // Intervals are sorted by routine, so each routine's temporaries form one contiguous run.
            while (temporaryIntervalOffset < temporaryIntervals.Length && _registers[temporaryIntervals[temporaryIntervalOffset].RegisterId].RoutineId < routine.Id) temporaryIntervalOffset++;
            var runEnd = temporaryIntervalOffset;
            while (runEnd < temporaryIntervals.Length && _registers[temporaryIntervals[runEnd].RegisterId].RoutineId == routine.Id) runEnd++;
            AssignTemporaries(temporaryIntervals.AsSpan(temporaryIntervalOffset, runEnd - temporaryIntervalOffset), routine.Id, result, nextPinned, routineMaxRegisterIndexes);
            temporaryIntervalOffset = runEnd;
            var localCount = Math.Max(0, routineMaxRegisterIndexes[routine.Id] + 1 - routine.ArgumentRegisters.Count);
            if (localCount > short.MaxValue) throw new InvalidOperationException($"Routine '{routine.Name}' requires too many local registers: {localCount}.");
            routineLocalCounts[routine.Id] = (short)localCount;
        }

        var hasGlobalIntervals = false;
        foreach (var interval in temporaryIntervals) hasGlobalIntervals |= _registers[interval.RegisterId].RoutineId == NoRoutineId;
        if (hasGlobalIntervals)
        {
            ushort nextPinned = 0;
            foreach (var register in _registers)
                if (register.RoutineId == NoRoutineId && !register.IsTemporary)
                    result[register.Id] = nextPinned++;
            AssignTemporaries(temporaryIntervals, NoRoutineId, result, nextPinned, null);
        }

        foreach (var interval in intervals)
            if (interval.HasValue && !result.ContainsKey(interval.RegisterId))
                throw new InvalidOperationException($"Register '{interval.RegisterId}' was not allocated.");
        return new RegisterAllocationResult(result, routineLocalCounts);
    }

    // Linear scan over temporary intervals sorted by start: a register is free again once its interval has ended.
    private void AssignTemporaries(ReadOnlySpan<RegisterInterval> intervals, int routineId, Dictionary<int, ushort> result, ushort next, int[]? routineMaxRegisterIndexes)
    {
        var active = new List<(int End, ushort Register)>();
        var freeRegisters = new Stack<ushort>();
        foreach (var interval in intervals)
        {
            if (_registers[interval.RegisterId].RoutineId != routineId) continue;
            for (var index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].End >= interval.Start) continue;
                freeRegisters.Push(active[index].Register);
                active.RemoveAt(index);
            }

            var register = freeRegisters.Count > 0 ? freeRegisters.Pop() : next++;
            result[interval.RegisterId] = register;
            if (routineMaxRegisterIndexes is not null) UpdateRoutineMaxRegisterIndex(routineMaxRegisterIndexes, routineId, register);
            active.Add((interval.End, register));
        }
    }

    private RegisterInterval[] BuildSortedTemporaryIntervals(RegisterInterval[] intervals)
    {
        var count = 0;
        foreach (var interval in intervals)
            if (interval.HasValue && _registers[interval.RegisterId].IsTemporary)
                count++;
        if (count == 0) return [];
        var result = new RegisterInterval[count];
        var offset = 0;
        foreach (var interval in intervals)
            if (interval.HasValue && _registers[interval.RegisterId].IsTemporary)
                result[offset++] = interval;
        Array.Sort(result, CompareRegisterIntervalByRoutineThenStart);
        return result;
    }

    private void UpdateRoutineMaxRegisterIndex(int[] routineMaxRegisterIndexes, int routineId, ushort registerIndex)
    {
        if (routineId != NoRoutineId && registerIndex > routineMaxRegisterIndexes[routineId]) routineMaxRegisterIndexes[routineId] = registerIndex;
    }

    private int CompareRegisterIntervalByRoutineThenStart(RegisterInterval left, RegisterInterval right)
    {
        var routineCompare = _registers[left.RegisterId].RoutineId.CompareTo(_registers[right.RegisterId].RoutineId);
        return routineCompare != 0 ? routineCompare : CompareRegisterInterval(left, right);
    }

    private sealed record RegisterAllocationResult(IReadOnlyDictionary<int, ushort> RegisterMap, IReadOnlyList<short> RoutineLocalCounts);

    private readonly record struct RegisterSymbol(int Id, string? Name, bool IsTemporary, int RoutineId, bool IsDebugVisible);

    private readonly record struct LabelSymbol(int Id, string? Name)
    {
        public string DisplayName => Name ?? $"L_{Id}";
    }

    private sealed record BindPlan(int Index, GesBinaryBindKind Kind, string Name, IReadOnlyList<string> ArgumentNames, GesLabelRef? EntryLabel, ushort? Id, IReadOnlyList<string> RequiredTags, IReadOnlyList<string> ExcludedTags);

    private struct RegisterInterval(int registerId, int start, int end)
    {
        public int RegisterId { get; } = registerId;
        public int Start { get; } = start;
        public int End { get; set; } = end;
        public bool HasValue { get; } = true;
    }

    internal readonly record struct PlanItem(GesLabelRef? Label, InstructionPlan? Instruction, GesSourceLocation? SourceRange)
    {
        public static PlanItem ForLabel(GesLabelRef label, GesSourceLocation? sourceRange = null) => new(label, null, sourceRange);

        public static PlanItem ForInstruction(InstructionPlan instruction, GesSourceLocation? sourceRange = null) => new(null, instruction, sourceRange);
    }

    internal sealed record InstructionPlan(int RoutineId, GesBytecodeOpCode OpCode, GesQuantityUnit Unit, GesInstructionFlag Flags, GesOperand Destination, GesOperand X, GesOperand Y, GesOperand A, GesOperand B,
        GesOperand C, GesOperand D, GesOperand SecondaryList, short? Count, long? I64, double? F64)
    {
        public InstructionPlan WithDestination(GesOperand destination) => this with { Destination = destination };

        public bool ReadsRegister(GesRegisterRef register)
            => X.CountRegister(register) + Y.CountRegister(register) + A.CountRegister(register) + B.CountRegister(register) + C.CountRegister(register) + D.CountRegister(register) + SecondaryList.CountRegister(register) > 0;
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
    public readonly GesBytecodeTypeKind TypeKind;
    public readonly ushort UShort;
    public readonly short Short;

    private GesOperand(GesOperandKind kind, GesRegisterRef register = default, GesLabelRef label = default, GesBindRef bind = default, string? text = null, IReadOnlyList<GesRegisterRef>? registerList = null,
        IReadOnlyList<string>? textList = null, GesBytecodeTypeKind typeKind = GesBytecodeTypeKind.Nothing, ushort uShort = 0, short @short = 0)
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

    public static GesOperand Type(GesBytecodeTypeKind typeKind) => new(GesOperandKind.Type, typeKind: typeKind);

    public static GesOperand U16(ushort value) => new(GesOperandKind.UShort, uShort: value);

    public static GesOperand I16(short value) => new(GesOperandKind.Short, @short: value);

    internal int CountRegister(GesRegisterRef register)
    {
        if (Kind == GesOperandKind.Register) return RegisterRef.Id == register.Id ? 1 : 0;
        if (Kind != GesOperandKind.RegisterList || RegisterListValue is null) return 0;
        var count = 0;
        for (var index = 0; index < RegisterListValue.Count; index++)
            if (RegisterListValue[index].Id == register.Id)
                count++;
        return count;
    }

    private static GesRegisterRef[] CopyRegisterList(IReadOnlyList<GesRegisterRef>? source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Count == 0 ? [] : [.. source];
    }

    private static string[] CopyTextList(IReadOnlyList<string>? source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (source.Count == 0) return [];
        var result = new string[source.Count];
        for (var index = 0; index < source.Count; index++) result[index] = source[index] ?? throw new ArgumentException("Text list operands cannot contain null entries.", nameof(source));
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
