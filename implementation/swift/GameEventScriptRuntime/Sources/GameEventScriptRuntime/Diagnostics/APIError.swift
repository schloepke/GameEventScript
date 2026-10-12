// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Caller errors in API arguments or lifecycle operations.
public enum GameEventScriptAPIError: Error, Sendable {
    /// An argument violates an API precondition; the associated string explains it.
    case invalidArgument(String)
    /// The requested operation is invalid in the current lifecycle state.
    case invalidOperation(String)
}
