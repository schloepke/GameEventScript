// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Synchronous native handler invoked atomically within serial host dispatch.
public protocol GameEventScriptNativeMessageHandler {
    /// Handles a message with the owning host context. Do not pump the same host reentrantly.
    ///
    /// - Throws: An application fault or callback error, classified by the host at the native boundary.
    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws
}
