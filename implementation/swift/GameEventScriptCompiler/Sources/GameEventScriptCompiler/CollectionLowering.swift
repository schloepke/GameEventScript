// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

extension GesCompiler {
    // Selectors that an enclosing pipeline terminal evaluates inline, element by element.
    static let prefixSelectors: Set<String> = ["filter", "select"]

    static let terminals: Set<String> = ["foreach", "filter", "select", "any", "all", "count", "sum", "average", "min", "max", "map", "group", "order", "distinct", "first", "last", "single", "objectMatch", "take", "drop", "draw"]

    // How a selector lowers, decided before any operand is evaluated.
    enum SelectorRoute {
        case combinedFold
        case combinedDistinct
        case setSlice
        case combinedPipeline(GesExpression, [GesSelector])
        case prefixPipeline(GesExpression, [GesSelector])
        case operation
    }

    func selector(_ source: GesExpression, _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        switch selectorRoute(source, s) {
        case .combinedFold: try fold(0, s, d, r, scope, sourceExpression: source)
        // The native operation preserves Dice and rejects Map sources.
        case .combinedDistinct: r.emit(.distinct, d, try expression(source, r, scope))
        // Slices preserve Dice and reject Maps unless a filter/select has produced a List.
        case .setSlice: try slice(s, d, expression(source, r, scope), r)
        case .combinedPipeline(let root, let prefix): try pipeline(0, prefix, s, d, r, scope, sourceExpression: root)
        case .prefixPipeline(let root, let prefix): try pipeline(expression(root, r, scope), prefix, s, d, r, scope)
        case .operation: try selectorOperation(expression(source, r, scope), s, d, r, scope)
        }
    }

    // Filter and select selectors directly below `s` form its inline prefix.
    func selectorRoute(_ source: GesExpression, _ s: GesSelector) -> SelectorRoute {
        var root = source
        var prefix: [GesSelector] = []
        while case .selector(let parent, let selector) = root.kind, Self.prefixSelectors.contains(selector.operation) {
            prefix.insert(selector, at: 0)
            root = parent
        }
        if isCombinedSource(source) && ["fold", "reduce"].contains(s.operation) { return .combinedFold }
        if isCombinedSource(root) && s.operation == "distinct" && s.expressions.isEmpty { return .combinedDistinct }
        if prefix.isEmpty, case .combined(let operation, _) = root.kind, ["union", "intersect", "difference"].contains(operation), ["take", "drop", "draw"].contains(s.operation) { return .setSlice }
        if isCombinedSource(root) && Self.terminals.contains(s.operation) { return .combinedPipeline(root, prefix) }
        // As C#: a plain distinct is no inline terminal; the prefix result is collected and the opcode applied.
        if !prefix.isEmpty && Self.terminals.contains(s.operation) && !(s.operation == "distinct" && s.expressions.isEmpty) { return .prefixPipeline(root, prefix) }
        return .operation
    }

    // A selector applied to its evaluated source.
    func selectorOperation(_ input: Int, _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        switch s.operation {
        case "fold", "reduce": try fold(input, s, d, r, scope)
        case "index": try indexAccess(input, s.expressions[0], d, r, scope)
        case "keys", "values", "entries": r.emit(s.operation == "keys" ? .keysOfMap : s.operation == "values" ? .valuesOfMap : .entriesOfMap, d, input)
        case "shuffle", "reverse": r.emit(s.operation == "shuffle" ? .shuffle : .reverse, d, input)
        case "sort": r.emit(s.mode == "descending" ? .sortDescending : .sortAscending, d, input)
        case "contains": r.emit(s.mode == "all" ? .containsAll : s.mode == "any" ? .containsAny : .contains, d, try expression(s.expressions[0], r, scope), input)
        case "term": r.emit(.term, d, input, try expression(s.expressions[0], r, scope))
        case "distinct" where s.expressions.isEmpty: r.emit(.distinct, d, input)
        // As C#: a count whose predicate is the literal true counts directly.
        case "count" where s.expressions.isEmpty || s.expressions.first.flatMap(scalarConstant) == .boolean(true): r.emit(.count, d, input)
        case "first" where s.expressions.isEmpty, "last" where s.expressions.isEmpty, "single" where s.expressions.isEmpty: r.emit(s.operation == "first" ? .first : s.operation == "last" ? .last : .single, d, input)
        case "take", "drop", "draw": try slice(s, d, input, r)
        case "hasPattern", "takePattern": try dicePattern(input, s, d, r, scope)
        case "choose": try choose(input, s, d, r, scope)
        default: try pipeline(input, [], s, d, r, scope)
        }
    }

