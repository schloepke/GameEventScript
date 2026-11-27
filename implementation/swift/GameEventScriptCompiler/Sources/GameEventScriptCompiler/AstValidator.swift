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
    // The handle types by name without `@`; the validator checks every `@Name` against it.
    let handleTypes: [String: GesHandleType]
    // The handle type names in declaration order.
    let handleTypeOrder: [String]
    private(set) var moduleName = ""
    private(set) var constants: [String: GesExpression] = [:]
    // Callables by signature; a predicate wins over a function of the same signature, as in C#.
    private(set) var definitions: [String: GesDefinition] = [:]
    private(set) var records: [String: GesRecord] = [:]
    private var diagnostics: [GesDiagnostic] = []
    // The result types of callables whose inference met no cycle. Such a result depends only on the callee, so every
    // later inference reuses it instead of inferring the callee again; see CallableResults.
    var callResults: [String: String] = [:]

    init(modules: [GesModule], catalog: (any GesExternalTypeCatalog)?, handleTypes: [String: GesHandleType] = [:], handleTypeOrder: [String]? = nil, diagnostics: [GesDiagnostic] = []) {
        self.modules = modules
        self.catalog = catalog
        self.handleTypes = handleTypes
        self.handleTypeOrder = handleTypeOrder ?? handleTypes.keys.sorted()
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
                if GesHandleTypeNames.isHandleType(field.type) {
                    handleTypeName(field.type, field.location)
                } else if !knownType(field.type) {
                    report("validate.invalidTypeConstructor", field.location, symbol: field.type, kind: .type, message: "Unknown type ':\(field.type)' in this program.")
                }
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
            if let type = p.type, GesHandleTypeNames.isHandleType(type) {
                handleTypeName(type, p.location)
            } else if let type = p.type, !parameterType(type) {
                report("validate.invalidTypeConstructor", p.location, symbol: type, kind: .type, message: "Unknown type ':\(type)' in this program.")
            }
        }
    }

    // A repeated parameter name, already reported, keeps its last declared type, as in C#.
    private func parameterTypes(_ d: GesDefinition) -> [String: String] { Dictionary(d.parameters.compactMap { p in p.type.map { (p.name, $0) } }, uniquingKeysWith: { _, last in last }) }

    func tagLiteral(_ tag: GesExpression) {
        var value = tag.kind
        if case .list(let items) = value { return items.forEach(tagLiteral) }
        if case .constant(let name) = value, let constant = constants[name] { value = constant.kind }
        if case .literal(let literal) = value, literal.valueKind == .nothing {
            report("validate.invalidTag", tag.location, symbol: "nothing", kind: .type, message: "The value nothing is no tag.")
        }
    }

    // A parameter hint names a type a value can be converted to, as in C#: a built-in constructor type, a quantity with
    // a known unit, a script record or a catalog type; `:Record` and the numeric helpers are no conversion targets.
    func parameterType(_ type: String) -> Bool {
        ["nothing", "tag", "text", "percentage", "vector", "point", "boolean", "number", "numeric", "series", "list", "range", "message", "handler", "map", "dice", "bytes"].contains(type)
            || type.hasPrefix("quantity:") && GesCompilerSupport.quantityUnit(String(type.dropFirst(9))) != nil
            || records[type] != nil || catalog?.types.contains { $0.name == type } == true
    }

    func knownType(_ type: String) -> Bool {
        ["record", "number", "numeric", "nothing", "boolean", "percentage", "vector", "point", "series", "tag", "text", "list", "range", "message", "handler", "map", "dice", "bytes", "unit", "integer", "fractional"].contains(type)
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
            for tag in tags {
                tagLiteral(tag)
                expression(tag, scope.types)
            }
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
        case .predicate, .send, .cancel, .intrinsic, .seeded, .check, .cast: checkedOperands(e, types)
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
        case .member(let target, let member):
            if case let type = infer(target, types), GesHandleTypeNames.isHandleType(type) {
                if member == "version" || member == "root" {
                    handleMember(e, member, type)
                } else if member != "id" && GesHandleTypeNames.hasProjection(type) {
                    projectedMember(e, member, type)
                }
            }
            expression(target, types)
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
            if let callee = definitions[callableSignature(name, args.map(\.label))] {
                for (argument, parameter) in zip(args, callee.parameters) { handleView(argument.value, parameter.type, types) }
            }
        case .message(_, let args): for argument in args { expression(argument.value, types) }
        case .constructor(let type, let args): constructor(type, args, e, types) { expression($0, types) }
        default: preconditionFailure()
        }
    }

    // Forms with a check of their own besides their operands.
    private func checkedOperands(_ e: GesExpression, _ types: [String: String]) {
        switch e.kind {
        case .predicate(let value, let name):
            predicateReference(name, count: value == nil ? 0 : 1, e.location)
            if let value { expression(value, types) }
        case .send(_, let message, let tags, let delay, let name, _):
            expression(message, types)
            if let delay {
                expression(delay, types)
                sendDelay(delay, types)
            }
            if let name { timerName(name, types) }
            for tag in tags {
                tagLiteral(tag)
                expression(tag, types)
            }
        case .cancel(_, let name): if let name { timerName(name, types) }
        // Geometric intrinsics are not visited, as in C#; clamp, min and max visit their operands.
        case .intrinsic(let name, let args): if Self.visitedIntrinsics.contains(name) { for arg in args { expression(arg, types) } }
        case .seeded(let seed, let body):
            expression(seed, types)
            validateSeed(seed, types)
            expression(body, types)
        // A cast or check may name a custom type that is bound only later, such as a record value a message delivers.
        case .check(let value, let type):
            if GesHandleTypeNames.isHandleType(type) { handleTypeName(type, e.location) }
            expression(value, types)
        case .cast(let value, let type):
            if type == "record" { report("validate.invalidTypeConstructor", e.location, symbol: "record", kind: .type, message: "':Record' is no cast target; use ':Record(type, fields)' to build a Record.") }
            // A handle has no constructor and no cast; only parameter and field types check handles (specs/Language.md#handles).
            if GesHandleTypeNames.isHandleType(type) {
                report("validate.invalidHandleUse", e.location, symbol: type, kind: .type, message: "'as \(type)' is only a parameter or field type; use 'is \(type)' to test a value.")
            } else if case let source = infer(value, types), GesHandleTypeNames.isHandleType(source), GesHandleTypeNames.hasProjection(source) {
                projectedStateCast(e, type, source)
            }
            expression(value, types)
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
        let type = infer(delay, types)
        let time = type.hasPrefix("quantity:") && GesCompilerSupport.quantityUnit(String(type.dropFirst(9))) == .second
        if !time && !["unknown", "other", "nothing"].contains(type) {
            report("validate.invalidTypeConstructor", delay.location, symbol: "Quantity(s)", kind: .type, message: "Send delay requires a time quantity, for example 0.2s.")
        }
    }

    // A timer name is Text, a Tag or a handle; statically unknown names are checked at runtime.
    private func timerName(_ name: GesExpression, _ types: [String: String]) {
        expression(name, types)
        let type = infer(name, types)
        if !["text", "tag", "unknown", "other", "nothing"].contains(type) && !GesHandleTypeNames.isHandleType(type) {
            report("validate.invalidTypeConstructor", name.location, symbol: "Text", kind: .type, message: "Timer name requires Text, Tag or a handle.")
        }
    }

    // Each alternative of a handle type or union is a declared `@Name`, `@Name~` names a type with versions and
    // `@Name(:P)` a projection of a type that is not composite, at most once per type in a union.
    private func handleTypeName(_ typeName: String, _ location: GesSourceLocation) {
        let alternatives = GesHandleTypeNames.alternatives(typeName)
        for (index, alternative) in alternatives.enumerated() {
            guard GesHandleTypeNames.isHandleType(alternative) else {
                let written = GesHandleTypeNames.sourceName(alternative)
                report("validate.invalidHandleUse", location, symbol: written, kind: .type, message: "A union is written only of handle types, not '\(written)'.")
                continue
            }
            let (name, versioned) = GesHandleTypeNames.split(alternative)
            let projection = GesHandleTypeNames.projection(alternative)
            guard let type = handleTypes[name] else {
                report("validate.unknownHandleType", location, symbol: "@" + name, kind: .type, message: "Handle type '@\(name)' is not declared.")
                continue
            }
            if versioned && type.version == .none {
                report("validate.invalidHandleUse", location, symbol: alternative, kind: .type, message: "'@\(name)~' needs a handle type with versions.")
            } else if !projection.isEmpty && type.root != nil {
                report("validate.invalidProjection", location, symbol: alternative, kind: .type, message: "'@\(name)' is composite; a projection reads a handle type that is not.")
            } else if !projection.isEmpty && type.findProjection(projection) == nil {
                report("validate.invalidProjection", location, symbol: alternative, kind: .type, message: "':\(projection)' is no projection of '@\(name)', whose state is ':\(type.stateName)'.")
            } else if alternatives[..<index].contains(where: { other in
                GesHandleTypeNames.isHandleType(other) && GesHandleTypeNames.split(other).name == name && (!projection.isEmpty || !GesHandleTypeNames.projection(other).isEmpty)
            }) {
                report("validate.invalidProjection", location, symbol: alternative, kind: .type, message: "A union holds '@\(name)' with two projections or with and without one.")
            }
        }
    }

    // A member other than the identity of a projected handle must be a field of its projection (specs/Language.md#projections).
    private func projectedMember(_ e: GesExpression, _ member: String, _ typeName: String) {
        for alternative in GesHandleTypeNames.alternatives(typeName) {
            let projectionName = GesHandleTypeNames.projection(alternative)
            guard !projectionName.isEmpty, let projection = handleTypes[GesHandleTypeNames.split(alternative).name]?.findProjection(projectionName) else { continue }
            if !projection.fields.contains(where: { $0.name == member }) && !projection.computedFields.contains(where: { $0.name == member }) {
                report("validate.invalidProjection", e.location, symbol: member, kind: .type, message: "'\(member)' is no field of ':\(projectionName)', the view of '\(alternative)'.")
                return
            }
        }
    }

    // `h as :S` on a projected handle reads no more than its view: the state schema or a projection that does not narrow
    // the view is widening; any other target is an ordinary cast.
    private func projectedStateCast(_ e: GesExpression, _ castType: String, _ typeName: String) {
        // A union reads the view through every alternative that can, so only a cast none of them reads would widen.
        var widening: (alternative: String, view: String)?
        for alternative in GesHandleTypeNames.alternatives(typeName) {
            guard let type = handleTypes[GesHandleTypeNames.split(alternative).name] else { continue }
            let target = type.findProjection(castType)
            if type.stateName != castType && target == nil { continue }
            let projectionName = GesHandleTypeNames.projection(alternative)
            guard !projectionName.isEmpty, let view = type.findProjection(projectionName) else { return }
            if let target, target === view || target.narrows(view) { return }
            if widening == nil { widening = (alternative, projectionName) }
        }
        if let widening {
            report("validate.handleWidening", e.location, symbol: castType, kind: .type, message: "'\(widening.alternative)' reads only ':\(widening.view)', so 'as :\(castType)' would read more.")
        }
    }

    // A value of a statically projected handle type passes only to a view it narrows to (specs/Language.md#projections).
    private func handleView(_ value: GesExpression, _ declaredType: String?, _ types: [String: String]) {
        guard let declaredType, GesHandleTypeNames.isHandleType(declaredType) else { return }
        let sourceType = infer(value, types)
        guard GesHandleTypeNames.isHandleType(sourceType), GesHandleTypeNames.hasProjection(sourceType) else { return }
        for alternative in GesHandleTypeNames.alternatives(sourceType) {
            let viewName = GesHandleTypeNames.projection(alternative)
            let name = GesHandleTypeNames.split(alternative).name
            guard !viewName.isEmpty, let type = handleTypes[name], let view = type.findProjection(viewName) else { continue }
            for target in GesHandleTypeNames.alternatives(declaredType) where GesHandleTypeNames.isHandleType(target) && GesHandleTypeNames.split(target).name == name {
                let targetName = GesHandleTypeNames.projection(target)
                let targetView = targetName.isEmpty ? nil : type.findProjection(targetName)
                if targetView == nil && !targetName.isEmpty { break }
                if let targetView, targetView === view || targetView.narrows(view) { break }
                report("validate.handleWidening", value.location, symbol: target, kind: .type, message: "'\(alternative)' reads only ':\(viewName)', so it cannot pass to '\(target)'.")
                return
            }
        }
    }

    // `h.version` needs a type with versions and `h.root` composites of one root type; other alternatives are not
    // declared and already reported.
    private func handleMember(_ e: GesExpression, _ member: String, _ typeName: String) {
        var versioned = false
        for alternative in GesHandleTypeNames.alternatives(typeName) {
            guard let type = handleTypes[GesHandleTypeNames.split(alternative).name] else { return }
            versioned = versioned || type.version != .none
        }
        if member == "version" && !versioned {
            report("validate.invalidHandleUse", e.location, symbol: typeName, kind: .type, message: "'\(typeName)' has no versions.")
        } else if member == "root" && typeName.unicodeScalars.contains("|") && GesHandleTypeNames.commonRoot(typeName, handleTypes) == nil {
            report("validate.invalidHandleUse", e.location, symbol: typeName, kind: .type, message: noCommonRoot(typeName))
        }
    }

    // Names the two alternatives whose roots differ, or the first one that is no composite.
    private func noCommonRoot(_ typeName: String) -> String {
        var first: (name: String, root: GesHandleType)?
        for alternative in GesHandleTypeNames.alternatives(typeName) {
            let name = GesHandleTypeNames.split(alternative).name
            guard let root = handleTypes[name]?.root else { return "'@\(name)' is no composite, so '\(typeName)' has no common root type." }
            if let first {
                if first.root.name != root.name {
                    return "The root of '@\(first.name)' is '@\(first.root.name)' and the root of '@\(name)' is '@\(root.name)', so '\(typeName)' has no common root type."
                }
            } else {
                first = (name, root)
            }
        }
        return "The alternatives of '\(typeName)' have no common root type."
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

    private func constructor(_ type: String, _ args: [GesArgument], _ e: GesExpression, _ types: [String: String], _ visit: (GesExpression) -> Void) {
        for argument in args { visit(argument.value) }

        func invalid() { report("validate.invalidTypeConstructor", e.location, symbol: type, kind: .type) }

        if ["__split", "__splitWhitespace"].contains(type) { return }
        if ["record", "range", "series", "number", "message", "nothing", "percentage", "boolean", "text", "tag", "list", "map", "dice", "bytes", "handler"].contains(type) {
            if type == "message" {
                var isLiteral = false
                if case .message = args.first?.value.kind { isLiteral = true }
                if args.contains(where: { $0.label == "value" || ($0.label == "tags" && !isLiteral) }) { return invalid() }
                if args.count == 2 { tagLiteral(args[1].value) }
            }
            if !GesCompilerSupport.dataArguments(type, args.map(\.label)) { invalid() }
            return
        }
        if ["vector", "point"].contains(type) {
            for argument in args {
                if case .literal(let value) = argument.value.kind, value.valueKind == .boolean { return invalid() }
            }
            let labeled = args.filter { $0.label != "_" }
            if labeled.isEmpty && (args.count == 1 || args.count == 2) { return }
            if !labeled.isEmpty && labeled.count != args.count { return invalid() }
            if !labeled.isEmpty || args.isEmpty {
                var previous = -1
                for argument in args {
                    guard let index = ["x", "y", "z"].firstIndex(of: argument.label), index > previous, previous < 0 || index == previous + 1 else { return invalid() }
                    previous = index
                }
                return
            }
            if args.count > 3 { invalid() }
            return
        }
        if ["numeric", "integer", "fractional", "unit"].contains(type) || type.hasPrefix("quantity:") {
            if type.hasPrefix("quantity:") && GesCompilerSupport.quantityUnit(String(type.dropFirst(9))) == nil { return invalid() }
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
        for argument in args where argument.label != "_" {
            if !named.contains(argument.label) {
                invalid()
            } else if let field = records[type]?.fields.first(where: { $0.name == argument.label }) {
                handleView(argument.value, field.type, types)
            }
        }
    }
}
