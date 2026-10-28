// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Literal recognition outside a Host exceeded one of its fixed protection limits.
public struct GesLiteralLimitError: Error, Equatable {
    /// The stable name of the exceeded limit: `MaxLiteralInputScalars`, `MaxLiteralDepth`, `MaxLiteralItems` or
    /// `MaxRangeItems`.
    public let limitName: String

    /// The exceeded limit value.
    public let limit: Int

    // Source position of the form whose evaluation exceeded the limit, for document errors; not part of equality.
    var offset = 0

    /// Creates a limit failure for the named limit.
    public init(limitName: String, limit: Int) {
        self.limitName = limitName
        self.limit = limit
    }

    /// Compares the limit name and value.
    public static func == (left: Self, right: Self) -> Bool { left.limitName == right.limitName && left.limit == right.limit }
}
