// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

// Validates parsed modules and collects their program-wide declarations, as GesAstValidator of the C# compiler
// together with the declaration checks of its GesProgramBuilder. Every rule reports into one ordered diagnostic list
// instead of stopping at the first error: the sources' own rules from the Syntax validator, program-wide duplicates and
// conflicts, then per module the rules of its records, predicates, functions and handlers that need other sources or
// the catalog, and last the module name.
final class GesAstValidator {
    let modules: [GesModule]
    let catalog: (any GesExternalTypeCatalog)?
    private(set) var moduleName = ""
    private(set) var constants: [String: GesExpression] = [:]
    // Callables by signature; a predicate wins over a function of the same signature, as in C#.
    private(set) var definitions: [String: GesDefinition] = [:]
    private(set) var records: [String: GesRecord] = [:]
    private var diagnostics: [GesDiagnostic] = []

    init(modules: [GesModule], catalog: (any GesExternalTypeCatalog)?, diagnostics: [GesDiagnostic] = []) {
        self.modules = modules
        self.catalog = catalog
        self.diagnostics = diagnostics
    }

    func validate() throws {
        declarations()
        for module in modules { validate(module) }
        try throwIfAny()
        resolveModuleName()
        try throwIfAny()
    }

    func report(_ code: String, _ location: GesSourceLocation, symbol: String? = nil, kind: GesDiagnosticSymbolKind = .unknown, message: String? = nil) {
        diagnostics += compileError(code, location, symbol: symbol, kind: kind, message: message).diagnostics
    }

    private func throwIfAny() throws {
        if !diagnostics.isEmpty { throw GesCompileError(diagnostics: diagnostics) }
    }

    // MARK: Program-wide declarations

    private func declarations() {
        for module in modules {
            for (name, value, location) in module.constants {
                if constants[name] != nil { report("validate.duplicateConstant", location, symbol: name, kind: .globalDefinition) } else { constants[name] = value }
            }
        }
        for module in modules {
            for record in module.records {
                if records[record.name] != nil { report("validate.duplicateType", record.location, symbol: record.name, kind: .type) } else { records[record.name] = record }
            }
        }
        for type in catalog?.types ?? [] {
            if let record = records[type.name] { report("validate.duplicateType", record.location, symbol: type.name, kind: .type) }
        }
        var predicates: [String: GesDefinition] = [:]
        var functions: [String: GesDefinition] = [:]
        var functionOrder: [String] = []
        for module in modules {
            for d in module.definitions where d.kind == "predicate" {
                if predicates[d.signature] != nil { report("validate.duplicatePredicate", d.location, symbol: d.name, kind: .predicate) } else { predicates[d.signature] = d }
            }
        }
        for module in modules {
            for d in module.definitions where d.kind == "function" {
                if functions[d.signature] != nil {
                    report("validate.duplicateFunction", d.location, symbol: d.name, kind: .function)
                } else {
                    functions[d.signature] = d
                    functionOrder.append(d.signature)
                }
            }
        }
        // One conflict per name, at the first function of that name.
        let predicateNames = Set(predicates.values.map(\.name))
        var reported: Set<String> = []
        for signature in functionOrder {
            let function = functions[signature]!
            if predicateNames.contains(function.name), reported.insert(function.name).inserted { report("validate.predicateFunctionConflict", function.location, symbol: function.name, kind: .globalDefinition) }
        }
        definitions = predicates
        for (signature, function) in functions where definitions[signature] == nil { definitions[signature] = function }
    }

    private func resolveModuleName() {
        for module in modules where !module.name.isEmpty {
            if moduleName.isEmpty {
                moduleName = module.name
            } else if moduleName != module.name {
                report(
                    "validate.invalidIdentifierCase",
                    module.location,
                    symbol: module.name,
                    kind: .globalDefinition,
                    message: "All source files compiled together must declare the same module name; expected '\(moduleName)', found '\(module.name)'."
                )
            }
        }
    }

    // MARK: Module declarations

