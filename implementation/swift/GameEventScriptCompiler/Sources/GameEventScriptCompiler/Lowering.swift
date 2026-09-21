// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    func statements(_ body: [GesStatement], _ r: GesRoutine, _ scope: GesScope) throws {
        for statement in body {
            try checkLimits(r)
            r.location = statement.location
            switch statement.kind {
            case .letBinding(let name, let value):
                let d = r.local()
                scope.values[name] = d
                _ = try expression(value, r, scope, destination: d)
                r.symbols.append((name, d, max(0, r.code.count - 1), false))
                if case .handler(_, let parameters) = value.kind { scope.handlers[name] = parameters.map(\.label) }
            case .expression(let value): _ = try expression(value, r, scope)
            case .publish(let publish, let message, let tags):
                let tagRegisters = try tags.map { try expression($0, r, scope) }
                if case .message(let name, let arguments) = message.kind {
                    let regs = try arguments.map { try expression($0.value, r, scope) }
                    let bind = importBinding(.outboundMessage, name, arguments.map(\.label))
                    r.emit(publish ? (tags.isEmpty ? .publishMessage : .publishMessageWithTags) : (tags.isEmpty ? .emitMessage : .emitMessageWithTags), bind, tags.isEmpty ? 0 : list(tagRegisters), list(regs))
                } else {
                    let reg = try expression(message, r, scope)
                    r.emit(publish ? (tags.isEmpty ? .publishMessageValue : .publishMessageValueWithTags) : (tags.isEmpty ? .emitMessageValue : .emitMessageValueWithTags), 0, reg, tags.isEmpty ? 0 : list(tagRegisters))
                }
            case .condition(let condition, let yes, let no):
                let reg = try expression(condition, r, scope)
                let branch = r.emit(.jumpIfNotTrue, 0, reg)
                try statements(yes, r, GesScope(scope))
                let end = r.emit(.jump)
                r.patch(branch, target: r.code.count)
                try statements(no, r, GesScope(scope))
                r.patch(end, target: r.code.count)
            case .loop(let name, let sequence, let range, let body):
                let child = GesScope(scope)
                let item = r.local()
                child.values[name] = item
                r.symbols.append((name, item, 0, false))
                let iterator = try iterator(sequence, range, r, scope)
                let start = r.code.count
                let next = r.emit(.iteratorNext, item, iterator)
                try statements(body, r, child)
                r.location = statement.location
                r.emit(.jump, 0, 0, start)
                r.patch(next, target: r.code.count)
                r.emit(.iteratorClose, 0, iterator)
            case .seeded(let seed, let body):
                try randomPush(seed, r, scope)
                try statements(body, r, GesScope(scope))
                r.emit(.randomPop)
            }
        }
    }

    func randomPush(_ seed: GesExpression, _ r: GesRoutine, _ scope: GesScope) throws {
        if let value = scalarConstant(seed), let i = value.integerValue, !value.hasUnit {
            r.emit(.randomPushConstant, payload: UInt64(bitPattern: i))
        } else {
            let value = try expression(seed, r, scope)
            r.emit(.randomPush, 0, value)
        }
    }

    func iterator(_ e: GesExpression, _ range: Bool, _ r: GesRoutine, _ scope: GesScope) throws -> Int {
        let target = r.temporary()
        if range, case .range(let from, let to, let step) = e.kind {
            if let a = scalarConstant(from)?.integerValue, let b = scalarConstant(to)?.integerValue, let c = step.map({ scalarConstant($0)?.integerValue }) ?? 1, [a, b, c].allSatisfy({ $0 >= Int16.min && $0 <= Int16.max }),
                scalarConstant(from)?.hasUnit == false, scalarConstant(to)?.hasUnit == false, step == nil || scalarConstant(step!)?.hasUnit == false
            {
                r.emit(.createRangeIteratorShort, target, Int(a), Int(b), payload: UInt64(UInt16(bitPattern: Int16(c))))
                return target
            }
            let a = try expression(from, r, scope)
            let b = try expression(to, r, scope)
            if let step {
                let c = try expression(step, r, scope)
                r.emit(.createRangeIteratorWithStep, target, a, b, payload: UInt64(c))
            } else {
                r.emit(.createRangeIterator, target, a, b)
            }
        } else {
            let value = try expression(e, r, scope)
            r.emit(.iteratorCreate, target, value)
        }
        return target
    }

    func literal(_ value: GesValue, _ target: Int, _ r: GesRoutine) throws {
        let flag: UInt8 = value.unit == .degree ? 1 : value.unit == .meter ? 2 : value.unit == .second ? 3 : 0
        switch value.kind {
        case .nothing: r.emit(.loadNothing, target)
        case .boolean: r.emit(value.asBoolean ? .loadTrue : .loadFalse, target)
        case .integer: r.emit(.loadInteger, target, payload: UInt64(bitPattern: value.integerValue!), flags: flag)
        case .float: r.emit(.loadFloat, target, payload: value.asNumber.bitPattern, flags: flag)
        case .percentage: r.emit(.loadPercentage, target, payload: value.asNumber.bitPattern)
        case .text: r.emit(.loadText, target, text(value.textValue!))
        case .tag: r.emit(.loadTag, target, text(value.textValue!))
        default: throw error("compile.unsupportedConstruct", r.location, phase: .compile)
        }
    }

    func expression(_ e: GesExpression, _ r: GesRoutine, _ scope: GesScope, destination: Int? = nil) throws -> Int {
        let oldLocation = r.location
        r.location = e.location
        defer { r.location = oldLocation }
        if case .name(let name) = e.kind {
            guard let value = scope.get(name) else { throw error("compile.unresolvedSymbol", e.location, symbol: name, phase: .compile) }
            if let destination, destination != value {
                r.emit(.move, destination, value)
                return destination
            }
            return value
        }
        if case .constant(let name) = e.kind {
            guard let value = constants[name] else { throw error("compile.unresolvedSymbol", e.location, symbol: name, phase: .compile) }
            return try expression(value, r, scope, destination: destination)
        }
        let d = destination ?? r.temporary()
        if let value = scalarConstant(e), [.nothing, .boolean, .integer, .float, .percentage, .text, .tag].contains(value.kind) {
            try literal(value, d, r)
            return d
        }
        switch e.kind {
        case .literal(let value): try literal(value, d, r)
        case .name, .constant: break
        case .unary(let op, let input):
            if op == "predicate", case .extensionCall(let ns, let function, let args) = input.kind {
                let regs = try args.map { try expression($0.value, r, scope) }
                r.emit(.callExternal, d, importBinding(.extensionCall, ns + "." + function, args.map(\.label)), list(regs), flags: 0x20)
                break
            }
            let a = try expression(input, r, scope)
            let mapping: [String: Op] = [
                "-": .negate, "!": .not, "parse": .parseLiteral, "empty": .isEmpty, "hasValue": .hasValue, "abs": .abs, "ln": .logN, "exp": .exp, "chance": .chance, "floor": .floor, "ceil": .ceil, "truncate": .truncate, "rad": .degreeToRadians,
                "deg": .degreeFromRadians, "wrapDegree": .wrapDegree, "roundHalfEven": .roundHalfEven, "roundHalfUp": .roundHalfUp, "roundHalfDown": .roundHalfDown, "sin": .sin, "cos": .cos, "tan": .tan, "asin": .asin, "acos": .acos, "atan": .atan,
            ]
            if op == "predicate" {
                r.emit(.loadTrue, d)
                r.emit(.equal, d, a, d)
            } else if let opcode = mapping[op] {
                r.emit(opcode, d, a)
            }
        case .binary(let op, let left, let right):
            let a = try expression(left, r, scope)
            let short = ["or", "and", "->"].contains(op) ? r.emit(op == "or" ? .jumpIfTrue : .jumpIfFalse, 0, a) : nil
            let b = try expression(right, r, scope)
            let mapping: [String: Op] = [
                "+": .add, "-": .subtract, "*": .multiply, "/": .divide, "^": .power, "div": .integerDivide, "mod": .modulo, "rem": .remainder, "=": .equal, "<>": .notEqual, "<": .less, ">": .greater, "<=": .lessOrEqual, ">=": .greaterOrEqual,
                "default": .default, "or": .or, "and": .and, "xor": .xor, "->": .implies, "in": .contains, "not in": .contains, "inValues": .containsValue, "startsWith": .startsWith, "endsWith": .endsWith, "|": .union, "&": .intersect, "zip": .zip,
            ]
            if let opcode = mapping[op] {
                r.emit(opcode, d, a, b)
                if op == "not in" { r.emit(.not, d, d) }
            }
            if let short {
                let end = r.emit(.jump)
                r.patch(short, target: r.code.count)
                r.emit(op == "and" ? .loadFalse : .loadTrue, d)
                r.patch(end, target: r.code.count)
            }
        case .cast(let value, let type): try cast(d, expression(value, r, scope), type, r)
        case .check(let value, let type): try cast(d, expression(value, r, scope), type, r, check: true)
        case .call(let name, let arguments):
            let regs = try arguments.map { try expression($0.value, r, scope) }
            if let handler = scope.get(name) {
                if let labels = scope.handler(name), labels != arguments.map(\.label) { r.emit(.loadNothing, d) } else { r.emit(.bindHandler, d, handler, list(regs)) }
            } else {
                let key = signature(name, arguments.map(\.label))
                if definitions[key] == nil {
                    if let definition = definitions.values.first(where: { $0.name == name }) {
                        throw error(definition.kind == "predicate" ? "validate.wrongPredicateArity" : "validate.wrongFunctionArity", e.location, symbol: name, kind: definition.kind == "predicate" ? .predicate : .function)
                    }
                    throw error("validate.missingCallable", e.location, symbol: name, kind: .globalDefinition)
                }
                stage(regs, r)
                let instruction = r.emit(.call, d, flags: definitions[key]?.kind == "predicate" ? 0x20 : 0)
                r.calls.append((instruction, key))
                r.dependencies.insert(key)
            }
        case .predicate(let value, let name):
            let matches = definitions.values.filter { $0.name == name && $0.kind == "predicate" && $0.parameters.count == 1 }
            guard matches.count == 1, let definition = matches.first else { throw error("validate.invalidPredicate", e.location, symbol: name, kind: .predicate) }
            stage([try expression(value, r, scope)], r)
            let instruction = r.emit(.call, d, flags: 0x20)
            r.calls.append((instruction, definition.signature))
            r.dependencies.insert(definition.signature)
        case .extensionCall(let ns, let function, let arguments):
            let regs = try arguments.map { try expression($0.value, r, scope) }
            let bind = importBinding(.extensionCall, ns + "." + function, arguments.map(\.label))
            r.emit(.callExternal, d, bind, list(regs))
        case .handler(let name, let parameters): r.emit(.loadHandler, d, 0, textList([name] + parameters.map(\.label)))
        case .message(let name, let arguments):
            let regs = try arguments.map { try expression($0.value, r, scope) }
            r.emit(.loadMessage, d, textList([name] + arguments.map(\.label)), list(regs))
        case .constructor(let type, let arguments): try constructor(type, arguments, d, r, scope)
        case .list(let items):
            let location = r.location
            if items.allSatisfy({ scalarConstant($0) != nil }) { r.location = .init(sourceName: "UnknownSource") }
            try stageExpressions(items, r, scope)
            r.location = location
            r.emit(.createList, d)
        case .map(let entries):
            try stageExpressions(entries.map(\.1), r, scope)
            r.emit(.createMap, d, textList(entries.map(\.0)))
        case .member(let value, let name):
            let a = try expression(value, r, scope)
            r.emit(.memberAccess, d, text(name), a)
        case .selector(let value, let selection): try selector(value, selection, d, r, scope)
        case .intrinsic(let name, let args): try intrinsic(name, args, d, r, scope)
        case .range(let from, let to, let step):
            let a = try expression(from, r, scope)
            let b = try expression(to, r, scope)
            if let step {
                let c = try expression(step, r, scope)
                r.emit(.createRangeWithStep, d, a, b, payload: UInt64(c))
            } else {
                r.emit(.createRange, d, a, b)
            }
        case .random(let from, let to):
            let a = try expression(from, r, scope)
            let b = try expression(to, r, scope)
            r.emit(from.decimalLiteral || to.decimalLiteral ? .randomTakeFloat : .randomTake, d, a, b)
        case .seeded(let seed, let value):
            try randomPush(seed, r, scope)
            _ = try expression(value, r, scope, destination: d)
            r.emit(.randomPop)
        case .dice(let count, let sides):
            if count > Int(Int16.max) || sides > Int(Int16.max) { throw error("compile.numericLimitExceeded", e.location, phase: .compile) }
            r.emit(.createDice, d, count, sides)
        case .series(let kind): r.emit(.createSeries, d, 0, kind == "fibonacci" ? 1 : 2)
        case .choice(let branches, let fallback):
            var ends: [Int] = []
            for (value, condition) in branches {
                let c = try expression(condition, r, scope)
                let branch = r.emit(.jumpIfNotTrue, 0, c)
                _ = try expression(value, r, scope, destination: d)
                ends.append(r.emit(.jump))
                r.patch(branch, target: r.code.count)
            }
            _ = try expression(fallback, r, scope, destination: d)
            for end in ends { r.patch(end, target: r.code.count) }
        case .generated(_, let name, let source, let range, let condition, let projection):
            let builder = r.temporary()
            r.emit(.listBuilderCreate, builder)
            let child = GesScope(scope)
            let item = r.local()
            let iterator = try iterator(source, range, r, scope)
            child.values[name] = item
            r.symbols.append((name, item, 0, false))
            let begin = r.code.count
            let next = r.emit(.iteratorNext, item, iterator)
            let skip = try condition.map { r.emit(.jumpIfNotTrue, 0, try expression($0, r, child)) }
            let value = try expression(projection, r, child)
            r.emit(.listBuilderAdd, 0, builder, value)
            if let skip { r.patch(skip, target: r.code.count) }
            r.emit(.jump, 0, 0, begin)
            r.patch(next, target: r.code.count)
            r.emit(.iteratorClose, 0, iterator)
            r.emit(.listBuilderFinish, d, builder)
        }
        return d
    }

    func constructor(_ type: String, _ arguments: [GesArgument], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        if type == "vector" || type == "point" {
            let first = arguments.first?.label ?? "_"
            let start = ["x", "y", "z"].firstIndex(of: first) ?? 0
            try stageExpressions(arguments.map(\.value), r, scope)
            r.emit(type == "vector" ? .createVector : .createPoint, d, start)
            return
        }
        if let record = records[type] {
            var unnamed = 0
            let args = record.fields.filter { $0.label != nil }.map { field -> GesExpression in
                let value: GesExpression?
                if field.label == "_" {
                    let values = arguments.filter { $0.label == "_" }
                    value = unnamed < values.count ? values[unnamed].value : nil
                    unnamed += 1
                } else {
                    value = arguments.first { $0.label == field.label }?.value
                }
                return value ?? .init(.literal(.nothing), r.location)
            }
            try stageExpressions(args, r, scope)
            r.emit(.createRecord, d, recordIDs[type] ?? records.keys.sorted().firstIndex(of: type)!)
            r.dependencies.insert(signature(type, record.fields.compactMap(\.label)))
            return
        }
        if type.first?.isUppercase == true, let external = try catalog?.resolve(type) {
            let labels = arguments.map(\.label)
            guard let ctor = external.constructors.first(where: { $0.parameters.map(\.name).sorted() == labels.sorted() }) else { throw error("validate.invalidTypeConstructor", r.location, symbol: type, kind: .type) }
            let ordered = ctor.parameters.map { p in arguments.first { $0.label == p.name }! }
            try stageExpressions(ordered.map(\.value), r, scope)
            r.emit(.createExternalType, d, importBinding(.externalType, type, ctor.parameters.map(\.name)), textList(ctor.parameters.map(\.name)))
            return
        }
        guard arguments.count == 1 else { throw error("compile.invalidArity", r.location, symbol: type, phase: .compile) }
        try cast(d, expression(arguments[0].value, r, scope), type, r)
    }

    func intrinsic(_ name: String, _ args: [GesExpression], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let regs = try args.map { try expression($0, r, scope) }
        if name == "min" || name == "max" {
            if let first = regs.first {
                r.emit(.move, d, first)
                for value in regs.dropFirst() { r.emit(name == "min" ? .min : .max, d, d, value) }
            }
            return
        }
        let signatures: [String: [Int: Op]] = [
            "clamp": [3: .clamp], "atan2": [2: .atan2], "hypot": [2: .hypot2D, 3: .hypot3D], "distance": [2: .distance, 4: .distance2D, 6: .distance3D], "distanceSquared": [2: .distanceSquared, 4: .distanceSquared2D, 6: .distanceSquared3D],
            "lengthSquared": [1: .lengthSquared, 2: .lengthSquared2D, 3: .lengthSquared3D], "normalize": [1: .normalize, 2: .normalize2D, 3: .normalize3D], "dot": [2: .dot, 4: .dot2D, 6: .dot3D], "cross": [2: .cross, 4: .cross2D, 6: .cross3D],
            "angleBetween": [2: .angleBetween, 4: .angleBetween2D, 6: .angleBetween3D],
        ]
        guard let opcode = signatures[name]?[regs.count] else { throw error("compile.invalidArity", r.location, symbol: name, phase: .compile) }
        var payload: UInt64 = 0
        for (i, value) in regs.dropFirst(2).enumerated() { payload |= UInt64(value) << (i * 16) }
        r.emit(opcode, d, regs[0], regs.count > 1 ? regs[1] : 0, payload: payload)
    }
}
