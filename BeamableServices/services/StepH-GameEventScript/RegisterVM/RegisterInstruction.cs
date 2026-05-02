#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.RegisterVM;

internal readonly record struct RegisterInstruction(
    RegisterOpCode OpCode,
    int A = -1,
    int B = -1,
    int C = -1,
    int D = -1);
