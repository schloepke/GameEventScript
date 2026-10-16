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

// Binary operators of the source language. `not in` parses as `not` applied to `contains`, as in C#.
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

    // Name of an argument register in bindings and debug symbols: a message-name handler names its single
    // argument after the local binding, every other routine after the argument label, as in C#.
    func argumentName(_ parameter: GesParameter) -> String { kind == "messageNameHandler" ? parameter.name : parameter.label }
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

// Callable overload identity: the name and its ordered argument labels, as C# GesCallableSignatures.
func callableSignature(_ name: String, _ labels: [String]) -> String { name + "(" + labels.joined(separator: ",") + ")" }

// A diagnostic of any compiler phase with its standard message, unless `message` replaces it.
func compileError(_ code: String, _ location: GesSourceLocation, symbol: String? = nil, kind: GesDiagnosticSymbolKind = .unknown, phase: GesDiagnosticPhase = .validate, message: String? = nil) -> GesCompileError {
    compileError(phase, code, message ?? diagnosticMessage(code, symbol: symbol), location, symbol: symbol, kind: kind)
}

private func diagnosticMessage(_ code: String, symbol: String?) -> String {
    let name = symbol.map { "'\($0)'" } ?? "this expression"
    switch code {
    case "validate.duplicateConstant": return "Constant \(name) is already declared."
    case "validate.duplicateType": return "Type \(name) is already declared."
    case "validate.duplicateFunction": return "Function \(name) already has an overload with these argument labels."
    case "validate.duplicatePredicate": return "Predicate \(name) already has an overload with these argument labels."
    case "validate.duplicateVariable": return "Binding \(name) is declared more than once in this scope."
    case "validate.shadowedVariable": return "Binding \(name) would shadow a binding in an enclosing scope. Use a different name."
    case "validate.duplicateDefinitionParameter", "validate.duplicateHandlerParameter": return "The parameter list for \(name) contains a duplicate name or label."
    case "validate.duplicatePublishArgument": return "Message or call \(name) contains a repeated argument label."
    case "validate.invalidIdentifierCase": return "Identifier \(name) does not follow the naming rules for this declaration."
    case "validate.invalidMessageCase": return "Invalid handler declaration \(name). Check its name and parameter form."
    case "validate.invalidPredicate": return "Predicate \(name) must produce a Boolean value or nothing."
    case "validate.invalidTypeConstructor": return "Invalid arguments for type constructor \(name). Check the argument count and labels."
    case "validate.predicateFunctionConflict": return "A function and a predicate cannot share the name \(name)."
    case "validate.missingCallable": return "No function or predicate matches \(name). Check the name and argument labels."
    case "validate.wrongFunctionArity", "validate.wrongPredicateArity", "compile.invalidArity": return "The arguments for \(name) do not match its declared parameters."
    case "compile.unresolvedSymbol": return "Unknown symbol \(name). Declare it in this program before using it."
    case "compile.cyclicCallGraph": return "The call graph contains a cycle involving \(name). Recursive calls are not supported."
    case "compile.numericLimitExceeded": return "Compilation exceeds a numeric limit of the program format near \(name). Reduce the program size or complexity."
    case "compile.unsupportedConstruct": return "The compiler cannot generate code for \(name)."
    default: return "Compilation failed near \(name). See the diagnostic code for the failure category."
    }
}
