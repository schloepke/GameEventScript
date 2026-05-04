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
    Decimal,
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
        decimal number = 0m,
        bool boolean = false,
        GameEventScriptDecimalUnit? unit = null,
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

    public decimal Number { get; }

    public bool Boolean { get; }

    public GameEventScriptDecimalUnit? Unit { get; }

    public bool IsNaN { get; }

    public bool IsInfinity { get; }

    public bool IsNegativeInfinity { get; }

    public IReadOnlyList<string> Labels { get; }

    public static GameEventScriptBytecodeConstant Nothing()
        => new(GameEventScriptBytecodeConstantKind.Nothing);

    public static GameEventScriptBytecodeConstant FromBoolean(bool value)
        => new(GameEventScriptBytecodeConstantKind.Boolean, boolean: value);

    public static GameEventScriptBytecodeConstant FromInteger(long value)
        => new(GameEventScriptBytecodeConstantKind.Integer, integer: value);

    public static GameEventScriptBytecodeConstant FromDecimal(
        decimal value,
        GameEventScriptDecimalUnit? unit = null,
        bool isNaN = false,
        bool isInfinity = false,
        bool isNegativeInfinity = false)
        => new(GameEventScriptBytecodeConstantKind.Decimal, number: value, unit: unit, isNaN: isNaN, isInfinity: isInfinity, isNegativeInfinity: isNegativeInfinity);

    public static GameEventScriptBytecodeConstant FromPercentage(decimal ratio)
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
            GameEventScriptBytecodeConstantKind.Integer => Integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GameEventScriptBytecodeConstantKind.Decimal when IsNaN => "NaN",
            GameEventScriptBytecodeConstantKind.Decimal when IsInfinity => IsNegativeInfinity ? "-Infinity" : "Infinity",
            GameEventScriptBytecodeConstantKind.Decimal => Unit is { } unit
                ? $"{Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}{unit.ToSuffix()}"
                : Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GameEventScriptBytecodeConstantKind.Percentage => $"{(Number * 100m).ToString(System.Globalization.CultureInfo.InvariantCulture)}%",
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
    RulePredicate,
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
    GuardedChoice
}

public enum GameEventScriptBytecodeCallableKind
{
    Rule,
    Select
}

public enum GameEventScriptBytecodeCastKind
{
    Boolean,
    Integer,
    Decimal,
    Number,
    Percentage,
    Degree,
    Meter,
    Second,
    Point2,
    Point3,
    Sequence
}

public readonly record struct GameEventScriptBytecodeInstruction(
    GameEventScriptBytecodeOpCode OpCode,
    int A = -1,
    int B = -1,
    int ConstantIndex = -1,
    GameEventScriptBytecodeCastKind CastKind = default,
    GameEventScriptBytecodeCallableKind CallableKind = default,
    GameEventScriptBytecodeExpressionProgram? ExpressionProgram = null,
    GameEventScriptBytecodePipelineProgram? PipelineProgram = null,
    GameEventScriptBytecodeGeneratedCollectionProgram? GeneratedCollectionProgram = null,
    GameEventScriptBytecodeGuardedChoiceProgram? GuardedChoiceProgram = null,
    string? DiagnosticName = null,
    string? DiagnosticArgumentName = null,
    string[]? Names = null,
    int[]? Slots = null);

