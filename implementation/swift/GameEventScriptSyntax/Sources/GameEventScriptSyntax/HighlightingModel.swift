// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The language a highlighter reads.
public enum GesSyntaxLanguage: String, Sendable {
    /// Game Event Script source, including incomplete editor input.
    case ges
    /// GESA assembler with embedded GES source regions.
    case gesa
    /// GES document data (`.gesd`) with its schema header; diagnostics report its first document error.
    case gesd
    /// GES schema source (`.gess`); diagnostics report its first syntax error.
    case gess
}

/// Theme-independent categories the highlighter derives from the syntax tree; these are not compiler tokens.
public enum GesSyntaxKind: String, CaseIterable, Sendable {
    /// Text without a more specific category, including whitespace and line breaks.
    case plain
    /// A keyword, contextual keyword, operator or boolean literal.
    case keyword
    /// A built-in function, collection selector, extension name, unit or inline-assembly opcode.
    case builtin
    /// A variable, parameter, field, member or label identifier.
    case identifier
    /// A message or handler name.
    case message
    /// A source type or record name.
    case type
    /// A tag name.
    case tag
    /// A named constant.
    case constant
    /// A numeric literal, including units, dice syntax and math constants.
    case number
    /// A string literal, including an unterminated one.
    case string
    /// A comment.
    case comment
    /// Punctuation such as parentheses, brackets and separators.
    case symbol
    /// A function or predicate name, where declared or called.
    case function
    /// A module name.
    case module
    /// An assembler label or symbol reference.
    case label
    /// An assembler register.
    case register
}

/// A diagnostic of the highlighted source: a syntax error or a violated single-source rule.
///
/// Rules that need other sources, catalogs or a host are checked only by the Compiler.
public struct GesHighlightDiagnostic: Equatable, Sendable {
    /// The stable diagnostic code, such as `parse.syntax` or `validate.duplicateVariable`.
    public let code: String
    /// A message for people; its wording is not stable.
    public let message: String
    /// Zero-based UTF-16 offset of the range in the highlighted text.
    public let start: Int
    /// Range length in UTF-16 code units; zero for a position, such as the end of an unfinished source.
    public let length: Int
}

/// An immutable UTF-16 range with its category and the diagnostics that cover it.
public struct GesHighlightSpan: Equatable, Sendable {
    /// Zero-based UTF-16 offset in the highlighted text.
    public let start: Int
    /// Range length in UTF-16 code units; always positive.
    public let length: Int
    /// The category of the range.
    public let kind: GesSyntaxKind
    /// The diagnostics whose range overlaps this span, in source order; a diagnostic without length belongs to the span at its position.
    public let diagnostics: [GesHighlightDiagnostic]
}

/// A highlighting result. An input above the length bound returns an incomplete result without spans or diagnostics.
public struct GesHighlightResult: Sendable {
    /// Ordered, nonoverlapping ranges covering the complete input when complete; empty for empty input.
    public let spans: [GesHighlightSpan]
    /// All diagnostics of the input in source order; GESA input has none.
    public let diagnostics: [GesHighlightDiagnostic]
    /// Whether the input was within the length bound and has been highlighted.
    public let isComplete: Bool
}

/// Invalid API input; malformed GES/GESA code is accepted and does not produce these errors.
public enum GesHighlightingError: Error {
    /// The input-length bound must be positive.
    case invalidInputLimit
    /// Rendering spans overlap, exceed the supplied text or split a Unicode scalar.
    case invalidSpans
    /// ANSI colors must be nil or one of 30...37, 39, 90...97.
    case invalidAnsiColor
}
