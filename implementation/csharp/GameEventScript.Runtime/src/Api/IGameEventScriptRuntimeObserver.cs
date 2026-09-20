// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// Observes synchronous host behavior without participating in dispatch.
/// Implementations must not throw and must not recursively pump the observed host.
/// </summary>
public interface IGameEventScriptRuntimeObserver
{
    /// <summary>
    /// Observes a local emit attempt.
    /// </summary>
    /// <param name="message">The emitted message.</param>
    /// <param name="accepted">Whether the local queue accepted it.</param>
    void MessageEmitted(GameEventScriptMessage message, bool accepted);

    /// <summary>
    /// Observes a local-plus-outbound publish attempt.
    /// </summary>
    /// <param name="message">The published message.</param>
    /// <param name="result">The independent local and outbound outcomes.</param>
    void MessagePublished(GameEventScriptMessage message, GameEventScriptPublishResult result);

    /// <summary>
    /// Observes entry into one matching native or script handler.
    /// </summary>
    /// <param name="message">The message being dispatched.</param>
    /// <param name="dispatchSignatureId">The stable matched signature identifier.</param>
    void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId);

    /// <summary>
    /// Observes exit from one handler, including cleanup after a runtime failure or limit.
    /// </summary>
    /// <param name="message">The message that was dispatched.</param>
    /// <param name="dispatchSignatureId">The stable matched signature identifier.</param>
    void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId);

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
    void RuntimeError(GameEventScriptDiagnostic diagnostic);
}