    func indexAccess(_ input: Int, _ index: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        if case .literal(let v) = index.kind, let n = v.integerValue, (0...65535).contains(n) {
            r.emit(.indexAccess, d, Int(n), input)
        } else if case .literal(let v) = index.kind, v.valueKind == .tag || v.valueKind == .text, let tag = v.textValue {
            r.emit(.memberAccess, d, builder.text(tag), input)
        } else {
            r.emit(.propertyAccess, d, try expression(index, r, scope), input)
        }
    }

    func dicePattern(_ input: Int, _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let kind = s.mode == "fullHouse" ? 2 : s.mode == "straight" ? 3 : s.mode == "face" ? 1 : 0
        let face = try s.expressions.first.map { try expression($0, r, scope) } ?? 0
        r.emit(s.operation == "hasPattern" ? .hasPattern : .takePattern, d, input, s.count, payload: UInt64(kind) | UInt64(face) << 16)
    }

    func choose(_ input: Int, _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        if s.count > Int(Int16.max) { throw compileError("compile.numericLimitExceeded", r.location, phase: .compile) }
        if s.expressions.count > 1 { return try weightedChoose(input, s, d, r, scope) }
        var values = input
        if let condition = s.expressions.first {
            values = r.temporary()
            try pipeline(input, [], .init(operation: "filter", name: s.name, names: s.names, expressions: [condition]), values, r, scope)
        }
        if s.mode == "random" { r.emit(s.count == 1 ? .oneRandom : .takeRandom, d, values, s.count == 1 ? 0 : s.count) } else { r.emit(s.count == 1 ? .first : .takeFirst, d, values, s.count == 1 ? 0 : s.count) }
    }

    func fold(_ input: Int, _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope, sourceExpression: GesExpression? = nil) throws {
        let accumulator = r.temporary()
        let iterator = r.temporary()
        let item = r.temporary()
        var invalid: [Int] = []
        var row: GesComponentRow?
        var validSource: Int?
        if let sourceExpression {
            let valid = r.temporary()
            r.emit(.loadFalse, valid)
            var prepared: [Int] = []
            row = try componentIterator(iterator, sourceExpression, &prepared, r, scope)
            r.emit(.loadTrue, valid)
            r.mark(prepared)
            validSource = valid
        }
        if s.operation == "fold" { _ = try expression(s.expressions[0], r, scope, destination: accumulator) } else { r.emit(.loadNothing, accumulator) }
        if let validSource { invalid.append(r.emit(.jumpIfNotTrue, 0, validSource)) } else { invalid.append(r.emit(.iteratorCreateOrJump, iterator, input)) }
        let first = s.operation == "reduce" ? r.emit(.iteratorNext, accumulator, iterator) : nil
        let loop = r.mark()
        let end = r.emit(.iteratorNext, row.map { builder.list($0.components) } ?? item, iterator, flags: row == nil ? 0 : 32)
        if let row { r.componentRows[item] = row }
        let child = GesScope(scope)
        child.values[s.accumulator] = accumulator
        bindComponents(s.names, fallback: s.name, current: item, r, child)
        let value = try expression(s.expressions.last!, r, child)
        r.componentRows.removeValue(forKey: item)
        if value != accumulator { r.emit(.move, accumulator, value) }
        r.emit(.jump, 0, 0, loop)
        r.mark([end])
        if let first { r.mark([first]) }
        r.emit(.iteratorClose, 0, iterator)
        r.emit(.move, d, accumulator)
        let done = r.emit(.jump)
        r.mark(invalid)
        r.emit(.loadNothing, d)
        r.mark([done])
    }

