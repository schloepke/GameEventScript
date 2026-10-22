// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Optional synchronous boundary for delivering publications outside the host.
public protocol GesPublishSink {
    /// Attempts outbound delivery. Return true on acceptance; thrown errors are classified by the host publication
    /// boundary.
    func publish(_ message: GameEventScriptMessage) throws -> Bool
}
