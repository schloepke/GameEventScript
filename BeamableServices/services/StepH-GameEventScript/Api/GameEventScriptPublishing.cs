#pragma warning disable CS1591 // Public architecture is documented in Documentation/Specification/HostRuntime.md.

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptPublishSink
{
    bool Publish(GameEventScriptMessage message);
}

public readonly struct GameEventScriptPublishResult(bool localAccepted, bool outboundAttempted, bool outboundAccepted)
{
    public bool LocalAccepted { get; } = localAccepted;
    public bool OutboundAttempted { get; } = outboundAttempted;
    public bool OutboundAccepted { get; } = outboundAccepted;
    public bool AnyAccepted => LocalAccepted || OutboundAccepted;
}
