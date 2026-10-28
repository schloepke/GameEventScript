// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Literal recognition outside a Host exceeded one of its fixed protection limits.
public struct GesLiteralLimitError: Error, Equatable {
    /// The stable name of the exceeded limit: `MaxLiteralInputScalars`, `MaxLiteralDepth`, `MaxLiteralItems` or
    /// `MaxRangeItems`.
    public let limitName: String

    /// The exceeded limit value.
    public let limit: Int

    /// Creates a limit failure for the named limit.
    public init(limitName: String, limit: Int) {
        self.limitName = limitName
        self.limit = limit
    }
}
