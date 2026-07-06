namespace StepH.GameEventScript.Api;

/// <summary>
/// Serializes access to the single-threaded GameEventScript runtime core.
/// </summary>
/// <remarks>
/// Runtime gate implementations belong to the platform bridge. A gate must allow nested entry
/// from the same execution context because script handlers may publish or emit while dispatch is running.
/// </remarks>
public interface IGameEventScriptRuntimeGate
{
    /// <summary>
    /// Enters serialized runtime-core access.
    /// </summary>
    void Enter();

    /// <summary>
    /// Leaves serialized runtime-core access.
    /// </summary>
    void Exit();
}
