#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

public enum GameEventScriptBytecodeConstantKind
{
    Nothing,
    Boolean,
    Integer,
    Float,
    Percentage,
    Text,
    Tag,
    Handler
}

public sealed class GameEventScriptBytecodeConstant : IEquatable<GameEventScriptBytecodeConstant>
{
    private GameEventScriptBytecodeConstant(
        GameEventScriptBytecodeConstantKind kind,
        string? text = null,
        long integer = 0,
        double number = 0d,
        bool boolean = false,
        GameEventScriptNumericUnit? unit = null,
        bool isNaN = false,
        bool isInfinity = false,
        bool isNegativeInfinity = false,
        IReadOnlyList<string>? labels = null)
    {
        Kind = kind;
        Text = text;
        Integer = integer;
        Number = number;
        Boolean = boolean;
        Unit = unit;
        IsNaN = isNaN;
        IsInfinity = isInfinity;
        IsNegativeInfinity = isNegativeInfinity;
        Labels = labels?.Select(GameEventScriptMessageSignature.NormalizeParameterName).ToArray() ?? [];
    }

    public GameEventScriptBytecodeConstantKind Kind { get; }

    public string? Text { get; }

    public long Integer { get; }

    public double Number { get; }

    public bool Boolean { get; }

    public GameEventScriptNumericUnit? Unit { get; }

    public bool IsNaN { get; }

    public bool IsInfinity { get; }

    public bool IsNegativeInfinity { get; }

    public IReadOnlyList<string> Labels { get; }

    public static GameEventScriptBytecodeConstant Nothing()
        => new(GameEventScriptBytecodeConstantKind.Nothing);

    public static GameEventScriptBytecodeConstant FromBoolean(bool value)
        => new(GameEventScriptBytecodeConstantKind.Boolean, boolean: value);

    public static GameEventScriptBytecodeConstant FromInteger(long value, GameEventScriptNumericUnit? unit = null)
        => new(GameEventScriptBytecodeConstantKind.Integer, integer: value, unit: unit);

    public static GameEventScriptBytecodeConstant FromFloat(
        double value,
        GameEventScriptNumericUnit? unit = null,
        bool isNaN = false,
        bool isInfinity = false,
        bool isNegativeInfinity = false)
        => new(GameEventScriptBytecodeConstantKind.Float, number: value, unit: unit, isNaN: isNaN, isInfinity: isInfinity, isNegativeInfinity: isNegativeInfinity);

    public static GameEventScriptBytecodeConstant FromPercentage(double ratio)
        => new(GameEventScriptBytecodeConstantKind.Percentage, number: ratio);

    public static GameEventScriptBytecodeConstant FromText(string value)
        => new(GameEventScriptBytecodeConstantKind.Text, text: value ?? string.Empty);

    public static GameEventScriptBytecodeConstant FromTag(string value)
        => new(GameEventScriptBytecodeConstantKind.Tag, text: value ?? string.Empty);

    public static GameEventScriptBytecodeConstant FromHandler(string messageName, IReadOnlyList<string> labels)
        => new(GameEventScriptBytecodeConstantKind.Handler, text: GameEventScriptMessageSignature.NormalizeMessageName(messageName), labels: labels);

