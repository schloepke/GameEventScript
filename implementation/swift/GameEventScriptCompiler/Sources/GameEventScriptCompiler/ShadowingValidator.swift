// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// Reports bindings that shadow a visible binding and repeated component bindings, as GesShadowingValidator of the
// C# compiler. GesAstValidator runs it for each module after that module's other rules; every finding is collected.
final class GesShadowingValidator {
    private var diagnostics: [GesDiagnostic] = []

    func validate(_ module: GesModule) -> [GesDiagnostic] {
        for record in module.records {
            let names = Set(record.fields.map(\.name))
            for field in record.fields { for value in [field.minimum, field.maximum, field.computed].compactMap({ $0 }) { expression(value, names) } }
        }
        for definition in module.definitions where definition.kind == "predicate" { if let value = definition.expression { expression(value, Set(definition.parameters.map(\.name))) } }
        for definition in module.definitions where definition.kind == "function" { if let value = definition.expression { expression(value, Set(definition.parameters.map(\.name))) } }
        for definition in module.definitions where definition.kind.hasSuffix("andler") { statements(definition.statements, Set(definition.parameters.map(\.name)), []) }
        return diagnostics
    }

    // `initial` holds the bindings of the current scope, `ancestors` those of every enclosing scope. Nested blocks and
    // expressions recurse through small functions, so that an unoptimized build does not reserve stack for the locals
    // of every form on each level.
    private func statements(_ list: [GesStatement], _ initial: Set<String>, _ ancestors: Set<String>) {
        var names = initial
        for statement in list { self.statement(statement, &names, ancestors) }
    }

    private func statement(_ statement: GesStatement, _ names: inout Set<String>, _ ancestors: Set<String>) {
        switch statement.kind {
        case .assembly(let block):
            for line in block.lines { for operand in line.operands { expression(operand, names.union(ancestors)) } }
            for declaration in block.declarations where declaration.export {
                if ancestors.contains(declaration.name) { report(shadowed(declaration.name, statement.location)) }
                names.insert(declaration.name)
            }
        case .letBinding(let name, let value):
            expression(value, names.union(ancestors))
            if ancestors.contains(name) { report(shadowed(name, statement.location)) }
            names.insert(name)
        case .expression(let value): expression(value, names.union(ancestors))
        case .publish(_, let message, let tags): for value in [message] + tags { expression(value, names.union(ancestors)) }
        case .condition(let conditions, let then, let otherwise): condition(conditions, then, otherwise, names, ancestors)
        case .loop(let bindings, let source, _, let body): loop(bindings, source, body, statement.location, names, ancestors)
        case .seeded(let seed, let body):
            expression(seed, names.union(ancestors))
            statements(body, [], ancestors.union(names))
        }
    }

    private func condition(
        _ conditions: [(binding: String?, value: GesExpression, location: GesSourceLocation)],
        _ then: [GesStatement],
        _ otherwise: [GesStatement],
        _ names: Set<String>,
        _ ancestors: Set<String>
    ) {
        var bindings: Set<String> = []
        for (binding, value, location) in conditions {
            expression(value, names.union(ancestors).union(bindings))
            if let binding {
                if names.contains(binding) || ancestors.contains(binding) { report(shadowed(binding, location)) }
                bindings.insert(binding)
            }
        }
        statements(then, bindings, ancestors.union(names))
        statements(otherwise, [], ancestors.union(names))
    }

    private func loop(_ bindings: [String], _ source: GesExpression, _ body: [GesStatement], _ location: GesSourceLocation, _ names: Set<String>, _ ancestors: Set<String>) {
        expression(source, names.union(ancestors))
        var bound: Set<String> = []
        for name in bindings {
            if names.contains(name) || ancestors.contains(name) { report(shadowed(name, location)) }
            if !bound.insert(name).inserted { report(duplicate(name, location)) }
        }
        statements(body, bound, ancestors.union(names))
    }

