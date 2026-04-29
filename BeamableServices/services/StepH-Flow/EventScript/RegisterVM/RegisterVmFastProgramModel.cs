#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.Flow.EventScript.RegisterVM;

internal enum RegisterFastOpCode
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
    Multiply,
    Modulo,
    Cast,
    RulePredicate,
    MemberAccess,
    IndexedAccess,
    Pipeline
}

internal enum RegisterFastCastKind
{
    Boolean,
    Integer,
    Decimal,
    Number,
    Percentage,
    Degree,
    Meter,
    Second
}

internal readonly record struct RegisterFastInstruction(
    RegisterFastOpCode OpCode,
    int A = -1,
    RegisterFastValue Constant = default,
    RegisterFastCastKind CastKind = default,
    RegisterFastExpressionProgram? ExpressionProgram = null,
    RegisterFastPipelineProgram? PipelineProgram = null,
    string? DiagnosticName = null,
    string? DiagnosticArgumentName = null);

internal sealed class RegisterFastExpressionProgram(
    RegisterFastInstruction[] instructions,
    int maxStackDepth)
{
    public RegisterFastInstruction[] Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));

    public int MaxStackDepth { get; } = maxStackDepth;
}

internal enum RegisterFastSelectorKind
{
    Filter,
    Select,
    Sum,
    Average,
    Count,
    Edge
}

internal sealed class RegisterFastSelectorProgram(
    RegisterFastSelectorKind kind,
    int identifierSlot,
    RegisterFastExpressionProgram? expressionProgram,
    string? edgeMode = null)
{
    public RegisterFastSelectorKind Kind { get; } = kind;

    public int IdentifierSlot { get; } = identifierSlot;

    public RegisterFastExpressionProgram? ExpressionProgram { get; } = expressionProgram;

    public string? EdgeMode { get; } = edgeMode;
}

internal sealed class RegisterFastPipelineProgram(
    RegisterFastExpressionProgram sourceProgram,
    RegisterFastSelectorProgram[] prefixSelectors,
    RegisterFastSelectorProgram terminalSelector)
{
    public RegisterFastExpressionProgram SourceProgram { get; } = sourceProgram ?? throw new ArgumentNullException(nameof(sourceProgram));

    public RegisterFastSelectorProgram[] PrefixSelectors { get; } = prefixSelectors ?? throw new ArgumentNullException(nameof(prefixSelectors));

    public RegisterFastSelectorProgram TerminalSelector { get; } = terminalSelector ?? throw new ArgumentNullException(nameof(terminalSelector));
}

internal sealed class RegisterFastPublishLayout(
    string messageName,
    string signatureId,
    string[] argumentNames,
    RegisterFastExpressionProgram[] argumentPrograms)
{
    public string MessageName { get; } = messageName ?? throw new ArgumentNullException(nameof(messageName));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public string[] ArgumentNames { get; } = argumentNames ?? throw new ArgumentNullException(nameof(argumentNames));

    public RegisterFastExpressionProgram[] ArgumentPrograms { get; } = argumentPrograms ?? throw new ArgumentNullException(nameof(argumentPrograms));
}
