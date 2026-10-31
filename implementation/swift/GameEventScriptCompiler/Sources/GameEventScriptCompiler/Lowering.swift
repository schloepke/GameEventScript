// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    // Nested blocks and expressions recurse through `statements` and `expression`. Each statement and expression form
    // lowers in its own function, so that an unoptimized build does not reserve stack for the locals of every form on
    // each level.
    func statements(_ body: [GesStatement], _ r: GesRoutine, _ scope: GesScope) throws {
        for statement in body {
            try builder.checkLimits(r)
            r.location = statement.location
            switch statement.kind {
            case .assembly(let block): try assembly(block, r, scope)
            case .letBinding(let name, let value): _ = try letBinding(name, value, r, scope)
            case .expression(let value): _ = try expression(value, r, scope)
            case .publish(let publish, let message, let tags): try publishStatement(publish, message, tags, r, scope)
            case .condition(let conditions, let yes, let no): try conditionStatement(conditions, yes, no, statement.location, r, scope)
            case .loop(let names, let sequence, let range, let body): try loopStatement(names, sequence, range, body, statement.location, r, scope)
            case .seeded(let seed, let body):
                try randomPush(seed, r, scope)
                try statements(body, r, GesScope(scope))
                r.location = statement.location
                r.emit(.randomPop)
            }
        }
    }

    func publishStatement(_ publish: Bool, _ message: GesExpression, _ tags: [GesExpression], _ r: GesRoutine, _ scope: GesScope) throws {
        let tagRegisters = try tags.map { try expression($0, r, scope) }
        if case .message(let name, let arguments) = message.kind {
            let regs = try arguments.map { try expression($0.value, r, scope) }
            let bind = builder.importBinding(.outboundMessage, name, arguments.map(\.label))
            r.emit(publish ? (tags.isEmpty ? .publishMessage : .publishMessageWithTags) : (tags.isEmpty ? .emitMessage : .emitMessageWithTags), bind, tags.isEmpty ? 0 : builder.list(tagRegisters), builder.list(regs))
        } else {
            let reg = try expression(message, r, scope)
            r.emit(publish ? (tags.isEmpty ? .publishMessageValue : .publishMessageValueWithTags) : (tags.isEmpty ? .emitMessageValue : .emitMessageValueWithTags), 0, reg, tags.isEmpty ? 0 : builder.list(tagRegisters))
        }
    }

    func conditionStatement(
        _ conditions: [(binding: String?, value: GesExpression, location: GesSourceLocation)],
        _ yes: [GesStatement],
        _ no: [GesStatement],
        _ location: GesSourceLocation,
        _ r: GesRoutine,
        _ scope: GesScope
    ) throws {
        let child = GesScope(scope)
        var branches: [Int] = []
        for (binding, value, _) in conditions { branches.append(r.emit(.jumpIfNotTrue, 0, try condition(binding, value, r, child))) }
        try statements(yes, r, child)
        r.location = location
        let end = r.emit(.jump)
        r.mark(branches)
        try statements(no, r, GesScope(scope))
        r.mark([end])
    }

    // The register an `if` condition tests.
    func condition(_ binding: String?, _ value: GesExpression, _ r: GesRoutine, _ scope: GesScope) throws -> Int {
        guard let binding else { return try expression(value, r, scope) }
        // As in C#: an ordinary let statement, then a presence check of the binding.
        let bound = try letBinding(binding, value, r, scope)
        let reg = r.temporary()
        // The synthetic presence check has no source range in C#, so it carries no mapping.
        let outer = r.location
        r.location = GesAstOptimizer.noRange(outer)
        r.emit(.hasValue, reg, bound)
        r.location = outer
        return reg
    }

    func loopStatement(_ names: [String], _ sequence: GesExpression, _ range: Bool, _ body: [GesStatement], _ location: GesSourceLocation, _ r: GesRoutine, _ scope: GesScope) throws {
        let child = GesScope(scope)
        let items = names.map { name in
            let item = r.local()
            child.values[name] = item
            r.symbols.append((name, item, false))
            return item
        }
        let iterator = try iterator(sequence, range, r, scope)
        let start = r.mark()
        let next = r.emit(.iteratorNext, items.count == 1 ? items[0] : builder.list(items), iterator, flags: items.count == 1 ? 0 : 32)
        try statements(body, r, child)
        r.location = location
        r.emit(.jump, 0, 0, start)
        r.mark([next])
        r.emit(.iteratorClose, 0, iterator)
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
        } else if isCombinedSource(e) {
            var invalid: [Int] = []
            _ = try componentIterator(target, e, &invalid, r, scope)
            r.mark(invalid)
        } else {
            let value = try expression(e, r, scope)
            r.emit(.iteratorCreate, target, value)
        }
        return target
    }

    // A local binding in its own register, as C# declares it for `let`.
    func letBinding(_ name: String, _ value: GesExpression, _ r: GesRoutine, _ scope: GesScope) throws -> Int {
        let d = r.local()
        // Declared before its value is evaluated, so the symbol precedes bindings declared inside the value.
        r.symbols.append((name, d, false))
        scope.values[name] = d
        _ = try expression(value, r, scope, destination: d)
        if knownNonList(value, r, scope) { r.nonListBindings.insert(d) }
        if case .handler(_, let parameters) = value.kind { scope.handlers[name] = parameters.map(\.label) }
        return d
    }

    func literal(_ value: GesValue, _ target: Int, _ r: GesRoutine) throws {
        let flag = GesCompilerSupport.unitID(value.unit)
        switch value.valueKind {
        case .nothing: r.emit(.loadNothing, target)
        case .boolean: r.emit(value.asBoolean ? .loadTrue : .loadFalse, target)
        case .integer: r.emit(.loadInteger, target, payload: UInt64(bitPattern: value.integerValue!), flags: flag)
        case .float: r.emit(.loadFloat, target, payload: value.asNumber.bitPattern, flags: flag)
        case .percentage: r.emit(.loadPercentage, target, payload: value.asNumber.bitPattern)
        case .text: r.emit(.loadText, target, builder.text(value.textValue!))
        case .tag: r.emit(.loadTag, target, builder.text(value.textValue!))
        default: throw compileError("compile.unsupportedConstruct", r.location, phase: .compile)
        }
    }

    // The dispatcher binds no payloads: an unoptimized build reserves stack for the bindings of every case on each
    // call, so each form function reads its own.
    func expression(_ e: GesExpression, _ r: GesRoutine, _ scope: GesScope, destination: Int? = nil) throws -> Int {
        let oldLocation = r.location
        r.location = e.location
        defer { r.location = oldLocation }
        switch e.kind {
        case .name: return try nameValue(e, r, scope, destination: destination)
        case .constant: return try expression(constantValue(e), r, scope, destination: destination)
        default: break
        }
        let d = destination ?? r.temporary()
        if try scalarLiteral(e, d, r) { return d }
        switch e.kind {
        case .name, .constant: break
        case .assembly, .literal, .handler, .dice, .series: try leafExpression(e, d, r, scope)
        case .unary: try unaryExpression(e, d, r, scope)
        case .binary: try binaryExpression(e, d, r, scope)
        case .between: try betweenExpression(e, d, r, scope)
        case .cast, .check: try castExpression(e, d, r, scope)
        case .call: try callExpression(e, d, r, scope)
        case .predicate: try predicateExpression(e, d, r, scope)
        case .extensionCall: try extensionCallExpression(e, d, r, scope)
        case .send: try sendExpression(e, d, r, scope)
        case .message: try messageExpression(e, d, r, scope)
        case .constructor: try constructorExpression(e, d, r, scope)
        case .list, .map: try collectionLiteral(e, d, r, scope)
        case .member: try memberExpression(e, d, r, scope)
        case .combined: try combinedExpression(e, d, r, scope)
        case .selector: try selectorExpression(e, d, r, scope)
        case .intrinsic: try intrinsicExpression(e, d, r, scope)
        case .range: try rangeExpression(e, d, r, scope)
        case .random: try randomExpression(e, d, r, scope)
        case .seeded: try seededExpression(e, d, r, scope)
        case .choice: try choiceExpression(e, d, r, scope)
        case .generated: try generatedExpression(e, d, r, scope)
        }
        return d
    }

    func nameValue(_ e: GesExpression, _ r: GesRoutine, _ scope: GesScope, destination: Int?) throws -> Int {
        guard case .name(let name) = e.kind else { preconditionFailure() }
        guard let value = scope.get(name) else { throw compileError("compile.unresolvedSymbol", e.location, symbol: name, phase: .compile) }
        if let destination, destination != value {
            r.emit(.move, destination, value)
            return destination
        }
        return value
    }

    // The constant's value at the location of its use.
    func constantValue(_ e: GesExpression) throws -> GesExpression {
        guard case .constant(let name) = e.kind else { preconditionFailure() }
        guard let value = constants[name] else { throw compileError("compile.unresolvedSymbol", e.location, symbol: name, phase: .compile) }
        return GesExpression(value.kind, e.location)
    }

    // Loads a scalar literal into `d`; false for every other expression.
    func scalarLiteral(_ e: GesExpression, _ d: Int, _ r: GesRoutine) throws -> Bool {
        guard let value = scalarConstant(e), [.nothing, .boolean, .integer, .float, .percentage, .text, .tag].contains(value.valueKind) else { return false }
        try literal(value, d, r)
        return true
    }

    // Forms without nested expressions, and inline assembly.
    func leafExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        switch e.kind {
        case .assembly(let block): try assembly(block, r, scope, destination: d)
        case .literal(let value): try literal(value, d, r)
        case .handler(let name, let parameters): r.emit(.loadHandler, d, 0, builder.textList([name] + parameters.map(\.label)))
        case .dice(let count, let sides):
            if count > Int(Int16.max) || sides > Int(Int16.max) { throw compileError("compile.numericLimitExceeded", e.location, phase: .compile) }
            r.emit(.createDice, d, count, sides)
        case .series(let kind): r.emit(.createSeries, d, 0, kind == "fibonacci" ? 1 : 2)
        default: preconditionFailure()
        }
    }

    func unaryExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .unary(let op, let input) = e.kind else { preconditionFailure() }
        if op == .predicate, case .extensionCall(let ns, let function, let args) = input.kind {
            let regs = try args.map { try expression($0.value, r, scope) }
            r.emit(.callExternal, d, builder.importBinding(.extensionCall, ns + "." + function, args.map(\.label)), builder.list(regs), flags: 0x20)
            return
        }
        let a = try expression(input, r, scope)
        if op == .predicate {
            r.emit(.loadTrue, d)
            r.emit(.equal, d, a, d)
        } else {
            r.emit(op.opcode, d, a, flags: op == .parseStrict ? 64 : 0)
        }
    }

    func binaryExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .binary(let op, let left, let right) = e.kind else { preconditionFailure() }
        let a = try expression(left, r, scope)
        let short = shortCircuit(op, a, d, r)
        let b = try expression(right, r, scope)
        if op == .multiply && !knownNonList(left, r, scope) && !knownNonList(right, r, scope) {
            listOrNumberProduct(a, b, d, r)
        } else {
            r.emit(op.opcode, d, a, b)
        }
        if let short { r.mark([short]) }
    }

    // The jump that skips the right operand of `or`, `and` and `implies`; nil for every other operator.
    func shortCircuit(_ op: GesBinaryOperator, _ a: Int, _ d: Int, _ r: GesRoutine) -> Int? {
        let shortOpcode: GesBytecodeOpCode? = op == .or ? .or : op == .and ? .and : op == .implies ? .implies : nil
        guard let shortOpcode else { return nil }
        r.emit(shortOpcode, d, a, a)
        return r.emit(op == .or ? .jumpIfTrue : .jumpIfFalse, 0, a)
    }

    // A product of operands that may be lists: two lists form their cartesian product.
    func listOrNumberProduct(_ a: Int, _ b: Int, _ d: Int, _ r: GesRoutine) {
        let check = r.temporary()
        r.emit(.checkType, check, a, Int(GesBytecodeTypeKind.list.rawValue))
        let first = r.emit(.jumpIfNotTrue, 0, check)
        r.emit(.checkType, check, b, Int(GesBytecodeTypeKind.list.rawValue))
        let second = r.emit(.jumpIfNotTrue, 0, check)
        r.emit(.cartesian, d, a, b)
        let done = r.emit(.jump)
        r.mark([first])
        r.mark([second])
        r.emit(.multiply, d, a, b)
        r.mark([done])
    }

    func betweenExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .between(let input, let minimum, let maximum) = e.kind else { preconditionFailure() }
        let value = try expression(input, r, scope)
        let low = try expression(minimum, r, scope)
        let lower = r.temporary()
        r.emit(.greaterOrEqual, lower, value, low)
        r.emit(.and, d, lower, lower)
        let end = r.emit(.jumpIfFalse, 0, lower)
        let high = try expression(maximum, r, scope)
        let upper = r.temporary()
        r.emit(.lessOrEqual, upper, value, high)
        r.emit(.and, d, lower, upper)
        r.mark([end])
    }

    func castExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        switch e.kind {
        case .cast(let value, let type): try cast(d, expression(value, r, scope), type, r)
        case .check(let value, let type): try cast(d, expression(value, r, scope), type, r, check: true)
        default: preconditionFailure()
        }
    }

    func callExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .call(let name, let arguments) = e.kind else { preconditionFailure() }
        if let handler = scope.get(name) {
            let regs = try arguments.map { try expression($0.value, r, scope) }
            if let labels = scope.handler(name), labels != arguments.map(\.label) { r.emit(.loadNothing, d) } else { r.emit(.bindHandler, d, handler, builder.list(regs)) }
            return
        }
        let key = try callableKey(name, arguments, e.location)
        try stageExpressions(arguments.map(\.value), r, scope)
        r.emit(.call, d, 0, r.call(key), flags: definitions[key]?.kind == "predicate" ? 0x20 : 0)
        r.dependencies.insert(key)
    }

    // The signature a call names; a missing one reports the name's other arity, or that no callable exists.
    func callableKey(_ name: String, _ arguments: [GesArgument], _ location: GesSourceLocation) throws -> String {
        let key = callableSignature(name, arguments.map(\.label))
        if definitions[key] != nil { return key }
        if let definition = definitions.values.first(where: { $0.name == name }) {
            throw compileError(definition.kind == "predicate" ? "validate.wrongPredicateArity" : "validate.wrongFunctionArity", location, symbol: name, kind: definition.kind == "predicate" ? .predicate : .function)
        }
        throw compileError("validate.missingCallable", location, symbol: name, kind: .globalDefinition)
    }

    func predicateExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .predicate(let value, let name) = e.kind else { preconditionFailure() }
        let signature = try predicateSignature(name, count: value == nil ? 0 : 1, e.location)
        if let value { try stageExpressions([value], r, scope) }
        r.emit(.call, d, 0, r.call(signature), flags: 0x20)
        r.dependencies.insert(signature)
    }

    func predicateSignature(_ name: String, count: Int, _ location: GesSourceLocation) throws -> String {
        let matches = definitions.values.filter { $0.name == name && $0.kind == "predicate" && $0.parameters.count == count }
        guard matches.count == 1, let definition = matches.first else {
            throw compileError("validate.invalidPredicate", location, symbol: name, kind: .predicate, message: "No unique predicate named '\(name)' with \(count) parameters is available.")
        }
        return definition.signature
    }

    func extensionCallExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .extensionCall(let ns, let function, let arguments) = e.kind else { preconditionFailure() }
        let regs = try arguments.map { try expression($0.value, r, scope) }
        let bind = builder.importBinding(.extensionCall, ns + "." + function, arguments.map(\.label))
        r.emit(.callExternal, d, bind, builder.list(regs))
    }

    func sendExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .send(let publish, let message, let tags, let delay) = e.kind else { preconditionFailure() }
        let time = try delay.map { try expression($0, r, scope) }
        let tagRegisters = try tags.map { try expression($0, r, scope) }
        var flags: UInt8 = tags.isEmpty ? 0 : 0x40
        let x: Int
        var y = 0
        if case .message(let name, let arguments) = message.kind {
            let values = try arguments.map { try expression($0.value, r, scope) }
            x = builder.importBinding(.outboundMessage, name, arguments.map(\.label))
            y = builder.list(values)
        } else {
            flags |= 0x80
            x = try expression(message, r, scope)
        }
        let op: GesBytecodeOpCode = publish ? (time == nil ? .publishInstant : .publishAfter) : (time == nil ? .emitInstant : .emitAfter)
        r.emit(op, d, x, y, payload: UInt64(tags.isEmpty ? 0 : builder.list(tagRegisters)) | UInt64(time ?? 0) << 16, flags: flags)
    }

    func messageExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .message(let name, let arguments) = e.kind else { preconditionFailure() }
        let regs = try arguments.map { try expression($0.value, r, scope) }
        r.emit(.loadMessage, d, builder.textList([name] + arguments.map(\.label)), builder.list(regs))
    }

    func constructorExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .constructor(let type, let arguments) = e.kind else { preconditionFailure() }
        try constructor(type, arguments, d, r, scope)
    }

    func collectionLiteral(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        switch e.kind {
        case .list(let items):
            let location = r.location
            if items.allSatisfy({ scalarConstant($0) != nil }) { r.location = .init(sourceName: "UnknownSource") }
            try stageExpressions(items, r, scope)
            r.location = location
            r.emit(.createList, d)
        case .map(let entries):
            try stageExpressions(entries.map(\.1), r, scope)
            r.emit(.createMap, d, builder.textList(entries.map(\.0)))
        default: preconditionFailure()
        }
    }

    func memberExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .member(let value, let name) = e.kind else { preconditionFailure() }
        let a = try expression(value, r, scope)
        r.emit(.memberAccess, d, builder.text(name), a)
    }

    func combinedExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .combined(let operation, let sources) = e.kind else { preconditionFailure() }
        try combinedCollection(operation, sources, d, r, scope)
    }

    func selectorExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .selector(let value, let selection) = e.kind else { preconditionFailure() }
        try selector(value, selection, d, r, scope)
    }

    func intrinsicExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .intrinsic(let name, let args) = e.kind else { preconditionFailure() }
        try intrinsic(name, args, d, r, scope)
    }

    func rangeExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .range(let from, let to, let step) = e.kind else { preconditionFailure() }
        let a = try expression(from, r, scope)
        let b = try expression(to, r, scope)
        if let step {
            let c = try expression(step, r, scope)
            r.emit(.createRangeWithStep, d, a, b, payload: UInt64(c))
        } else {
            r.emit(.createRange, d, a, b)
        }
    }

    func randomExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .random(let from, let to) = e.kind else { preconditionFailure() }
        let a = try expression(from, r, scope)
        let b = try expression(to, r, scope)
        r.emit(from.decimalLiteral || to.decimalLiteral ? .randomTakeFloat : .randomTake, d, a, b)
    }

    func seededExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .seeded(let seed, let value) = e.kind else { preconditionFailure() }
        try randomPush(seed, r, scope)
        _ = try expression(value, r, scope, destination: d)
        r.emit(.randomPop)
    }

    func choiceExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .choice(let branches, let fallback) = e.kind else { preconditionFailure() }
        var ends: [Int] = []
        for (value, condition) in branches {
            let c = try expression(condition, r, scope)
            let branch = r.emit(.jumpIfNotTrue, 0, c)
            // As C# EmitGuardedChoiceInto: read each value, then move it into the result.
            let result = try expression(value, r, scope)
            if result != d { r.emit(.move, d, result) }
            ends.append(r.emit(.jump))
            r.mark([branch])
        }
        let otherwise = try expression(fallback, r, scope)
        if otherwise != d { r.emit(.move, d, otherwise) }
        r.mark(ends)
    }

    func generatedExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .generated(_, let name, let source, let range, let condition, let projection) = e.kind else { preconditionFailure() }
        let collector = r.temporary()
        r.emit(.listBuilderCreate, collector)
        let child = GesScope(scope)
        let item = r.local()
        let iterator = try iterator(source, range, r, scope)
        child.values[name] = item
        r.symbols.append((name, item, false))
        let begin = r.mark()
        let next = r.emit(.iteratorNext, item, iterator)
        let skip = try condition.map { r.emit(.jumpIfNotTrue, 0, try expression($0, r, child)) }
        let value = try expression(projection, r, child)
        r.emit(.listBuilderAdd, 0, collector, value)
        if let skip { r.mark([skip]) }
        r.emit(.jump, 0, 0, begin)
        r.mark([next])
        r.emit(.iteratorClose, 0, iterator)
        r.emit(.listBuilderFinish, d, collector)
    }

    static let splitTypes: Set<String> = ["__split", "__splitWhitespace"]

    // Built-in types whose constructor builds data from labeled or several arguments instead of casting one value.
    static let dataTypes: Set<String> = ["number", "range", "message", "nothing", "percentage", "boolean", "text", "tag", "list", "map", "dice", "handler"]

    func constructor(_ type: String, _ arguments: [GesArgument], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        if Self.splitTypes.contains(type) { return try splitText(arguments, d, r, scope) }
        if type == "record" || type == "series" || Self.dataTypes.contains(type) && (arguments.count != 1 || arguments[0].label != "_") { return try constructData(type, arguments, d, r, scope) }
        if type == "vector" || type == "point" { return try spatial(type, arguments, d, r, scope) }
        if let record = records[type] { return try recordConstructor(type, record, arguments, d, r, scope) }
        if type.first?.isUppercase == true, let external = try catalog?.resolve(type) { return try externalConstructor(type, external, arguments, d, r, scope) }
        guard arguments.count == 1 else { throw compileError("compile.invalidArity", r.location, symbol: type, phase: .compile) }
        try cast(d, expression(arguments[0].value, r, scope), type, r)
    }

    func splitText(_ arguments: [GesArgument], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let regs = try arguments.map { try expression($0.value, r, scope) }
        r.emit(.splitText, d, regs[0], regs.count > 1 ? regs[1] : 0, payload: regs.count == 1 ? 1 : 0)
    }

    func constructData(_ type: String, _ arguments: [GesArgument], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let regs = try arguments.map { try expression($0.value, r, scope) }
        r.emit(.constructData, d, builder.text(type), builder.list(regs), payload: UInt64(builder.textList(arguments.map(\.label))))
    }

    func spatial(_ type: String, _ arguments: [GesArgument], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let first = arguments.first?.label ?? "_"
        let start = ["x", "y", "z"].firstIndex(of: first) ?? 0
        try stageExpressions(arguments.map(\.value), r, scope)
        r.emit(type == "vector" ? .createVector : .createPoint, d, start)
    }

    func recordConstructor(_ type: String, _ record: GesRecord, _ arguments: [GesArgument], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
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
        r.emit(.createRecord, d, builder.recordIDs[type] ?? records.keys.sorted().firstIndex(of: type)!)
        r.dependencies.insert(callableSignature(type, record.fields.compactMap(\.label)))
    }

    func externalConstructor(_ type: String, _ external: GesExternalTypeDefinition, _ arguments: [GesArgument], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let labels = arguments.map(\.label)
        guard let ctor = external.constructors.first(where: { $0.parameters.map(\.name).sorted() == labels.sorted() }) else { throw compileError("validate.invalidTypeConstructor", r.location, symbol: type, kind: .type) }
        let ordered = ctor.parameters.map { p in arguments.first { $0.label == p.name }! }
        try stageExpressions(ordered.map(\.value), r, scope)
        r.emit(.createExternalType, d, builder.importBinding(.externalType, type, ctor.parameters.map(\.name)), builder.textList(ctor.parameters.map(\.name)))
    }

    func intrinsic(_ name: String, _ args: [GesExpression], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        if name == "min" || name == "max" {
            // As C# EmitVariadic: each further operand is evaluated right before it is combined.
            guard let first = args.first else {
                r.emit(.loadNothing, d)
                return
            }
            let value = try expression(first, r, scope)
            if value != d { r.emit(.move, d, value) }
            for argument in args.dropFirst() { r.emit(name == "min" ? .min : .max, d, d, try expression(argument, r, scope)) }
            return
        }
        let regs = try args.map { try expression($0, r, scope) }
        let signatures: [String: [Int: GesBytecodeOpCode]] = [
            "clamp": [3: .clamp], "atan2": [2: .atan2], "hypot": [2: .hypot2D, 3: .hypot3D], "distance": [2: .distance, 4: .distance2D, 6: .distance3D], "distanceSquared": [2: .distanceSquared, 4: .distanceSquared2D, 6: .distanceSquared3D],
            "lengthSquared": [1: .lengthSquared, 2: .lengthSquared2D, 3: .lengthSquared3D], "normalize": [1: .normalize, 2: .normalize2D, 3: .normalize3D], "dot": [2: .dot, 4: .dot2D, 6: .dot3D], "cross": [2: .cross, 4: .cross2D, 6: .cross3D],
            "angleBetween": [2: .angleBetween, 4: .angleBetween2D, 6: .angleBetween3D],
        ]
        guard let opcode = signatures[name]?[regs.count] else { throw compileError("compile.invalidArity", r.location, symbol: name, phase: .compile) }
        var payload: UInt64 = 0
        for (i, value) in regs.dropFirst(2).enumerated() { payload |= UInt64(value) << (i * 16) }
        r.emit(opcode, d, regs[0], regs.count > 1 ? regs[1] : 0, payload: payload)
    }
}

