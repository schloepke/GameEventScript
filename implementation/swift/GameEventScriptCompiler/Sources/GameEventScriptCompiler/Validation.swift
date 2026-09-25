// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    func validate() throws {
        for module in modules {
            for (name, value) in module.constants {
                if constants[name] != nil { throw error("validate.duplicateConstant", value.location, symbol: name, kind: .globalDefinition) }
                constants[name] = value
            }
            for record in module.records {
                if records[record.name] != nil || catalog?.types.contains(where: { $0.name == record.name }) == true { throw error("validate.duplicateType", record.location, symbol: record.name, kind: .type) }
                records[record.name] = record
            }
            for definition in module.definitions where !definition.kind.hasSuffix("andler") {
                if definitions[definition.signature] != nil {
                    throw error(definition.kind == "predicate" ? "validate.duplicatePredicate" : "validate.duplicateFunction", definition.location, symbol: definition.name, kind: definition.kind == "predicate" ? .predicate : .function)
                }
                if definitions.values.contains(where: { $0.name == definition.name && $0.kind != definition.kind }) { throw error("validate.predicateFunctionConflict", definition.location, symbol: definition.name, kind: .globalDefinition) }
                definitions[definition.signature] = definition
            }
        }
        for module in modules where !module.name.isEmpty {
            if moduleName.isEmpty { moduleName = module.name } else if moduleName != module.name { throw error("validate.invalidIdentifierCase", module.location, symbol: module.name, kind: .globalDefinition) }
        }
        // A body may infer the return type of a later definition, including in another source.
        // Validate every parameter scope before inference constructs any parameter dictionary.
        for module in modules {
            for d in module.definitions {
                if !d.kind.hasSuffix("andler") && d.name.contains("_") { throw error("validate.invalidIdentifierCase", d.location, symbol: d.name, kind: d.kind == "predicate" ? .predicate : .function) }
                var names: Set<String> = []
                for p in d.parameters {
                    if !names.insert(p.name).inserted {
                        throw error(
                            d.kind.hasSuffix("andler") ? "validate.duplicateHandlerParameter" : "validate.duplicateDefinitionParameter",
                            p.location,
                            symbol: d.name,
                            kind: d.kind.hasSuffix("andler") ? .handler : d.kind == "predicate" ? .predicate : .function
                        )
                    }
                    if let type = p.type { try validateType(type, p.location) }
                }
                if d.name == "undeliverable", d.kind != "messageNameHandler" { throw error("validate.invalidMessageCase", d.location, symbol: d.name, kind: .handler) }
            }
        }
        for module in modules {
            for d in module.definitions {
                let names = Set(d.parameters.map(\.name))
                let types = Dictionary(uniqueKeysWithValues: d.parameters.compactMap { p in p.type.map { (p.name, $0) } })
                try validateStatements(d.statements, names, [], types)
                if d.kind == "predicate", let expression = d.expression, !["boolean", "nothing"].contains(infer(expression, types)) { throw error("validate.invalidPredicate", expression.location, symbol: d.name, kind: .predicate) }
                if let expression = d.expression { try validateExpression(expression, visible: names, types: types) }
            }
            for record in module.records {
                var names: Set<String> = []
                for field in record.fields {
                    if !names.insert(field.name).inserted { throw error("validate.duplicateVariable", field.location, symbol: field.name, kind: .variable) }
                    if field.name.contains("_") { throw error("validate.invalidIdentifierCase", field.location, symbol: field.name, kind: .variable) }
                    try validateType(field.type, field.location)
                }
                let types = Dictionary(uniqueKeysWithValues: record.fields.map { ($0.name, $0.type) })
                for field in record.fields { for e in [field.minimum, field.maximum, field.computed].compactMap({ $0 }) { try validateExpression(e, visible: names, types: types) } }
            }
        }
    }

    func knownType(_ type: String) -> Bool {
        ["record", "number", "numeric", "nothing", "boolean", "percentage", "vector", "point", "series", "tag", "text", "list", "range", "message", "handler", "map", "dice", "unit", "integer", "fractional"].contains(type)
            || type.hasPrefix("quantity:")
            || records[type] != nil || catalog?.types.contains { $0.name == type } == true
    }

    func validateType(_ type: String, _ location: GameEventScriptSourceLocation) throws { if !knownType(type) { throw error("validate.invalidTypeConstructor", location, symbol: type, kind: .type) } }

    func validateStatements(_ statements: [GesStatement], _ initial: Set<String>, _ ancestors: Set<String>, _ initialTypes: [String: String] = [:]) throws {
        var names = initial
        var types = initialTypes
        for s in statements {
            switch s.kind {
            case .letBinding(let name, let expression):
                try validateExpression(expression, visible: names.union(ancestors), types: types)
                types[name] = infer(expression, types)
                if names.contains(name) { throw error("validate.duplicateVariable", s.location, symbol: name, kind: .variable) }
                if ancestors.contains(name) { throw error("validate.shadowedVariable", s.location, symbol: name, kind: .variable) }
                names.insert(name)
            case .expression(let e): try validateExpression(e, visible: names.union(ancestors), types: types)
            case .publish(_, let e, let tags):
                try validateExpression(e, visible: names.union(ancestors), types: types)
                for tag in tags { try validateExpression(tag, visible: names.union(ancestors), types: types) }
            case .condition(let conditions, let yes, let no):
                var bindings: Set<String> = []
                var conditionTypes = types
                for (binding, value) in conditions {
                    try validateExpression(value, visible: names.union(ancestors).union(bindings), types: conditionTypes)
                    if let binding {
                        if bindings.contains(binding) { throw error("validate.duplicateVariable", value.location, symbol: binding, kind: .variable) }
                        if names.contains(binding) || ancestors.contains(binding) { throw error("validate.shadowedVariable", value.location, symbol: binding, kind: .variable) }
                        conditionTypes[binding] = infer(value, conditionTypes)
                        bindings.insert(binding)
                    }
                }
                try validateStatements(yes, bindings, ancestors.union(names), conditionTypes)
                try validateStatements(no, [], ancestors.union(names), types)
            case .loop(let name, let e, _, let body):
                try validateExpression(e, visible: names.union(ancestors), types: types)
                if names.contains(name) || ancestors.contains(name) { throw error("validate.shadowedVariable", s.location, symbol: name, kind: .variable) }
                try validateStatements(body, [name], ancestors.union(names), types)
            case .seeded(let seed, let body):
                try validateSeed(seed, types)
                try validateExpression(seed, visible: names.union(ancestors), types: types)
                try validateStatements(body, [], ancestors.union(names), types)
            }
        }
    }

    func validateExpression(_ e: GesExpression, visible: Set<String> = [], types: [String: String] = [:]) throws {
        var children: [GesExpression] = []

        func arguments(_ args: [GesArgument], _ name: String = "") throws {
            var labels: Set<String> = []
            for argument in args where argument.label != "_" {
                if argument.label.contains("_") { throw error("validate.invalidIdentifierCase", e.location, symbol: name) }
                if !labels.insert(argument.label).inserted { throw error("validate.duplicatePublishArgument", e.location, symbol: name, kind: .message) }
            }
            children += args.map(\.value)
        }

        func boundExpressions(_ expressions: [GesExpression], name: String) throws {
            if visible.contains(name) { throw error("validate.shadowedVariable", e.location, symbol: name, kind: .variable) }
            let scope = name.isEmpty ? visible : visible.union([name])
            for expression in expressions { try validateExpression(expression, visible: scope, types: types) }
        }

        switch e.kind {
        case .literal, .name, .constant, .dice, .series: break
        case .handler(let name, let params):
            var labels: Set<String> = []
            for p in params where p.label != "_" { if !labels.insert(p.label).inserted { throw error("validate.duplicateHandlerParameter", e.location, symbol: name, kind: .handler) } }
        case .send(_, let message, let tags, let delay):
            if let delay {
                let type = infer(delay, types)
                if !["unknown", "other", "nothing", "quantity:s", "quantity:second"].contains(type) { throw error("validate.invalidTypeConstructor", delay.location, symbol: "Quantity(s)", kind: .type) }
            }
            children = (delay.map { [$0] } ?? []) + [message] + tags
        case .unary(_, let a), .member(let a, _), .predicate(let a, _): children = [a]
        case .cast(let a, _), .check(let a, _): children = [a]
        case .binary(_, let a, let b), .random(let a, let b): children = [a, b]
        case .seeded(let a, let b):
            try validateSeed(a, types)
            children = [a, b]
        case .call(let name, let args), .message(let name, let args), .extensionCall(_, let name, let args): try arguments(args, name)
        case .constructor(let name, let args):
            if !["__split", "__splitWhitespace"].contains(name) { try validateType(name, e.location) }
            try arguments(args, name)
            if ["record", "number", "range", "series", "message", "nothing", "percentage", "boolean", "text", "tag", "list", "map", "dice", "handler"].contains(name) && !GameEventScriptCompilerSupport.dataArguments(name, args.map(\.label)) {
                throw error("validate.invalidTypeConstructor", e.location, symbol: name, kind: .type)
            }
            if ["vector", "point"].contains(name) {
                let labeled = args.filter { $0.label != "_" }
                let first = ["x", "y", "z"].firstIndex(of: labeled.first?.label ?? "") ?? -1
                let validLabels =
                    labeled.isEmpty || first >= 0 && first + labeled.count <= 3 && labeled.count == args.count && !(first == 0 && labeled.count == 1) && labeled.enumerated().allSatisfy { $0.element.label == ["x", "y", "z"][first + $0.offset] }
                if args.count > 3 || !validLabels { throw error("validate.invalidTypeConstructor", e.location, symbol: name, kind: .type) }
            } else if let record = records[name] {
                let labels = record.fields.compactMap(\.label)
                var positional = 0
                for arg in args {
                    if arg.label == "_" { positional += 1 }
                    if !labels.contains(arg.label) || positional > labels.filter({ $0 == "_" }).count { throw error("validate.invalidTypeConstructor", e.location, symbol: name, kind: .type) }
                }
            }
        case .list(let items), .intrinsic(_, let items): children = items
        case .map(let entries): children = entries.map(\.1)
        case .selector(let a, let s):
            try validateExpression(a, visible: visible, types: types)
            if s.operation == "fold" || s.operation == "reduce" {
                if s.operation == "fold" { try validateExpression(s.expressions[0], visible: visible, types: types) }
                if visible.contains(s.accumulator) { throw error("validate.shadowedVariable", e.location, symbol: s.accumulator, kind: .variable) }
                if visible.contains(s.name) { throw error("validate.shadowedVariable", e.location, symbol: s.name, kind: .variable) }
                if s.name == s.accumulator { throw error("validate.duplicateVariable", e.location, symbol: s.name, kind: .variable) }
                try validateExpression(s.expressions.last!, visible: visible.union([s.name, s.accumulator]), types: types)
            } else if s.operation == "choose" {
                for (index, expression) in s.expressions.enumerated() { try boundExpressions([expression], name: index == 0 ? s.name : s.weightName) }
            } else if !s.expressions.isEmpty && ["any", "all", "filter", "count", "sum", "average", "select", "min", "max", "first", "last", "single", "map", "group", "order", "distinct"].contains(s.operation) {
                try boundExpressions(s.expressions, name: s.name)
            } else {
                children = s.expressions
            }
        case .range(let a, let b, let c): children = [a, b] + (c.map { [$0] } ?? [])
        case .choice(let branches, let fallback): children = branches.flatMap { [$0.0, $0.1] } + [fallback]
        case .generated(_, let name, let source, _, let predicate, let projection):
            try validateExpression(source, visible: visible, types: types)
            try boundExpressions((predicate.map { [$0] } ?? []) + [projection], name: name)
        }
        for child in children { try validateExpression(child, visible: visible, types: types) }
    }
}
