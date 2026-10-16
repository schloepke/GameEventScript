// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// Rejects bindings that shadow a visible binding and repeated component bindings, as GesShadowingValidator of the
// C# compiler. GesAstValidator runs it after every other rule, so other diagnostics of a program come first.
struct GesShadowingValidator {
    func validate(_ modules: [GesModule]) throws {
        for module in modules {
            for definition in module.definitions {
                let names = Set(definition.parameters.map(\.name))
                try statements(definition.statements, names, [])
                if let value = definition.expression { try expression(value, names) }
            }
            for record in module.records {
                let names = Set(record.fields.map(\.name))
                for field in record.fields { for value in [field.minimum, field.maximum, field.computed].compactMap({ $0 }) { try expression(value, names) } }
            }
        }
    }

    // `initial` holds the bindings of the current scope, `ancestors` those of every enclosing scope.
    private func statements(_ list: [GesStatement], _ initial: Set<String>, _ ancestors: Set<String>) throws {
        var names = initial
        for statement in list {
            switch statement.kind {
            case .assembly(let block):
                for line in block.lines { for operand in line.operands { try expression(operand, names.union(ancestors)) } }
                for declaration in block.declarations where declaration.export {
                    if ancestors.contains(declaration.name) { throw shadowed(declaration.name, statement.location) }
                    names.insert(declaration.name)
                }
            case .letBinding(let name, let value):
                try expression(value, names.union(ancestors))
                if ancestors.contains(name) { throw shadowed(name, statement.location) }
                names.insert(name)
            case .expression(let value): try expression(value, names.union(ancestors))
            case .publish(_, let message, let tags): for value in [message] + tags { try expression(value, names.union(ancestors)) }
            case .condition(let conditions, let then, let otherwise):
                var bindings: Set<String> = []
                for (binding, value) in conditions {
                    try expression(value, names.union(ancestors).union(bindings))
                    if let binding {
                        if names.contains(binding) || ancestors.contains(binding) { throw shadowed(binding, value.location) }
                        bindings.insert(binding)
                    }
                }
                try statements(then, bindings, ancestors.union(names))
                try statements(otherwise, [], ancestors.union(names))
            case .loop(let bindings, let source, _, let body):
                try expression(source, names.union(ancestors))
                var bound: Set<String> = []
                for name in bindings {
                    if names.contains(name) || ancestors.contains(name) { throw shadowed(name, statement.location) }
                    if !bound.insert(name).inserted { throw duplicate(name, statement.location) }
                }
                try statements(body, bound, ancestors.union(names))
            case .seeded(let seed, let body):
                try expression(seed, names.union(ancestors))
                try statements(body, [], ancestors.union(names))
            }
        }
    }

    private func expression(_ e: GesExpression, _ visible: Set<String>) throws {
        func bound(_ expressions: [GesExpression], name: String, names: [String] = []) throws {
            var bound: Set<String> = []
            for binding in names.isEmpty ? (name.isEmpty ? [] : [name]) : names {
                if visible.contains(binding) { throw shadowed(binding, e.location) }
                if !bound.insert(binding).inserted { throw duplicate(binding, e.location) }
            }
            let scope = visible.union(bound)
            for expression in expressions { try self.expression(expression, scope) }
        }

        var children: [GesExpression] = []
        switch e.kind {
        case .assembly(let block): children = block.lines.flatMap(\.operands)
        case .literal, .name, .constant, .dice, .series, .handler: break
        case .send(_, let message, let tags, let delay): children = (delay.map { [$0] } ?? []) + [message] + tags
        case .unary(_, let a), .member(let a, _), .cast(let a, _), .check(let a, _): children = [a]
        case .predicate(let a, _): children = a.map { [$0] } ?? []
        case .binary(_, let a, let b), .random(let a, let b), .seeded(let a, let b): children = [a, b]
        case .call(_, let args), .message(_, let args), .extensionCall(_, _, let args), .constructor(_, let args): children = args.map(\.value)
        case .list(let items), .intrinsic(_, let items), .combined(_, let items): children = items
        case .map(let entries): children = entries.map(\.1)
        case .selector(let target, let s):
            try expression(target, visible)
            if s.operation == "fold" || s.operation == "reduce" {
                if s.operation == "fold" { try expression(s.expressions[0], visible) }
                try bound([s.expressions.last!], name: s.name, names: [s.accumulator] + (s.names.isEmpty ? [s.name] : s.names))
            } else if s.operation == "choose" {
                for (index, value) in s.expressions.enumerated() { try bound([value], name: index == 0 ? s.name : s.weightName, names: index == 0 ? s.names : s.weightNames) }
            } else if !s.expressions.isEmpty && ["any", "all", "filter", "count", "sum", "average", "foreach", "select", "min", "max", "first", "last", "single", "map", "group", "order", "distinct"].contains(s.operation) {
                try bound(s.expressions, name: s.name, names: s.names)
            } else {
                children = s.expressions
            }
        case .between(let value, let minimum, let maximum): children = [value, minimum, maximum]
        case .range(let a, let b, let c): children = [a, b] + (c.map { [$0] } ?? [])
        case .choice(let branches, let fallback): children = branches.flatMap { [$0.0, $0.1] } + [fallback]
        case .generated(_, let name, let source, _, let predicate, let projection):
            try expression(source, visible)
            try bound((predicate.map { [$0] } ?? []) + [projection], name: name)
        }
        for child in children { try expression(child, visible) }
    }

    private func shadowed(_ name: String, _ location: GesSourceLocation) -> GesCompileError { compileError("validate.shadowedVariable", location, symbol: name, kind: .variable) }

    private func duplicate(_ name: String, _ location: GesSourceLocation) -> GesCompileError { compileError("validate.duplicateVariable", location, symbol: name, kind: .variable) }
}