    public bool Equals(GameEventScriptBytecodeConstant? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Kind == other.Kind &&
               string.Equals(Text, other.Text, StringComparison.Ordinal) &&
               Integer == other.Integer &&
               Number == other.Number &&
               Boolean == other.Boolean &&
               Unit == other.Unit &&
               IsNaN == other.IsNaN &&
               IsInfinity == other.IsInfinity &&
               IsNegativeInfinity == other.IsNegativeInfinity &&
               Labels.SequenceEqual(other.Labels, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
        => obj is GameEventScriptBytecodeConstant other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Text, StringComparer.Ordinal);
        hash.Add(Integer);
        hash.Add(Number);
        hash.Add(Boolean);
        hash.Add(Unit);
        hash.Add(IsNaN);
        hash.Add(IsInfinity);
        hash.Add(IsNegativeInfinity);
        foreach (var label in Labels)
        {
            hash.Add(label, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
        => Kind switch
        {
            GameEventScriptBytecodeConstantKind.Nothing => "nothing",
            GameEventScriptBytecodeConstantKind.Boolean => Boolean ? "True" : "False",
            GameEventScriptBytecodeConstantKind.Integer => Unit is { } unit
                ? $"{Integer.ToString(System.Globalization.CultureInfo.InvariantCulture)}{unit.ToSuffix()}"
                : Integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GameEventScriptBytecodeConstantKind.Float when IsNaN => "NaN",
            GameEventScriptBytecodeConstantKind.Float when IsInfinity => IsNegativeInfinity ? "-Infinity" : "Infinity",
            GameEventScriptBytecodeConstantKind.Float => Unit is { } unit
                ? $"{Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}{unit.ToSuffix()}"
                : Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GameEventScriptBytecodeConstantKind.Percentage => $"{(Number * 100d).ToString(System.Globalization.CultureInfo.InvariantCulture)}%",
            GameEventScriptBytecodeConstantKind.Text => Text ?? string.Empty,
            GameEventScriptBytecodeConstantKind.Tag => $":{Text}",
            GameEventScriptBytecodeConstantKind.Handler => $"{Text}({string.Join(",", Labels)})",
            _ => Kind.ToString()
        };
}

public enum GameEventScriptBytecodeOpCode
{
    LoadConstant,
    LoadSlot,
    Or,
    Xor,
    And,
    Equal,
    NotEqual,
    ApproxEqual,
    Less,
    Greater,
    LessOrEqual,
    GreaterOrEqual,
    Add,
    Subtract,
    Multiply,
    Divide,
    IntegerDivide,
    Modulo,
    Remainder,
    PrimitiveIntegerEqual,
    PrimitiveIntegerNotEqual,
    PrimitiveIntegerLess,
    PrimitiveIntegerGreater,
    PrimitiveIntegerLessOrEqual,
    PrimitiveIntegerGreaterOrEqual,
    PrimitiveIntegerAdd,
    PrimitiveIntegerSubtract,
    PrimitiveIntegerMultiply,
    PrimitiveIntegerDivide,
    PrimitiveIntegerFloorDivide,
    PrimitiveIntegerModulo,
    PrimitiveIntegerRemainder,
    Default,
    Contains,
    ContainsValue,
    StartsWith,
    EndsWith,
    Intersect,
    Combine,
    Except,
    Zip,
    Unary,
    Variadic,
    Clamp,
    Random,
    Range,
    Dice,
    SeededRandom,
    Cast,
    TypeConstructor,
    PredicateTest,
    MemberAccess,
    IndexedAccess,
    BuildList,
    BuildSequence,
    BuildSet,
    BuildDictionary,
    BuildMessage,
    BindHandler,
    CallExtension,
    Call,
    TypeCheck,
    Pipeline,
    GeneratedCollection,
    GuardedChoice,
    Power,
    ShortCircuitOr,
    ShortCircuitAnd,
    ShortCircuitImplies,
    Nop,
    BindParameter,
    CoerceSlot,
    CopySlot,
    Jump,
    JumpIfTrue,
    JumpIfFalse,
    JumpIfNotTrue,
    EnterScope,
    ExitScope,
    Return,
    PublishValue,
    PublishMessageValue,
    ForRange,
    ForCollection,
    SeededRandomBlock
}

public enum GameEventScriptBytecodeCallableKind
{
    Predicate,
    Function
}

public enum GameEventScriptBytecodeCastKind
{
    Boolean,
    Integer,
    Float,
    Number,
    Percentage,
    Degree,
    Meter,
    Second,
    Vector,
    Point,
    Uuid,
    Sequence,
    Series,
    Envelope,
    Ref
}

public readonly record struct GameEventScriptBytecodeInstruction(
    GameEventScriptBytecodeOpCode OpCode,
    int Dest = -1,
    int A = -1,
    int B = -1,
    int C = -1,
    int Target = -1,
    int Target2 = -1,
    int Data = -1);

internal sealed record class GameEventScriptBytecodeStackInstruction(
    GameEventScriptBytecodeOpCode OpCode,
    int A = -1,
    int B = -1,
    int ConstantIndex = -1,
    GameEventScriptBytecodeCastKind CastKind = default,
    GameEventScriptBytecodeCallableKind CallableKind = GameEventScriptBytecodeCallableKind.Function,
    GameEventScriptBytecodeExpressionProgram? ExpressionProgram = null,
    GameEventScriptBytecodePipelineProgram? PipelineProgram = null,
    GameEventScriptBytecodeGeneratedCollectionProgram? GeneratedCollectionProgram = null,
    GameEventScriptBytecodeGuardedChoiceProgram? GuardedChoiceProgram = null,
    string? DiagnosticName = null,
    string? DiagnosticArgumentName = null,
    string[]? Names = null,
    int[]? Slots = null,
    string?[]? DeclaredTypes = null);

internal enum GameEventScriptBytecodeProjectionFastKind
{
    None,
    Operand,
    PredicateTest,
    Binary,
    BinaryCastBoolean,
    BinaryThenBinary,
    BinaryThenBinaryThenBinary,
    Stack
}

internal sealed class GameEventScriptBytecodeExpressionProgram(GameEventScriptBytecodeStackInstruction[] instructions, int maxStackDepth)
{
    public GameEventScriptBytecodeStackInstruction[] Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));

    public int MaxStackDepth { get; } = maxStackDepth;

    internal GameEventScriptBytecodeProjectionFastKind ProjectionFastKind { get; } = GetProjectionFastKind(instructions, maxStackDepth);

    internal bool CanEvaluateProjectionFast => ProjectionFastKind != GameEventScriptBytecodeProjectionFastKind.None;

    private static GameEventScriptBytecodeProjectionFastKind GetProjectionFastKind(GameEventScriptBytecodeStackInstruction[] instructions, int maxStackDepth)
    {
        if (instructions.Length is 0 or > 8 || maxStackDepth > 3 || !instructions.All(CanEvaluateProjectionInstructionFast))
        {
            return GameEventScriptBytecodeProjectionFastKind.None;
        }

        return instructions.Length switch
        {
            1 when IsProjectionOperand(instructions[0]) => GameEventScriptBytecodeProjectionFastKind.Operand,
            2 when IsProjectionOperand(instructions[0]) &&
                   instructions[1].OpCode == GameEventScriptBytecodeOpCode.PredicateTest => GameEventScriptBytecodeProjectionFastKind.PredicateTest,
            3 when IsProjectionOperand(instructions[0]) &&
                   IsProjectionOperand(instructions[1]) &&
                   IsProjectionBinaryOp(instructions[2].OpCode) => GameEventScriptBytecodeProjectionFastKind.Binary,
            4 when IsProjectionOperand(instructions[0]) &&
                   IsProjectionOperand(instructions[1]) &&
                   IsProjectionBinaryOp(instructions[2].OpCode) &&
                   instructions[3].OpCode == GameEventScriptBytecodeOpCode.Cast &&
                   instructions[3].CastKind == GameEventScriptBytecodeCastKind.Boolean => GameEventScriptBytecodeProjectionFastKind.BinaryCastBoolean,
            5 when IsProjectionOperand(instructions[0]) &&
                   IsProjectionOperand(instructions[1]) &&
                   IsProjectionBinaryOp(instructions[2].OpCode) &&
                   IsProjectionOperand(instructions[3]) &&
                   IsProjectionBinaryOp(instructions[4].OpCode) => GameEventScriptBytecodeProjectionFastKind.BinaryThenBinary,
            7 when IsProjectionOperand(instructions[0]) &&
                   IsProjectionOperand(instructions[1]) &&
                   IsProjectionBinaryOp(instructions[2].OpCode) &&
                   IsProjectionOperand(instructions[3]) &&
                   IsProjectionBinaryOp(instructions[4].OpCode) &&
                   IsProjectionOperand(instructions[5]) &&
                   IsProjectionBinaryOp(instructions[6].OpCode) => GameEventScriptBytecodeProjectionFastKind.BinaryThenBinaryThenBinary,
            _ => GameEventScriptBytecodeProjectionFastKind.Stack
        };
    }

    private static bool IsProjectionOperand(GameEventScriptBytecodeStackInstruction instruction)
        => instruction.OpCode is GameEventScriptBytecodeOpCode.LoadConstant or GameEventScriptBytecodeOpCode.LoadSlot;

    private static bool CanEvaluateProjectionInstructionFast(GameEventScriptBytecodeStackInstruction instruction)
        => instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.LoadConstant => true,
            GameEventScriptBytecodeOpCode.LoadSlot => true,
            GameEventScriptBytecodeOpCode.Cast => instruction.CastKind == GameEventScriptBytecodeCastKind.Boolean,
            GameEventScriptBytecodeOpCode.PredicateTest => instruction.ExpressionProgram is not null,
            GameEventScriptBytecodeOpCode.Or => true,
            GameEventScriptBytecodeOpCode.Xor => true,
            GameEventScriptBytecodeOpCode.And => true,
            GameEventScriptBytecodeOpCode.Power => true,
            GameEventScriptBytecodeOpCode.Equal => true,
            GameEventScriptBytecodeOpCode.NotEqual => true,
            GameEventScriptBytecodeOpCode.ApproxEqual => true,
            GameEventScriptBytecodeOpCode.Less => true,
            GameEventScriptBytecodeOpCode.Greater => true,
            GameEventScriptBytecodeOpCode.LessOrEqual => true,
            GameEventScriptBytecodeOpCode.GreaterOrEqual => true,
            GameEventScriptBytecodeOpCode.Add => true,
            GameEventScriptBytecodeOpCode.Subtract => true,
            GameEventScriptBytecodeOpCode.Multiply => true,
            GameEventScriptBytecodeOpCode.Divide => true,
            GameEventScriptBytecodeOpCode.IntegerDivide => true,
            GameEventScriptBytecodeOpCode.Modulo => true,
            GameEventScriptBytecodeOpCode.Remainder => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLess => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo => true,
            GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder => true,
            _ => false
        };

