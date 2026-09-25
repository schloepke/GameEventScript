// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The bundled source or assembler grammar used for presentation.
public enum GameEventScriptSyntaxLanguage: String, Sendable {
    /// Game Event Script source, including incomplete editor input.
    case ges
    /// GESA assembler with embedded GES source regions.
    case gesa
}

/// Theme-independent categories derived from TextMate scopes; these are not compiler tokens.
public enum GameEventScriptSyntaxKind: String, CaseIterable, Sendable {
    /// Text without a more specific category, including whitespace.
    case plain
    /// A keyword, word operator or boolean literal.
    case keyword
    /// A built-in function or collection selector.
    case builtin
    /// A variable or parameter identifier.
    case identifier
    /// A message or handler name.
    case message
    /// A source type name.
    case type
    /// A tag name.
    case tag
    /// A named constant.
    case constant
    /// A numeric literal, including units or dice syntax.
    case number
    /// A string literal, delimiter or escape.
    case string
    /// A comment.
    case comment
    /// Punctuation such as parentheses and separators.
    case symbol
    /// A declared function or predicate name.
    case function
    /// A module name.
    case module
    /// An assembler label or symbol reference.
    case label
    /// An assembler register.
    case register
}

/// An immutable UTF-16 range with its category and outer-to-inner TextMate scope stack.
public struct GameEventScriptHighlightSpan: Equatable, Sendable {
    /// Zero-based UTF-16 offset in the supplied document or line.
    public let start: Int
    /// Range length in UTF-16 code units.
    public let length: Int
    /// The most specific recognized category.
    public let kind: GameEventScriptSyntaxKind
    /// Outer-to-inner TextMate scopes, including the root language scope.
    public let scopes: [String]

    init(_ start: Int, _ length: Int, _ scopes: [String]) {
        self.start = start
        self.length = length
        self.scopes = scopes
        kind = ScopeKinds.classify(scopes)
    }
}

/// Immutable, equatable line-boundary context for incremental convergence; not a serializable document or compiler state.
public struct GameEventScriptHighlightState: Equatable, Sendable {
    /// The grammar this state belongs to. States cannot be reused for another language.
    public let language: GameEventScriptSyntaxLanguage
    let frames: [Int]
}

/// A highlighting result. Failed resource bounds return no spans or usable continuation state.
public struct GameEventScriptHighlightResult: Sendable {
    /// Ordered, nonoverlapping ranges covering all input when successful.
    public let spans: [GameEventScriptHighlightSpan]
    /// Whether all input was highlighted within the resource limits.
    public var isComplete: Bool { nextState != nil }
    /// Context after the final line, or nil when incomplete. Do not cache an incomplete result.
    public let nextState: GameEventScriptHighlightState?
}

/// Invalid API input; malformed GES/GESA code is accepted and does not produce these errors.
public enum GameEventScriptHighlightingError: Error {
    /// The input-length bound must be positive.
    case invalidInputLimit
    /// A line API call contained an interior CR or LF.
    case expectedSingleLine
    /// The continuation state belongs to another language.
    case incompatibleState
    /// Rendering spans overlap, exceed the supplied text or split a Unicode scalar.
    case invalidSpans
    /// ANSI colors must be nil or one of 30...37, 39, 90...97.
    case invalidAnsiColor
}

enum ScopeKinds {
    static func classify(_ scopes: [String]) -> GameEventScriptSyntaxKind {
        for scope in scopes.reversed() {
            if scope.hasPrefix("comment") { return .comment }
            if scope.hasPrefix("string") || scope.hasPrefix("constant.character.escape") { return .string }
            if scope.hasPrefix("constant.numeric") || scope.hasPrefix("constant.language.numeric") { return .number }
            if scope.hasPrefix("variable.other.constant") { return .constant }
            if scope.hasPrefix("variable.other.register") { return .register }
            if scope.hasPrefix("variable.other.label") || scope.hasPrefix("constant.other.symbol") { return .label }
            if scope.hasPrefix("entity.name.type.class.message") || scope.hasPrefix("entity.name.function.handler") { return .message }
            if scope.hasPrefix("support.type") || scope.hasPrefix("entity.name.type") { return .type }
            if scope.hasPrefix("entity.name.namespace") { return .module }
            if scope.hasPrefix("entity.name.function") { return .function }
            if scope.hasPrefix("entity.other.attribute-name.tag") { return .tag }
            if scope.hasPrefix("support") || scope.hasPrefix("entity") { return .builtin }
            if scope.hasPrefix("keyword") || scope.hasPrefix("constant.language.boolean") { return .keyword }
            if scope.hasPrefix("variable") { return .identifier }
            if scope.hasPrefix("punctuation") { return .symbol }
        }
        return .plain
    }
}
