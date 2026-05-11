#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptCompileOptions
{
    public bool Optimize { get; init; } = true;

    public bool EnableDiagnostics { get; init; }

    public bool EnableDebugInfo { get; init; }

    internal GameEventScriptCompileOptions NormalizeDebugInfo()
    {
        if (!EnableDiagnostics || EnableDebugInfo)
        {
            return this;
        }

        return new GameEventScriptCompileOptions
        {
            Optimize = Optimize,
            EnableDiagnostics = EnableDiagnostics,
            EnableDebugInfo = true
        };
    }
}