    private func expression(_ e: GesExpression, _ visible: Set<String>) {
        switch e.kind {
        case .selector(let target, let s):
            expression(target, visible)
            selector(s, s.location ?? e.location, visible)
        case .generated(_, let name, let source, _, let predicate, let projection):
            expression(source, visible)
            bound((predicate.map { [$0] } ?? []) + [projection], names: name.isEmpty ? [] : [name], at: e.location, visible)
        default: for child in Self.children(e) { expression(child, visible) }
        }
    }

    private func selector(_ s: GesSelector, _ location: GesSourceLocation, _ visible: Set<String>) {
        if s.operation == "fold" || s.operation == "reduce" {
            if s.operation == "fold" { expression(s.expressions[0], visible) }
            bound([s.expressions.last!], names: [s.accumulator] + (s.names.isEmpty ? [s.name] : s.names), at: location, visible)
        } else if s.operation == "choose" {
            for (index, value) in s.expressions.enumerated() { bound([value], names: Self.bindingNames(index == 0 ? s.name : s.weightName, index == 0 ? s.names : s.weightNames), at: location, visible) }
        } else if !s.expressions.isEmpty && Self.bindingSelectors.contains(s.operation) {
            bound(s.expressions, names: Self.bindingNames(s.name, s.names), at: location, visible)
        } else {
            for value in s.expressions { expression(value, visible) }
        }
    }

    static let bindingSelectors: Set<String> = ["any", "all", "filter", "count", "sum", "average", "foreach", "select", "min", "max", "first", "last", "single", "map", "group", "order", "distinct"]

    // The names a selector binds: its binding list, or else its single binding name unless that is empty.
    static func bindingNames(_ name: String, _ names: [String]) -> [String] { names.isEmpty ? (name.isEmpty ? [] : [name]) : names }

    // Visits `expressions` with `names` bound, reporting each name that shadows a visible binding or repeats.
    private func bound(_ expressions: [GesExpression], names: [String], at location: GesSourceLocation, _ visible: Set<String>) {
        var bound: Set<String> = []
        for binding in names {
            if visible.contains(binding) { report(shadowed(binding, location)) }
            if !bound.insert(binding).inserted { report(duplicate(binding, location)) }
        }
        let scope = visible.union(bound)
        for expression in expressions { self.expression(expression, scope) }
    }

    // The children every other form visits in the current scope.
    static func children(_ e: GesExpression) -> [GesExpression] {
        switch e.kind {
        case .assembly(let block): return block.lines.flatMap(\.operands)
        case .literal, .name, .constant, .dice, .series, .handler, .selector, .generated: return []
        case .send(_, let message, let tags, let delay): return (delay.map { [$0] } ?? []) + [message] + tags
        case .unary(_, let a), .member(let a, _), .cast(let a, _), .check(let a, _): return [a]
        case .predicate(let a, _): return a.map { [$0] } ?? []
        case .binary(_, let a, let b), .random(let a, let b), .seeded(let a, let b): return [a, b]
        case .call(_, let args), .message(_, let args), .extensionCall(_, _, let args), .constructor(_, let args): return args.map(\.value)
        case .list(let items), .intrinsic(_, let items), .combined(_, let items): return items
        case .map(let entries): return entries.map(\.1)
        case .between(let value, let minimum, let maximum): return [value, minimum, maximum]
        case .range(let a, let b, let c): return [a, b] + (c.map { [$0] } ?? [])
        case .choice(let branches, let fallback): return branches.flatMap { [$0.0, $0.1] } + [fallback]
        }
    }

    private func report(_ error: GesCompileError) { diagnostics += error.diagnostics }

    private func shadowed(_ name: String, _ location: GesSourceLocation) -> GesCompileError { compileError("validate.shadowedVariable", location, symbol: name, kind: .variable) }

    private func duplicate(_ name: String, _ location: GesSourceLocation) -> GesCompileError { compileError("validate.duplicateVariable", location, symbol: name, kind: .variable) }
}