    private static bool IsProjectionBinaryOp(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.Or or
            GameEventScriptBytecodeOpCode.Xor or
            GameEventScriptBytecodeOpCode.And or
            GameEventScriptBytecodeOpCode.Power or
            GameEventScriptBytecodeOpCode.Equal or
            GameEventScriptBytecodeOpCode.NotEqual or
            GameEventScriptBytecodeOpCode.ApproxEqual or
            GameEventScriptBytecodeOpCode.Less or
            GameEventScriptBytecodeOpCode.Greater or
            GameEventScriptBytecodeOpCode.LessOrEqual or
            GameEventScriptBytecodeOpCode.GreaterOrEqual or
            GameEventScriptBytecodeOpCode.Add or
            GameEventScriptBytecodeOpCode.Subtract or
            GameEventScriptBytecodeOpCode.Multiply or
            GameEventScriptBytecodeOpCode.Divide or
            GameEventScriptBytecodeOpCode.IntegerDivide or
            GameEventScriptBytecodeOpCode.Modulo or
            GameEventScriptBytecodeOpCode.Remainder or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLess or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder;
}

public enum GameEventScriptBytecodeSelectorKind
{
    Filter,
    Select,
    Predicate,
    Sum,
    Average,
    Count,
    Edge,
    Min,
    Max,
    Dictionary,
    Contains,
    Sort,
    Distinct,
    GroupBy,
    OrderBy,
    Reverse,
    SequenceSlice,
    SeriesTerm,
    Pattern,
    ObjectMatch,
    TakePattern,
    Choose,
    Draw,
    Shuffle
}

internal sealed class GameEventScriptBytecodeSelectorProgram(
    GameEventScriptBytecodeSelectorKind kind,
    int identifierSlot,
    GameEventScriptBytecodeExpressionProgram? expressionProgram,
    string? edgeMode = null,
    GameEventScriptBytecodeExpressionProgram? secondaryExpressionProgram = null,
    string? secondaryMode = null,
    int count = 0,
    int secondaryIdentifierSlot = -1,
    bool flag = false,
    GameEventScriptBytecodeDicePattern? dicePattern = null,
    GameEventScriptBytecodeObjectMatchPattern? objectPattern = null)
{
    public GameEventScriptBytecodeSelectorKind Kind { get; } = kind;

    public int IdentifierSlot { get; } = identifierSlot;

    public GameEventScriptBytecodeExpressionProgram? ExpressionProgram { get; } = expressionProgram;

    public string? EdgeMode { get; } = edgeMode;

    public GameEventScriptBytecodeExpressionProgram? SecondaryExpressionProgram { get; } = secondaryExpressionProgram;

    public string? SecondaryMode { get; } = secondaryMode;

    public int Count { get; } = count;

    public int SecondaryIdentifierSlot { get; } = secondaryIdentifierSlot;

    public bool Flag { get; } = flag;

    public GameEventScriptBytecodeDicePattern? DicePattern { get; } = dicePattern;

    public GameEventScriptBytecodeObjectMatchPattern? ObjectPattern { get; } = objectPattern;
}

internal sealed class GameEventScriptBytecodePipelineProgram(
    GameEventScriptBytecodeExpressionProgram sourceProgram,
    GameEventScriptBytecodeSelectorProgram[] prefixSelectors,
    GameEventScriptBytecodeSelectorProgram terminalSelector)
{
    public GameEventScriptBytecodeExpressionProgram SourceProgram { get; } = sourceProgram ?? throw new ArgumentNullException(nameof(sourceProgram));

    public GameEventScriptBytecodeSelectorProgram[] PrefixSelectors { get; } = prefixSelectors ?? throw new ArgumentNullException(nameof(prefixSelectors));

    public GameEventScriptBytecodeSelectorProgram TerminalSelector { get; } = terminalSelector ?? throw new ArgumentNullException(nameof(terminalSelector));
}

internal sealed class GameEventScriptBytecodeGeneratedCollectionProgram(
    string collectionType,
    int identifierSlot,
    GameEventScriptBytecodeIterationSourceProgram source,
    GameEventScriptBytecodeExpressionProgram? predicateProgram,
    GameEventScriptBytecodeExpressionProgram projectionProgram)
{
    public string CollectionType { get; } = collectionType ?? throw new ArgumentNullException(nameof(collectionType));

    public int IdentifierSlot { get; } = identifierSlot;

    public GameEventScriptBytecodeIterationSourceProgram Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

    public GameEventScriptBytecodeExpressionProgram? PredicateProgram { get; } = predicateProgram;

    public GameEventScriptBytecodeExpressionProgram ProjectionProgram { get; } = projectionProgram ?? throw new ArgumentNullException(nameof(projectionProgram));
}

internal sealed class GameEventScriptBytecodeGuardedChoiceProgram(
    GameEventScriptBytecodeExpressionProgram[] valuePrograms,
    GameEventScriptBytecodeExpressionProgram[] conditionPrograms,
    GameEventScriptBytecodeExpressionProgram otherwiseProgram)
{
    public GameEventScriptBytecodeExpressionProgram[] ValuePrograms { get; } = valuePrograms ?? throw new ArgumentNullException(nameof(valuePrograms));

    public GameEventScriptBytecodeExpressionProgram[] ConditionPrograms { get; } = conditionPrograms ?? throw new ArgumentNullException(nameof(conditionPrograms));

    public GameEventScriptBytecodeExpressionProgram OtherwiseProgram { get; } = otherwiseProgram ?? throw new ArgumentNullException(nameof(otherwiseProgram));
}

internal sealed class GameEventScriptBytecodePublishLayout(
    string messageName,
    string signatureId,
    string[] argumentNames,
    GameEventScriptBytecodeExpressionProgram[] argumentPrograms)
{
    public string MessageName { get; } = messageName ?? throw new ArgumentNullException(nameof(messageName));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public string[] ArgumentNames { get; } = argumentNames ?? throw new ArgumentNullException(nameof(argumentNames));

    public GameEventScriptBytecodeExpressionProgram[] ArgumentPrograms { get; } = argumentPrograms ?? throw new ArgumentNullException(nameof(argumentPrograms));
}

public enum GameEventScriptBytecodeIterationSourceKind
{
    Collection,
    Range
}

internal sealed class GameEventScriptBytecodeIterationSourceProgram(
    GameEventScriptBytecodeIterationSourceKind kind,
    GameEventScriptBytecodeExpressionProgram? collectionProgram,
    GameEventScriptBytecodeExpressionProgram? rangeFromProgram,
    GameEventScriptBytecodeExpressionProgram? rangeToProgram,
    GameEventScriptBytecodeExpressionProgram? rangeStepProgram)
{
    public GameEventScriptBytecodeIterationSourceKind Kind { get; } = kind;

    public GameEventScriptBytecodeExpressionProgram? CollectionProgram { get; } = collectionProgram;

    public GameEventScriptBytecodeExpressionProgram? RangeFromProgram { get; } = rangeFromProgram;

    public GameEventScriptBytecodeExpressionProgram? RangeToProgram { get; } = rangeToProgram;

    public GameEventScriptBytecodeExpressionProgram? RangeStepProgram { get; } = rangeStepProgram;
}

internal abstract class GameEventScriptBytecodeDicePattern;

internal sealed class GameEventScriptBytecodeFullHousePattern : GameEventScriptBytecodeDicePattern;

internal sealed class GameEventScriptBytecodeStraightPattern : GameEventScriptBytecodeDicePattern;

internal sealed class GameEventScriptBytecodeDiceCountPattern(int count, GameEventScriptBytecodeExpressionProgram? faceProgram) : GameEventScriptBytecodeDicePattern
{
    public int Count { get; } = count;

