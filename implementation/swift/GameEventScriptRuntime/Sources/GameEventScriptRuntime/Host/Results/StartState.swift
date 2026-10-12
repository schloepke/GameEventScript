// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Outcome of initial-group or later per-instance initialization.
public enum GameEventScriptStartState: Sendable {
    /// All initialization covered by this result succeeded.
    case ready
    /// Initialization failed with a runtime diagnostic.
    case runtimeError
    /// Initialization exceeded a configured runtime limit.
    case runtimeLimitReached
}
