#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.RegisterVM;

public sealed class RegisterEventScriptCompilationOptions
{
    public bool EnableDiagnostics { get; init; }

    public RegisterVmFallbackMode FallbackMode { get; init; } = RegisterVmFallbackMode.Allow;
}

public enum RegisterVmFallbackMode
{
    Allow,
    Throw
}