extension GesUnaryOperator {
    // Opcode of every operator lowered as one instruction; `predicate` lowers to a comparison instead.
    var opcode: GesBytecodeOpCode {
        switch self {
        case .negate: .negate
        case .not: .not
        case .parse, .parseStrict: .parseLiteral
        case .empty: .isEmpty
        case .hasValue: .hasValue
        case .abs: .abs
        case .naturalLog: .logN
        case .exp: .exp
        case .chance: .chance
        case .floor: .floor
        case .ceil: .ceil
        case .truncate: .truncate
        case .degreeToRadians: .degreeToRadians
        case .degreeFromRadians: .degreeFromRadians
        case .wrapDegree: .wrapDegree
        case .roundHalfEven: .roundHalfEven
        case .roundHalfUp: .roundHalfUp
        case .roundHalfDown: .roundHalfDown
        case .sin: .sin
        case .cos: .cos
        case .tan: .tan
        case .asin: .asin
        case .acos: .acos
        case .atan: .atan
        case .predicate: .equal
        }
    }
}

extension GesBinaryOperator {
    var opcode: GesBytecodeOpCode {
        switch self {
        case .add: .add
        case .subtract: .subtract
        case .multiply: .multiply
        case .divide: .divide
        case .power: .power
        case .integerDivide: .integerDivide
        case .modulo: .modulo
        case .remainder: .remainder
        case .equal: .equal
        case .notEqual: .notEqual
        case .less: .less
        case .greater: .greater
        case .lessOrEqual: .lessOrEqual
        case .greaterOrEqual: .greaterOrEqual
        case .default: .default
        case .or: .or
        case .and: .and
        case .xor: .xor
        case .implies: .implies
        case .contains: .contains
        case .containsValue: .containsValue
        case .startsWith: .startsWith
        case .endsWith: .endsWith
        case .union: .union
        case .intersect: .intersect
        case .zip: .zip
        }
    }

    // Arithmetic operators that constant folding evaluates.
    var isArithmetic: Bool { [.add, .subtract, .multiply, .divide, .integerDivide, .modulo, .remainder, .power].contains(self) }
}