    private func validate(_ module: GesModule) {
        for record in module.records {
            for field in record.fields {
                if !knownType(field.type) { report("validate.invalidTypeConstructor", field.location, symbol: field.type, kind: .type, message: "Unknown type ':\(field.type)' in this program.") }
                for value in [field.minimum, field.maximum, field.computed].compactMap({ $0 }) { expression(value, [:]) }
            }
        }
        for d in module.definitions where d.kind == "predicate" { callable(d, symbolKind: .predicate) }
        for d in module.definitions where d.kind == "function" { callable(d, symbolKind: .function) }
        for d in module.definitions where d.kind.hasSuffix("andler") { handler(d) }
    }

    private func callable(_ d: GesDefinition, symbolKind: GesDiagnosticSymbolKind) {
        parameterTypeHints(d)
        guard let value = d.expression else { return }
        let types = parameterTypes(d)
        expression(value, types)
        if d.kind == "predicate", !["boolean", "nothing"].contains(infer(value, types)) { report("validate.invalidPredicate", value.location, symbol: d.name, kind: .predicate) }
    }

    private func handler(_ d: GesDefinition) {
        parameterTypeHints(d)
        let scope = Scope(names: Set(d.parameters.map(\.name)), types: parameterTypes(d))
        statements(d.statements, scope)
    }

    private func parameterTypeHints(_ d: GesDefinition) {
        for p in d.parameters {
            if let type = p.type, !knownType(type) { report("validate.invalidTypeConstructor", p.location, symbol: type, kind: .type, message: "Unknown type ':\(type)' in this program.") }
        }
    }

    // A repeated parameter name, already reported, keeps its last declared type, as in C#.
    private func parameterTypes(_ d: GesDefinition) -> [String: String] { Dictionary(d.parameters.compactMap { p in p.type.map { (p.name, $0) } }, uniquingKeysWith: { _, last in last }) }

    func knownType(_ type: String) -> Bool {
        ["record", "number", "numeric", "nothing", "boolean", "percentage", "vector", "point", "series", "tag", "text", "list", "range", "message", "handler", "map", "dice", "unit", "integer", "fractional"].contains(type)
            || type.hasPrefix("quantity:")
            || records[type] != nil || catalog?.types.contains { $0.name == type } == true
    }

    // MARK: Statements

    // The bindings of one scope and the declared types visible in it; a child starts without bindings.
    struct Scope {
        var names: Set<String> = []
        var types: [String: String] = [:]

        func child() -> Scope { Scope(names: [], types: types) }
    }

    private func statements(_ list: [GesStatement], _ scope: Scope) {
        var scope = scope
        for s in list { statement(s, &scope) }
    }

    private func statement(_ s: GesStatement, _ scope: inout Scope) {
        switch s.kind {
        case .publish(_, let message, let tags):
            expression(message, scope.types)
            for tag in tags { expression(tag, scope.types) }
        case .assembly(let block):
            for line in block.lines { for operand in line.operands { expression(operand, scope.types) } }
            for declaration in block.declarations where declaration.export { bind(declaration.name, nil, s.location, &scope) }
        case .letBinding(let name, let value):
            expression(value, scope.types)
            bind(name, value, s.location, &scope)
        case .condition(let conditions, let then, let otherwise):
            var conditionScope = scope.child()
            for (binding, value, location) in conditions {
                expression(value, conditionScope.types)
                if let binding { bind(binding, value, location, &conditionScope) }
            }
            statements(then, conditionScope)
            statements(otherwise, scope.child())
        case .loop(let bindings, let source, _, let body):
            expression(source, scope.types)
            var loopScope = scope.child()
            if let first = bindings.first { loopScope.names.insert(first) }
            statements(body, loopScope.child())
        case .seeded(let seed, let body):
            expression(seed, scope.types)
            validateSeed(seed, scope.types)
            statements(body, scope.child())
        case .expression(let value): expression(value, scope.types)
        }
    }

    // Declares a let, if-let or exported assembly binding; a repeated name in the same scope, which the Syntax
    // validator reports, keeps the type of the first.
    private func bind(_ name: String, _ value: GesExpression?, _ location: GesSourceLocation, _ scope: inout Scope) {
        if scope.names.contains(name) { return }
        scope.names.insert(name)
        if let value { scope.types[name] = infer(value, scope.types) }
    }

