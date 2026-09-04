namespace StepH.GameEventScript.Api;

/// <summary>
/// Receives synchronous outbound publications after the host has attempted local enqueue.
/// </summary>
public interface IGameEventScriptPublishSink
{
    /// <summary>
    /// Attempts to accept one immutable outbound message.
    /// </summary>
    /// <param name="message">The message to hand off.</param>
    /// <returns><see langword="true"/> when the sink accepted responsibility for it; otherwise <see langword="false"/>.</returns>
    /// <remarks>A thrown exception is converted into an outbound rejection and reported to the runtime observer; it does not undo local dispatch.</remarks>
    bool Publish(GameEventScriptMessage message);
}

/// <summary>
/// Describes the independent local and outbound outcomes of one publish operation without allocating.
/// </summary>
public readonly struct GameEventScriptPublishResult(bool localAccepted, bool outboundAttempted, bool outboundAccepted)
{
    /// <summary>
    /// Gets whether the host's local message queue accepted the publication.
    /// </summary>
    public bool LocalAccepted { get; } = localAccepted;
    /// <summary>
    /// Gets whether a configured sink was invoked.
    /// </summary>
    public bool OutboundAttempted { get; } = outboundAttempted;
    /// <summary>
    /// Gets whether the invoked sink accepted the publication.
    /// </summary>
    public bool OutboundAccepted { get; } = outboundAccepted;
    /// <summary>
    /// Gets whether either the local host or the outbound sink accepted the publication.
    /// </summary>
    public bool AnyAccepted => LocalAccepted || OutboundAccepted;
}
