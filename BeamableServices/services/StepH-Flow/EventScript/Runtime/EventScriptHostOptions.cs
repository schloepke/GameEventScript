#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Runtime;

public static class EventScriptMessageHandlerPriority
{
    public const int Script = 0;
    public const int External = 100;
}

public sealed class EventScriptHostOptions
{
    public int MaxProcessedEventsPerRun { get; set; } = 64;

    public int DefaultScriptHandlerPriority { get; set; } = EventScriptMessageHandlerPriority.Script;

    public int DefaultExternalHandlerPriority { get; set; } = EventScriptMessageHandlerPriority.External;
}
