// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// Validates parsed modules and collects their program-wide declarations, as GesAstValidator of the C# compiler
// together with the declaration checks of its GesProgramBuilder. Every rule reports into one ordered diagnostic list
// instead of stopping at the first error: program-wide duplicates and conflicts, then per module its records,
// predicates, functions and handlers followed by GesShadowingValidator, and last the module name.
final class GesAstValidator {
    let modules: [GesModule]
    let catalog: (any GesExternalTypeCatalog)?
    private(set) var moduleName = ""
    private(set) var constants: [String: GesExpression] = [:]
    // Callables by signature; a predicate wins over a function of the same signature, as in C#.
    private(set) var definitions: [String: GesDefinition] = [:]
    private(set) var records: [String: GesRecord] = [:]
    private var diagnostics: [GesDiagnostic] = []

    init(modules: [GesModule], catalog: (any GesExternalTypeCatalog)?) {
        self.modules = modules
        self.catalog = catalog
    }

    func validate() throws {
        declarations()
        for module in modules {
            validate(module)
            diagnostics += GesShadowingValidator().validate(module)
        }
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
            for (name, value) in module.constants {
                if constants[name] != nil { report("validate.duplicateConstant", value.location, symbol: name, kind: .globalDefinition) } else { constants[name] = value }
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
            removedType(record.name, record.location)
            var names: Set<String> = []
            for field in record.fields {
                if !Self.isLowerName(field.name, index: false) { report("validate.invalidIdentifierCase", field.location, symbol: field.name, kind: .variable) }
                // Swift additionally rejects repeated fields and unknown field types.
                if !names.insert(field.name).inserted { report("validate.duplicateVariable", field.location, symbol: field.name, kind: .variable) }
                if !knownType(field.type) { report("validate.invalidTypeConstructor", field.location, symbol: field.type, kind: .type, message: "Unknown type ':\(field.type)' in this program.") }
                for value in [field.minimum, field.maximum, field.computed].compactMap({ $0 }) { expression(value, [:]) }
            }
        }
        for d in module.definitions where d.kind == "predicate" { callable(d, symbolKind: .predicate) }
        for d in module.definitions where d.kind == "function" { callable(d, symbolKind: .function) }
        for d in module.definitions where d.kind.hasSuffix("andler") { handler(d) }
    }

    private func callable(_ d: GesDefinition, symbolKind: GesDiagnosticSymbolKind) {
        if !Self.isLowerName(d.name, index: false) { report("validate.invalidIdentifierCase", d.location, symbol: d.name, kind: symbolKind) }
        parameters(d, symbolKind: symbolKind, duplicateCode: "validate.duplicateDefinitionParameter")
        guard let value = d.expression else { return }
        let types = parameterTypes(d)
        expression(value, types)
        if d.kind == "predicate", !["boolean", "nothing"].contains(infer(value, types)) { report("validate.invalidPredicate", value.location, symbol: d.name, kind: .predicate) }
    }

    private func handler(_ d: GesDefinition) {
        if d.name == "initialization" {
            if d.kind != "handler" || !d.parameters.isEmpty {
                report("validate.invalidMessageCase", d.location, symbol: d.name, kind: .handler, message: "System endpoint 'initialization' must use parameterless syntax.")
            } else if !d.required.isEmpty || !d.excluded.isEmpty {
                report("validate.invalidMessageCase", d.location, symbol: d.name, kind: .handler, message: "System endpoint 'initialization' cannot use tag filters.")
            }
        } else if d.name == "undeliverable" {
            if d.kind != "messageNameHandler" {
                report("validate.invalidMessageCase", d.location, symbol: d.name, kind: .handler, message: "System endpoint 'undeliverable' must use 'as message' syntax.")
            } else if let parameter = d.parameters.first, parameter.label != "message" || parameter.type != "message" {
                report("validate.invalidMessageCase", parameter.location, symbol: parameter.name, kind: .handler)
            }
        } else if !Self.isUpperName(d.name) {
            report("validate.invalidMessageCase", d.location, symbol: d.name, kind: .handler)
        }
        parameters(d, symbolKind: .handler, duplicateCode: "validate.duplicateHandlerParameter")
        let scope = Scope(names: Set(d.parameters.map(\.name)), types: parameterTypes(d))
        statements(d.statements, scope)
    }

    // Parameter names, type hints and repeated names, in this order, as the C# validator checks them.
    private func parameters(_ d: GesDefinition, symbolKind: GesDiagnosticSymbolKind, duplicateCode: String) {
        for p in d.parameters where !Self.isLowerName(p.name, index: true) { report("validate.invalidIdentifierCase", p.location, symbol: d.name, kind: symbolKind) }
        for p in d.parameters {
            if let type = p.type, !knownType(type) { report("validate.invalidTypeConstructor", p.location, symbol: type, kind: .type, message: "Unknown type ':\(type)' in this program.") }
        }
        var seen: Set<String> = []
        var repeated: Set<String> = []
        for p in d.parameters where !seen.insert(p.name).inserted && repeated.insert(p.name).inserted { report(duplicateCode, p.location, symbol: d.name, kind: symbolKind) }
    }

