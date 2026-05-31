using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Extensions.GameEventScriptOpcodePrinter.OperandPart;

namespace StepH.GameEventScript.Extensions;

/// <summary>
/// Creates an assembler-style disassembly of the portable GameEventScript binary model.
/// </summary>
public static class GameEventScriptBinaryDumper
{
    private const string CodeIndent = "\t\t\t\t";
    private const ushort NoAddress = 0xFFFF;

    /// <summary>
    /// Dumps the binary header, tables, binds, and code as an assembler-like text format.
    /// </summary>
    public static string Dump(this GameEventScriptBinary binary)
        => Dump(binary, includeInstructionAddresses: false);

    /// <summary>
    /// Dumps the binary header, tables, binds, and code as an assembler-like text format.
    /// </summary>
    public static string Dump(this GameEventScriptBinary binary, bool includeInstructionAddresses)
    {
        var context = new DisassemblyContext(binary);
        var builder = new StringBuilder();

        AppendHeader(builder, binary);

        builder
            .Append(".gesb ").Append(binary.Header.Version.ToString(CultureInfo.InvariantCulture)).AppendLine()
            .Append(".module \"").Append(Escape(binary.ModuleName)).AppendLine("\"")
            .Append(".flags ").AppendLine(FormatHeaderFlags(binary.Header.Flags));

        AppendTextSegment(builder, binary);
        AppendListSegment(builder, context);
        AppendBindSegment(builder, context);
        AppendCodeSegment(builder, context, includeInstructionAddresses);
        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder, GameEventScriptBinary binary)
    {
        builder
            .AppendLine("// -------------------------------------------------------------------------------")
            .Append("//  Module: ").AppendLine(binary.ModuleName)
            .AppendLine("//  Type: Game Event Script Assembler")
            .Append("//  Format version: ").Append(binary.Header.Version.ToString(CultureInfo.InvariantCulture)).AppendLine(".0")
            .AppendLine("//")
            .Append("//  Disassembled at ").AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .AppendLine("// -------------------------------------------------------------------------------")
            .AppendLine();
    }

    private static void AppendTextSegment(StringBuilder builder, GameEventScriptBinary binary)
    {
        builder.AppendLine().AppendLine().AppendLine(".segment text").AppendLine();
        for (var i = 0; i < binary.TextConstantTable.Slices.Length; i++)
        {
            AppendAlignedLabel(builder, TextLabel(i))
                .Append(".text \"")
                .Append(Escape(binary.TextConstantTable.Resolve(checked((ushort)i))))
                .AppendLine("\"");
        }
    }

    private static void AppendListSegment(StringBuilder builder, DisassemblyContext context)
    {
        var table = context.Binary.Uint16ConstantTable;
        builder.AppendLine().AppendLine().AppendLine(".segment lists").AppendLine();
        for (var i = 0; i < table.Slices.Length; i++)
        {
            var label = context.ListLabel(i);
            var values = table.Resolve(checked((ushort)i)).ToArray();
            AppendAlignedLabel(builder, label);
            switch (context.GetListRole(i))
            {
                case ListRole.Registers:
                    builder.Append(".registers [").Append(string.Join(", ", values.Select(Register))).Append(']');
                    break;
                case ListRole.Texts:
                    builder.Append(".texts [").Append(string.Join(", ", values.Select(value => TextLabel(value)))).Append(']');
                    break;
                default:
                    builder.Append(".u16 [").Append(string.Join(", ", values.Select(value => value.ToString(CultureInfo.InvariantCulture)))).Append(']');
                    break;
            }

            AppendListComment(builder, context, values, context.GetListRole(i));
            builder.AppendLine();
        }
    }

