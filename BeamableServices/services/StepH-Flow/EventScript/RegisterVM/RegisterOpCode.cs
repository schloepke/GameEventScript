#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.RegisterVM;

internal enum RegisterOpCode
{
    LoadParameter,
    LoadConstant,
    EvaluateExpression,
    StoreLocal,
    Publish,
    JumpIfFalse,
    Jump,
    ForEach,
    SeededRandom,
    EnterScope,
    ExitScope,
    Call,
    Return
}