    public GameEventScriptBytecodeExpressionProgram? FaceProgram { get; } = faceProgram;
}

internal sealed class GameEventScriptBytecodeObjectMatchPattern(GameEventScriptBytecodeObjectMatchEntry[] entries)
{
    public GameEventScriptBytecodeObjectMatchEntry[] Entries { get; } = entries ?? throw new ArgumentNullException(nameof(entries));
}

internal sealed class GameEventScriptBytecodeObjectMatchEntry(string key, GameEventScriptBytecodeObjectMatchValue value)
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    public GameEventScriptBytecodeObjectMatchValue Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}

internal abstract class GameEventScriptBytecodeObjectMatchValue;

internal sealed class GameEventScriptBytecodeObjectMatchExpressionValue(GameEventScriptBytecodeExpressionProgram expressionProgram) : GameEventScriptBytecodeObjectMatchValue
{
    public GameEventScriptBytecodeExpressionProgram ExpressionProgram { get; } = expressionProgram ?? throw new ArgumentNullException(nameof(expressionProgram));
}

internal sealed class GameEventScriptBytecodeObjectMatchNestedValue(GameEventScriptBytecodeObjectMatchPattern pattern) : GameEventScriptBytecodeObjectMatchValue
{
    public GameEventScriptBytecodeObjectMatchPattern Pattern { get; } = pattern ?? throw new ArgumentNullException(nameof(pattern));
}