    // MARK: Expressions

    // Visits an expression in the order of the C# ValidateExpressionReferences; names resolve during lowering.
    // The dispatcher binds no payloads: an unoptimized build reserves stack for the bindings of every case on each
    // call, so each form function reads its own.
    private func expression(_ e: GesExpression, _ types: [String: String]) {
        switch e.kind {
        case .literal, .constant, .dice, .series: break
        case .unary, .binary, .between, .range, .random, .combined, .list, .extensionCall, .assembly: operands(e, types)
        case .name, .handler, .member, .map, .generated: namedReferences(e, types)
        case .call, .message, .constructor: argumentReferences(e, types)
        case .predicate, .send, .intrinsic, .seeded, .check, .cast: checkedOperands(e, types)
        case .choice: choiceReferences(e, types)
        case .selector: selectorReferences(e, types)
        }
    }

    // Forms whose operands are visited without further checks.
    private func operands(_ e: GesExpression, _ types: [String: String]) {
        switch e.kind {
        case .unary(_, let a): expression(a, types)
        case .binary(_, let a, let b):
            expression(a, types)
            expression(b, types)
        case .between(let value, let minimum, let maximum):
            expression(value, types)
            expression(minimum, types)
            expression(maximum, types)
        case .range(let from, let to, let step):
            expression(from, types)
            expression(to, types)
            if let step { expression(step, types) }
        case .random(let from, let to):
            expression(from, types)
            expression(to, types)
        case .combined(_, let items), .list(let items): for item in items { expression(item, types) }
        case .extensionCall(_, _, let args): for argument in args { expression(argument.value, types) }
        case .assembly(let block): for line in block.lines { for operand in line.operands { expression(operand, types) } }
        default: preconditionFailure()
        }
    }

    // Forms that name variables, members, map keys or handlers.
    private func namedReferences(_ e: GesExpression, _ types: [String: String]) {
        switch e.kind {
        case .name, .handler: break
        case .member(let target, _): expression(target, types)
        case .map(let entries): for (_, value) in entries { expression(value, types) }
        case .generated(_, _, let source, _, let predicate, let projection):
            expression(source, types)
            if let predicate { expression(predicate, types) }
            expression(projection, types)
        default: preconditionFailure()
        }
    }

    private func argumentReferences(_ e: GesExpression, _ types: [String: String]) {
        switch e.kind {
        case .call(let name, let args):
            call(name, args, e.location)
            for argument in args { expression(argument.value, types) }
        case .message(_, let args): for argument in args { expression(argument.value, types) }
        case .constructor(let type, let args): constructor(type, args, e) { expression($0, types) }
        default: preconditionFailure()
        }
    }

    // Forms with a check of their own besides their operands.
    private func checkedOperands(_ e: GesExpression, _ types: [String: String]) {
        switch e.kind {
        case .predicate(let value, let name):
            predicateReference(name, count: value == nil ? 0 : 1, e.location)
            if let value { expression(value, types) }
        case .send(_, let message, let tags, let delay):
            expression(message, types)
            if let delay {
                expression(delay, types)
                sendDelay(delay, types)
            }
            for tag in tags { expression(tag, types) }
        // Geometric intrinsics are not visited, as in C#; clamp, min and max visit their operands.
        case .intrinsic(let name, let args): if Self.visitedIntrinsics.contains(name) { for arg in args { expression(arg, types) } }
        case .seeded(let seed, let body):
            expression(seed, types)
            validateSeed(seed, types)
            expression(body, types)
        // A cast or check may name a custom type that is bound only later, such as a record value a message delivers.
        case .check(let value, _), .cast(let value, _): expression(value, types)
        default: preconditionFailure()
        }
    }

    private func choiceReferences(_ e: GesExpression, _ types: [String: String]) {
        guard case .choice(let branches, let fallback) = e.kind else { preconditionFailure() }
        for (value, condition) in branches {
            expression(value, types)
            expression(condition, types)
        }
        expression(fallback, types)
    }