    // A repeated parameter name, already reported, keeps its last declared type, as in C#.
    private func parameterTypes(_ d: GesDefinition) -> [String: String] { Dictionary(d.parameters.compactMap { p in p.type.map { (p.name, $0) } }, uniquingKeysWith: { _, last in last }) }

    func knownType(_ type: String) -> Bool {
        ["record", "number", "numeric", "nothing", "boolean", "percentage", "vector", "point", "series", "tag", "text", "list", "range", "message", "handler", "map", "dice", "unit", "integer", "fractional"].contains(type)
            || type.hasPrefix("quantity:")
            || records[type] != nil || catalog?.types.contains { $0.name == type } == true
    }

    private func removedType(_ type: String, _ location: GesSourceLocation) {
        if ["optional", "set", "uuid", "ref"].contains(type) { report("validate.invalidTypeConstructor", location, symbol: type, kind: .type, message: "Type ':\(type)' is not supported.") }
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
            for (binding, value) in conditions {
                expression(value, conditionScope.types)
                if let binding { bind(binding, value, value.location, &conditionScope) }
            }
            statements(then, conditionScope)
            statements(otherwise, scope.child())
        case .loop(let bindings, let source, _, let body):
            expression(source, scope.types)
            if let first = bindings.first, !Self.isLowerName(first, index: true) { report("validate.invalidIdentifierCase", s.location, symbol: first, kind: .variable) }
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

    // Declares a let, if-let or exported assembly binding; a repeated name in the same scope is reported, not declared.
    private func bind(_ name: String, _ value: GesExpression?, _ location: GesSourceLocation, _ scope: inout Scope) {
        if !Self.isLowerName(name, index: true) { report("validate.invalidIdentifierCase", location, symbol: name, kind: .variable) }
        if scope.names.contains(name) {
            report("validate.duplicateVariable", location, symbol: name, kind: .variable)
            return
        }
        scope.names.insert(name)
        if let value { scope.types[name] = infer(value, scope.types) }
    }

    // MARK: Expressions

    // Visits an expression in the order of the C# ValidateExpressionReferences; names resolve during lowering.
    private func expression(_ e: GesExpression, _ types: [String: String]) {
        func visit(_ child: GesExpression) { expression(child, types) }

        switch e.kind {
        case .assembly(let block): for line in block.lines { line.operands.forEach(visit) }
        case .literal, .constant, .dice, .series: break
        case .name(let name): if !Self.isLowerName(name, index: true) { report("validate.invalidIdentifierCase", e.location, symbol: name, kind: .variable) }
        case .call(let name, let args):
            if !Self.isLowerName(name, index: false) { report("validate.invalidIdentifierCase", e.location, symbol: name, kind: .globalDefinition) }
            repeatedArguments(args, name, e.location)
            call(name, args, e.location)
            labeledArguments(args, name, e.location, visit)
        case .handler(let name, let parameters):
            if !Self.isUpperName(name) { report("validate.invalidMessageCase", e.location, symbol: name, kind: .handler) }
            for p in parameters where p.label != "_" && !Self.isLowerName(p.label, index: false) { report("validate.invalidIdentifierCase", e.location, symbol: name, kind: .handler) }
            var seen: Set<String> = []
            var repeated: Set<String> = []
            for p in parameters where !seen.insert(p.name).inserted && repeated.insert(p.name).inserted { report("validate.duplicateHandlerParameter", e.location, symbol: name, kind: .handler) }
        case .message(let name, let args):
            if !Self.isUpperName(name) { report("validate.invalidMessageCase", e.location, symbol: name, kind: .message) }
            repeatedArguments(args, name, e.location)
            labeledArguments(args, name, e.location, visit)
        case .extensionCall(_, _, let args): args.map(\.value).forEach(visit)
        case .constructor(let type, let args): constructor(type, args, e, visit)
        case .predicate(let value, let name):
            if !Self.isLowerName(name, index: false) { report("validate.invalidIdentifierCase", e.location, symbol: name, kind: .predicate) }
            let count = value == nil ? 0 : 1
            if definitions.values.filter({ $0.name == name && $0.kind == "predicate" && $0.parameters.count == count }).count != 1 {
                report("validate.invalidPredicate", e.location, symbol: name, kind: .predicate, message: "No unique predicate named '\(name)' with \(count) parameters is available.")
            }
            if let value { visit(value) }
        case .send(_, let message, let tags, let delay):
            visit(message)
            if let delay {
                visit(delay)
                if !["unknown", "other", "nothing", "quantity:s", "quantity:second"].contains(infer(delay, types)) {
                    report("validate.invalidTypeConstructor", delay.location, symbol: "Quantity(s)", kind: .type, message: "Send delay requires a time quantity, for example 0.2s.")
                }
            }
            tags.forEach(visit)
        case .unary(_, let a): visit(a)
        // Geometric intrinsics are not visited, as in C#; clamp, min and max visit their operands.
        case .intrinsic(let name, let args): if ["clamp", "min", "max"].contains(name) { args.forEach(visit) }
        case .between(let value, let minimum, let maximum): [value, minimum, maximum].forEach(visit)
        case .range(let from, let to, let step): ([from, to] + (step.map { [$0] } ?? [])).forEach(visit)
        case .random(let from, let to): [from, to].forEach(visit)
        case .seeded(let seed, let body):
            visit(seed)
            validateSeed(seed, types)
            visit(body)
        case .generated(_, let name, let source, _, let predicate, let projection):
            if !Self.isLowerName(name, index: true) { report("validate.invalidIdentifierCase", e.location, symbol: name, kind: .variable) }
            visit(source)
            if let predicate { visit(predicate) }
            visit(projection)
        case .choice(let branches, let fallback):
            for (value, condition) in branches {
                visit(value)
                visit(condition)
            }
            visit(fallback)
        case .binary(_, let a, let b): [a, b].forEach(visit)
        case .check(let value, let type), .cast(let value, let type):
            removedType(type, e.location)
            visit(value)
        case .member(let target, let name):
            if !Self.isLowerName(name, index: false) { report("validate.invalidIdentifierCase", e.location, symbol: name, kind: .variable) }
            visit(target)
        case .selector(let target, let s):
            visit(target)
            selector(s, e.location, visit)
        case .combined(_, let sources): sources.forEach(visit)
        case .list(let items): items.forEach(visit)
        case .map(let entries):
            for (key, value) in entries {
                if !Self.isLowerName(key, index: false) { report("validate.invalidIdentifierCase", e.location, symbol: key, kind: .variable) }
                visit(value)
            }
        }
    }

    private func selector(_ s: GesSelector, _ location: GesSourceLocation, _ visit: (GesExpression) -> Void) {
        func binding(_ name: String) { if !name.isEmpty && !Self.isLowerName(name, index: true) { report("validate.invalidIdentifierCase", location, symbol: name, kind: .variable) } }

        switch s.operation {
        case "fold", "reduce":
            binding(s.accumulator)
            binding(s.name)
        case "choose":
            binding(s.name)
            if let predicate = s.expressions.first { visit(predicate) }
            binding(s.weightName)
            if s.expressions.count > 1 { visit(s.expressions[1]) }
            return
        case "any", "all", "filter", "count", "sum", "average", "select", "min", "max", "first", "last", "single", "map", "group", "order", "distinct":
            if !s.expressions.isEmpty { binding(s.name) }
        default: break
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

    // Each repeated label once, at its second use.
    private func repeatedArguments(_ args: [GesArgument], _ name: String, _ location: GesSourceLocation) {
        var seen: Set<String> = []
        var repeated: Set<String> = []
        for argument in args where argument.label != "_" && !seen.insert(argument.label).inserted && repeated.insert(argument.label).inserted {
            report("validate.duplicatePublishArgument", location, symbol: name, kind: .message)
        }
    }

    private func labeledArguments(_ args: [GesArgument], _ name: String, _ location: GesSourceLocation, _ visit: (GesExpression) -> Void) {
        for argument in args {
            if argument.label != "_" && !Self.isLowerName(argument.label, index: false) { report("validate.invalidIdentifierCase", location, symbol: name) }
            visit(argument.value)
        }
    }

    private func constructor(_ type: String, _ args: [GesArgument], _ e: GesExpression, _ visit: (GesExpression) -> Void) {
        repeatedArguments(args, type, e.location)
        labeledArguments(args, type, e.location, visit)

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

    // MARK: Names

    // A lowercase ASCII name of letters and digits; variables may end in one canonical `_index` suffix.
    static func isLowerName(_ name: String, index allowsIndex: Bool) -> Bool {
        let scalars = Array(name.unicodeScalars)
        guard let first = scalars.first, GesLexer.lower(first) else { return false }
        var index = 1
        while index < scalars.count && (GesLexer.letter(scalars[index]) || GesLexer.digit(scalars[index])) { index += 1 }
        if index == scalars.count { return true }
        guard allowsIndex, scalars[index] == "_", index + 1 < scalars.count else { return false }
        index += 1
        if scalars[index] == "0" { return index + 1 == scalars.count }
        return scalars[index...].allSatisfy(GesLexer.digit)
    }

    static func isUpperName(_ name: String) -> Bool {
        guard let first = name.unicodeScalars.first, GesLexer.upper(first) else { return false }
        return name.unicodeScalars.dropFirst().allSatisfy { GesLexer.letter($0) || GesLexer.digit($0) }
    }
}
