// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// Portable synchronous message handler invoked by a <see cref="GameEventScriptHost"/>.
/// </summary>
public interface IGesNativeMessageHandler
{
    /// <summary>
    /// Handles one dispatched message synchronously. The host invokes the handler atomically within its serial dispatch, in descending
    /// priority and then registration order; the call cannot pause. Do not pump the same host reentrantly from the handler.
    /// </summary>
    /// <param name="message">The immutable dispatched message; it may be retained.</param>
    /// <param name="context">The owning host's context with its random stream, limits, <c>Emit</c> and <c>Publish</c>; it must not be
    /// retained for use after the call returns.</param>
    /// <remarks>An exception ends only this handler invocation: <see cref="GesExtensionFaultException"/> reports a caller-chosen fault and
    /// any other exception is reported as <c>runtime.nativeHandlerFailure</c>. The host remains usable for later messages.</remarks>
    void Handle(GameEventScriptMessage message, GesContext context);
}