    private func selectorReferences(_ e: GesExpression, _ types: [String: String]) {
        guard case .selector(let target, let s) = e.kind else { preconditionFailure() }
        expression(target, types)
        selector(s, e.location) { expression($0, types) }
    }

    static let visitedIntrinsics: Set<String> = ["clamp", "min", "max"]

    private func predicateReference(_ name: String, count: Int, _ location: GesSourceLocation) {
        if definitions.values.filter({ $0.name == name && $0.kind == "predicate" && $0.parameters.count == count }).count != 1 {
            report("validate.invalidPredicate", location, symbol: name, kind: .predicate, message: "No unique predicate named '\(name)' with \(count) parameters is available.")
        }
    }

    private func sendDelay(_ delay: GesExpression, _ types: [String: String]) {
        if !["unknown", "other", "nothing", "quantity:s", "quantity:second"].contains(infer(delay, types)) {
            report("validate.invalidTypeConstructor", delay.location, symbol: "Quantity(s)", kind: .type, message: "Send delay requires a time quantity, for example 0.2s.")
        }
    }

    private func selector(_ s: GesSelector, _ location: GesSourceLocation, _ visit: (GesExpression) -> Void) {
        if s.operation == "choose" {
            if let predicate = s.expressions.first { visit(predicate) }
            if s.expressions.count > 1 { visit(s.expressions[1]) }
            return
        }
        s.expressions.forEach(visit)
    }

    // A call must name a declared signature; otherwise the name has another arity, or no callable exists when every
    // argument is positional. A labeled call to an unknown name is a handler call that lowering resolves.
    private func call(_ name: String, _ args: [GesArgument], _ location: GesSourceLocation) {
        if definitions[callableSignature(name, args.map(\.label))] != nil { return }
        if let definition = definitions.values.first(where: { $0.name == name }) {
            report(definition.kind == "predicate" ? "validate.wrongPredicateArity" : "validate.wrongFunctionArity", location, symbol: name, kind: definition.kind == "predicate" ? .predicate : .function)
        } else if args.allSatisfy({ $0.label == "_" }) {
            report("validate.missingCallable", location, symbol: name, kind: .globalDefinition)
        }
    }

    private func constructor(_ type: String, _ args: [GesArgument], _ e: GesExpression, _ visit: (GesExpression) -> Void) {
        for argument in args { visit(argument.value) }

        func invalid() { report("validate.invalidTypeConstructor", e.location, symbol: type, kind: .type) }

        if ["__split", "__splitWhitespace"].contains(type) { return }
        if ["record", "range", "series", "number", "message", "nothing", "percentage", "boolean", "text", "tag", "list", "map", "dice", "handler"].contains(type) {
            if !GesCompilerSupport.dataArguments(type, args.map(\.label)) { invalid() }
            return
        }
        if ["vector", "point"].contains(type) {
            let labeled = args.filter { $0.label != "_" }
            if labeled.isEmpty && (args.count == 1 || args.count == 2) { return }
            if !labeled.isEmpty && labeled.count != args.count { return invalid() }
            if !labeled.isEmpty || args.isEmpty {
                var previous = -1
                for argument in args {
                    guard let index = ["x", "y", "z"].firstIndex(of: argument.label), index > previous else { return invalid() }
                    previous = index
                }
                return
            }
            if args.count > 3 { invalid() }
            return
        }
        if ["numeric", "integer", "fractional", "unit"].contains(type) || type.hasPrefix("quantity:") {
            if args.count != 1 || args[0].label != "_" { invalid() }
            return
        }
        // Script records and external types take their constructor fields by label; unlabeled fields take positions.
        let labels: [String]
        if let record = records[type] {
            labels = record.fields.compactMap(\.label)
        } else if let external = catalog?.types.first(where: { $0.name == type }) {
            labels = external.fields.map(\.name)
        } else {
            return report("validate.invalidTypeConstructor", e.location, symbol: type, kind: .type, message: "Unknown type ':\(type)' in this program.")
        }
        if args.filter({ $0.label == "_" }).count > labels.filter({ $0 == "_" }).count { invalid() }
        let named = Set(labels.filter { $0 != "_" })
        for argument in args where argument.label != "_" && !named.contains(argument.label) { invalid() }
    }
}
