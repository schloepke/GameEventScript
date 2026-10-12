// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Outcome of one host pump call.
public enum GameEventScriptExecutionState: Sendable {
    /// Only delayed work remains. The caller may wait until the next message delay before pumping again.
    case waiting
    /// Work remains after the frame or scheduling budget was consumed.
    case paused
    /// The host finished the available work.
    case completed
    /// A configured runtime safeguard stopped execution.
    case runtimeLimitReached
    /// A script or native callback failed.
    case runtimeError
}