public enum GameEventScriptBytecodeStatementKind
{
    Let,
    Publish,
    If,
    ForRange,
    ForCollection,
    Expression,
    SeededRandom
}

public enum GameEventScriptBytecodePublishKind
{
    Emit,
    Publish
}

internal sealed class GameEventScriptBytecodeStatementProgram(GameEventScriptBytecodeStatement[] statements, bool createsScope)
{
    public GameEventScriptBytecodeStatement[] Statements { get; } = statements ?? throw new ArgumentNullException(nameof(statements));

    public bool CreatesScope { get; } = createsScope;
}

internal sealed class GameEventScriptBytecodeStatement(
    GameEventScriptBytecodeStatementKind kind,
    string? name = null,
    string? declaredType = null,
    GameEventScriptBytecodeExpressionProgram? expressionProgram = null,
    GameEventScriptBytecodePublishLayout? publishLayout = null,
    GameEventScriptBytecodeStatementProgram? thenProgram = null,
    GameEventScriptBytecodeStatementProgram? elseProgram = null,
    GameEventScriptBytecodeStatementProgram? bodyProgram = null,
    GameEventScriptBytecodeIterationSourceProgram? iterationSource = null,
    string? diagnosticName = null,
    GameEventScriptBytecodePublishKind publishKind = GameEventScriptBytecodePublishKind.Emit,
    GameEventScriptBytecodeExpressionProgram[]? tagPrograms = null)
{
    public GameEventScriptBytecodeStatementKind Kind { get; } = kind;

    public string? Name { get; } = name;

    public string? DeclaredType { get; } = declaredType;

    public GameEventScriptBytecodeExpressionProgram? ExpressionProgram { get; } = expressionProgram;

    public GameEventScriptBytecodePublishLayout? PublishLayout { get; } = publishLayout;

    public GameEventScriptBytecodeStatementProgram? ThenProgram { get; } = thenProgram;

    public GameEventScriptBytecodeStatementProgram? ElseProgram { get; } = elseProgram;

    public GameEventScriptBytecodeStatementProgram? BodyProgram { get; } = bodyProgram;

    public GameEventScriptBytecodeIterationSourceProgram? IterationSource { get; } = iterationSource;

    public string? DiagnosticName { get; } = diagnosticName;

    public GameEventScriptBytecodePublishKind PublishKind { get; } = publishKind;

    public GameEventScriptBytecodeExpressionProgram[] TagPrograms { get; } = tagPrograms ?? [];
}