    private static void AppendBindSegment(StringBuilder builder, DisassemblyContext context)
    {
        builder.AppendLine().AppendLine().AppendLine(".segment bind").AppendLine();
        var entries = context.Binary.BindTable.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            AppendAlignedLabel(builder, context.BindLabel(i))
                .Append(".bind ")
                .Append(entry.Kind)
                .Append(" id=")
                .Append(entry.Id == NoAddress ? "none" : entry.Id.ToString(CultureInfo.InvariantCulture))
                .Append(" name=")
                .Append(TextLabel(entry.Name))
                .Append(" args=[")
                .Append(string.Join(", ", entry.ArgumentNames.Select(value => TextLabel(value))))
                .Append(']');

            if (entry.EntryAddress != NoAddress)
            {
                builder.Append(" entry=").Append(context.CodeLabel(entry.EntryAddress));
            }

            builder
                .Append(" // \"")
                .Append(Escape(context.ResolveText(entry.Name)))
                .Append('(')
                .Append(Escape(string.Join(", ", entry.ArgumentNames.Select(context.ResolveText))))
                .AppendLine(")\"");
        }
    }

    private static void AppendCodeSegment(StringBuilder builder, DisassemblyContext context, bool includeInstructionAddresses)
    {
        var instructions = context.Binary.InstructionTable;
        builder.AppendLine().AppendLine().AppendLine(".segment code").AppendLine();
        for (var i = 0; i < instructions.Length; i++)
        {
            if (context.HasCodeLabel(i))
            {
                builder.Append(context.CodeLabel(i)).Append(':');
                if (context.TryGetCodeLabelComment(i, out var labelComment))
                {
                    builder.Append(" // ").Append(labelComment);
                }

                builder.AppendLine();
            }

            var instruction = instructions[i];
            if (includeInstructionAddresses)
            {
                builder.Append('\t').Append('@').Append(i.ToString("0000", CultureInfo.InvariantCulture)).Append("\t\t");
            }
            else
            {
                builder.Append(CodeIndent);
            }

            builder.Append(instruction.OpCode);

            var operands = GameEventScriptOpcodePrinter.PrintInstruction(instruction);
            for (var operandIndex = 0; operandIndex < operands.Length; operandIndex++)
            {
                var operand = FormatOperand(context, instruction, operands[operandIndex], operandIndex);
                if (operand.Length == 0)
                {
                    continue;
                }

                builder
                    .Append(operandIndex == 0 ? ' ' : ", ")
                    .Append(operand);
            }

            AppendInstructionFlags(builder, instruction);
            AppendInstructionComment(builder, context, instruction, operands);
            builder.AppendLine();
        }
    }

    private static string FormatOperand(DisassemblyContext context, GameEventScriptBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part, int operandIndex)
        => part switch
        {
            TargetRegister => Register(instruction.DestinationSlot),
            OutboundMessage => context.OutboundMessageLabel(instruction.MessageDestination),

            SourceRegister => Register(instruction.XSlot),
            LeftRegister => Register(instruction.XSlot),
            OperandRegister => Register(instruction.XSlot),
            ReturnRegister => Register(instruction.XSlot),
            MessageRegister => Register(instruction.XSlot),
            HandlerRegister => Register(instruction.XSlot),
            PropertyRegister => Register(instruction.XSlot),
            BuilderRegister => Register(instruction.XSlot),
            ConditionRegister => Register(instruction.ConditionSlot),
            CollectionRegister => Register(instruction.XSlot),
            IteratorRegister => Register(instruction.XSlot),
            SourceIteratorRegister => Register(instruction.XSlot),
            SeriesRegister => Register(instruction.XSlot),
            FromRegister => Register(instruction.XSlot),

            RightRegister => Register(instruction.YSlot),
            ObjectRegister => Register(instruction.YSlot),
            ItemRegister => Register(instruction.YSlot),
            IndexRegister => Register(instruction.YSlot),
            DefaultRegister => Register(instruction.YSlot),
            SeedRegister => Register(instruction.XSlot),
            NeedleRegister => Register(instruction.YSlot),
            ToRegister => Register(instruction.YSlot),

            ItemBindingRegister => Register(operandIndex >= 3 ? instruction.AU : instruction.YSlot),
            StepRegister => Register(instruction.AU),
            MinimumRegister => Register(instruction.YSlot),
            MaximumRegister => Register(instruction.AU),

            LocalRegisterDelta => Immediate(instruction.Count),
            DiceCount => Immediate(instruction.Count),
            DiceSideCount => Immediate(instruction.ImmediateY),
            ComponentCount => Immediate(instruction.ImmediateX),
            CountImmediate => Immediate(instruction.ImmediateY),
            IndexImmediate => Immediate(instruction.Index),
            FromImmediate => Immediate(instruction.ImmediateX),
            ToImmediate => Immediate(instruction.ImmediateY),
            StepImmediate => Immediate(instruction.AS),
            IntegerImmediate => Immediate(instruction.I64),
            FloatImmediate => Immediate(instruction.F64),
            Unit => FormatUnit(instruction.UnitAndFlags),

            JumpTarget => context.CodeLabel(instruction.TargetAddress),
            EntryTarget => context.CodeLabel(instruction.EntryAddress),
            CallableEntry => context.CodeLabel(instruction.EntryAddress),
            PredicateEntry => context.CodeLabel(instruction.EntryAddress),
            NextEntry => context.CodeLabel(instruction.EntryAddress),
            ReducerEntry => context.CodeLabel(instruction.OpCode is GameEventScriptBytecodeOpCode.StreamReduce ? instruction.AU : instruction.BU),
            KeyEntry => context.CodeLabel(instruction.AU),
            ValueEntry => context.CodeLabel(instruction.BU),
            FaceEntry => context.CodeLabel(instruction.AU),
            WeightEntry => context.CodeLabel(instruction.BU),

            GameEventScriptOpcodePrinter.OperandPart.String => FormatTextReference(instruction.StringIndex),
            Text => FormatTextReference(instruction.StringIndex),
            Tag => FormatTextReference(instruction.StringIndex),
            MemberName => FormatTextReference(instruction.StringIndex),
            TypeKind => instruction.TypeKind.ToString(),
            CustomTypeName => FormatTextReference(instruction.SecondaryStringIndex),
            TypeName => FormatTextReference(instruction.StringIndex),

            MessageShapeList => context.ListLabel(instruction.OpCode == GameEventScriptBytecodeOpCode.LoadMessage ? instruction.SecondaryListIndex : instruction.ListIndex),
            StandardExtensionShapeList => context.ListLabel(instruction.SecondaryListIndex),
            ArgumentNameList => context.ListLabel(instruction.ListIndex),
            ArgumentRegisterList => context.ListLabel(instruction.ListIndex),
            ItemRegisterList => context.ListLabel(instruction.ListIndex),
            KeyNameList => context.ListLabel(instruction.SecondaryListIndex),
            ValueRegisterList => context.ListLabel(instruction.ListIndex),
            CaptureRegisterList => context.ListLabel(instruction.BU),
            TagRegisterList => context.ListLabel(instruction.OpCode is GameEventScriptBytecodeOpCode.EmitMessageWithTags or GameEventScriptBytecodeOpCode.PublishMessageWithTags
                ? instruction.SecondaryListIndex
                : instruction.ListIndex),
            RecordReference => context.RecordLabel(instruction.ExternalReferenceIndex),
            ExternalReference => context.ExternalReferenceLabel(instruction.OpCode, instruction.ExternalReferenceIndex),

            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };

    private static void AppendInstructionFlags(StringBuilder builder, GameEventScriptBytecodeInstruction instruction)
    {
        var flags = GameEventScriptBytecodeInstruction.DecodeInstructionFlags(instruction.UnitAndFlags);
        if (flags != GameEventScriptInstructionFlag.None)
        {
            builder.Append(" flags=").Append(flags);
        }
    }

    private static void AppendInstructionComment(
        StringBuilder builder,
        DisassemblyContext context,
        GameEventScriptBytecodeInstruction instruction,
        IReadOnlyList<GameEventScriptOpcodePrinter.OperandPart> operands)
    {
        var comments = new List<string>();
        foreach (var operand in operands)
        {
            switch (operand)
            {
                case GameEventScriptOpcodePrinter.OperandPart.String:
                case Text:
                case Tag:
                case MemberName:
                case TypeName:
                    AddTextComment(comments, context, instruction.StringIndex);
                    break;
                case CustomTypeName:
                    AddTextComment(comments, context, instruction.SecondaryStringIndex);
                    break;
                case MessageShapeList:
                    AddListTextComment(comments, context, instruction.OpCode == GameEventScriptBytecodeOpCode.LoadMessage ? instruction.SecondaryListIndex : instruction.ListIndex);
                    break;
                case StandardExtensionShapeList:
                    AddListTextComment(comments, context, instruction.SecondaryListIndex);
                    break;
                case ArgumentNameList:
                    AddListTextComment(comments, context, instruction.ListIndex);
                    break;
                case KeyNameList:
                    AddListTextComment(comments, context, instruction.SecondaryListIndex);
                    break;
                case OutboundMessage:
                    AddOutboundMessageComment(comments, context, instruction.MessageDestination);
                    break;
                case RecordReference:
                    AddRecordComment(comments, context, instruction.ExternalReferenceIndex);
                    break;
                case ExternalReference:
                    AddExternalReferenceComment(comments, context, instruction.OpCode, instruction.ExternalReferenceIndex);
                    break;
                case CallableEntry:
                    AddCodeEntryComment(comments, context, instruction.EntryAddress);
                    break;
                case PredicateEntry:
                case NextEntry:
                    AddCodeEntryComment(comments, context, instruction.EntryAddress);
                    break;
                case ReducerEntry:
                    AddCodeEntryComment(comments, context, instruction.OpCode is GameEventScriptBytecodeOpCode.StreamReduce ? instruction.AU : instruction.BU);
                    break;
                case KeyEntry:
                case FaceEntry:
                    AddCodeEntryComment(comments, context, instruction.AU);
                    break;
                case ValueEntry:
                case WeightEntry:
                    AddCodeEntryComment(comments, context, instruction.BU);
                    break;
            }
        }

        if (comments.Count > 0)
        {
            builder.Append(" // ").Append(string.Join(", ", comments));
        }
    }

    private static void AddTextComment(List<string> comments, DisassemblyContext context, ushort textIndex)
    {
        if (textIndex < context.Binary.TextConstantTable.Slices.Length)
        {
            comments.Add("\"" + Escape(context.ResolveText(textIndex)) + "\"");
        }
    }

    private static void AddListTextComment(List<string> comments, DisassemblyContext context, ushort listIndex)
    {
        if (listIndex >= context.Binary.Uint16ConstantTable.Slices.Length)
        {
            return;
        }

        var values = context.Binary.Uint16ConstantTable.Resolve(listIndex);
        if (values.Length == 0)
        {
            return;
        }

        var text = new List<string>();
        foreach (var value in values)
        {
            if (value < context.Binary.TextConstantTable.Slices.Length)
            {
                text.Add("\"" + Escape(context.ResolveText(value)) + "\"");
            }
        }

        if (text.Count > 0)
        {
            comments.Add(string.Join(", ", text));
        }
    }

    private static void AddOutboundMessageComment(List<string> comments, DisassemblyContext context, ushort id)
    {
        if (context.TryGetBindEntry(GameEventScriptBinaryBindKind.OutboundMessage, id, out var entry))
        {
            AddBindSignatureComment(comments, context, entry);
        }
    }

    private static void AddRecordComment(List<string> comments, DisassemblyContext context, ushort id)
    {
        if (context.TryGetBindEntry(GameEventScriptBinaryBindKind.Record, id, out var entry))
        {
            AddBindSignatureComment(comments, context, entry);
        }
    }

    private static void AddExternalReferenceComment(List<string> comments, DisassemblyContext context, GameEventScriptBytecodeOpCode opCode, ushort id)
    {
        var kind = opCode == GameEventScriptBytecodeOpCode.CreateExternalType
            ? GameEventScriptBinaryBindKind.ExternalType
            : GameEventScriptBinaryBindKind.ExtensionCall;
        if (context.TryGetBindEntry(kind, id, out var entry))
        {
            AddBindSignatureComment(comments, context, entry);
        }
    }

    private static void AddBindSignatureComment(List<string> comments, DisassemblyContext context, GameEventScriptBinaryBindTable.GameEventScriptBinaryBindEntry entry)
    {
        comments.Add("\"" + Escape(context.ResolveText(entry.Name)) + "(" + string.Join(", ", entry.ArgumentNames.Select(context.ResolveText)) + ")\"");
    }

    private static void AddCodeEntryComment(List<string> comments, DisassemblyContext context, ushort address)
    {
        if (context.TryGetCodeLabelComment(address, out var comment))
        {
            comments.Add(comment);
        }
    }

    private static void AppendListComment(StringBuilder builder, DisassemblyContext context, IReadOnlyList<ushort> values, ListRole role)
    {
        if (values.Count == 0 || role != ListRole.Texts)
        {
            return;
        }

        var anyText = false;
        var comment = new StringBuilder();
        foreach (var value in values)
        {
            if (value >= context.Binary.TextConstantTable.Slices.Length)
            {
                continue;
            }

            if (comment.Length > 0)
            {
                comment.Append(", ");
            }

            comment.Append('"').Append(Escape(context.ResolveText(value))).Append('"');
            anyText = true;
        }

        if (anyText)
        {
            builder.Append(" // ").Append(comment);
        }
    }

    private static string FormatHeaderFlags(GameEventScriptBinaryHeader.GameEventScriptBinaryFlags flags)
        => flags == GameEventScriptBinaryHeader.GameEventScriptBinaryFlags.None ? "none" : flags.ToString().ToLowerInvariant();

    private static string FormatTextReference(ushort index)
        => TextLabel(index);

    private static string FormatUnit(byte unitAndFlags)
    {
        var unit = GameEventScriptBytecodeInstruction.DecodeUnit(unitAndFlags);
        return unit == GameEventScriptBytecodeInstructionUnit.UnitNone ? string.Empty : "unit:" + unit.ToTypeName();
    }

    private static string Immediate(long value)
        => "#" + value.ToString(CultureInfo.InvariantCulture);

    private static string Immediate(double value)
        => "#" + value.ToString("R", CultureInfo.InvariantCulture);

    private static string Register(ushort value)
        => "r" + value.ToString(CultureInfo.InvariantCulture);

    private static StringBuilder AppendAlignedLabel(StringBuilder builder, string label)
    {
        builder.Append(label).Append(':');
        AppendTabsToColumn(builder, label.Length + 1, 16);
        return builder;
    }

    private static void AppendTabsToColumn(StringBuilder builder, int currentColumn, int targetColumn)
    {
        var tabCount = currentColumn >= targetColumn
            ? 1
            : ((targetColumn - currentColumn + 3) / 4);
        for (var i = 0; i < tabCount; i++)
        {
            builder.Append('\t');
        }
    }

    private static string TextLabel(int index)
        => "T_" + index.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private enum ListRole
    {
        Raw,
        Registers,
        Texts
    }

    private sealed class DisassemblyContext
    {
        private readonly string[] _bindLabels;
        private readonly SortedSet<int> _codeLabels = [];
        private readonly Dictionary<int, string> _codeLabelNames = [];
        private readonly Dictionary<int, string> _codeLabelComments = [];
        private readonly string[] _listLabels;
        private readonly ListRole[] _listRoles;
        private readonly Dictionary<(GameEventScriptBinaryBindKind Kind, ushort Id), int> _bindsByKindAndId = [];

        internal DisassemblyContext(GameEventScriptBinary binary)
        {
            Binary = binary;
            _bindLabels = BuildBindLabels(binary, _bindsByKindAndId);
            _listLabels = new string[binary.Uint16ConstantTable.Slices.Length];
            _listRoles = new ListRole[binary.Uint16ConstantTable.Slices.Length];
            BuildListLabels(binary, _listLabels, _listRoles);
            BuildCodeLabels(binary, _codeLabels, _codeLabelNames, _codeLabelComments);
        }

        internal GameEventScriptBinary Binary { get; }

        internal bool HasCodeLabel(int address)
            => _codeLabels.Contains(address);

        internal string CodeLabel(ushort address)
            => address == NoAddress ? "L_none" : CodeLabel((int)address);

        internal string CodeLabel(int address)
            => _codeLabelNames.TryGetValue(address, out var label)
                ? label
                : "L_" + address.ToString(CultureInfo.InvariantCulture);

        internal bool TryGetCodeLabelComment(ushort address, out string comment)
        {
            if (address == NoAddress)
            {
                comment = string.Empty;
                return false;
            }

            return TryGetCodeLabelComment((int)address, out comment);
        }

        internal bool TryGetCodeLabelComment(int address, out string comment)
            => _codeLabelComments.TryGetValue(address, out comment);

        internal string ListLabel(int index)
            => (uint)index < (uint)_listLabels.Length ? _listLabels[index] : "U16_" + index.ToString(CultureInfo.InvariantCulture);

        internal ListRole GetListRole(int index)
            => (uint)index < (uint)_listRoles.Length ? _listRoles[index] : GameEventScriptBinaryDumper.ListRole.Raw;

        internal string BindLabel(int index)
            => (uint)index < (uint)_bindLabels.Length ? _bindLabels[index] : "Bind_" + index.ToString(CultureInfo.InvariantCulture);

        internal string OutboundMessageLabel(ushort id)
            => BindLabel(GameEventScriptBinaryBindKind.OutboundMessage, id, "Outbound_" + id.ToString(CultureInfo.InvariantCulture));

        internal string RecordLabel(ushort id)
            => BindLabel(GameEventScriptBinaryBindKind.Record, id, "Record_" + id.ToString(CultureInfo.InvariantCulture));

        internal string ExternalReferenceLabel(GameEventScriptBytecodeOpCode opCode, ushort id)
            => opCode == GameEventScriptBytecodeOpCode.CreateExternalType
                ? BindLabel(GameEventScriptBinaryBindKind.ExternalType, id, "ExternalType_" + id.ToString(CultureInfo.InvariantCulture))
                : BindLabel(GameEventScriptBinaryBindKind.ExtensionCall, id, "External_" + id.ToString(CultureInfo.InvariantCulture));

        internal string ResolveText(ushort index)
            => index < Binary.TextConstantTable.Slices.Length ? Binary.TextConstantTable.Resolve(index) : "#" + index.ToString(CultureInfo.InvariantCulture);

        internal bool TryGetBindEntry(GameEventScriptBinaryBindKind kind, ushort id, out GameEventScriptBinaryBindTable.GameEventScriptBinaryBindEntry entry)
        {
            if (_bindsByKindAndId.TryGetValue((kind, id), out var index))
            {
                entry = Binary.BindTable.Entries[index];
                return true;
            }

            entry = default;
            return false;
        }

        private string BindLabel(GameEventScriptBinaryBindKind kind, ushort id, string fallback)
            => _bindsByKindAndId.TryGetValue((kind, id), out var index) ? BindLabel(index) : fallback;

        private static string[] BuildBindLabels(GameEventScriptBinary binary, Dictionary<(GameEventScriptBinaryBindKind Kind, ushort Id), int> bindsByKindAndId)
        {
            var entries = binary.BindTable.Entries;
            var labels = new string[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                labels[i] = BindPrefix(entry.Kind) + "_" + (entry.Id == NoAddress ? i : entry.Id).ToString(CultureInfo.InvariantCulture);
                if (entry.Id != NoAddress)
                {
                    bindsByKindAndId[(entry.Kind, entry.Id)] = i;
                }
            }

            return labels;
        }

        private static void BuildListLabels(GameEventScriptBinary binary, string[] labels, ListRole[] roles)
        {
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i] = "U16_" + i.ToString(CultureInfo.InvariantCulture);
            }

            foreach (var instruction in binary.InstructionTable)
            {
                var operands = GameEventScriptOpcodePrinter.PrintInstruction(instruction);
                for (var operandIndex = 0; operandIndex < operands.Length; operandIndex++)
                {
                    if (!TryGetListIndex(instruction, operands[operandIndex], out var listIndex, out var prefix, out var role))
                    {
                        continue;
                    }

                    if ((uint)listIndex >= (uint)labels.Length)
                    {
                        continue;
                    }

                    labels[listIndex] = prefix + "_" + listIndex.ToString(CultureInfo.InvariantCulture);
                    roles[listIndex] = PreferListRole(roles[listIndex], role);
                }
            }
        }

        private static void BuildCodeLabels(
            GameEventScriptBinary binary,
            SortedSet<int> codeLabels,
            Dictionary<int, string> codeLabelNames,
            Dictionary<int, string> codeLabelComments)
        {
            if (binary.InstructionTable.Length > 0)
            {
                codeLabels.Add(0);
            }

            foreach (var entry in binary.BindTable.Entries)
            {
                AddCodeLabel(codeLabels, binary, entry.EntryAddress);
                AddNamedCodeLabel(codeLabelNames, codeLabelComments, binary, entry);
            }

            foreach (var instruction in binary.InstructionTable)
            {
                foreach (var part in GameEventScriptOpcodePrinter.PrintInstruction(instruction))
                {
                    AddCodeLabelsForOperand(codeLabels, binary, instruction, part);
                }
            }
        }

        private static void AddCodeLabelsForOperand(SortedSet<int> labels, GameEventScriptBinary binary, GameEventScriptBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part)
        {
            switch (part)
            {
                case JumpTarget:
                case EntryTarget:
                case CallableEntry:
                case PredicateEntry:
                case NextEntry:
                    AddCodeLabel(labels, binary, instruction.TargetAddress);
                    break;
                case ReducerEntry:
                    AddCodeLabel(labels, binary, instruction.OpCode is GameEventScriptBytecodeOpCode.StreamReduce ? instruction.AU : instruction.BU);
                    break;
                case KeyEntry:
                case FaceEntry:
                    AddCodeLabel(labels, binary, instruction.AU);
                    break;
                case ValueEntry:
                case WeightEntry:
                    AddCodeLabel(labels, binary, instruction.BU);
                    break;
            }
        }

        private static void AddCodeLabel(SortedSet<int> labels, GameEventScriptBinary binary, ushort address)
        {
            if (address != NoAddress && address < binary.InstructionTable.Length)
            {
                labels.Add(address);
            }
        }

        private static void AddNamedCodeLabel(
            Dictionary<int, string> labels,
            Dictionary<int, string> comments,
            GameEventScriptBinary binary,
            GameEventScriptBinaryBindTable.GameEventScriptBinaryBindEntry entry)
        {
            if (entry.EntryAddress == NoAddress || entry.EntryAddress >= binary.InstructionTable.Length)
            {
                return;
            }

            if (entry.Kind is not (GameEventScriptBinaryBindKind.Function or GameEventScriptBinaryBindKind.Predicate or GameEventScriptBinaryBindKind.Record))
            {
                return;
            }

            var name = entry.Name < binary.TextConstantTable.Slices.Length
                ? binary.TextConstantTable.Resolve(entry.Name)
                : string.Empty;
            var label = ToCodeLabelName(name);
            if (label.Length == 0)
            {
                return;
            }

            var candidate = label;
            var suffix = 2;
            while (labels.ContainsValue(candidate))
            {
                candidate = label + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            labels[entry.EntryAddress] = candidate;
            comments[entry.EntryAddress] = FormatCodeLabelComment(binary, entry);
        }

        private static string FormatCodeLabelComment(GameEventScriptBinary binary, GameEventScriptBinaryBindTable.GameEventScriptBinaryBindEntry entry)
        {
            var kind = entry.Kind switch
            {
                GameEventScriptBinaryBindKind.Predicate => "predicate",
                GameEventScriptBinaryBindKind.Record => "record",
                _ => "function"
            };
            var name = entry.Name < binary.TextConstantTable.Slices.Length
                ? binary.TextConstantTable.Resolve(entry.Name)
                : "#" + entry.Name.ToString(CultureInfo.InvariantCulture);
            var args = entry.ArgumentNames
                .Select(index => index < binary.TextConstantTable.Slices.Length
                    ? binary.TextConstantTable.Resolve(index)
                    : "#" + index.ToString(CultureInfo.InvariantCulture));
            return kind + " " + name + "(" + string.Join(", ", args) + ")";
        }

        private static string ToCodeLabelName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if (i == 0)
                {
                    if (char.IsLetter(ch) || ch == '_')
                    {
                        builder.Append(ch);
                    }
                    else
                    {
                        builder.Append('_');
                        if (char.IsDigit(ch))
                        {
                            builder.Append(ch);
                        }
                    }

                    continue;
                }

                builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            }

            return builder.ToString();
        }

        private static string BindPrefix(GameEventScriptBinaryBindKind kind)
            => kind switch
            {
                GameEventScriptBinaryBindKind.MessageHandler => "Handler",
                GameEventScriptBinaryBindKind.Function => "Function",
                GameEventScriptBinaryBindKind.Predicate => "Predicate",
                GameEventScriptBinaryBindKind.ExtensionCall => "Extension",
                GameEventScriptBinaryBindKind.OutboundMessage => "Outbound",
                GameEventScriptBinaryBindKind.Record => "Record",
                GameEventScriptBinaryBindKind.ExternalType => "ExternalType",
                _ => "Bind"
            };

        private static ListRole PreferListRole(ListRole current, ListRole candidate)
            => current == ListRole.Raw ? candidate : current;

        private static bool TryGetListIndex(
            GameEventScriptBytecodeInstruction instruction,
            GameEventScriptOpcodePrinter.OperandPart part,
            out ushort index,
            out string prefix,
            out ListRole role)
        {
            switch (part)
            {
                case MessageShapeList:
                    index = instruction.OpCode == GameEventScriptBytecodeOpCode.LoadMessage ? instruction.SecondaryListIndex : instruction.ListIndex;
                    prefix = "Shape";
                    role = ListRole.Texts;
                    return true;
                case StandardExtensionShapeList:
                    index = instruction.SecondaryListIndex;
                    prefix = "StdExt";
                    role = ListRole.Texts;
                    return true;
                case ArgumentNameList:
                    index = instruction.ListIndex;
                    prefix = "Names";
                    role = ListRole.Texts;
                    return true;
                case KeyNameList:
                    index = instruction.SecondaryListIndex;
                    prefix = "Keys";
                    role = ListRole.Texts;
                    return true;
                case ArgumentRegisterList:
                    index = instruction.ListIndex;
                    prefix = "Args";
                    role = ListRole.Registers;
                    return true;
                case ItemRegisterList:
                    index = instruction.ListIndex;
                    prefix = "Items";
                    role = ListRole.Registers;
                    return true;
                case ValueRegisterList:
                    index = instruction.ListIndex;
                    prefix = "Values";
                    role = ListRole.Registers;
                    return true;
                case CaptureRegisterList:
                    index = instruction.BU;
                    prefix = "Captures";
                    role = ListRole.Registers;
                    return true;
                case TagRegisterList:
                    index = instruction.OpCode is GameEventScriptBytecodeOpCode.EmitMessageWithTags or GameEventScriptBytecodeOpCode.PublishMessageWithTags
                        ? instruction.SecondaryListIndex
                        : instruction.ListIndex;
                    prefix = "Tags";
                    role = ListRole.Registers;
                    return true;
                default:
                    index = 0;
                    prefix = string.Empty;
                    role = ListRole.Raw;
                    return false;
            }
        }
    }
}
