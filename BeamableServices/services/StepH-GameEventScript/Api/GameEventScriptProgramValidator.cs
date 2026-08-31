#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using StepH.GameEventScript.Runtime.VM;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;

namespace StepH.GameEventScript.Api;

public static class GameEventScriptProgramValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static void Validate(GameEventScriptProgram program)
    {
        _ = program ?? throw new ArgumentNullException(nameof(program));
        if (program.FormatVersion != GameEventScriptBinaryFormat.Version)
            Throw(GameEventScriptProgramFormatErrorCode.UnsupportedFormatVersion, $"Unsupported program format version '{program.FormatVersion}'.");

        ValidateStrings(program.StringConstants);
        ValidateLists(program.UInt16IndexLists);
        var indexedBindingIds = ValidateBindings(program);
        ValidateCode(program, indexedBindingIds);
        ValidateDebug(program);
        ValidateOpaque(program);

        try
        {
            GesProgramCallGraphValidator.Validate(program);
        }
        catch (GameEventScriptDynamicLinkException exception)
        {
            var code = exception.Message.Contains("Recursive calls", StringComparison.Ordinal)
                ? GameEventScriptProgramFormatErrorCode.CyclicCallGraph
                : GameEventScriptProgramFormatErrorCode.InvalidCallAddress;
            throw new GameEventScriptProgramFormatException(code, exception.Message, innerException: exception);
        }
    }

    private static void ValidateStrings(GameEventScriptStringConstantSegment segment)
    {
        if (segment.Slices.Count > ushort.MaxValue) Throw(GameEventScriptProgramFormatErrorCode.TooManyEntries, "StringConstantSegment exceeds 65535 entries.");
        var data = segment.Data.UnsafeItems;
        for (var index = 0; index < segment.Slices.Count; index++)
        {
            var slice = segment.Slices[index];
            if ((long)slice.Start + slice.Length > data.Length)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "String slice is outside the segment payload.", (ushort)GameEventScriptSectionType.StringConstants, index);
            try { _ = StrictUtf8.GetString(data, slice.Start, slice.Length); }
            catch (DecoderFallbackException exception)
            {
                throw new GameEventScriptProgramFormatException(GameEventScriptProgramFormatErrorCode.InvalidUtf8, "StringConstantSegment contains invalid UTF-8.", sectionType: (ushort)GameEventScriptSectionType.StringConstants, entryIndex: index, innerException: exception);
            }
        }
    }

    private static void ValidateLists(GameEventScriptUInt16IndexListSegment segment)
    {
        if (segment.Slices.Count > ushort.MaxValue) Throw(GameEventScriptProgramFormatErrorCode.TooManyEntries, "UInt16IndexListSegment exceeds 65535 lists.");
        for (var index = 0; index < segment.Slices.Count; index++)
        {
            var slice = segment.Slices[index];
            if ((long)slice.Start + slice.Length > segment.Data.Count)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "Index list is outside the segment payload.", (ushort)GameEventScriptSectionType.UInt16IndexLists, index);
        }
    }

    private static HashSet<ulong> ValidateBindings(GameEventScriptProgram program)
    {
        if (program.Bindings.Entries.Count > ushort.MaxValue) Throw(GameEventScriptProgramFormatErrorCode.TooManyEntries, "BindingSegment exceeds 65535 entries.");
        var maxRegisters = 0;
        var maxDepth = 0;
        var bindingIds = new HashSet<ulong>();
        for (var index = 0; index < program.Bindings.Entries.Count; index++)
        {
            var bind = program.Bindings.Entries[index];
            if (!Enum.IsDefined(typeof(GameEventScriptBinaryBindKind), bind.Kind))
                Throw(GameEventScriptProgramFormatErrorCode.InvalidBindingKind, $"Unknown binding kind 0x{(byte)bind.Kind:X2}.", (ushort)GameEventScriptSectionType.Bindings, index);
            if (bind.Name >= program.StringConstants.Slices.Count)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "Binding name references a missing string.", (ushort)GameEventScriptSectionType.Bindings, index);
            ValidateTextIndexes(bind.ArgumentNames, program.StringConstants.Slices.Count, index);
            ValidateTextIndexes(bind.RequiredTags, program.StringConstants.Slices.Count, index);
            ValidateTextIndexes(bind.ExcludedTags, program.StringConstants.Slices.Count, index);
            var bindingId = bind.Kind is MessageHandler or MessageNameHandler
                ? (1UL << 63) | ((ulong)(byte)bind.Kind << 32) | ((ulong)bind.Name << 16) | bind.Id
                : ((ulong)(byte)bind.Kind << 16) | bind.Id;
            if (bind.Id != ushort.MaxValue && !bindingIds.Add(bindingId))
                Throw(GameEventScriptProgramFormatErrorCode.DuplicateBindingId, "Binding ID is duplicated in its binding scope.", (ushort)GameEventScriptSectionType.Bindings, index);

            if (IsExecutable(bind.Kind))
            {
                if (bind.EntryAddress == ushort.MaxValue || bind.EntryAddress >= program.Code.Count)
                    Throw(GameEventScriptProgramFormatErrorCode.InvalidEntryAddress, "Executable binding has an invalid entry address.", (ushort)GameEventScriptSectionType.Bindings, index);
            }
            else if (bind.EntryAddress != ushort.MaxValue)
            {
                Throw(GameEventScriptProgramFormatErrorCode.InvalidEntryAddress, "Non-executable binding must use the no-entry sentinel.", (ushort)GameEventScriptSectionType.Bindings, index);
            }
            if (bind.Kind is MessageHandler or MessageNameHandler)
            {
                maxRegisters = Math.Max(maxRegisters, bind.RequiredRegisterCount);
                maxDepth = Math.Max(maxDepth, bind.RequiredCallStackDepth);
            }
        }
        if (program.RequiredRegisterCount != maxRegisters || program.RequiredCallStackDepth != maxDepth)
            Throw(GameEventScriptProgramFormatErrorCode.InvalidResourceMetadata, "Program resource requirements must equal the maxima declared by executable bindings.", (ushort)GameEventScriptSectionType.ProgramMetadata);

        var moduleFound = false;
        for (var index = 0; index < program.StringConstants.Slices.Count; index++)
            if (string.Equals(program.StringConstants.Resolve(checked((ushort)index)), program.ModuleName, StringComparison.Ordinal)) { moduleFound = true; break; }
        if (!moduleFound) Throw(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "ModuleName is not present in StringConstantSegment.", (ushort)GameEventScriptSectionType.ProgramMetadata);
        return bindingIds;
    }

    private static void ValidateCode(GameEventScriptProgram program, HashSet<ulong> indexedBindingIds)
    {
        if (program.Code.Count > ushort.MaxValue) Throw(GameEventScriptProgramFormatErrorCode.TooManyEntries, "CodeSegment exceeds 65535 instructions.");
        for (var index = 0; index < program.Code.Count; index++)
        {
            var instruction = program.Code[index];
            if (!Enum.IsDefined(typeof(GameEventScriptBytecodeOpCode), instruction.OpCode))
                Throw(GameEventScriptProgramFormatErrorCode.InvalidOpcode, $"Unknown opcode 0x{(byte)instruction.OpCode:X2}.", (ushort)GameEventScriptSectionType.Code, index);
            ValidateInstructionOperands(program, indexedBindingIds, instruction, index);
        }
    }

    private static void ValidateInstructionOperands(GameEventScriptProgram program, HashSet<ulong> indexedBindingIds, GameEventScriptBytecodeInstruction instruction, int instructionIndex)
    {
        var encodedUnit = instruction.UnitAndFlags & 0x1F;
        var encodedFlags = instruction.UnitAndFlags & 0xE0;
        if (!Enum.IsDefined(typeof(GameEventScriptBytecodeInstructionUnit), (byte)encodedUnit) || encodedUnit == (byte)GameEventScriptBytecodeInstructionUnit.UnitInvalid ||
            (encodedFlags & ~(byte)GameEventScriptInstructionFlag.NormalizeResultAsPredicate) != 0)
            InvalidOperand("Instruction contains an unknown unit or instruction flag.", instructionIndex);

        var operands = GameEventScriptOpcodePrinter.PrintInstruction(instruction);
        for (var operandIndex = 0; operandIndex < operands.Length; operandIndex++)
        {
            var operand = operands[operandIndex];
            if (IsRegisterOperand(operand))
            {
                var register = ReadRegisterOperand(instruction, operand, operandIndex);
                if (register >= program.RequiredRegisterCount) InvalidOperand("Instruction references a register outside RequiredRegisterCount.", instructionIndex);
                continue;
            }
            if (IsAddressOperand(operand))
            {
                var address = ReadAddressOperand(instruction, operand);
                if (address >= program.Code.Count) Throw(GameEventScriptProgramFormatErrorCode.InvalidJumpAddress, "Instruction references an address outside CodeSegment.", (ushort)GameEventScriptSectionType.Code, instructionIndex);
                continue;
            }
            if (IsStringOperand(operand))
            {
                var stringIndex = operand == GameEventScriptOpcodePrinter.OperandPart.CustomTypeName ? instruction.SecondaryStringIndex : instruction.StringIndex;
                if (stringIndex >= program.StringConstants.Slices.Count) Throw(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "Instruction references a missing string.", (ushort)GameEventScriptSectionType.Code, instructionIndex);
                continue;
            }
            if (IsListOperand(operand))
            {
                var listIndex = ReadListOperand(instruction, operand);
                if (listIndex >= program.UInt16IndexLists.Slices.Count) Throw(GameEventScriptProgramFormatErrorCode.InvalidListIndex, "Instruction references a missing list.", (ushort)GameEventScriptSectionType.Code, instructionIndex);
                ValidateInstructionList(program, listIndex, operand, instructionIndex);
                continue;
            }
            if (operand == GameEventScriptOpcodePrinter.OperandPart.TypeKind && !Enum.IsDefined(typeof(GameEventScriptBytecodeTypeKind), instruction.TypeKind)) InvalidOperand("Instruction contains an unknown type kind.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.PatternKind && !Enum.IsDefined(typeof(GameEventScriptBytecodePatternKind), instruction.AU)) InvalidOperand("Instruction contains an unknown pattern kind.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.SeriesKind && !Enum.IsDefined(typeof(GameEventScriptBytecodeSeriesKind), instruction.TypeOperand)) InvalidOperand("Instruction contains an unknown series kind.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.OutboundMessage && !HasBind(indexedBindingIds, OutboundMessage, instruction.MessageDestination)) InvalidOperand("Instruction references a missing outbound-message binding.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.RecordReference && !HasBind(indexedBindingIds, Record, instruction.BindId)) InvalidOperand("Instruction references a missing record binding.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.ExternalReference)
            {
                var kind = instruction.OpCode == GameEventScriptBytecodeOpCode.CallExternal ? ExtensionCall : ExternalType;
                if (!HasBind(indexedBindingIds, kind, instruction.BindId)) InvalidOperand("Instruction references a missing external binding.", instructionIndex);
            }
        }
    }

    private static bool IsRegisterOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part != GameEventScriptOpcodePrinter.OperandPart.OutboundMessage &&
           (part is >= GameEventScriptOpcodePrinter.OperandPart.TargetRegister and <= GameEventScriptOpcodePrinter.OperandPart.AuxDRegister || part == GameEventScriptOpcodePrinter.OperandPart.FaceRegister);

    private static ushort ReadRegisterOperand(GameEventScriptBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part, int operandIndex)
        => part switch
        {
            GameEventScriptOpcodePrinter.OperandPart.TargetRegister or GameEventScriptOpcodePrinter.OperandPart.OutboundMessage => instruction.DestinationRegister,
            GameEventScriptOpcodePrinter.OperandPart.RightRegister or GameEventScriptOpcodePrinter.OperandPart.ObjectRegister or GameEventScriptOpcodePrinter.OperandPart.ItemRegister or GameEventScriptOpcodePrinter.OperandPart.KeyRegister or GameEventScriptOpcodePrinter.OperandPart.IndexRegister or GameEventScriptOpcodePrinter.OperandPart.DefaultRegister or GameEventScriptOpcodePrinter.OperandPart.NeedleRegister or GameEventScriptOpcodePrinter.OperandPart.ToRegister => instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.ValueRegister or GameEventScriptOpcodePrinter.OperandPart.AuxItemBindingRegister or GameEventScriptOpcodePrinter.OperandPart.StepRegister or GameEventScriptOpcodePrinter.OperandPart.MaximumRegister or GameEventScriptOpcodePrinter.OperandPart.AuxARegister => instruction.AU,
            GameEventScriptOpcodePrinter.OperandPart.ItemBindingRegister => operandIndex >= 3 ? instruction.AU : instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.WeightRegister => instruction.OpCode == GameEventScriptBytecodeOpCode.TakeWeighted ? instruction.AU : instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.MinimumRegister => instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.AuxBRegister or GameEventScriptOpcodePrinter.OperandPart.FaceRegister => instruction.BU,
            GameEventScriptOpcodePrinter.OperandPart.AuxCRegister => instruction.CU,
            GameEventScriptOpcodePrinter.OperandPart.AuxDRegister => instruction.DU,
            _ => instruction.XRegister
        };

    private static bool IsAddressOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part is >= GameEventScriptOpcodePrinter.OperandPart.JumpTarget and <= GameEventScriptOpcodePrinter.OperandPart.ValueEntry;

    private static ushort ReadAddressOperand(GameEventScriptBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part)
        => part switch
        {
            GameEventScriptOpcodePrinter.OperandPart.ProjectionEntry or GameEventScriptOpcodePrinter.OperandPart.KeyEntry => instruction.AU,
            GameEventScriptOpcodePrinter.OperandPart.ValueEntry => instruction.BU,
            _ => instruction.EntryAddress
        };

    private static bool IsStringOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part is >= GameEventScriptOpcodePrinter.OperandPart.String and <= GameEventScriptOpcodePrinter.OperandPart.TypeName && part is not GameEventScriptOpcodePrinter.OperandPart.TypeKind and not GameEventScriptOpcodePrinter.OperandPart.PatternKind and not GameEventScriptOpcodePrinter.OperandPart.SeriesKind;

    private static bool IsListOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part is >= GameEventScriptOpcodePrinter.OperandPart.MessageShapeList and <= GameEventScriptOpcodePrinter.OperandPart.TagRegisterList;

    private static ushort ReadListOperand(GameEventScriptBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part)
        => part switch
        {
            GameEventScriptOpcodePrinter.OperandPart.MessageShapeList when instruction.OpCode == GameEventScriptBytecodeOpCode.LoadMessage => instruction.SecondaryListIndex,
            GameEventScriptOpcodePrinter.OperandPart.KeyNameList => instruction.SecondaryListIndex,
            GameEventScriptOpcodePrinter.OperandPart.CaptureRegisterList => instruction.BU,
            GameEventScriptOpcodePrinter.OperandPart.TagRegisterList when instruction.OpCode is GameEventScriptBytecodeOpCode.EmitMessageWithTags or GameEventScriptBytecodeOpCode.PublishMessageWithTags => instruction.SecondaryListIndex,
            _ => instruction.ListIndex
        };

    private static void ValidateInstructionList(GameEventScriptProgram program, ushort listIndex, GameEventScriptOpcodePrinter.OperandPart role, int instructionIndex)
    {
        var list = program.UInt16IndexLists.Resolve(listIndex);
        var textIndexes = role is GameEventScriptOpcodePrinter.OperandPart.MessageShapeList or GameEventScriptOpcodePrinter.OperandPart.ArgumentNameList or GameEventScriptOpcodePrinter.OperandPart.KeyNameList;
        for (var index = 0; index < list.Length; index++)
        {
            if (textIndexes && list[index] >= program.StringConstants.Slices.Count) Throw(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "Instruction list references a missing string.", (ushort)GameEventScriptSectionType.Code, instructionIndex);
            if (!textIndexes && list[index] >= program.RequiredRegisterCount) InvalidOperand("Instruction list references a register outside RequiredRegisterCount.", instructionIndex);
        }
    }

    private static bool HasBind(HashSet<ulong> indexedBindingIds, GameEventScriptBinaryBindKind kind, ushort id)
        => indexedBindingIds.Contains(((ulong)(byte)kind << 16) | id);

    private static void InvalidOperand(string message, int instructionIndex)
        => Throw(GameEventScriptProgramFormatErrorCode.InvalidOperand, message, (ushort)GameEventScriptSectionType.Code, instructionIndex);

    private static void ValidateDebug(GameEventScriptProgram program)
    {
        if (program.DebugSymbols is not null)
        {
            for (var index = 0; index < program.DebugSymbols.Symbols.Count; index++)
            {
                var symbol = program.DebugSymbols.Symbols[index];
                if (!Enum.IsDefined(typeof(GameEventScriptDebugSymbolKind), symbol.Kind) || string.IsNullOrEmpty(symbol.Name) || symbol.CodeLength == 0 ||
                    (ulong)symbol.CodeStart + symbol.CodeLength > (ulong)program.Code.Count || symbol.RegisterId >= program.RequiredRegisterCount)
                    Throw(GameEventScriptProgramFormatErrorCode.InvalidDebugSymbol, "Debug symbol has an invalid kind, name, register, or code range.", (ushort)GameEventScriptSectionType.DebugSymbols, index);
            }
        }

        if (program.SourceMap is not null) ValidateSourceMap(program);
        if (program.SourceArchive is not null) ValidateSourceArchive(program.SourceArchive);
        if (program.SourceMap is not null && program.SourceArchive is not null) ValidateSourceAgreement(program.SourceMap, program.SourceArchive);
        if (program.BuildMetadata is not null && (string.IsNullOrWhiteSpace(program.BuildMetadata.CompilerId) || string.IsNullOrWhiteSpace(program.BuildMetadata.CompilerVersion)))
            Throw(GameEventScriptProgramFormatErrorCode.InvalidProgram, "BuildMetadata fields must be non-empty.", (ushort)GameEventScriptSectionType.BuildMetadata);
    }

    private static void ValidateSourceMap(GameEventScriptProgram program)
    {
        var map = program.SourceMap!;
        var ids = new HashSet<uint>();
        for (var index = 0; index < map.Sources.Count; index++)
        {
            var source = map.Sources[index];
            if (source.SourceId != (uint)index || !ids.Add(source.SourceId) || string.IsNullOrWhiteSpace(source.SourceName) || source.Sha256.Count != 32 || source.LineStartByteOffsets.Count == 0 || source.LineStartByteOffsets[0] != 0)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidSourceMap, "SourceMap source metadata is invalid.", (ushort)GameEventScriptSectionType.SourceMap, index);
            uint previous = 0;
            for (var line = 0; line < source.LineStartByteOffsets.Count; line++)
            {
                var offset = source.LineStartByteOffsets[line];
                if (offset > source.SourceByteLength || line > 0 && offset <= previous)
                    Throw(GameEventScriptProgramFormatErrorCode.InvalidSourceMap, "SourceMap line offsets must be strictly increasing and inside the source.", (ushort)GameEventScriptSectionType.SourceMap, index);
                previous = offset;
            }
        }
        ulong previousCodeEnd = 0;
        for (var index = 0; index < map.Entries.Count; index++)
        {
            var entry = map.Entries[index];
            GameEventScriptSourceMapSource? source = null;
            for (var sourceIndex = 0; sourceIndex < map.Sources.Count; sourceIndex++) if (map.Sources[sourceIndex].SourceId == entry.SourceId) { source = map.Sources[sourceIndex]; break; }
            if (source is null || entry.CodeLength == 0 || entry.SourceByteLength == 0 || entry.CodeStart < previousCodeEnd ||
                (ulong)entry.CodeStart + entry.CodeLength > (ulong)program.Code.Count ||
                (ulong)entry.SourceStartByteOffset + entry.SourceByteLength > source.SourceByteLength)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidSourceMap, "SourceMap mapping is outside code or source bounds.", (ushort)GameEventScriptSectionType.SourceMap, index);
            previousCodeEnd = (ulong)entry.CodeStart + entry.CodeLength;
        }
    }

    private static void ValidateSourceArchive(GameEventScriptSourceArchiveSegment archive)
    {
        var ids = new HashSet<uint>();
        for (var index = 0; index < archive.Sources.Count; index++)
        {
            var source = archive.Sources[index];
            if (!ids.Add(source.SourceId) || string.IsNullOrWhiteSpace(source.SourceName)) Throw(GameEventScriptProgramFormatErrorCode.InvalidSourceArchive, "SourceArchive SourceIds must be unique and names non-empty.", (ushort)GameEventScriptSectionType.SourceArchive, index);
            try { _ = StrictUtf8.GetString(source.Utf8Content.UnsafeItems); }
            catch (DecoderFallbackException exception)
            {
                throw new GameEventScriptProgramFormatException(GameEventScriptProgramFormatErrorCode.InvalidUtf8, "SourceArchive contains invalid UTF-8.", sectionType: (ushort)GameEventScriptSectionType.SourceArchive, entryIndex: index, innerException: exception);
            }
        }
    }

    private static void ValidateSourceAgreement(GameEventScriptSourceMapSegment map, GameEventScriptSourceArchiveSegment archive)
    {
        if (map.Sources.Count != archive.Sources.Count)
            Throw(GameEventScriptProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap and SourceArchive must describe the same source set.", (ushort)GameEventScriptSectionType.SourceMap);
        using var sha256 = SHA256.Create();
        for (var index = 0; index < map.Sources.Count; index++)
        {
            var metadata = map.Sources[index];
            GameEventScriptSourceArchiveEntry? content = null;
            for (var archiveIndex = 0; archiveIndex < archive.Sources.Count; archiveIndex++) if (archive.Sources[archiveIndex].SourceId == metadata.SourceId) { content = archive.Sources[archiveIndex]; break; }
            if (content is null)
            {
                Throw(GameEventScriptProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap has no matching SourceArchive entry.", (ushort)GameEventScriptSectionType.SourceMap, index);
                continue;
            }
            if (!string.Equals(content.SourceName, metadata.SourceName, StringComparison.Ordinal) || content.Utf8Content.Count != metadata.SourceByteLength)
                Throw(GameEventScriptProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap and SourceArchive metadata do not agree.", (ushort)GameEventScriptSectionType.SourceMap, index);
            var hash = sha256.ComputeHash(content.Utf8Content.UnsafeItems);
            for (var byteIndex = 0; byteIndex < hash.Length; byteIndex++) if (hash[byteIndex] != metadata.Sha256[byteIndex])
                Throw(GameEventScriptProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap SHA-256 does not match SourceArchive.", (ushort)GameEventScriptSectionType.SourceMap, index);
            ValidateUtf8Offsets(content.Utf8Content.UnsafeItems, metadata, index);
        }
    }

    private static void ValidateUtf8Offsets(byte[] bytes, GameEventScriptSourceMapSource metadata, int entryIndex)
    {
        for (var index = 0; index < metadata.LineStartByteOffsets.Count; index++)
        {
            var offset = checked((int)metadata.LineStartByteOffsets[index]);
            if (offset > 0 && offset < bytes.Length && (bytes[offset] & 0xC0) == 0x80)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidSourceMap, "A line offset is not on a UTF-8 code-point boundary.", (ushort)GameEventScriptSectionType.SourceMap, entryIndex);
        }
    }

    private static void ValidateOpaque(GameEventScriptProgram program)
    {
        var ordinals = new HashSet<int>();
        var knownTypes = new HashSet<ushort>();
        for (var index = 0; index < program.OpaqueSections.Count; index++)
        {
            var section = program.OpaqueSections[index];
            if (section.SectionType is 0 or ushort.MaxValue || section.OriginalOrdinal < 0 || (section.Flags & GameEventScriptSectionFlags.Required) != 0)
                Throw(GameEventScriptProgramFormatErrorCode.UnknownRequiredSection, "Opaque sections must be valid optional sections.", section.SectionType, index);
            if (((ushort)section.Flags & ~(ushort)(GameEventScriptSectionFlags.Required | GameEventScriptSectionFlags.CompressionMask)) != 0)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionFlags, "Opaque section flags contain unassigned V1 bits.", section.SectionType, index);
            if (section.SectionType is (ushort)GameEventScriptSectionType.ProgramMetadata or (ushort)GameEventScriptSectionType.StringConstants or
                (ushort)GameEventScriptSectionType.UInt16IndexLists or (ushort)GameEventScriptSectionType.Bindings or (ushort)GameEventScriptSectionType.Code)
                Throw(GameEventScriptProgramFormatErrorCode.DuplicateSection, "Required known sections cannot be stored as opaque data.", section.SectionType, index);
            if (section.SectionType == (ushort)GameEventScriptSectionType.DebugSymbols && program.DebugSymbols is not null ||
                section.SectionType == (ushort)GameEventScriptSectionType.SourceMap && program.SourceMap is not null ||
                section.SectionType == (ushort)GameEventScriptSectionType.SourceArchive && program.SourceArchive is not null ||
                section.SectionType == (ushort)GameEventScriptSectionType.BuildMetadata && program.BuildMetadata is not null)
                Throw(GameEventScriptProgramFormatErrorCode.DuplicateSection, "A known optional section cannot be both parsed and opaque.", section.SectionType, index);
            var knownOptional = section.SectionType is (ushort)GameEventScriptSectionType.DebugSymbols or (ushort)GameEventScriptSectionType.SourceMap or
                (ushort)GameEventScriptSectionType.SourceArchive or (ushort)GameEventScriptSectionType.BuildMetadata;
            var codec = (ushort)section.Flags & (ushort)GameEventScriptSectionFlags.CompressionMask;
            if (knownOptional && section.SectionVersion == 1 && codec == 0)
                Throw(GameEventScriptProgramFormatErrorCode.InvalidProgram, "A supported known section must be represented by its parsed segment model.", section.SectionType, index);
            if (knownOptional && !knownTypes.Add(section.SectionType))
                Throw(GameEventScriptProgramFormatErrorCode.DuplicateSection, "A singleton known section occurs more than once.", section.SectionType, index);
            if (!ordinals.Add(section.OriginalOrdinal)) Throw(GameEventScriptProgramFormatErrorCode.InvalidProgram, "Opaque section ordinals must be unique.", section.SectionType, index);
        }
    }

    private static void ValidateTextIndexes(GameEventScriptReadOnlyArray<ushort> values, int stringCount, int bindingIndex)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] >= stringCount) Throw(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "Binding list references a missing string.", (ushort)GameEventScriptSectionType.Bindings, bindingIndex);
    }

    private static bool IsExecutable(GameEventScriptBinaryBindKind kind)
        => kind is MessageHandler or MessageNameHandler or Function or Predicate or Record;

    private static void Throw(GameEventScriptProgramFormatErrorCode code, string message, ushort? sectionType = null, int? entryIndex = null)
        => throw new GameEventScriptProgramFormatException(code, message, sectionType: sectionType, entryIndex: entryIndex);
}