    func weightedChoose(_ input: Int, _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let iterator = r.temporary()
        let item = r.temporary()
        let itemsBuilder = r.temporary()
        let weightsBuilder = r.temporary()
        let items = r.temporary()
        let weights = r.temporary()
        let zero = r.temporary()
        let infinity = r.temporary()
        let comparison = r.temporary()
        let invalid = r.emit(.iteratorCreateOrJump, iterator, input)
        r.emit(.listBuilderCreate, itemsBuilder)
        r.emit(.listBuilderCreate, weightsBuilder)
        let begin = r.mark()
        let end = r.emit(.iteratorNext, item, iterator)
        let predicateScope = GesScope(scope)
        bindComponents(s.names, fallback: s.name, current: item, r, predicateScope)
        var skips: [Int] = []
        // Only an explicit filter is evaluated; the parser's placeholder `true` is not, as in C#.
        if !s.name.isEmpty, let condition = s.expressions.first { skips.append(r.emit(.jumpIfNotTrue, 0, try expression(condition, r, predicateScope))) }
        let weightScope = GesScope(scope)
        bindComponents(s.weightNames, fallback: s.weightName, current: item, r, weightScope)
        let weight = try expression(s.expressions[1], r, weightScope)
        r.emit(.loadInteger, zero)
        r.emit(.loadFloat, infinity, payload: Double.infinity.bitPattern)
        r.emit(.greater, comparison, weight, zero)
        skips.append(r.emit(.jumpIfNotTrue, 0, comparison))
        r.emit(.less, comparison, weight, infinity)
        skips.append(r.emit(.jumpIfNotTrue, 0, comparison))
        r.emit(.listBuilderAdd, 0, itemsBuilder, item)
        r.emit(.listBuilderAdd, 0, weightsBuilder, weight)
        r.mark(skips)
        r.emit(.jump, 0, 0, begin)
        r.mark([end])
        r.emit(.iteratorClose, 0, iterator)
        r.emit(.listBuilderFinish, items, itemsBuilder)
        r.emit(.listBuilderFinish, weights, weightsBuilder)
        if s.count == 1 { r.emit(.oneWeighted, d, items, weights) } else { r.emit(.takeWeighted, d, items, s.count, payload: UInt64(weights)) }
        let done = r.emit(.jump)
        r.mark([invalid])
        r.emit(.loadNothing, d)
        r.mark([done])
    }

    func slice(_ s: GesSelector, _ d: Int, _ source: Int, _ r: GesRoutine) throws {
        if s.count > Int(Int16.max) { throw compileError("compile.numericLimitExceeded", r.location, phase: .compile) }
        if s.operation == "draw" {
            r.emit(s.count == 1 ? .first : .takeFirst, d, source, s.count == 1 ? 0 : s.count)
            return
        }
        let take = s.operation == "take"
        let op: GesBytecodeOpCode = s.mode == "first" ? (take ? .takeFirst : .dropFirst) : s.mode == "last" ? (take ? .takeLast : .dropLast) : s.mode == "highest" ? (take ? .takeHighest : .dropHighest) : (take ? .takeLowest : .dropLowest)
        r.emit(op, d, source, s.count)
    }

    static let aggregateSelectors: Set<String> = ["sum", "average"]

    // Terminals that collect into a builder.
    static let collectionTerminals: Set<String> = ["filter", "select", "map", "group", "distinct", "order", "take", "drop", "draw"]

    // Terminals that consume the whole current element.
    static let materializedTerminals: Set<String> = ["filter", "map", "group", "distinct", "order", "first", "last", "single", "min", "max", "objectMatch", "take", "drop", "draw"]

    // The registers and jumps of one pipeline loop, which its start, element and finish share.
    struct PipelineLoop {
        var iterator = 0
        var item = 0
        var start = 0
        var collector = 0
        var count = 0
        var one = 0
        var seen = 0
        var sum = 0
        var winner = 0
        var compare = 0
        var invalid: [Int] = []
        var endJumps: [Int] = []
        var nextJumps: [Int] = []
    }

    // Nested selectors recurse through pipelineElement only; the loop's start and finish emit in frames of their own,
    // so that an unoptimized build does not keep their locals on the stack of every nesting level.
    func pipeline(_ source: Int, _ prefix: [GesSelector], _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope, sourceExpression: GesExpression? = nil) throws {
        if sourceExpression == nil && s.names.count < 2 && prefix.isEmpty && Self.aggregateSelectors.contains(s.operation) && isIdentity(s) {
            aggregate(source, average: s.operation == "average", d, r)
            return
        }
        var loop = try pipelineStart(source, prefix, s, d, r, scope, sourceExpression: sourceExpression)
        try pipelineElement(&loop, prefix, s, d, r, scope)
        try pipelineFinish(loop, s, d, r)
    }

    // A selector without a projection, or one that projects its own binding.
    func isIdentity(_ s: GesSelector) -> Bool {
        if s.expressions.isEmpty { return true }
        if case .name(let name) = s.expressions[0].kind { return name == s.name }
        return false
    }