public sealed class GameEventScriptBytecodeExpressionProgram(GameEventScriptBytecodeInstruction[] instructions, int maxStackDepth)
{
    public GameEventScriptBytecodeInstruction[] Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));

    public int MaxStackDepth { get; } = maxStackDepth;

    internal bool CanEvaluateProjectionFast { get; } = CanEvaluateProjectionFastCore(instructions, maxStackDepth);

    private static bool CanEvaluateProjectionFastCore(GameEventScriptBytecodeInstruction[] instructions, int maxStackDepth)
        => instructions.Length is > 0 and <= 8 &&
           maxStackDepth <= 3 &&
           instructions.All(CanEvaluateProjectionInstructionFast);

    private static bool CanEvaluateProjectionInstructionFast(GameEventScriptBytecodeInstruction instruction)
        => instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.LoadConstant => true,
            GameEventScriptBytecodeOpCode.LoadSlot => true,
            GameEventScriptBytecodeOpCode.Cast => instruction.CastKind == GameEventScriptBytecodeCastKind.Boolean,
            GameEventScriptBytecodeOpCode.RulePredicate => instruction.ExpressionProgram is not null,
            GameEventScriptBytecodeOpCode.Or => true,
            GameEventScriptBytecodeOpCode.Xor => true,
            GameEventScriptBytecodeOpCode.And => true,
            GameEventScriptBytecodeOpCode.Equal => true,
            GameEventScriptBytecodeOpCode.NotEqual => true,
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
            _ => false
        };
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
    Pattern,
    ObjectMatch,
    TakePattern,
    Choose,
    Draw,
    Shuffle
}

public sealed class GameEventScriptBytecodeSelectorProgram(
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

public sealed class GameEventScriptBytecodePipelineProgram(
    GameEventScriptBytecodeExpressionProgram sourceProgram,
    GameEventScriptBytecodeSelectorProgram[] prefixSelectors,
    GameEventScriptBytecodeSelectorProgram terminalSelector)
{
    public GameEventScriptBytecodeExpressionProgram SourceProgram { get; } = sourceProgram ?? throw new ArgumentNullException(nameof(sourceProgram));

    public GameEventScriptBytecodeSelectorProgram[] PrefixSelectors { get; } = prefixSelectors ?? throw new ArgumentNullException(nameof(prefixSelectors));

    public GameEventScriptBytecodeSelectorProgram TerminalSelector { get; } = terminalSelector ?? throw new ArgumentNullException(nameof(terminalSelector));
}

public sealed class GameEventScriptBytecodeGeneratedCollectionProgram(
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

public sealed class GameEventScriptBytecodeGuardedChoiceProgram(
    GameEventScriptBytecodeExpressionProgram[] valuePrograms,
    GameEventScriptBytecodeExpressionProgram[] conditionPrograms,
    GameEventScriptBytecodeExpressionProgram otherwiseProgram)
{
    public GameEventScriptBytecodeExpressionProgram[] ValuePrograms { get; } = valuePrograms ?? throw new ArgumentNullException(nameof(valuePrograms));

    public GameEventScriptBytecodeExpressionProgram[] ConditionPrograms { get; } = conditionPrograms ?? throw new ArgumentNullException(nameof(conditionPrograms));

    public GameEventScriptBytecodeExpressionProgram OtherwiseProgram { get; } = otherwiseProgram ?? throw new ArgumentNullException(nameof(otherwiseProgram));
}

public sealed class GameEventScriptBytecodePublishLayout(
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

public sealed class GameEventScriptBytecodeIterationSourceProgram(
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

public abstract class GameEventScriptBytecodeDicePattern;

public sealed class GameEventScriptBytecodeFullHousePattern : GameEventScriptBytecodeDicePattern;

public sealed class GameEventScriptBytecodeStraightPattern : GameEventScriptBytecodeDicePattern;

public sealed class GameEventScriptBytecodeDiceCountPattern(int count, GameEventScriptBytecodeExpressionProgram? faceProgram) : GameEventScriptBytecodeDicePattern
{
    public int Count { get; } = count;

    public GameEventScriptBytecodeExpressionProgram? FaceProgram { get; } = faceProgram;
}

public sealed class GameEventScriptBytecodeObjectMatchPattern(GameEventScriptBytecodeObjectMatchEntry[] entries)
{
    public GameEventScriptBytecodeObjectMatchEntry[] Entries { get; } = entries ?? throw new ArgumentNullException(nameof(entries));
}

public sealed class GameEventScriptBytecodeObjectMatchEntry(string key, GameEventScriptBytecodeObjectMatchValue value)
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    public GameEventScriptBytecodeObjectMatchValue Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}

public abstract class GameEventScriptBytecodeObjectMatchValue;

public sealed class GameEventScriptBytecodeObjectMatchExpressionValue(GameEventScriptBytecodeExpressionProgram expressionProgram) : GameEventScriptBytecodeObjectMatchValue
{
    public GameEventScriptBytecodeExpressionProgram ExpressionProgram { get; } = expressionProgram ?? throw new ArgumentNullException(nameof(expressionProgram));
}

public sealed class GameEventScriptBytecodeObjectMatchNestedValue(GameEventScriptBytecodeObjectMatchPattern pattern) : GameEventScriptBytecodeObjectMatchValue
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

public sealed class GameEventScriptBytecodeStatementProgram(GameEventScriptBytecodeStatement[] statements, bool createsScope)
{
    public GameEventScriptBytecodeStatement[] Statements { get; } = statements ?? throw new ArgumentNullException(nameof(statements));

    public bool CreatesScope { get; } = createsScope;
}

public sealed class GameEventScriptBytecodeStatement(
    GameEventScriptBytecodeStatementKind kind,
    string? name = null,
    string? declaredType = null,
    GameEventScriptBytecodeExpressionProgram? expressionProgram = null,
    GameEventScriptBytecodePublishLayout? publishLayout = null,
    GameEventScriptBytecodeStatementProgram? thenProgram = null,
    GameEventScriptBytecodeStatementProgram? elseProgram = null,
    GameEventScriptBytecodeStatementProgram? bodyProgram = null,
    GameEventScriptBytecodeIterationSourceProgram? iterationSource = null,
    string? diagnosticName = null)
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
}

public sealed class GameEventScriptBytecodeTypeDefinition(
    string name,
    GameEventScriptBytecodeTypeFieldDefinition[] fields)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<GameEventScriptBytecodeTypeFieldDefinition> Fields { get; } = fields ?? throw new ArgumentNullException(nameof(fields));
}

public sealed class GameEventScriptBytecodeTypeFieldDefinition(
    string name,
    string typeName,
    GameEventScriptBytecodeExpressionProgram? minimumProgram,
    GameEventScriptBytecodeExpressionProgram? maximumProgram,
    GameEventScriptBytecodeExpressionProgram? computedProgram)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public string TypeName { get; } = typeName ?? throw new ArgumentNullException(nameof(typeName));

    public GameEventScriptBytecodeExpressionProgram? MinimumProgram { get; } = minimumProgram;

    public GameEventScriptBytecodeExpressionProgram? MaximumProgram { get; } = maximumProgram;

    public GameEventScriptBytecodeExpressionProgram? ComputedProgram { get; } = computedProgram;
}