public sealed class GameEventScriptBytecodeTypeDefinition(
    string name,
    GameEventScriptBytecodeTypeFieldDefinition[] fields)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<GameEventScriptBytecodeTypeFieldDefinition> Fields { get; } = fields ?? throw new ArgumentNullException(nameof(fields));
}

public sealed class GameEventScriptBytecodeTypeFieldDefinition
{
    internal GameEventScriptBytecodeTypeFieldDefinition(
        string name,
        string typeName,
        GameEventScriptBytecodeExpressionProgram? minimumProgram,
        GameEventScriptBytecodeExpressionProgram? maximumProgram,
        GameEventScriptBytecodeExpressionProgram? computedProgram)
        : this(name, typeName, -1, -1, -1, minimumProgram, maximumProgram, computedProgram)
    {
    }

    internal GameEventScriptBytecodeTypeFieldDefinition(
        string name,
        string typeName,
        int minimumEntryAddress,
        int maximumEntryAddress,
        int computedEntryAddress,
        GameEventScriptBytecodeExpressionProgram? minimumProgram,
        GameEventScriptBytecodeExpressionProgram? maximumProgram,
        GameEventScriptBytecodeExpressionProgram? computedProgram)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        MinimumEntryAddress = minimumEntryAddress;
        MaximumEntryAddress = maximumEntryAddress;
        ComputedEntryAddress = computedEntryAddress;
        MinimumProgram = minimumProgram;
        MaximumProgram = maximumProgram;
        ComputedProgram = computedProgram;
    }

    public string Name { get; }

    public string TypeName { get; }

    public int MinimumEntryAddress { get; internal set; }

    public int MaximumEntryAddress { get; internal set; }

    public int ComputedEntryAddress { get; internal set; }

    internal GameEventScriptBytecodeExpressionProgram? MinimumProgram { get; }

    internal GameEventScriptBytecodeExpressionProgram? MaximumProgram { get; }

    internal GameEventScriptBytecodeExpressionProgram? ComputedProgram { get; }
}

