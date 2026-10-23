// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// Observes synchronous host behavior without participating in dispatch.
/// Implementations must not throw and must not recursively pump the observed host.
/// Each accepted send carries a host-local send ID: it starts at 1, increases by one per accepted send and is reported
/// by every callback about that send, from emit, publish or scheduling to each dispatch. 0 reports a send without an ID.
/// </summary>
public interface IGesRuntimeObserver
{
    /// <summary>
    /// Observes a local emit attempt.
    /// </summary>
    /// <param name="message">The emitted message.</param>
    /// <param name="id">The send ID, or 0 when the emit was not queued.</param>
    /// <param name="accepted">Whether the local queue accepted it.</param>
    void MessageEmitted(GameEventScriptMessage message, long id, bool accepted);

    /// <summary>
    /// Observes a local-plus-outbound publish attempt.
    /// </summary>
    /// <param name="message">The published message.</param>
    /// <param name="id">The send ID, also without local recipients, or 0 when the host did not accept the publish.</param>
    /// <param name="result">The independent local and outbound outcomes.</param>
    void MessagePublished(GameEventScriptMessage message, long id, GesPublishResult result);

    /// <summary>
    /// Observes entry into one matching native or script handler.
    /// </summary>
    /// <param name="message">The message being dispatched.</param>
    /// <param name="id">The send ID of the dispatched message.</param>
    /// <param name="dispatchSignatureId">The stable matched signature identifier.</param>
    void DispatchStarted(GameEventScriptMessage message, long id, string dispatchSignatureId);

    /// <summary>
    /// Observes exit from one handler, including cleanup after a runtime failure or limit.
    /// </summary>
    /// <param name="message">The message that was dispatched.</param>
    /// <param name="id">The send ID of the dispatched message.</param>
    /// <param name="dispatchSignatureId">The stable matched signature identifier.</param>
    void DispatchCompleted(GameEventScriptMessage message, long id, string dispatchSignatureId);

    /// <summary>
    /// Observes exhaustion of a configured runtime safety limit.
    /// </summary>
    /// <param name="limitName">The portable limit property name.</param>
    /// <param name="detail">A human-readable explanation.</param>
    /// <param name="limit">The configured limit value.</param>
    void RuntimeLimitReached(string limitName, string detail, int limit);

    /// <summary>
    /// Observes a structured runtime diagnostic.
    /// </summary>
    /// <param name="diagnostic">The language-neutral diagnostic.</param>
    void RuntimeError(GesDiagnostic diagnostic);

    /// <summary>
    /// Observes acceptance of one delayed <c>emit … after</c> or <c>publish … after</c>, once, when it is scheduled.
    /// The default implementation does nothing.
    /// </summary>
    /// <param name="message">The scheduled message.</param>
    /// <param name="id">The send ID, which later emit, publish and dispatch callbacks of this message repeat.</param>
    /// <param name="deadline">The absolute due time in whole microseconds on the host's monotonic clock, the clock
    /// <see cref="GameEventScriptHost.NextMessageDelay"/> uses; neither a relative delay nor wall-clock time.</param>
    /// <param name="publish">Whether the send is a publish rather than an emit.</param>
    void MessageScheduled(GameEventScriptMessage message, long id, long deadline, bool publish)
    {
    }

    /// <summary>
    /// Observes removal of a scheduled message before it became due, for example because its initialization failed.
    /// A due message is not reported here; its ordinary delivery callbacks follow. The default implementation does nothing.
    /// </summary>
    /// <param name="id">The send ID that <see cref="MessageScheduled"/> reported.</param>
    void ScheduledMessageDiscarded(long id)
    {
    }
}