public sealed class GameEventScriptBytecodeExecutionPlan
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

public sealed class GameEventScriptBytecodeHandler(
    string message,
    IReadOnlyList<string> parameters,
    IReadOnlyList<string> signatureLabels,
    string signatureId,
    int declarationOrder,
    GameEventScriptBytecodeExecutionPlan executionPlan)
{
    public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

    public IReadOnlyList<string> Parameters { get; } = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public IReadOnlyList<string> SignatureLabels { get; } = signatureLabels ?? throw new ArgumentNullException(nameof(signatureLabels));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public int DeclarationOrder { get; } = declarationOrder;

    public GameEventScriptBytecodeExecutionPlan ExecutionPlan { get; } = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));

    public GameEventScriptMessageSignature Definition { get; } = GameEventScriptMessageSignature.Create(message, signatureLabels);
}

public sealed class GameEventScriptBytecodeCallable(
    string name,
    GameEventScriptBytecodeCallableKind kind,
    IReadOnlyList<string> parameters,
    IReadOnlyList<string> signatureLabels,
    string signatureId,
    GameEventScriptBytecodeExpressionProgram expressionProgram)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public GameEventScriptBytecodeCallableKind Kind { get; } = kind;

    public IReadOnlyList<string> Parameters { get; } = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public IReadOnlyList<string> SignatureLabels { get; } = signatureLabels ?? throw new ArgumentNullException(nameof(signatureLabels));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public GameEventScriptBytecodeExpressionProgram ExpressionProgram { get; } = expressionProgram ?? throw new ArgumentNullException(nameof(expressionProgram));
}
