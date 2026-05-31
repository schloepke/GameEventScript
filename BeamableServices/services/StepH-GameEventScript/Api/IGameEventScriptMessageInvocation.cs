namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a running message handler invocation that can be executed in opcode slices.
/// </summary>
public interface IGameEventScriptMessageInvocation
{
    /// <summary>
    /// Gets whether the invocation has completed.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Runs at most <paramref name="maxOpcodes"/> opcodes and returns how many opcodes were executed.
    /// </summary>
    int RunSlice(int maxOpcodes);
}