    func pipelineStart(_ source: Int, _ prefix: [GesSelector], _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope, sourceExpression: GesExpression?) throws -> PipelineLoop {
        var loop = PipelineLoop()
        loop.iterator = r.temporary()
        loop.item = r.temporary()
        if sourceExpression == nil && prefix.isEmpty && ["distinct", "order", "group"].contains(s.operation) { collectionGuard(source, group: s.operation == "group", &loop.invalid, r) }
        let row: GesComponentRow?
        if let sourceExpression {
            row = try componentIterator(loop.iterator, sourceExpression, &loop.invalid, r, scope, guardedTerminal: prefix.isEmpty ? s : nil)
        } else {
            loop.invalid.append(r.emit(.iteratorCreateOrJump, loop.iterator, source))
            row = nil
        }
        loop.count = d
        loop.sum = d
        pipelineAccumulators(s, d, &loop, r)
        loop.start = r.mark()
        loop.endJumps = [r.emit(.iteratorNext, row.map { builder.list($0.components) } ?? loop.item, loop.iterator, flags: row == nil ? 0 : 32)]
        if let row { r.componentRows[loop.item] = row }
        return loop
    }

    // Jumps to `invalid` unless the source is a List, or for a group also a Map or custom value.
    func collectionGuard(_ source: Int, group: Bool, _ invalid: inout [Int], _ r: GesRoutine) {
        var guardOK: [Int] = []
        let compare = r.temporary()
        let types: [GesBytecodeTypeKind] = group ? [.list, .map, .custom] : [.list]
        for type in types {
            r.emit(.checkType, compare, source, Int(type.rawValue))
            guardOK.append(r.emit(.jumpIfTrue, 0, compare))
        }
        invalid.append(r.emit(.jump))
        r.mark(guardOK)
    }

    func pipelineAccumulators(_ s: GesSelector, _ d: Int, _ loop: inout PipelineLoop, _ r: GesRoutine) {
        switch s.operation {
        case "filter", "select", "map", "group", "distinct", "order", "take", "drop", "draw":
            loop.collector = r.temporary()
            let create: GesBytecodeOpCode =
                s.operation == "map" ? .mapBuilderCreate : s.operation == "group" ? .groupBuilderCreate : s.operation == "distinct" ? .distinctBuilderCreate : s.operation == "order" ? .orderBuilderCreate : .listBuilderCreate
            r.emit(create, loop.collector)
        case "count", "average":
            r.emit(.loadInteger, loop.count)
            loop.one = r.temporary()
            r.emit(.loadInteger, loop.one, payload: 1)
            if s.operation == "average" {
                loop.seen = r.temporary()
                loop.sum = r.temporary()
                r.emit(.loadFalse, loop.seen)
                r.emit(.loadNothing, loop.sum)
            }
        case "all", "any": r.emit(s.operation == "all" ? .loadTrue : .loadFalse, d)
        case "sum":
            loop.seen = r.temporary()
            r.emit(.loadFalse, loop.seen)
            r.emit(.loadNothing, loop.sum)
        case "min", "max":
            loop.seen = r.temporary()
            loop.winner = r.temporary()
            loop.compare = r.temporary()
            r.emit(.loadFalse, loop.seen)
            r.emit(.loadNothing, d)
            r.emit(.loadNothing, loop.winner)
        case "single":
            loop.count = r.temporary()
            loop.one = r.temporary()
            r.emit(.loadInteger, loop.count)
            r.emit(.loadInteger, loop.one, payload: 1)
            r.emit(.loadNothing, d)
        case "objectMatch": r.emit(.loadFalse, d)
        default: r.emit(.loadNothing, d)
        }
    }

    // One element: the prefix selectors, the projection and the operands the terminal evaluates.
    func pipelineElement(_ loop: inout PipelineLoop, _ prefix: [GesSelector], _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        let current = try pipelinePrefix(&loop, prefix, r, scope)
        let child = GesScope(scope)
        if Self.materializedTerminals.contains(s.operation) { materializeRow(current, r) }
        // Implicit aggregates consume the whole element, just like an identity projection.
        if s.expressions.isEmpty && Self.aggregateSelectors.contains(s.operation) { materializeRow(current, r) }
        if !s.expressions.isEmpty { bindComponents(s.names, fallback: s.name, current: current, r, child) }
        let projected = projectsCurrent(s) ? current : try s.expressions.first.map { try expression($0, r, child) } ?? current
        let value = s.operation == "map" && s.expressions.count > 1 ? try expression(s.expressions[1], r, child) : current
        let match = s.operation == "objectMatch" ? try objectMatch(current, s.expressions[0], r, scope) : 0
        try pipelineAccumulate(&loop, s, current: current, projected: projected, value: value, match: match, d, r)
    }

