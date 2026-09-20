// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

struct GesSource {
    let text: String
    let name: String
    let id: UInt32
}
struct GesToken {
    // Decoded Text content never participates in keyword or punctuation matching.
    var syntaxText: String { kind == "text" ? "" : text }
    let kind: String
    let text: String
    let line: Int
    let column: Int
    let endLine: Int
    let endColumn: Int
    let start: Int
    let end: Int
}
struct GesParameter {
    let label: String
    let name: String
    let type: String?
    let location: GameEventScriptSourceLocation
}
struct GesArgument {
    let label: String
    let value: GesExpression
}
final class GesExpression {
    enum Kind {
        case literal(GesValue)
        case name(String)
        case constant(String)
        case unary(String, GesExpression)
        case binary(String, GesExpression, GesExpression)
        case call(String, [GesArgument])
        case extensionCall(String, String, [GesArgument])
        case constructor(String, [GesArgument])
        case message(String, [GesArgument])
        case handler(String, [GesParameter])
        case list([GesExpression])
        case map([(String, GesExpression)])
        case member(GesExpression, String)
        case selector(GesExpression, GesSelector)
        case cast(GesExpression, String)
        case check(GesExpression, String)
        case predicate(GesExpression, String)
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
    let location: GameEventScriptSourceLocation
    var depth: Int
    var decimalLiteral = false
    init(_ kind: Kind, _ location: GameEventScriptSourceLocation, depth: Int = 1) {
        self.kind = kind
        self.location = location
        self.depth = depth
    }
}
struct GesSelector {
    var operation: String
    var name = "value"
    var expressions: [GesExpression] = []
    var count = 0
    var mode = ""
    var weightName = ""
}
struct GesStatement {
    indirect enum Kind {
        case expression(GesExpression)
        case letBinding(String, GesExpression)
        case publish(Bool, GesExpression, [GesExpression])
        case condition(GesExpression, [GesStatement], [GesStatement])
        case loop(String, GesExpression, Bool, [GesStatement])
        case seeded(GesExpression, [GesStatement])
    }
    let kind: Kind
    let location: GameEventScriptSourceLocation
}
struct GesDefinition {
    let kind: String
    let name: String
    let parameters: [GesParameter]
    let expression: GesExpression?
    let statements: [GesStatement]
    let required: [String]
    let excluded: [String]
    let location: GameEventScriptSourceLocation
    var signature: String { name + "(" + parameters.map(\.label).joined(separator: ",") + ")" }
}
struct GesField {
    let name: String
    let type: String
    let label: String?
    let minimum: GesExpression?
    let maximum: GesExpression?
    let computed: GesExpression?
    let location: GameEventScriptSourceLocation
}
struct GesRecord {
    let name: String
    let fields: [GesField]
    let location: GameEventScriptSourceLocation
}
struct GesModule {
    let name: String
    let definitions: [GesDefinition]
    let constants: [(String, GesExpression)]
    let records: [GesRecord]
    let location: GameEventScriptSourceLocation
}

func compileError(
    _ phase: GameEventScriptDiagnosticPhase, _ code: String, _ message: String,
    _ location: GameEventScriptSourceLocation, symbol: String? = nil,
    kind: GameEventScriptSymbolKind = .unknown
) -> GameEventScriptCompileError {
    .init(diagnostics: [
        .init(
            phase: phase, code: code, message: message, symbol: symbol,
            symbolKind: kind, sourceLocation: location, programName: location.moduleName)
    ])
}
