// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// Portable synchronous message handler invoked by a <see cref="GameEventScriptHost"/>.
/// </summary>
public interface IGameEventScriptNativeMessageHandler
{
    /// <summary>
    /// Performs the handle operation.
    /// </summary>
    /// <param name="message">The message value.</param>
    /// <param name="context">The context value.</param>
    void Handle(GameEventScriptMessage message, GameEventScriptContext context);
}