    // The current element after the prefix filters and selects.
    func pipelinePrefix(_ loop: inout PipelineLoop, _ prefix: [GesSelector], _ r: GesRoutine, _ scope: GesScope) throws -> Int {
        var current = loop.item
        for p in prefix {
            let child = GesScope(scope)
            bindComponents(p.names, fallback: p.name, current: current, r, child)
            if p.operation != "filter" {
                current = try expression(p.expressions[0], r, child)
            } else if scalarConstant(p.expressions[0]) != .boolean(true) {
                loop.nextJumps.append(r.emit(.jumpIfNotTrue, 0, try expression(p.expressions[0], r, child)))
            }
        }
        return current
    }

    // Terminals that use the current element itself instead of evaluating a projection.
    func projectsCurrent(_ s: GesSelector) -> Bool {
        ["objectMatch", "take", "drop", "draw"].contains(s.operation) || ["count", "filter"].contains(s.operation) && s.expressions.first.flatMap(scalarConstant) == .boolean(true)
    }

    func pipelineAccumulate(_ loop: inout PipelineLoop, _ s: GesSelector, current: Int, projected: Int, value: Int, match: Int, _ d: Int, _ r: GesRoutine) throws {
        switch s.operation {
        case "foreach": break
        case "filter", "select", "take", "drop", "draw":
            if s.operation == "filter" && s.expressions.first.flatMap(scalarConstant) != .boolean(true) { loop.nextJumps.append(r.emit(.jumpIfNotTrue, 0, projected)) }
            r.emit(.listBuilderAdd, 0, loop.collector, s.operation == "select" ? projected : current)
        case "map", "group", "distinct", "order":
            let op: GesBytecodeOpCode = s.operation == "map" ? .mapBuilderAdd : s.operation == "group" ? .groupBuilderAdd : s.operation == "distinct" ? .distinctBuilderAdd : .orderBuilderAdd
            r.emit(op, 0, loop.collector, projected, payload: UInt64(value))
        case "any", "all":
            loop.nextJumps.append(r.emit(s.operation == "all" ? .jumpIfTrue : .jumpIfNotTrue, 0, projected))
            r.emit(s.operation == "all" ? .loadFalse : .loadTrue, d)
            loop.endJumps.append(r.emit(.jump))
        case "count":
            if !s.expressions.isEmpty && s.expressions.first.flatMap(scalarConstant) != .boolean(true) { loop.nextJumps.append(r.emit(.jumpIfNotTrue, 0, projected)) }
            r.emit(.add, loop.count, loop.count, loop.one)
        case "sum", "average":
            if s.operation == "average" { r.emit(.add, loop.count, loop.count, loop.one) }
            let add = r.emit(.jumpIfTrue, 0, loop.seen)
            numericCopy(loop.sum, projected, r)
            r.emit(.loadTrue, loop.seen)
            loop.nextJumps.append(r.emit(.jump))
            r.mark([add])
            r.emit(.add, loop.sum, loop.sum, projected, flags: 64)
        case "min", "max":
            let compareJump = r.emit(.jumpIfTrue, 0, loop.seen)
            r.emit(.move, d, current)
            r.emit(.move, loop.winner, projected)
            r.emit(.loadTrue, loop.seen)
            loop.nextJumps.append(r.emit(.jump))
            r.mark([compareJump])
            r.emit(s.operation == "min" ? .less : .greater, loop.compare, projected, loop.winner)
            let update = r.emit(.jumpIfTrue, 0, loop.compare)
            r.emit(.loadTrue, loop.seen)
            loop.nextJumps.append(r.emit(.jump))
            r.mark([update])
            r.emit(.move, d, current)
            r.emit(.move, loop.winner, projected)
            r.emit(.loadTrue, loop.seen)
        case "first", "last", "single":
            if !s.expressions.isEmpty { loop.nextJumps.append(r.emit(.jumpIfNotTrue, 0, projected)) }
            if s.operation == "single" {
                let duplicate = r.emit(.jumpIfTrue, 0, loop.count)
                r.emit(.move, d, current)
                r.emit(.add, loop.count, loop.count, loop.one)
                loop.nextJumps.append(r.emit(.jump))
                r.mark([duplicate])
                r.emit(.loadNothing, d)
                loop.endJumps.append(r.emit(.jump))
            } else {
                r.emit(.move, d, current)
                if s.operation == "first" { loop.endJumps.append(r.emit(.jump)) }
            }
        case "objectMatch":
            loop.nextJumps.append(r.emit(.jumpIfNotTrue, 0, match))
            r.emit(.loadTrue, d)
            loop.endJumps.append(r.emit(.jump))
        default: throw compileError("compile.unsupportedConstruct", r.location, symbol: s.operation, phase: .compile)
        }
    }

