// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Token kinds of the C# lexer, named like its GesTokenKind cases. Keywords, punctuation and operators have
// their own kinds; contextual words such as `from` or `where` stay identifiers, as in C#. A `:word` that is not
// a selector keyword is a `selector`, which the C# lexer folds into Tag.
@_spi(Syntax) public enum GesTokenKind: Sendable {
    case endOfFile
    case illegal
    case newLine
    case message
    case identifier
    case typeName
    case constantReference
    case tag
    case selector
    case float
    case percentage
    case unitNumber
    case text
    case `true`
    case `false`
    case nothing
    case mathConstantPi
    case mathConstantE
    case mathConstantTau
    case mathConstantInfinity
    case intrinsicAbs
    case intrinsicLn
    case intrinsicExp
    case intrinsicSqrt
    case intrinsicCbrt
    case intrinsicChance
    case intrinsicFloor
    case intrinsicCeil
    case intrinsicTruncate
    case intrinsicRad
    case intrinsicDeg
    case intrinsicWrap
    case intrinsicRound
    case intrinsicSin
    case intrinsicCos
    case intrinsicTan
    case intrinsicAsin
    case intrinsicAcos
    case intrinsicAtan
    case intrinsicAtan2
    case intrinsicHypot
    case intrinsicDistance
    case intrinsicSquared
    case intrinsicLength
    case intrinsicNormalize
    case intrinsicDot
    case intrinsicCross
    case intrinsicAngle
    case keywordRandom
    case keywordParse
    case series
    case fibonacci
    case factorial
    case clamp
    case min
    case max
    case zip
    case roll
    case `default`
    case module
    case constant
    case record
    case predicate
    case function
    case on
    case emit
    case publish
    case matching
    case without
    case `let`
    case `as`
    case be
    case when
    case otherwise
    case has
    case empty
    case `if`
    case `else`
    case `for`
    case `in`
    case of
    case notIn
    case starts
    case ends
    case with
    case `is`
    case numeric
    case to
    case selectorAny
    case selectorAll
    case selectorFilter
    case selectorHas
    case selectorTake
    case selectorDrop
    case selectorCount
    case selectorChoose
    case selectorDraw
    case selectorShuffle
    case selectorReverse
    case selectorSum
    case selectorAverage
    case selectorSelect
    case selectorContains
    case selectorSort
    case dot
    case comma
    case semicolon
    case leftParen
    case rightParen
    case leftBrace
    case rightBrace
    case leftBracket
    case rightBracket
    case colon
    case underscore
    case operatorImplication
    case projectionArrow
    case operatorOr
    case operatorXor
    case operatorAnd
    case operatorCollectionUnion
    case operatorCollectionIntersect
    case operatorEqual
    case operatorNotEqual
    case operatorLess
    case operatorGreater
    case operatorLessOrEqual
    case operatorGreaterOrEqual
    case operatorPlus
    case operatorMinus
    case operatorMultiply
    case operatorDivide
    case operatorIntegerDivide
    case operatorModulo
    case operatorRemainder
    case operatorPower
    case superscriptInteger
    case operatorNot
}

// `text` is the token's normalized value, such as a decoded Text literal or `*` for `×`. Offsets are UTF-8 offsets of
// the lexed source: [fullStart, start) is the leading trivia (inline whitespace, comments and a leading BOM) and
// [start, end) the token's own spelling, so the tokens of a source, ending with endOfFile, cover it without gaps.
@_spi(Syntax) public struct GesToken: Sendable {
    @_spi(Syntax) public let kind: GesTokenKind
    @_spi(Syntax) public let text: String
    // A lowercase source word: an identifier or a keyword. Some positions, such as argument labels and extension
    // function names, accept keywords as names.
    @_spi(Syntax) public let isWord: Bool
    @_spi(Syntax) public let line: Int
    @_spi(Syntax) public let column: Int
    @_spi(Syntax) public let endLine: Int
    @_spi(Syntax) public let endColumn: Int
    @_spi(Syntax) public let fullStart: Int
    @_spi(Syntax) public let start: Int
    @_spi(Syntax) public let end: Int

    @_spi(Syntax) public init(
        kind: GesTokenKind,
        text: String,
        isWord: Bool,
        line: Int,
        column: Int,
        endLine: Int,
        endColumn: Int,
        fullStart: Int,
        start: Int,
        end: Int
    ) {
        self.kind = kind
        self.text = text
        self.isWord = isWord
        self.line = line
        self.column = column
        self.endLine = endLine
        self.endColumn = endColumn
        self.fullStart = fullStart
        self.start = start
        self.end = end
    }
}

extension GesToken {
    // A contextual word: an identifier with this spelling, as C# MatchWord compares it.
    @_spi(Syntax) public func isIdentifier(_ word: String) -> Bool { kind == .identifier && text == word }

    @_spi(Syntax) public var isNumber: Bool { kind == .float || kind == .percentage || kind == .unitNumber }

    // Every identifier token, including the non-word `°` spelling of `degree`, as C# GesTokenKind.Identifier.
    @_spi(Syntax) public var isName: Bool { kind == .identifier }
}

extension GesTokenKind {
    // A `:word`: a selector keyword or any other selector name.
    @_spi(Syntax) public var isSelector: Bool {
        switch self {
        case .selector, .selectorAny, .selectorAll, .selectorFilter, .selectorHas, .selectorTake, .selectorDrop, .selectorCount, .selectorChoose, .selectorDraw, .selectorShuffle,
            .selectorReverse, .selectorSum, .selectorAverage, .selectorSelect, .selectorContains, .selectorSort:
            true
        default: false
        }
    }
}
