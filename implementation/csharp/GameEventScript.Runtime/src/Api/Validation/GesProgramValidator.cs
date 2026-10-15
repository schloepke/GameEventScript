// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Api.GesBinaryBindKind;

namespace GameEventScript.Api;

/// <summary>
/// Enforces the shared structural and semantic invariants required by the reader, writer, and host linker.
/// </summary>
internal static class GesProgramValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Validates the complete portable program object graph without linking host-specific runtime objects.
    /// </summary>
    /// <param name="program">The immutable program to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="program"/> is <see langword="null"/>.</exception>
    /// <exception cref="GesProgramFormatException">Thrown with a stable error code when an invariant is violated.</exception>
    public static void Validate(GameEventScriptProgram program)
    {
        _ = program ?? throw new ArgumentNullException(nameof(program));
        if (program.FormatVersion != GesBinaryFormat.Version)
            Throw(GesProgramFormatErrorCode.UnsupportedFormatVersion, $"Unsupported program format version '{program.FormatVersion}'.");
        if (!GesText.IsModuleName(program.ModuleName))
            Throw(GesProgramFormatErrorCode.InvalidProgram, $"Module name '{program.ModuleName}' does not use the portable module-name grammar.", (ushort)GesSectionType.ProgramMetadata);

        ValidateStrings(program.StringConstants);
        ValidateLists(program.UInt16IndexLists);
        var indexedBindingIds = ValidateBindings(program);
        ValidateCode(program, indexedBindingIds);
        GesProgramDebugValidator.Validate(program);
        GesProgramDebugValidator.ValidateOpaque(program);

        try
        {
            GesProgramCallGraphValidator.Validate(program);
        }
        catch (GesDynamicLinkException exception)
        {
            var code = exception.Diagnostic.Code == GesDiagnosticCodes.LinkCyclicCallGraph
                ? GesProgramFormatErrorCode.CyclicCallGraph
                : GesProgramFormatErrorCode.InvalidCallAddress;
            throw new GesProgramFormatException(code, exception.Message, innerException: exception);
        }
    }

    private static void ValidateStrings(GesStringConstantSegment segment)
    {
        if (segment.Slices.Count > ushort.MaxValue) Throw(GesProgramFormatErrorCode.TooManyEntries, "StringConstantSegment exceeds 65535 entries.");
        var data = segment.Data.AsSpan();
        for (var index = 0; index < segment.Slices.Count; index++)
        {
            var slice = segment.Slices[index];
            if ((long)slice.Start + slice.Length > data.Length)
                Throw(GesProgramFormatErrorCode.InvalidPayloadLength, "String slice is outside the segment payload.", (ushort)GesSectionType.StringConstants, index);
            try { _ = segment.Data.DecodeUtf8(StrictUtf8, slice.Start, slice.Length); }
            catch (DecoderFallbackException exception)
            {
                throw new GesProgramFormatException(
                    GesProgramFormatErrorCode.InvalidUtf8, "StringConstantSegment contains invalid UTF-8.", sectionType: (ushort)GesSectionType.StringConstants, entryIndex: index, innerException: exception);
            }
        }
    }

    private static void ValidateLists(GesUInt16IndexListSegment segment)
    {
        if (segment.Slices.Count > ushort.MaxValue) Throw(GesProgramFormatErrorCode.TooManyEntries, "UInt16IndexListSegment exceeds 65535 lists.");
        for (var index = 0; index < segment.Slices.Count; index++)
        {
            var slice = segment.Slices[index];
            if ((long)slice.Start + slice.Length > segment.Data.Count)
                Throw(GesProgramFormatErrorCode.InvalidPayloadLength, "Index list is outside the segment payload.", (ushort)GesSectionType.UInt16IndexLists, index);
        }
    }

    private static HashSet<ulong> ValidateBindings(GameEventScriptProgram program)
    {
        if (program.Bindings.Entries.Count > ushort.MaxValue) Throw(GesProgramFormatErrorCode.TooManyEntries, "BindingSegment exceeds 65535 entries.");
        var maxRegisters = 0;
        var maxDepth = 0;
        var bindingIds = new HashSet<ulong>();
        var functionSignatures = new HashSet<string>(StringComparer.Ordinal);
        var predicateSignatures = new HashSet<string>(StringComparer.Ordinal);
        var functionNames = new HashSet<string>(StringComparer.Ordinal);
        var predicateNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < program.Bindings.Entries.Count; index++)
        {
            var bind = program.Bindings.Entries[index];
            if (!GesProgramEnumValidation.IsDefined(bind.Kind))
                Throw(GesProgramFormatErrorCode.InvalidBindingKind, $"Unknown binding kind 0x{(byte)bind.Kind:X2}.", (ushort)GesSectionType.Bindings, index);
            if (bind.Name >= program.StringConstants.Slices.Count)
                Throw(GesProgramFormatErrorCode.InvalidStringIndex, "Binding name references a missing string.", (ushort)GesSectionType.Bindings, index);
            ValidateTextIndexes(bind.ArgumentNames, program.StringConstants.Slices.Count, index);
            ValidateTextIndexes(bind.RequiredTags, program.StringConstants.Slices.Count, index);
            ValidateTextIndexes(bind.ExcludedTags, program.StringConstants.Slices.Count, index);
            ValidateBindingText(program, bind, index);
            if (bind.Kind is Function or Predicate)
            {
                var name = program.StringConstants.Resolve(bind.Name);
                var signature = BuildCallableSignature(program, bind, name);
                var signatures = bind.Kind == Function ? functionSignatures : predicateSignatures;
                var names = bind.Kind == Function ? functionNames : predicateNames;
                if (!signatures.Add(signature))
                    Throw(GesProgramFormatErrorCode.InvalidProgram, $"Callable signature '{signature}' is duplicated.", (ushort)GesSectionType.Bindings, index);
                names.Add(name);
            }
            var bindingId = bind.Kind is MessageHandler or MessageNameHandler
                ? (1UL << 63) | ((ulong)(byte)bind.Kind << 32) | ((ulong)bind.Name << 16) | bind.Id
                : ((ulong)(byte)bind.Kind << 16) | bind.Id;
            if (bind.Id != ushort.MaxValue && !bindingIds.Add(bindingId))
                Throw(GesProgramFormatErrorCode.DuplicateBindingId, "Binding ID is duplicated in its binding scope.", (ushort)GesSectionType.Bindings, index);

            if (IsExecutable(bind.Kind))
            {
                if (bind.EntryAddress == ushort.MaxValue || bind.EntryAddress >= program.Code.Count)
                    Throw(GesProgramFormatErrorCode.InvalidEntryAddress, "Executable binding has an invalid entry address.", (ushort)GesSectionType.Bindings, index);
            }
            else if (bind.EntryAddress != ushort.MaxValue)
            {
                Throw(GesProgramFormatErrorCode.InvalidEntryAddress, "Non-executable binding must use the no-entry sentinel.", (ushort)GesSectionType.Bindings, index);
            }
            if (bind.Kind is MessageHandler or MessageNameHandler)
            {
                maxRegisters = Math.Max(maxRegisters, bind.RequiredRegisterCount);
                maxDepth = Math.Max(maxDepth, bind.RequiredCallStackDepth);
            }
        }
        foreach (var name in functionNames)
            if (predicateNames.Contains(name))
                Throw(GesProgramFormatErrorCode.InvalidProgram, $"Name '{name}' is declared as both a predicate and a function.", (ushort)GesSectionType.Bindings);
        if (program.RequiredRegisterCount != maxRegisters || program.RequiredCallStackDepth != maxDepth)
            Throw(GesProgramFormatErrorCode.InvalidResourceMetadata, "Program resource requirements must equal the maxima declared by executable bindings.", (ushort)GesSectionType.ProgramMetadata);

        var moduleFound = false;
        for (var index = 0; index < program.StringConstants.Slices.Count; index++)
            if (string.Equals(program.StringConstants.Resolve(checked((ushort)index)), program.ModuleName, StringComparison.Ordinal)) { moduleFound = true; break; }
        if (!moduleFound) Throw(GesProgramFormatErrorCode.InvalidStringIndex, "ModuleName is not present in StringConstantSegment.", (ushort)GesSectionType.ProgramMetadata);
        return bindingIds;
    }

    private static string BuildCallableSignature(GameEventScriptProgram program, GesBindingSegment.Entry bind, string name)
    {
        var builder = new StringBuilder(name).Append('(');
        for (var index = 0; index < bind.ArgumentNames.Count; index++)
        {
            if (index != 0) builder.Append(',');
            builder.Append(program.StringConstants.Resolve(bind.ArgumentNames[index]));
        }
        return builder.Append(')').ToString();
    }

    private static void ValidateCode(GameEventScriptProgram program, HashSet<ulong> indexedBindingIds)
    {
        if (program.Code.Count > ushort.MaxValue) Throw(GesProgramFormatErrorCode.TooManyEntries, "CodeSegment exceeds 65535 instructions.");
        for (var index = 0; index < program.Code.Count; index++)
        {
            var instruction = program.Code[index];
            if (!GesProgramEnumValidation.IsDefined(instruction.OpCode))
                Throw(GesProgramFormatErrorCode.InvalidOpcode, $"Unknown opcode 0x{(byte)instruction.OpCode:X2}.", (ushort)GesSectionType.Code, index);
            ValidateInstructionOperands(program, indexedBindingIds, instruction, index);
        }
    }

    private static void ValidateInstructionOperands(GameEventScriptProgram program, HashSet<ulong> indexedBindingIds, GesBytecodeInstruction instruction, int instructionIndex)
    {
        var encodedUnit = instruction.UnitAndFlags & 0x1F;
        var encodedFlags = instruction.UnitAndFlags & 0xE0;
        var resultSend = GesOpcodePrinter.IsResultSend(instruction.OpCode);
        var iteratorCreate = instruction.OpCode is GesBytecodeOpCode.IteratorCreate or GesBytecodeOpCode.IteratorCreateOrJump;
        var iteratorNext = instruction.OpCode == GesBytecodeOpCode.IteratorNext;
        var numericArithmetic = instruction.OpCode is GesBytecodeOpCode.Move
            or GesBytecodeOpCode.Add
            or GesBytecodeOpCode.Subtract
            or GesBytecodeOpCode.Multiply
            or GesBytecodeOpCode.Divide
            or GesBytecodeOpCode.Power
            or GesBytecodeOpCode.IntegerDivide
            or GesBytecodeOpCode.Modulo
            or GesBytecodeOpCode.Remainder
            or GesBytecodeOpCode.Min
            or GesBytecodeOpCode.Max
            or GesBytecodeOpCode.Negate
            or GesBytecodeOpCode.Abs
            or GesBytecodeOpCode.Clamp;
        if (numericArithmetic && (encodedUnit != 0 || (instruction.OpCode == GesBytecodeOpCode.Clamp ? (instruction.Payload & ~0xFFFFUL) != 0 : instruction.Payload != 0)))
            InvalidOperand("Arithmetic instruction contains reserved unit or payload bits.", instructionIndex);
        var allowedFlags = numericArithmetic ? 0x40 : iteratorCreate ? 0xE0 : resultSend ? 0xC0 : (byte)GesInstructionFlag.NormalizeResultAsPredicate;
        if ((iteratorCreate || iteratorNext) && (encodedUnit != 0 || instruction.Payload != 0 ||
            iteratorCreate && (encodedFlags == 0xE0 || instruction.OpCode == GesBytecodeOpCode.IteratorCreate && instruction.YRegister != 0)))
            InvalidOperand("Iterator instruction contains an invalid mode or reserved operands.", instructionIndex);
        if (resultSend && (encodedUnit != 0 || instruction.CU != 0 || instruction.DU != 0 ||
            (encodedFlags & 0x40) == 0 && instruction.AU != 0 ||
            (encodedFlags & 0x80) != 0 && instruction.YRegister != 0 ||
            instruction.OpCode is GesBytecodeOpCode.EmitInstant or GesBytecodeOpCode.PublishInstant && instruction.BU != 0))
            InvalidOperand("Send instruction contains nonzero reserved operands.", instructionIndex);
        if (!GesProgramEnumValidation.IsDefined((GesQuantityUnit)encodedUnit) || encodedUnit == (byte)GesQuantityUnit.UnitInvalid ||
            (encodedFlags & ~allowedFlags) != 0)
            InvalidOperand("Instruction contains an unknown unit or instruction flag.", instructionIndex);

        if (instruction.OpCode is GesBytecodeOpCode.ConstructData or GesBytecodeOpCode.SplitText &&
            (instruction.UnitAndFlags != 0 || instruction.BU != 0 || instruction.CU != 0 || instruction.DU != 0
                || instruction.OpCode == GesBytecodeOpCode.SplitText && (instruction.AU > 1 || instruction.AU == 1 && instruction.YRegister != 0)))
            InvalidOperand("Data instruction contains nonzero reserved operands.", instructionIndex);
        if (instruction.OpCode == GesBytecodeOpCode.Cartesian && (instruction.UnitAndFlags != 0 || instruction.Payload != 0))
            InvalidOperand("Cartesian reserves the unit/flag byte and payload.", instructionIndex);
        if (instruction.OpCode == GesBytecodeOpCode.ParseLiteral &&
            (instruction.UnitAndFlags != 0 || instruction.YRegister != 0 || instruction.Payload != 0))
            InvalidOperand("ParseLiteral reserves the unit/flag byte, Y word, and payload.", instructionIndex);

        var operands = GesOpcodePrinter.PrintInstruction(instruction);
        for (var operandIndex = 0; operandIndex < operands.Length; operandIndex++)
        {
            var operand = operands[operandIndex];
            if (IsRegisterOperand(operand))
            {
                var register = ReadRegisterOperand(instruction, operand, operandIndex);
                if (register == ushort.MaxValue) InvalidOperand("Instruction references a register outside the V1 frame limit.", instructionIndex);
                continue;
            }
            if (IsAddressOperand(operand))
            {
                var address = ReadAddressOperand(instruction, operand);
                if (address >= program.Code.Count) Throw(GesProgramFormatErrorCode.InvalidJumpAddress, "Instruction references an address outside CodeSegment.", (ushort)GesSectionType.Code, instructionIndex);
                continue;
            }
            if (IsStringOperand(operand))
            {
                var stringIndex = operand == GesOpcodePrinter.OperandPart.CustomTypeName ? instruction.SecondaryStringIndex : instruction.StringIndex;
                if (stringIndex >= program.StringConstants.Slices.Count) Throw(GesProgramFormatErrorCode.InvalidStringIndex, "Instruction references a missing string.", (ushort)GesSectionType.Code, instructionIndex);
                if (operand == GesOpcodePrinter.OperandPart.Tag && !IsPortablePlainName(program.StringConstants, stringIndex)) InvalidOperand("Tag constants must use the portable lowercase ASCII name grammar.", instructionIndex);
                if (operand == GesOpcodePrinter.OperandPart.CustomTypeName && !IsPortableTypeName(program.StringConstants, stringIndex))
                    InvalidOperand("Custom type names must use the portable lowercase ASCII type-name grammar.", instructionIndex);
                continue;
            }
            if (IsListOperand(operand))
            {
                var listIndex = ReadListOperand(instruction, operand);
                if (listIndex >= program.UInt16IndexLists.Slices.Count) Throw(GesProgramFormatErrorCode.InvalidListIndex, "Instruction references a missing list.", (ushort)GesSectionType.Code, instructionIndex);
                ValidateInstructionList(program, listIndex, operand, instructionIndex);
                continue;
            }
            if (operand == GesOpcodePrinter.OperandPart.TypeKind && !GesProgramEnumValidation.IsDefined(instruction.TypeKind)) InvalidOperand("Instruction contains an unknown type kind.", instructionIndex);
            if (operand == GesOpcodePrinter.OperandPart.PatternKind && !GesProgramEnumValidation.IsDefined((GesBytecodePatternKind)instruction.AU))
                InvalidOperand("Instruction contains an unknown pattern kind.", instructionIndex);
            if (operand == GesOpcodePrinter.OperandPart.SeriesKind && !GesProgramEnumValidation.IsDefined((GesBytecodeSeriesKind)instruction.TypeOperand))
                InvalidOperand("Instruction contains an unknown series kind.", instructionIndex);
            if (operand == GesOpcodePrinter.OperandPart.OutboundMessage && !HasBind(indexedBindingIds, OutboundMessage, resultSend ? instruction.BindId : instruction.MessageDestination))
                InvalidOperand("Instruction references a missing outbound-message binding.", instructionIndex);
            if (operand == GesOpcodePrinter.OperandPart.RecordReference && !HasBind(indexedBindingIds, Record, instruction.BindId)) InvalidOperand("Instruction references a missing record binding.", instructionIndex);
            if (operand == GesOpcodePrinter.OperandPart.ExternalReference)
            {
                var kind = instruction.OpCode == GesBytecodeOpCode.CallExternal ? ExtensionCall : ExternalType;
                if (!HasBind(indexedBindingIds, kind, instruction.BindId)) InvalidOperand("Instruction references a missing external binding.", instructionIndex);
            }
        }
        if (instruction.OpCode == GesBytecodeOpCode.ConstructData)
        {
            var type = program.StringConstants.Resolve(instruction.StringIndex);
            var names = program.UInt16IndexLists.Resolve(instruction.AU);
            var labels = new string[names.Length];
            for (var index = 0; index < labels.Length; index++) labels[index] = program.StringConstants.Resolve(names[index]);
            if (!GameEventScript.Runtime.GesDataConstruction.IsType(type) || GameEventScript.Runtime.GesDataConstruction.Positions(type, labels) is null || names.Length != program.UInt16IndexLists.Resolve(instruction.ListIndex).Length)
                InvalidOperand("Invalid data constructor type or argument shape.", instructionIndex);
        }
    }

    internal static bool IsRegisterOperand(GesOpcodePrinter.OperandPart part)
        => part != GesOpcodePrinter.OperandPart.OutboundMessage &&
           (part is >= GesOpcodePrinter.OperandPart.TargetRegister and <= GesOpcodePrinter.OperandPart.AuxDRegister || part == GesOpcodePrinter.OperandPart.FaceRegister);

    internal static void ValidateInstructionFrame(GameEventScriptProgram program, GesBytecodeInstruction instruction, int instructionIndex, int frameLength)
    {
        var operands = GesOpcodePrinter.PrintInstruction(instruction);
        for (var index = 0; index < operands.Length; index++)
        {
            var operand = operands[index];
            if (IsRegisterOperand(operand) && ReadRegisterOperand(instruction, operand, index) >= frameLength)
                InvalidOperand("Instruction references a register outside its active frame.", instructionIndex);
            if (!IsListOperand(operand) || operand is GesOpcodePrinter.OperandPart.MessageShapeList or GesOpcodePrinter.OperandPart.ArgumentNameList or GesOpcodePrinter.OperandPart.KeyNameList) continue;
            var list = program.UInt16IndexLists.Resolve(ReadListOperand(instruction, operand));
            for (var element = 0; element < list.Length; element++)
                if (list[element] >= frameLength) InvalidOperand("Instruction list references a register outside its active frame.", instructionIndex);
        }
    }

    internal static ushort ReadRegisterOperand(GesBytecodeInstruction instruction, GesOpcodePrinter.OperandPart part, int operandIndex)
        => part switch
        {
            GesOpcodePrinter.OperandPart.TargetRegister or GesOpcodePrinter.OperandPart.OutboundMessage => instruction.DestinationRegister,
            GesOpcodePrinter.OperandPart.RightRegister or GesOpcodePrinter.OperandPart.ObjectRegister or GesOpcodePrinter.OperandPart.ItemRegister or
                GesOpcodePrinter.OperandPart.KeyRegister or GesOpcodePrinter.OperandPart.IndexRegister or GesOpcodePrinter.OperandPart.DefaultRegister or
                GesOpcodePrinter.OperandPart.NeedleRegister or GesOpcodePrinter.OperandPart.ToRegister => instruction.YRegister,
            GesOpcodePrinter.OperandPart.ValueRegister or GesOpcodePrinter.OperandPart.AuxItemBindingRegister or GesOpcodePrinter.OperandPart.StepRegister or
                GesOpcodePrinter.OperandPart.MaximumRegister or GesOpcodePrinter.OperandPart.AuxARegister => instruction.AU,
            GesOpcodePrinter.OperandPart.ItemBindingRegister => operandIndex >= 3 ? instruction.AU : instruction.YRegister,
            GesOpcodePrinter.OperandPart.WeightRegister => instruction.OpCode == GesBytecodeOpCode.TakeWeighted ? instruction.AU : instruction.YRegister,
            GesOpcodePrinter.OperandPart.MinimumRegister => instruction.YRegister,
            GesOpcodePrinter.OperandPart.AuxBRegister or GesOpcodePrinter.OperandPart.FaceRegister => instruction.BU,
            GesOpcodePrinter.OperandPart.AuxCRegister => instruction.CU,
            GesOpcodePrinter.OperandPart.AuxDRegister => instruction.DU,
            _ => instruction.XRegister
        };

    private static bool IsAddressOperand(GesOpcodePrinter.OperandPart part)
        => part is >= GesOpcodePrinter.OperandPart.JumpTarget and <= GesOpcodePrinter.OperandPart.ValueEntry;

    private static ushort ReadAddressOperand(GesBytecodeInstruction instruction, GesOpcodePrinter.OperandPart part)
        => part switch
        {
            GesOpcodePrinter.OperandPart.ProjectionEntry or GesOpcodePrinter.OperandPart.KeyEntry => instruction.AU,
            GesOpcodePrinter.OperandPart.ValueEntry => instruction.BU,
            _ => instruction.EntryAddress
        };

    private static bool IsStringOperand(GesOpcodePrinter.OperandPart part)
        => part is >= GesOpcodePrinter.OperandPart.String and <= GesOpcodePrinter.OperandPart.TypeName &&
           part is not GesOpcodePrinter.OperandPart.TypeKind and not GesOpcodePrinter.OperandPart.PatternKind and not GesOpcodePrinter.OperandPart.SeriesKind;

    private static bool IsListOperand(GesOpcodePrinter.OperandPart part)
        => part is >= GesOpcodePrinter.OperandPart.MessageShapeList and <= GesOpcodePrinter.OperandPart.TagRegisterList;

    internal static ushort ReadListOperand(GesBytecodeInstruction instruction, GesOpcodePrinter.OperandPart part)
        => part switch
        {
            GesOpcodePrinter.OperandPart.ArgumentNameList when instruction.OpCode == GesBytecodeOpCode.ConstructData => instruction.AU,
            GesOpcodePrinter.OperandPart.TagRegisterList when GesOpcodePrinter.IsResultSend(instruction.OpCode) => instruction.AU,
            GesOpcodePrinter.OperandPart.MessageShapeList when instruction.OpCode == GesBytecodeOpCode.LoadMessage => instruction.SecondaryListIndex,
            GesOpcodePrinter.OperandPart.KeyNameList => instruction.SecondaryListIndex,
            GesOpcodePrinter.OperandPart.CaptureRegisterList => instruction.BU,
            GesOpcodePrinter.OperandPart.SourceRegisterList => instruction.XRegister,
            GesOpcodePrinter.OperandPart.TargetRegisterList => instruction.DestinationRegister,
            GesOpcodePrinter.OperandPart.TagRegisterList when instruction.OpCode is GesBytecodeOpCode.EmitMessageWithTags or GesBytecodeOpCode.PublishMessageWithTags => instruction.SecondaryListIndex,
            _ => instruction.ListIndex
        };

    private static void ValidateInstructionList(GameEventScriptProgram program, ushort listIndex, GesOpcodePrinter.OperandPart role, int instructionIndex)
    {
        var list = program.UInt16IndexLists.Resolve(listIndex);
        if (role == GesOpcodePrinter.OperandPart.SourceRegisterList && list.Length < 2 ||
            role == GesOpcodePrinter.OperandPart.TargetRegisterList && list.Length == 0)
            InvalidOperand("Iterator register list has an invalid arity.", instructionIndex);
        if (role == GesOpcodePrinter.OperandPart.TargetRegisterList)
        {
            for (var index = 0; index < list.Length; index++)
            {
                if (list[index] == program.Code[instructionIndex].XRegister)
                    InvalidOperand("Iterator output cannot overwrite its iterator.", instructionIndex);
                for (var previous = 0; previous < index; previous++)
                    if (list[index] == list[previous]) InvalidOperand("Iterator output registers must be distinct.", instructionIndex);
            }
        }
        var textIndexes = role is GesOpcodePrinter.OperandPart.MessageShapeList or GesOpcodePrinter.OperandPart.ArgumentNameList or GesOpcodePrinter.OperandPart.KeyNameList;
        var validateUniqueArgumentNames = role is GesOpcodePrinter.OperandPart.MessageShapeList or GesOpcodePrinter.OperandPart.ArgumentNameList;
        for (var index = 0; index < list.Length; index++)
        {
            if (textIndexes && list[index] >= program.StringConstants.Slices.Count)
                Throw(GesProgramFormatErrorCode.InvalidStringIndex, "Instruction list references a missing string.", (ushort)GesSectionType.Code, instructionIndex);
            if (!textIndexes && list[index] == ushort.MaxValue) InvalidOperand("Instruction list references a register outside the V1 frame limit.", instructionIndex);
            if (!textIndexes) continue;
            if (role == GesOpcodePrinter.OperandPart.MessageShapeList && index == 0)
            {
                if (!IsPortableMessageName(program.StringConstants, list[index])) InvalidOperand("Message shape names must start uppercase and contain only ASCII letters or digits.", instructionIndex);
            }
            else if (!IsPortableUnlabeled(program.StringConstants, list[index]) && !IsPortablePlainName(program.StringConstants, list[index]))
            {
                InvalidOperand("Argument and key names must use the portable lowercase ASCII identifier grammar.", instructionIndex);
            }

            if (!validateUniqueArgumentNames || (role == GesOpcodePrinter.OperandPart.MessageShapeList && index == 0) || IsPortableUnlabeled(program.StringConstants, list[index])) continue;
            var firstArgumentIndex = role == GesOpcodePrinter.OperandPart.MessageShapeList ? 1 : 0;
            for (var previous = firstArgumentIndex; previous < index; previous++)
                if (!IsPortableUnlabeled(program.StringConstants, list[previous]) && StringConstantEquals(program.StringConstants, list[previous], list[index]))
                    InvalidOperand("Message argument name occurs more than once.", instructionIndex);
        }
    }

    private static void ValidateBindingText(GameEventScriptProgram program, GesBindingSegment.Entry bind, int bindingIndex)
    {
        var validName = bind.Kind switch
        {
            MessageHandler or MessageNameHandler or OutboundMessage => IsPortableMessageName(program.StringConstants, bind.Name),
            Function or Predicate => IsPortablePlainName(program.StringConstants, bind.Name),
            Record or ExternalType => IsPortableTypeName(program.StringConstants, bind.Name),
            ExtensionCall => IsPortableExtensionName(program.StringConstants, bind.Name),
            _ => true
        };
        if (!validName)
            Throw(GesProgramFormatErrorCode.InvalidProgram, "Binding name does not use the portable ASCII grammar for its binding kind.", (ushort)GesSectionType.Bindings, bindingIndex);

        ValidateBindingNameList(program, bind.ArgumentNames, bindingIndex, allowUnlabeled: true, "Binding argument name");
        ValidateBindingNameList(program, bind.RequiredTags, bindingIndex, allowUnlabeled: false, "Required tag");
        ValidateBindingNameList(program, bind.ExcludedTags, bindingIndex, allowUnlabeled: false, "Excluded tag");
    }

    private static void ValidateBindingNameList(GameEventScriptProgram program, GesReadOnlyArray<ushort> indexes, int bindingIndex, bool allowUnlabeled, string role)
    {
        for (var index = 0; index < indexes.Count; index++)
        {
            var valid = allowUnlabeled
                ? IsPortableUnlabeled(program.StringConstants, indexes[index]) || IsPortableIdentifier(program.StringConstants, indexes[index])
                : IsPortablePlainName(program.StringConstants, indexes[index]);
            if (!valid)
                Throw(GesProgramFormatErrorCode.InvalidProgram, $"{role} does not use the portable ASCII grammar.", (ushort)GesSectionType.Bindings, bindingIndex);
            if (!allowUnlabeled || IsPortableUnlabeled(program.StringConstants, indexes[index])) continue;
            for (var previous = 0; previous < index; previous++)
                if (!IsPortableUnlabeled(program.StringConstants, indexes[previous]) && StringConstantEquals(program.StringConstants, indexes[previous], indexes[index]))
                    Throw(GesProgramFormatErrorCode.InvalidProgram, $"{role} occurs more than once.", (ushort)GesSectionType.Bindings, bindingIndex);
        }
    }

    private static bool StringConstantEquals(GesStringConstantSegment strings, ushort leftIndex, ushort rightIndex)
    {
        var left = strings.Slices[leftIndex];
        var right = strings.Slices[rightIndex];
        if (left.Length != right.Length) return false;
        var data = strings.Data.AsSpan();
        for (var offset = 0; offset < left.Length; offset++)
            if (data[left.Start + offset] != data[right.Start + offset]) return false;
        return true;
    }

    private static bool IsPortableMessageName(GesStringConstantSegment strings, ushort stringIndex)
    {
        var slice = strings.Slices[stringIndex];
        if (slice.Length == 0) return false;
        var data = strings.Data.AsSpan();
        if (!IsAsciiUpper(data[slice.Start]))
            return MatchesAscii(strings, stringIndex, "initialization") || MatchesAscii(strings, stringIndex, "undeliverable");
        for (var index = slice.Start + 1; index < slice.Start + slice.Length; index++)
            if (!IsAsciiLetterOrDigit(data[index])) return false;
        return true;
    }

    private static bool IsPortableIdentifier(GesStringConstantSegment strings, ushort stringIndex)
        => IsPortableName(strings, stringIndex, allowNumericSuffix: true);

    private static bool IsPortablePlainName(GesStringConstantSegment strings, ushort stringIndex)
        => IsPortableName(strings, stringIndex, allowNumericSuffix: false);

    private static bool IsPortableTypeName(GesStringConstantSegment strings, ushort stringIndex)
    {
        var slice = strings.Slices[stringIndex];
        if (slice.Length == 0) return false;
        var data = strings.Data.AsSpan();
        if (!IsAsciiUpper(data[slice.Start])) return false;
        for (var index = slice.Start + 1; index < slice.Start + slice.Length; index++)
            if (!IsAsciiLetterOrDigit(data[index])) return false;
        return true;
    }

    private static bool IsPortableName(GesStringConstantSegment strings, ushort stringIndex, bool allowNumericSuffix)
    {
        var slice = strings.Slices[stringIndex];
        if (slice.Length == 0) return false;
        var data = strings.Data.AsSpan();
        var end = slice.Start + slice.Length;
        if (!IsAsciiLower(data[slice.Start])) return false;
        var index = slice.Start + 1;
        while (index < end && IsAsciiLetterOrDigit(data[index])) index++;
        if (index == end) return true;
        if (!allowNumericSuffix || data[index++] != (byte)'_' || index == end) return false;
        if (data[index] == (byte)'0') return index + 1 == end;
        if (data[index] is < (byte)'1' or > (byte)'9') return false;
        for (index++; index < end; index++)
            if (data[index] is < (byte)'0' or > (byte)'9') return false;
        return true;
    }

    private static bool IsPortableUnlabeled(GesStringConstantSegment strings, ushort stringIndex)
    {
        var slice = strings.Slices[stringIndex];
        return slice.Length == 1 && strings.Data.AsSpan()[slice.Start] == (byte)'_';
    }

    private static bool IsPortableExtensionName(GesStringConstantSegment strings, ushort stringIndex)
    {
        var slice = strings.Slices[stringIndex];
        var data = strings.Data.AsSpan();
        var separator = -1;
        for (var index = slice.Start; index < slice.Start + slice.Length; index++)
        {
            if (data[index] != (byte)'.') continue;
            if (separator >= 0) return false;
            separator = index;
        }

        if (separator <= slice.Start || separator >= slice.Start + slice.Length - 1) return false;
        return IsPortablePlainNameRange(data, slice.Start, separator) &&
               IsPortablePlainNameRange(data, separator + 1, slice.Start + slice.Length);
    }

    private static bool IsPortablePlainNameRange(ReadOnlySpan<byte> data, int start, int end)
    {
        if (start >= end || !IsAsciiLower(data[start])) return false;
        for (var index = start + 1; index < end; index++)
            if (!IsAsciiLetterOrDigit(data[index])) return false;
        return true;
    }

    private static bool IsAsciiLower(byte value) => value is >= (byte)'a' and <= (byte)'z';
    private static bool IsAsciiUpper(byte value) => value is >= (byte)'A' and <= (byte)'Z';
    private static bool IsAsciiLetter(byte value) => IsAsciiLower(value) || value is >= (byte)'A' and <= (byte)'Z';
    private static bool IsAsciiLetterOrDigit(byte value) => IsAsciiLetter(value) || value is >= (byte)'0' and <= (byte)'9';

    private static bool MatchesAscii(GesStringConstantSegment strings, ushort stringIndex, string expected)
    {
        var slice = strings.Slices[stringIndex];
        if (slice.Length != expected.Length) return false;
        var data = strings.Data.AsSpan();
        for (var index = 0; index < expected.Length; index++)
            if (data[slice.Start + index] != (byte)expected[index]) return false;
        return true;
    }

    private static bool HasBind(HashSet<ulong> indexedBindingIds, GesBinaryBindKind kind, ushort id)
        => indexedBindingIds.Contains(((ulong)(byte)kind << 16) | id);

    private static void InvalidOperand(string message, int instructionIndex)
        => Throw(GesProgramFormatErrorCode.InvalidOperand, message, (ushort)GesSectionType.Code, instructionIndex);

    private static void ValidateTextIndexes(GesReadOnlyArray<ushort> values, int stringCount, int bindingIndex)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] >= stringCount) Throw(GesProgramFormatErrorCode.InvalidStringIndex, "Binding list references a missing string.", (ushort)GesSectionType.Bindings, bindingIndex);
    }

    private static bool IsExecutable(GesBinaryBindKind kind)
        => kind is MessageHandler or MessageNameHandler or Function or Predicate or Record;

    internal static void Throw(GesProgramFormatErrorCode code, string message, ushort? sectionType = null, int? entryIndex = null)
        => throw new GesProgramFormatException(code, message, sectionType: sectionType, entryIndex: entryIndex);
}