    func pipelineFinish(_ loop: PipelineLoop, _ s: GesSelector, _ d: Int, _ r: GesRoutine) throws {
        r.componentRows.removeValue(forKey: loop.item)
        r.mark(loop.nextJumps)
        r.emit(.jump, 0, 0, loop.start)
        r.mark(loop.endJumps)
        r.emit(.iteratorClose, 0, loop.iterator)
        if Self.collectionTerminals.contains(s.operation) {
            let finish: GesBytecodeOpCode =
                s.operation == "map"
                ? .mapBuilderFinish
                : s.operation == "group"
                    ? .groupBuilderFinish : s.operation == "distinct" ? .distinctBuilderFinish : s.operation == "order" ? (s.mode == "descending" ? .orderBuilderFinishDescending : .orderBuilderFinishAscending) : .listBuilderFinish
            if ["take", "drop", "draw"].contains(s.operation) {
                let collected = r.temporary()
                r.emit(finish, collected, loop.collector)
                try slice(s, d, collected, r)
            } else {
                r.emit(finish, d, loop.collector)
            }
        }
        var close: [Int] = []
        if s.operation == "sum" {
            close.append(r.emit(.jumpIfTrue, 0, loop.seen))
            r.emit(.loadInteger, d)
        }
        if s.operation == "average" {
            let empty = r.emit(.jumpIfNotTrue, 0, loop.count)
            r.emit(.divide, d, loop.sum, loop.count, flags: 64)
            close.append(r.emit(.jump))
            r.mark([empty])
            r.emit(.loadNothing, d)
        }
        r.mark(close)
        let done = r.emit(.jump)
        r.mark(loop.invalid)
        r.emit(.loadNothing, d)
        r.mark([done])
    }

    func numericCopy(_ destination: Int, _ source: Int, _ r: GesRoutine) {
        // Copy the strict numeric view without parsing text or losing units.
        r.emit(.move, destination, source, flags: 64)
    }

    func aggregate(_ source: Int, average: Bool, _ d: Int, _ r: GesRoutine) {
        let iterator = r.temporary()
        let item = r.temporary()
        let accumulator = average ? r.temporary() : d
        let count = average ? r.temporary() : d
        let one = average ? r.temporary() : d
        let invalid = r.emit(.iteratorCreateOrJump, iterator, source)
        let empty = r.emit(.iteratorNext, item, iterator)
        numericCopy(accumulator, item, r)
        if average {
            r.emit(.loadInteger, count, payload: 1)
            r.emit(.loadInteger, one, payload: 1)
        }
        let loop = r.mark()
        let end = r.emit(.iteratorNext, item, iterator)
        if average { r.emit(.add, count, count, one) }
        r.emit(.add, accumulator, accumulator, item, flags: 64)
        r.emit(.jump, 0, 0, loop)
        r.mark([empty])
        r.emit(.iteratorClose, 0, iterator)
        r.emit(average ? .loadNothing : .loadInteger, d)
        let done1 = r.emit(.jump)
        r.mark([end])
        r.emit(.iteratorClose, 0, iterator)
        if average { r.emit(.divide, d, accumulator, count, flags: 64) }
        let done2 = r.emit(.jump)
        r.mark([invalid])
        r.emit(.loadNothing, d)
        r.mark([done1])
        r.mark([done2])
    }

    // As C# EmitObjectPatternPredicate: every entry is checked in source order and combined with And.
    func objectMatch(_ value: Int, _ pattern: GesExpression, _ r: GesRoutine, _ scope: GesScope) throws -> Int {
        var result = r.temporary()
        r.emit(.loadTrue, result)
        guard case .map(let entries) = pattern.kind else { return result }
        for (name, expression) in entries {
            let member = r.temporary()
            r.emit(.memberAccess, member, builder.text(name), value)
            let match: Int
            if case .map = expression.kind {
                match = try objectMatch(member, expression, r, scope)
            } else {
                let expected = try self.expression(expression, r, scope)
                match = r.temporary()
                r.emit(.equal, match, member, expected)
            }
            let combined = r.temporary()
            r.emit(.and, combined, result, match)
            result = combined
        }
        return result
    }
}