internal sealed class GameEventScriptBytecodeExecutionPlan
{
    private readonly IReadOnlyDictionary<string, int> _slots;

    private GameEventScriptBytecodeExecutionPlan(
        IReadOnlyDictionary<string, int> slots,
        GameEventScriptBytecodeStatementProgram statementProgram,
        int maxStackDepth)
    {
        _slots = slots;
        StatementProgram = statementProgram ?? throw new ArgumentNullException(nameof(statementProgram));
        SlotCount = slots.Count;
        MaxStackDepth = maxStackDepth;
    }

    public GameEventScriptBytecodeStatementProgram StatementProgram { get; }

    public IReadOnlyDictionary<string, int> Slots => _slots;

    public int SlotCount { get; }

    public int MaxStackDepth { get; }

    public static GameEventScriptBytecodeExecutionPlan Create(
        IReadOnlyDictionary<string, int> slots,
        GameEventScriptBytecodeStatementProgram statementProgram,
        int maxStackDepth)
        => new(new Dictionary<string, int>(slots, StringComparer.Ordinal), statementProgram, maxStackDepth);

    public bool TryGetSlot(string name, out int slot)
        => _slots.TryGetValue(name, out slot);
}

public enum GameEventScriptBytecodeHandlerDispatchKind
{
    ExactSignature,
    MessageEnvelope
}

