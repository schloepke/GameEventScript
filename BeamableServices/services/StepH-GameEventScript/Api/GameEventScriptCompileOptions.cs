#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptCompileOptions
{
    public GameEventScriptDebugInfoOptions DebugInfo { get; init; }

    public ulong ProgramVersion { get; init; }
}
