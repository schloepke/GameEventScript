// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Data literal recognition with the rules of the GES `parse` and `parse strict` expressions. Without a running
/// Program, Program Record constructors are unknown types; `:Record("Type", [...])` reconstructs Records directly.
/// Recognition never executes constructors or host callbacks.
extension GesValue {
    /// Recognizes one complete data literal, like the GES `parse` expression, for example `150kW`, `#standard` or
    /// `[energy: 150kW]`. Input that is not a complete data literal becomes unchanged Text. Throws
    /// `GesLiteralLimitError` when the input exceeds a fixed literal parsing limit.
    public init(literal input: String) throws {
        self = try GesLiteralParser.parseData(input) ?? .text(input)
    }

    /// Recognizes one complete data literal, like the GES `parse strict` expression; Text values must be quoted, for
    /// example `'standard'`. Returns nil when the input is not a complete data literal; the literal `nothing` produces
    /// Nothing. Throws `GesLiteralLimitError` when the input exceeds a fixed literal parsing limit.
    public init?(strictLiteral input: String) throws {
        guard let value = try GesLiteralParser.parseData(input) else { return nil }
        self = value
    }
}
