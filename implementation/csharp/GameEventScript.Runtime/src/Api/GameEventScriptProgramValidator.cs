// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Runtime.VM;
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
        if (!GameEventScriptText.IsModuleName(program.ModuleName))
            Throw(GesProgramFormatErrorCode.InvalidProgram, $"Module name '{program.ModuleName}' does not use the portable module-name grammar.", (ushort)GesSectionType.ProgramMetadata);

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
        var resultSend = GameEventScriptOpcodePrinter.IsResultSend(instruction.OpCode);
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

        var operands = GameEventScriptOpcodePrinter.PrintInstruction(instruction);
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
                var stringIndex = operand == GameEventScriptOpcodePrinter.OperandPart.CustomTypeName ? instruction.SecondaryStringIndex : instruction.StringIndex;
                if (stringIndex >= program.StringConstants.Slices.Count) Throw(GesProgramFormatErrorCode.InvalidStringIndex, "Instruction references a missing string.", (ushort)GesSectionType.Code, instructionIndex);
                if (operand == GameEventScriptOpcodePrinter.OperandPart.Tag && !IsPortablePlainName(program.StringConstants, stringIndex)) InvalidOperand("Tag constants must use the portable lowercase ASCII name grammar.", instructionIndex);
                if (operand == GameEventScriptOpcodePrinter.OperandPart.CustomTypeName && !IsPortableTypeName(program.StringConstants, stringIndex))
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
            if (operand == GameEventScriptOpcodePrinter.OperandPart.TypeKind && !GesProgramEnumValidation.IsDefined(instruction.TypeKind)) InvalidOperand("Instruction contains an unknown type kind.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.PatternKind && !GesProgramEnumValidation.IsDefined((GesBytecodePatternKind)instruction.AU))
                InvalidOperand("Instruction contains an unknown pattern kind.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.SeriesKind && !GesProgramEnumValidation.IsDefined((GesBytecodeSeriesKind)instruction.TypeOperand))
                InvalidOperand("Instruction contains an unknown series kind.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.OutboundMessage && !HasBind(indexedBindingIds, OutboundMessage, resultSend ? instruction.BindId : instruction.MessageDestination))
                InvalidOperand("Instruction references a missing outbound-message binding.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.RecordReference && !HasBind(indexedBindingIds, Record, instruction.BindId)) InvalidOperand("Instruction references a missing record binding.", instructionIndex);
            if (operand == GameEventScriptOpcodePrinter.OperandPart.ExternalReference)
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

    internal static bool IsRegisterOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part != GameEventScriptOpcodePrinter.OperandPart.OutboundMessage &&
           (part is >= GameEventScriptOpcodePrinter.OperandPart.TargetRegister and <= GameEventScriptOpcodePrinter.OperandPart.AuxDRegister || part == GameEventScriptOpcodePrinter.OperandPart.FaceRegister);

    internal static void ValidateInstructionFrame(GameEventScriptProgram program, GesBytecodeInstruction instruction, int instructionIndex, int frameLength)
    {
        var operands = GameEventScriptOpcodePrinter.PrintInstruction(instruction);
        for (var index = 0; index < operands.Length; index++)
        {
            var operand = operands[index];
            if (IsRegisterOperand(operand) && ReadRegisterOperand(instruction, operand, index) >= frameLength)
                InvalidOperand("Instruction references a register outside its active frame.", instructionIndex);
            if (!IsListOperand(operand) || operand is GameEventScriptOpcodePrinter.OperandPart.MessageShapeList or GameEventScriptOpcodePrinter.OperandPart.ArgumentNameList or GameEventScriptOpcodePrinter.OperandPart.KeyNameList) continue;
            var list = program.UInt16IndexLists.Resolve(ReadListOperand(instruction, operand));
            for (var element = 0; element < list.Length; element++)
                if (list[element] >= frameLength) InvalidOperand("Instruction list references a register outside its active frame.", instructionIndex);
        }
    }

    internal static ushort ReadRegisterOperand(GesBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part, int operandIndex)
        => part switch
        {
            GameEventScriptOpcodePrinter.OperandPart.TargetRegister or GameEventScriptOpcodePrinter.OperandPart.OutboundMessage => instruction.DestinationRegister,
            GameEventScriptOpcodePrinter.OperandPart.RightRegister or GameEventScriptOpcodePrinter.OperandPart.ObjectRegister or GameEventScriptOpcodePrinter.OperandPart.ItemRegister or
                GameEventScriptOpcodePrinter.OperandPart.KeyRegister or GameEventScriptOpcodePrinter.OperandPart.IndexRegister or GameEventScriptOpcodePrinter.OperandPart.DefaultRegister or
                GameEventScriptOpcodePrinter.OperandPart.NeedleRegister or GameEventScriptOpcodePrinter.OperandPart.ToRegister => instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.ValueRegister or GameEventScriptOpcodePrinter.OperandPart.AuxItemBindingRegister or GameEventScriptOpcodePrinter.OperandPart.StepRegister or
                GameEventScriptOpcodePrinter.OperandPart.MaximumRegister or GameEventScriptOpcodePrinter.OperandPart.AuxARegister => instruction.AU,
            GameEventScriptOpcodePrinter.OperandPart.ItemBindingRegister => operandIndex >= 3 ? instruction.AU : instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.WeightRegister => instruction.OpCode == GesBytecodeOpCode.TakeWeighted ? instruction.AU : instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.MinimumRegister => instruction.YRegister,
            GameEventScriptOpcodePrinter.OperandPart.AuxBRegister or GameEventScriptOpcodePrinter.OperandPart.FaceRegister => instruction.BU,
            GameEventScriptOpcodePrinter.OperandPart.AuxCRegister => instruction.CU,
            GameEventScriptOpcodePrinter.OperandPart.AuxDRegister => instruction.DU,
            _ => instruction.XRegister
        };

    private static bool IsAddressOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part is >= GameEventScriptOpcodePrinter.OperandPart.JumpTarget and <= GameEventScriptOpcodePrinter.OperandPart.ValueEntry;

    private static ushort ReadAddressOperand(GesBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part)
        => part switch
        {
            GameEventScriptOpcodePrinter.OperandPart.ProjectionEntry or GameEventScriptOpcodePrinter.OperandPart.KeyEntry => instruction.AU,
            GameEventScriptOpcodePrinter.OperandPart.ValueEntry => instruction.BU,
            _ => instruction.EntryAddress
        };

    private static bool IsStringOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part is >= GameEventScriptOpcodePrinter.OperandPart.String and <= GameEventScriptOpcodePrinter.OperandPart.TypeName &&
           part is not GameEventScriptOpcodePrinter.OperandPart.TypeKind and not GameEventScriptOpcodePrinter.OperandPart.PatternKind and not GameEventScriptOpcodePrinter.OperandPart.SeriesKind;

    private static bool IsListOperand(GameEventScriptOpcodePrinter.OperandPart part)
        => part is >= GameEventScriptOpcodePrinter.OperandPart.MessageShapeList and <= GameEventScriptOpcodePrinter.OperandPart.TagRegisterList;

    internal static ushort ReadListOperand(GesBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart part)
        => part switch
        {
            GameEventScriptOpcodePrinter.OperandPart.ArgumentNameList when instruction.OpCode == GesBytecodeOpCode.ConstructData => instruction.AU,
            GameEventScriptOpcodePrinter.OperandPart.TagRegisterList when GameEventScriptOpcodePrinter.IsResultSend(instruction.OpCode) => instruction.AU,
            GameEventScriptOpcodePrinter.OperandPart.MessageShapeList when instruction.OpCode == GesBytecodeOpCode.LoadMessage => instruction.SecondaryListIndex,
            GameEventScriptOpcodePrinter.OperandPart.KeyNameList => instruction.SecondaryListIndex,
            GameEventScriptOpcodePrinter.OperandPart.CaptureRegisterList => instruction.BU,
            GameEventScriptOpcodePrinter.OperandPart.SourceRegisterList => instruction.XRegister,
            GameEventScriptOpcodePrinter.OperandPart.TargetRegisterList => instruction.DestinationRegister,
            GameEventScriptOpcodePrinter.OperandPart.TagRegisterList when instruction.OpCode is GesBytecodeOpCode.EmitMessageWithTags or GesBytecodeOpCode.PublishMessageWithTags => instruction.SecondaryListIndex,
            _ => instruction.ListIndex
        };

    private static void ValidateInstructionList(GameEventScriptProgram program, ushort listIndex, GameEventScriptOpcodePrinter.OperandPart role, int instructionIndex)
    {
        var list = program.UInt16IndexLists.Resolve(listIndex);
        if (role == GameEventScriptOpcodePrinter.OperandPart.SourceRegisterList && list.Length < 2 ||
            role == GameEventScriptOpcodePrinter.OperandPart.TargetRegisterList && list.Length == 0)
            InvalidOperand("Iterator register list has an invalid arity.", instructionIndex);
        if (role == GameEventScriptOpcodePrinter.OperandPart.TargetRegisterList)
        {
            for (var index = 0; index < list.Length; index++)
            {
                if (list[index] == program.Code[instructionIndex].XRegister)
                    InvalidOperand("Iterator output cannot overwrite its iterator.", instructionIndex);
                for (var previous = 0; previous < index; previous++)
                    if (list[index] == list[previous]) InvalidOperand("Iterator output registers must be distinct.", instructionIndex);
            }
        }
        var textIndexes = role is GameEventScriptOpcodePrinter.OperandPart.MessageShapeList or GameEventScriptOpcodePrinter.OperandPart.ArgumentNameList or GameEventScriptOpcodePrinter.OperandPart.KeyNameList;
        var validateUniqueArgumentNames = role is GameEventScriptOpcodePrinter.OperandPart.MessageShapeList or GameEventScriptOpcodePrinter.OperandPart.ArgumentNameList;
        for (var index = 0; index < list.Length; index++)
        {
            if (textIndexes && list[index] >= program.StringConstants.Slices.Count)
                Throw(GesProgramFormatErrorCode.InvalidStringIndex, "Instruction list references a missing string.", (ushort)GesSectionType.Code, instructionIndex);
            if (!textIndexes && list[index] == ushort.MaxValue) InvalidOperand("Instruction list references a register outside the V1 frame limit.", instructionIndex);
            if (!textIndexes) continue;
            if (role == GameEventScriptOpcodePrinter.OperandPart.MessageShapeList && index == 0)
            {
                if (!IsPortableMessageName(program.StringConstants, list[index])) InvalidOperand("Message shape names must start uppercase and contain only ASCII letters or digits.", instructionIndex);
            }
            else if (!IsPortableUnlabeled(program.StringConstants, list[index]) && !IsPortablePlainName(program.StringConstants, list[index]))
            {
                InvalidOperand("Argument and key names must use the portable lowercase ASCII identifier grammar.", instructionIndex);
            }

            if (!validateUniqueArgumentNames || (role == GameEventScriptOpcodePrinter.OperandPart.MessageShapeList && index == 0) || IsPortableUnlabeled(program.StringConstants, list[index])) continue;
            var firstArgumentIndex = role == GameEventScriptOpcodePrinter.OperandPart.MessageShapeList ? 1 : 0;
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

    private static void ValidateDebug(GameEventScriptProgram program)
    {
        if (program.DebugSymbols is not null)
        {
            for (var index = 0; index < program.DebugSymbols.Symbols.Count; index++)
            {
                var symbol = program.DebugSymbols.Symbols[index];
                if (!GesProgramEnumValidation.IsDefined(symbol.Kind) ||
                    symbol.Name is null || (symbol.Name != GesMessageSignature.UnlabeledParameterName && !GameEventScriptText.IsIdentifier(symbol.Name)) || symbol.CodeLength == 0 ||
                    (ulong)symbol.CodeStart + symbol.CodeLength > (ulong)program.Code.Count || symbol.RegisterId == ushort.MaxValue)
                    Throw(GesProgramFormatErrorCode.InvalidDebugSymbol, "Debug symbol has an invalid kind, name, register, or code range.", (ushort)GesSectionType.DebugSymbols, index);
            }
        }

        if (program.SourceMap is not null) ValidateSourceMap(program);
        if (program.SourceArchive is not null) ValidateSourceArchive(program.SourceArchive);
        if (program.SourceMap is not null && program.SourceArchive is not null) ValidateSourceAgreement(program.SourceMap, program.SourceArchive);
        if (program.BuildMetadata is not null)
        {
            ValidateMetadataText(program.BuildMetadata.CompilerId, "CompilerId", (ushort)GesSectionType.BuildMetadata);
            ValidateMetadataText(program.BuildMetadata.CompilerVersion, "CompilerVersion", (ushort)GesSectionType.BuildMetadata);
        }
    }

    private static void ValidateSourceMap(GameEventScriptProgram program)
    {
        var map = program.SourceMap!;
        var ids = new HashSet<uint>();
        for (var index = 0; index < map.Sources.Count; index++)
        {
            var source = map.Sources[index];
            if (source.SourceId != (uint)index || !ids.Add(source.SourceId) || source.SourceName is null || source.SourceName.Length == 0 || GameEventScriptText.GetInvalidUtf16Offset(source.SourceName) >= 0 ||
                source.Sha256.Count != 32 || source.LineStartByteOffsets.Count == 0 || source.LineStartByteOffsets[0] != 0)
                Throw(GesProgramFormatErrorCode.InvalidSourceMap, "SourceMap source metadata is invalid.", (ushort)GesSectionType.SourceMap, index);
            uint previous = 0;
            for (var line = 0; line < source.LineStartByteOffsets.Count; line++)
            {
                var offset = source.LineStartByteOffsets[line];
                if (offset > source.SourceByteLength || line > 0 && offset <= previous)
                    Throw(GesProgramFormatErrorCode.InvalidSourceMap, "SourceMap line offsets must be strictly increasing and inside the source.", (ushort)GesSectionType.SourceMap, index);
                previous = offset;
            }
        }
        ulong previousCodeEnd = 0;
        for (var index = 0; index < map.Entries.Count; index++)
        {
            var entry = map.Entries[index];
            GesSourceMapSource? source = null;
            for (var sourceIndex = 0; sourceIndex < map.Sources.Count; sourceIndex++) if (map.Sources[sourceIndex].SourceId == entry.SourceId) { source = map.Sources[sourceIndex]; break; }
            if (source is null || entry.CodeLength == 0 || entry.SourceByteLength == 0 || entry.CodeStart < previousCodeEnd ||
                (ulong)entry.CodeStart + entry.CodeLength > (ulong)program.Code.Count ||
                (ulong)entry.SourceStartByteOffset + entry.SourceByteLength > source.SourceByteLength)
                Throw(GesProgramFormatErrorCode.InvalidSourceMap, "SourceMap mapping is outside code or source bounds.", (ushort)GesSectionType.SourceMap, index);
            previousCodeEnd = (ulong)entry.CodeStart + entry.CodeLength;
        }
    }

    private static void ValidateSourceArchive(GesSourceArchiveSegment archive)
    {
        var ids = new HashSet<uint>();
        for (var index = 0; index < archive.Sources.Count; index++)
        {
            var source = archive.Sources[index];
            if (!ids.Add(source.SourceId) || source.SourceName is null || source.SourceName.Length == 0 || GameEventScriptText.GetInvalidUtf16Offset(source.SourceName) >= 0)
                Throw(
                    GesProgramFormatErrorCode.InvalidSourceArchive, "SourceArchive SourceIds must be unique and names valid non-empty Unicode.", (ushort)GesSectionType.SourceArchive, index);
            try
            {
                var text = source.DecodeText(StrictUtf8);
                if (text.Length > 0 && text[0] == '\uFEFF')
                    Throw(GesProgramFormatErrorCode.InvalidSourceArchive, "SourceArchive stores logical source text and must not retain an initial UTF-8 BOM.", (ushort)GesSectionType.SourceArchive, index);
            }
            catch (DecoderFallbackException exception)
            {
                throw new GesProgramFormatException(
                    GesProgramFormatErrorCode.InvalidUtf8, "SourceArchive contains invalid UTF-8.", sectionType: (ushort)GesSectionType.SourceArchive, entryIndex: index, innerException: exception);
            }
        }
    }

    private static void ValidateSourceAgreement(GesSourceMapSegment map, GesSourceArchiveSegment archive)
    {
        if (map.Sources.Count != archive.Sources.Count)
            Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap and SourceArchive must describe the same source set.", (ushort)GesSectionType.SourceMap);
        using var sha256 = SHA256.Create();
        Span<byte> hash = stackalloc byte[32];
        for (var index = 0; index < map.Sources.Count; index++)
        {
            var metadata = map.Sources[index];
            GesSourceArchiveEntry? content = null;
            for (var archiveIndex = 0; archiveIndex < archive.Sources.Count; archiveIndex++) if (archive.Sources[archiveIndex].SourceId == metadata.SourceId) { content = archive.Sources[archiveIndex]; break; }
            if (content is null)
            {
                Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap has no matching SourceArchive entry.", (ushort)GesSectionType.SourceMap, index);
                continue;
            }
            if (!string.Equals(content.SourceName, metadata.SourceName, StringComparison.Ordinal) || content.Utf8Content.Count != metadata.SourceByteLength)
                Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap and SourceArchive metadata do not agree.", (ushort)GesSectionType.SourceMap, index);
            if (!sha256.TryComputeHash(content.Utf8Content.AsSpan(), hash, out var hashLength) || hashLength != hash.Length)
                Throw(GesProgramFormatErrorCode.InvalidProgram, "SHA-256 could not hash SourceArchive content.", (ushort)GesSectionType.SourceMap, index);
            for (var byteIndex = 0; byteIndex < hash.Length; byteIndex++) if (hash[byteIndex] != metadata.Sha256[byteIndex])
                Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap SHA-256 does not match SourceArchive.", (ushort)GesSectionType.SourceMap, index);
            ValidateUtf8Offsets(content.Utf8Content.AsSpan(), metadata, index);
            ValidateLogicalLineOffsets(content.DecodeText(StrictUtf8), metadata, index);
            ValidateMappingUtf8Offsets(content.Utf8Content.AsSpan(), map, metadata.SourceId, index);
        }
    }

    private static void ValidateMappingUtf8Offsets(ReadOnlySpan<byte> bytes, GesSourceMapSegment map, uint sourceId, int sourceIndex)
    {
        for (var index = 0; index < map.Entries.Count; index++)
        {
            var entry = map.Entries[index];
            if (entry.SourceId != sourceId) continue;
            var start = checked((int)entry.SourceStartByteOffset);
            var end = checked((int)(entry.SourceStartByteOffset + entry.SourceByteLength));
            if (!IsUtf8Boundary(bytes, start) || !IsUtf8Boundary(bytes, end))
                Throw(GesProgramFormatErrorCode.InvalidSourceMap, $"A source mapping for source {sourceIndex} is not on UTF-8 scalar boundaries.", (ushort)GesSectionType.SourceMap, index);
        }
    }

    private static bool IsUtf8Boundary(ReadOnlySpan<byte> bytes, int offset)
        => offset == 0 || offset == bytes.Length || offset > 0 && offset < bytes.Length && (bytes[offset] & 0xC0) != 0x80;

    private static void ValidateLogicalLineOffsets(string text, GesSourceMapSource metadata, int entryIndex)
    {
        var expected = new List<uint> { 0 };
        for (var utf16Offset = 0; utf16Offset < text.Length; utf16Offset++)
        {
            if (text[utf16Offset] == '\r')
            {
                if (utf16Offset + 1 < text.Length && text[utf16Offset + 1] == '\n') utf16Offset++;
                expected.Add(checked((uint)StrictUtf8.GetByteCount(text, 0, utf16Offset + 1)));
            }
            else if (text[utf16Offset] == '\n')
            {
                expected.Add(checked((uint)StrictUtf8.GetByteCount(text, 0, utf16Offset + 1)));
            }
        }

        if (expected.Count != metadata.LineStartByteOffsets.Count)
            Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap line offsets do not match the logical source newlines.", (ushort)GesSectionType.SourceMap, entryIndex);
        for (var index = 0; index < expected.Count; index++)
            if (expected[index] != metadata.LineStartByteOffsets[index])
                Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap line offsets do not match the logical source newlines.", (ushort)GesSectionType.SourceMap, entryIndex);
    }

    private static void ValidateMetadataText(string value, string role, ushort sectionType)
    {
        if (value is null || value.Length == 0 || GameEventScriptText.GetInvalidUtf16Offset(value) >= 0)
            Throw(GesProgramFormatErrorCode.InvalidProgram, $"{role} must be valid non-empty Unicode.", sectionType);
    }

    private static void ValidateUtf8Offsets(ReadOnlySpan<byte> bytes, GesSourceMapSource metadata, int entryIndex)
    {
        for (var index = 0; index < metadata.LineStartByteOffsets.Count; index++)
        {
            var offset = checked((int)metadata.LineStartByteOffsets[index]);
            if (offset > 0 && offset < bytes.Length && (bytes[offset] & 0xC0) == 0x80)
                Throw(GesProgramFormatErrorCode.InvalidSourceMap, "A line offset is not on a UTF-8 code-point boundary.", (ushort)GesSectionType.SourceMap, entryIndex);
        }
    }

    private static void ValidateOpaque(GameEventScriptProgram program)
    {
        var ordinals = new HashSet<int>();
        var knownTypes = new HashSet<ushort>();
        for (var index = 0; index < program.OpaqueSections.Count; index++)
        {
            var section = program.OpaqueSections[index];
            if (section.SectionType is 0 or ushort.MaxValue || section.OriginalOrdinal < 0 || (section.Flags & GesSectionFlags.Required) != 0)
                Throw(GesProgramFormatErrorCode.UnknownRequiredSection, "Opaque sections must be valid optional sections.", section.SectionType, index);
            if (((ushort)section.Flags & ~(ushort)(GesSectionFlags.Required | GesSectionFlags.CompressionMask)) != 0)
                Throw(GesProgramFormatErrorCode.InvalidSectionFlags, "Opaque section flags contain unassigned V1 bits.", section.SectionType, index);
            if (section.SectionType is (ushort)GesSectionType.ProgramMetadata or (ushort)GesSectionType.StringConstants or
                (ushort)GesSectionType.UInt16IndexLists or (ushort)GesSectionType.Bindings or (ushort)GesSectionType.Code)
                Throw(GesProgramFormatErrorCode.DuplicateSection, "Required known sections cannot be stored as opaque data.", section.SectionType, index);
            if (section.SectionType == (ushort)GesSectionType.DebugSymbols && program.DebugSymbols is not null ||
                section.SectionType == (ushort)GesSectionType.SourceMap && program.SourceMap is not null ||
                section.SectionType == (ushort)GesSectionType.SourceArchive && program.SourceArchive is not null ||
                section.SectionType == (ushort)GesSectionType.BuildMetadata && program.BuildMetadata is not null)
                Throw(GesProgramFormatErrorCode.DuplicateSection, "A known optional section cannot be both parsed and opaque.", section.SectionType, index);
            var knownOptional = section.SectionType is (ushort)GesSectionType.DebugSymbols or (ushort)GesSectionType.SourceMap or
                (ushort)GesSectionType.SourceArchive or (ushort)GesSectionType.BuildMetadata;
            var codec = (ushort)section.Flags & (ushort)GesSectionFlags.CompressionMask;
            if (knownOptional && section.SectionVersion == 1 && codec == 0)
                Throw(GesProgramFormatErrorCode.InvalidProgram, "A supported known section must be represented by its parsed segment model.", section.SectionType, index);
            if (knownOptional && !knownTypes.Add(section.SectionType))
                Throw(GesProgramFormatErrorCode.DuplicateSection, "A singleton known section occurs more than once.", section.SectionType, index);
            if (!ordinals.Add(section.OriginalOrdinal)) Throw(GesProgramFormatErrorCode.InvalidProgram, "Opaque section ordinals must be unique.", section.SectionType, index);
        }
    }

    private static void ValidateTextIndexes(GesReadOnlyArray<ushort> values, int stringCount, int bindingIndex)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index] >= stringCount) Throw(GesProgramFormatErrorCode.InvalidStringIndex, "Binding list references a missing string.", (ushort)GesSectionType.Bindings, bindingIndex);
    }

    private static bool IsExecutable(GesBinaryBindKind kind)
        => kind is MessageHandler or MessageNameHandler or Function or Predicate or Record;

    private static void Throw(GesProgramFormatErrorCode code, string message, ushort? sectionType = null, int? entryIndex = null)
        => throw new GesProgramFormatException(code, message, sectionType: sectionType, entryIndex: entryIndex);
}