public sealed class GameEventScriptBytecodeHandler
{
    internal GameEventScriptBytecodeHandler(
        string message,
        GameEventScriptBytecodeHandlerDispatchKind dispatchKind,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> signatureLabels,
        string signatureId,
        int declarationOrder,
        GameEventScriptBytecodeExecutionPlan executionPlan,
        IReadOnlyList<string?>? parameterTypes = null,
        IReadOnlyList<string>? requiredTags = null,
        IReadOnlyList<string>? excludedTags = null,
        int entryAddress = -1)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        DispatchKind = dispatchKind;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        SignatureLabels = signatureLabels ?? throw new ArgumentNullException(nameof(signatureLabels));
        ParameterTypes = NormalizeParameterTypes(parameterTypes, parameters);
        RequiredTags = NormalizeTags(requiredTags);
        ExcludedTags = NormalizeTags(excludedTags);
        SignatureId = signatureId ?? throw new ArgumentNullException(nameof(signatureId));
        DeclarationOrder = declarationOrder;
        ExecutionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
        EntryAddress = entryAddress;
        LocalSlotCount = executionPlan.SlotCount;
        Slots = executionPlan.Slots;
        Definition = GameEventScriptMessageSignature.Create(message, signatureLabels);
    }

    public string Message { get; }

    public GameEventScriptBytecodeHandlerDispatchKind DispatchKind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public IReadOnlyList<string> RequiredTags { get; }

    public IReadOnlyList<string> ExcludedTags { get; }

    public string SignatureId { get; }

    public int DeclarationOrder { get; }

    public int EntryAddress { get; internal set; }

    public int LocalSlotCount { get; }

    public IReadOnlyDictionary<string, int> Slots { get; }

    internal GameEventScriptBytecodeExecutionPlan ExecutionPlan { get; }

    public GameEventScriptMessageSignature Definition { get; }

    private static IReadOnlyList<string?> NormalizeParameterTypes(IReadOnlyList<string?>? parameterTypes, IReadOnlyList<string> parameters)
    {
        _ = parameters ?? throw new ArgumentNullException(nameof(parameters));
        if (parameterTypes is null)
        {
            return new string?[parameters.Count];
        }

        if (parameterTypes.Count != parameters.Count)
        {
            throw new ArgumentException("Parameter type hint count must match parameter count.", nameof(parameterTypes));
        }

        return parameterTypes.Select(type => string.IsNullOrWhiteSpace(type) ? null : type).ToArray();
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
        => GameEventScriptMessage.NormalizeTags(tags);
}

public sealed class GameEventScriptBytecodeCallable
{
    internal GameEventScriptBytecodeCallable(
        string name,
        GameEventScriptBytecodeCallableKind kind,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> signatureLabels,
        string signatureId,
        GameEventScriptBytecodeExpressionProgram expressionProgram,
        IReadOnlyList<string?>? parameterTypes = null,
        int entryAddress = -1,
        int localSlotCount = 0,
        int returnSlot = -1)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        SignatureLabels = signatureLabels ?? throw new ArgumentNullException(nameof(signatureLabels));
        ParameterTypes = NormalizeParameterTypes(parameterTypes, parameters);
        SignatureId = signatureId ?? throw new ArgumentNullException(nameof(signatureId));
        ExpressionProgram = expressionProgram ?? throw new ArgumentNullException(nameof(expressionProgram));
        EntryAddress = entryAddress;
        LocalSlotCount = localSlotCount;
        ReturnSlot = returnSlot;
    }

    public string Name { get; }

    public GameEventScriptBytecodeCallableKind Kind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public string SignatureId { get; }

    public int EntryAddress { get; internal set; }

    public int LocalSlotCount { get; internal set; }

    public int ReturnSlot { get; internal set; }

    internal GameEventScriptBytecodeExpressionProgram ExpressionProgram { get; }

    private static IReadOnlyList<string?> NormalizeParameterTypes(IReadOnlyList<string?>? parameterTypes, IReadOnlyList<string> parameters)
    {
        _ = parameters ?? throw new ArgumentNullException(nameof(parameters));
        if (parameterTypes is null)
        {
            return new string?[parameters.Count];
        }

        if (parameterTypes.Count != parameters.Count)
        {
            throw new ArgumentException("Parameter type hint count must match parameter count.", nameof(parameterTypes));
        }

        return parameterTypes.Select(type => string.IsNullOrWhiteSpace(type) ? null : type).ToArray();
    }
}
