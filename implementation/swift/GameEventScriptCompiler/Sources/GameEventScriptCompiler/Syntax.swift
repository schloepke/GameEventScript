// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

struct GesSource {
    let text: String
    let name: String
    let id: UInt32
}

// Lexical token categories. Keywords and punctuation stay identifiers or symbols and are matched by text.
enum GesTokenKind {
    case endOfFile
    case illegal
    case newLine
    case identifier
    case message
    case typeName
    case constantReference
    case selector
    case tag
    case number
    case text
    case symbol
    case superscript
}

struct GesToken {
    // Decoded Text content never participates in keyword or punctuation matching.
    var syntaxText: String { kind == .text ? "" : text }
    let kind: GesTokenKind
    let text: String
    let line: Int
    let column: Int
    let endLine: Int
    let endColumn: Int
    let start: Int
    let end: Int
}

// Unary operators of the source language; `predicate` wraps an extension predicate call.
enum GesUnaryOperator {
    case parse
    case negate
    case not
    case hasValue
    case empty
    case chance
    case abs
    case naturalLog
    case exp
    case floor
    case ceil
    case truncate
    case roundHalfEven
    case roundHalfUp
    case roundHalfDown
    case degreeToRadians
    case degreeFromRadians
    case wrapDegree
    case sin
    case cos
    case tan
    case asin
    case acos
    case atan
    case predicate

    // Function-style prefix operator after multi-word forms such as `wrap degree` are resolved.
    init?(prefixText: String) {
        switch prefixText {
        case "abs": self = .abs
        case "ln": self = .naturalLog
        case "exp": self = .exp
        case "chance": self = .chance
        case "floor": self = .floor
        case "ceil": self = .ceil
        case "truncate": self = .truncate
        case "rad": self = .degreeToRadians
        case "deg": self = .degreeFromRadians
        case "wrapDegree": self = .wrapDegree
        case "roundHalfEven": self = .roundHalfEven
        case "roundHalfUp": self = .roundHalfUp
        case "roundHalfDown": self = .roundHalfDown
        case "sin": self = .sin
        case "cos": self = .cos
        case "tan": self = .tan
        case "asin": self = .asin
        case "acos": self = .acos
        case "atan": self = .atan
        default: return nil
        }
    }
}

// Binary operators of the source language. `notContains` keeps `not in` a single operator.
enum GesBinaryOperator {
    case or
    case xor
    case and
    case implies
    case equal
    case notEqual
    case less
    case greater
    case lessOrEqual
    case greaterOrEqual
    case add
    case subtract
    case multiply
    case divide
    case integerDivide
    case modulo
    case remainder
    case power
    case `default`
    case contains
    case notContains
    case containsValue
    case startsWith
    case endsWith
    case union
    case intersect
    case zip

    // Operator spelled by an infix source token, after multi-word forms are resolved.
    init?(sourceText: String) {
        switch sourceText {
        case "or": self = .or
        case "xor": self = .xor
        case "and": self = .and
        case "->": self = .implies
        case "=": self = .equal
        case "<>": self = .notEqual
        case "<": self = .less
        case ">": self = .greater
        case "<=": self = .lessOrEqual
        case ">=": self = .greaterOrEqual
        case "+": self = .add
        case "-": self = .subtract
        case "*": self = .multiply
        case "/": self = .divide
        case "div": self = .integerDivide
        case "mod": self = .modulo
        case "rem": self = .remainder
        case "^": self = .power
        case "default": self = .default
        case "in": self = .contains
        case "not in": self = .notContains
        case "inValues": self = .containsValue
        case "startsWith": self = .startsWith
        case "endsWith": self = .endsWith
        case "|": self = .union
        case "&": self = .intersect
        case "zip": self = .zip
        default: return nil
        }
    }
}

struct GesParameter {
    let label: String
    let name: String
    let type: String?
    let location: GesSourceLocation
}

struct GesArgument {
    let label: String
    let value: GesExpression
}

final class GesExpression {
    enum Kind {
        case assembly(GesAssembly)
        case literal(GesValue)
        case name(String)
        case constant(String)
        case unary(GesUnaryOperator, GesExpression)
        case binary(GesBinaryOperator, GesExpression, GesExpression)
        case between(GesExpression, GesExpression, GesExpression)
        case call(String, [GesArgument])
        case extensionCall(String, String, [GesArgument])
        case constructor(String, [GesArgument])
        case send(Bool, GesExpression, [GesExpression], GesExpression?)
        case message(String, [GesArgument])
        case handler(String, [GesParameter])
        case list([GesExpression])
        case map([(String, GesExpression)])
        case member(GesExpression, String)
        case selector(GesExpression, GesSelector)
        case combined(String, [GesExpression])
        case cast(GesExpression, String)
        case check(GesExpression, String)
        case predicate(GesExpression?, String)
        case intrinsic(String, [GesExpression])
        case range(GesExpression, GesExpression, GesExpression?)
        case random(GesExpression, GesExpression)
        case seeded(GesExpression, GesExpression)
        case dice(Int, Int)
        case series(String)
        case choice([(GesExpression, GesExpression)], GesExpression)
        case generated(String, String, GesExpression, Bool, GesExpression?, GesExpression)
    }

    let kind: Kind
    let location: GesSourceLocation
    var depth: Int
    var decimalLiteral = false

    init(_ kind: Kind, _ location: GesSourceLocation, depth: Int = 1) {
        self.kind = kind
        self.location = location
        self.depth = depth
    }
}

struct GesSelector {
    var operation: String
    var name = "value"
    var names: [String] = []
    var weightNames: [String] = []
    var expressions: [GesExpression] = []
    var count = 0
    var mode = ""
    var weightName = ""
    var accumulator = ""
}

struct GesStatement {
    indirect enum Kind {
        case assembly(GesAssembly)
        case expression(GesExpression)
        case letBinding(String, GesExpression)
        case publish(Bool, GesExpression, [GesExpression])
        case condition([(String?, GesExpression)], [GesStatement], [GesStatement])
        case loop([String], GesExpression, Bool, [GesStatement])
        case seeded(GesExpression, [GesStatement])
    }

    let kind: Kind
    let location: GesSourceLocation
}

struct GesDefinition {
    let kind: String
    let name: String
    let parameters: [GesParameter]
    let expression: GesExpression?
    let statements: [GesStatement]
    let required: [String]
    let excluded: [String]
    let location: GesSourceLocation
    var signature: String { name + "(" + parameters.map(\.label).joined(separator: ",") + ")" }
}

struct GesField {
    let name: String
    let type: String
    let label: String?
    let minimum: GesExpression?
    let maximum: GesExpression?
    let computed: GesExpression?
    let location: GesSourceLocation
}

struct GesRecord {
    let name: String
    let fields: [GesField]
    let location: GesSourceLocation
}

struct GesModule {
    let name: String
    let definitions: [GesDefinition]
    let constants: [(String, GesExpression)]
    let records: [GesRecord]
    let location: GesSourceLocation
}

func compileError(_ phase: GesDiagnosticPhase, _ code: String, _ message: String, _ location: GesSourceLocation, symbol: String? = nil, kind: GesDiagnosticSymbolKind = .unknown) -> GesCompileError {
    .init(diagnostics: [.init(phase: phase, code: code, message: message, symbol: symbol, symbolKind: kind, sourceLocation: location, programName: location.moduleName)])
}
