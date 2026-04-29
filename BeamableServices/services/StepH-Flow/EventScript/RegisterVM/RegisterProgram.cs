#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.Flow.EventScript.RegisterVM;

internal sealed class RegisterProgram(
    string name,
    IReadOnlyList<RegisterInstruction> instructions,
    int registerCount,
    int localCount,
    bool createsScope = false)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<RegisterInstruction> Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));

    public int RegisterCount { get; } = registerCount;

    public int LocalCount { get; } = localCount;

    public bool CreatesScope { get; } = createsScope;
}
