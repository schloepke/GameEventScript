using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Defines a collection of message handlers for game event script messages.
/// </summary>
public interface IGameEventScriptMessageHandlerCollection
{
    /// <summary>
    /// Gets a collection of handlers that map each <see cref="GameEventScriptMessageSignature"/>
    /// to an associated <see cref="Action{GameEventScriptMessage, GameEventScriptSession}"/>.
    /// The handlers facilitate responding to game event script messages, defining specific actions to be executed
    /// when a message with a matching signature is received.
    /// </summary>
    IEnumerable<(GameEventScriptMessageSignature Signature, Action<GameEventScriptMessage, GameEventScriptSession> Handler)> Handlers { get; }

    void Bind(IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptExternalTypeRegistry typeRegistry);

}
