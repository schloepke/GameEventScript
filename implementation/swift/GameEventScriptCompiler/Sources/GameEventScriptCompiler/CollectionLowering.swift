// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

extension GesCompiler {
    func selector(_ source: GesExpression, _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        var root = source
        var prefix: [GesSelector] = []
        while case .selector(let parent, let selector) = root.kind, ["filter", "select"].contains(selector.operation) {
            prefix.insert(selector, at: 0)
            root = parent
        }
        let terminals = ["foreach", "filter", "select", "any", "all", "count", "sum", "average", "min", "max", "map", "group", "order", "distinct", "first", "last", "single", "objectMatch", "take", "drop", "draw"]
        if isCombinedSource(source) && ["fold", "reduce"].contains(s.operation) {
            try fold(0, s, d, r, scope, sourceExpression: source)
            return
        }
        if isCombinedSource(root) && s.operation == "distinct" && s.expressions.isEmpty {
            // The native operation preserves Dice and rejects Map sources.
            r.emit(.distinct, d, try expression(source, r, scope))
            return
        }
        if prefix.isEmpty, case .combined(let operation, _) = root.kind,
            ["union", "intersect", "difference"].contains(operation), ["take", "drop", "draw"].contains(s.operation)
        {
            // Slices preserve Dice and reject Maps unless a filter/select has produced a List.
            try slice(s, d, expression(source, r, scope), r)
            return
        }
        if isCombinedSource(root) && terminals.contains(s.operation) {
            try pipeline(0, prefix, s, d, r, scope, sourceExpression: root)
            return
        }
        if !prefix.isEmpty && terminals.contains(s.operation) {
            try pipeline(expression(root, r, scope), prefix, s, d, r, scope)
            return
        }
        let input = try expression(source, r, scope)
        switch s.operation {
        case "fold", "reduce": try fold(input, s, d, r, scope)
        case "index":
            let index = s.expressions[0]
            if case .literal(let v) = index.kind, let n = v.integerValue, (0...65535).contains(n) {
                r.emit(.indexAccess, d, Int(n), input)
            } else if case .literal(let v) = index.kind, v.kind == .tag || v.kind == .text, let tag = v.textValue {
                r.emit(.memberAccess, d, text(tag), input)
            } else {
                r.emit(.propertyAccess, d, try expression(index, r, scope), input)
            }
        case "keys", "values", "entries": r.emit(s.operation == "keys" ? .keysOfMap : s.operation == "values" ? .valuesOfMap : .entriesOfMap, d, input)
        case "shuffle", "reverse": r.emit(s.operation == "shuffle" ? .shuffle : .reverse, d, input)
        case "sort": r.emit(s.mode == "descending" ? .sortDescending : .sortAscending, d, input)
        case "contains": r.emit(s.mode == "all" ? .containsAll : s.mode == "any" ? .containsAny : .contains, d, try expression(s.expressions[0], r, scope), input)
        case "term": r.emit(.term, d, input, try expression(s.expressions[0], r, scope))
        case "distinct" where s.expressions.isEmpty: r.emit(.distinct, d, input)
        case "count" where s.expressions.isEmpty: r.emit(.count, d, input)
        case "first" where s.expressions.isEmpty, "last" where s.expressions.isEmpty, "single" where s.expressions.isEmpty: r.emit(s.operation == "first" ? .first : s.operation == "last" ? .last : .single, d, input)
        case "take", "drop", "draw": try slice(s, d, input, r)
        case "hasPattern", "takePattern":
            let kind = s.mode == "fullHouse" ? 2 : s.mode == "straight" ? 3 : s.mode == "face" ? 1 : 0
            let face = try s.expressions.first.map { try expression($0, r, scope) } ?? 0
            r.emit(s.operation == "hasPattern" ? .hasPattern : .takePattern, d, input, s.count, payload: UInt64(kind) | UInt64(face) << 16)
        case "choose":
            if s.count > Int(Int16.max) { throw error("compile.numericLimitExceeded", r.location, phase: .compile) }
            if s.expressions.count > 1 {
                try weightedChoose(input, s, d, r, scope)
            } else {
                var values = input
                if let condition = s.expressions.first {
                    values = r.temporary()
                    try pipeline(input, [], .init(operation: "filter", name: s.name, names: s.names, expressions: [condition]), values, r, scope)
                }
                if s.mode == "random" { r.emit(s.count == 1 ? .oneRandom : .takeRandom, d, values, s.count == 1 ? 0 : s.count) } else { r.emit(s.count == 1 ? .first : .takeFirst, d, values, s.count == 1 ? 0 : s.count) }
            }
        default: try pipeline(input, [], s, d, r, scope)
        }
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
            for jump in prepared { r.patch(jump, target: r.code.count) }
            validSource = valid
        }
        if s.operation == "fold" { _ = try expression(s.expressions[0], r, scope, destination: accumulator) } else { r.emit(.loadNothing, accumulator) }
        if let validSource { invalid.append(r.emit(.jumpIfNotTrue, 0, validSource)) } else { invalid.append(r.emit(.iteratorCreateOrJump, iterator, input)) }
        let first = s.operation == "reduce" ? r.emit(.iteratorNext, accumulator, iterator) : nil
        let loop = r.code.count
        let end = r.emit(.iteratorNext, row.map { list($0.components) } ?? item, iterator, flags: row == nil ? 0 : 32)
        if let row { r.componentRows[item] = row }
        let child = GesScope(scope)
        child.values[s.accumulator] = accumulator
        bindComponents(s.names, fallback: s.name, current: item, r, child)
        let value = try expression(s.expressions.last!, r, child)
        r.componentRows.removeValue(forKey: item)
        if value != accumulator { r.emit(.move, accumulator, value) }
        r.emit(.jump, 0, 0, loop)
        r.patch(end, target: r.code.count)
        if let first { r.patch(first, target: r.code.count) }
        r.emit(.iteratorClose, 0, iterator)
        r.emit(.move, d, accumulator)
        let done = r.emit(.jump)
        for jump in invalid { r.patch(jump, target: r.code.count) }
        r.emit(.loadNothing, d)
        r.patch(done, target: r.code.count)
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
        let begin = r.code.count
        let end = r.emit(.iteratorNext, item, iterator)
        let predicateScope = GesScope(scope)
        bindComponents(s.names, fallback: s.name, current: item, r, predicateScope)
        var skips: [Int] = []
        if let condition = s.expressions.first { skips.append(r.emit(.jumpIfNotTrue, 0, try expression(condition, r, predicateScope))) }
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
        for skip in skips { r.patch(skip, target: r.code.count) }
        r.emit(.jump, 0, 0, begin)
        r.patch(end, target: r.code.count)
        r.emit(.iteratorClose, 0, iterator)
        r.emit(.listBuilderFinish, items, itemsBuilder)
        r.emit(.listBuilderFinish, weights, weightsBuilder)
        if s.count == 1 { r.emit(.oneWeighted, d, items, weights) } else { r.emit(.takeWeighted, d, items, s.count, payload: UInt64(weights)) }
        let done = r.emit(.jump)
        r.patch(invalid, target: r.code.count)
        r.emit(.loadNothing, d)
        r.patch(done, target: r.code.count)
    }

    func slice(_ s: GesSelector, _ d: Int, _ source: Int, _ r: GesRoutine) throws {
        if s.count > Int(Int16.max) { throw error("compile.numericLimitExceeded", r.location, phase: .compile) }
        if s.operation == "draw" {
            r.emit(s.count == 1 ? .first : .takeFirst, d, source, s.count == 1 ? 0 : s.count)
            return
        }
        let take = s.operation == "take"
        let op: Op = s.mode == "first" ? (take ? .takeFirst : .dropFirst) : s.mode == "last" ? (take ? .takeLast : .dropLast) : s.mode == "highest" ? (take ? .takeHighest : .dropHighest) : (take ? .takeLowest : .dropLowest)
        r.emit(op, d, source, s.count)
    }

    func pipeline(_ source: Int, _ prefix: [GesSelector], _ s: GesSelector, _ d: Int, _ r: GesRoutine, _ scope: GesScope, sourceExpression: GesExpression? = nil) throws {
        let identity: Bool
        if s.expressions.isEmpty { identity = true } else if case .name(let name) = s.expressions[0].kind { identity = name == s.name } else { identity = false }
        if sourceExpression == nil && s.names.count < 2 && prefix.isEmpty && ["sum", "average"].contains(s.operation) && identity {
            aggregate(source, average: s.operation == "average", d, r)
            return
        }
        let iterator = r.temporary()
        let item = r.temporary()
        var invalid: [Int] = []
        var guardOK: [Int] = []
        if sourceExpression == nil && prefix.isEmpty && ["distinct", "order", "group"].contains(s.operation) {
            let compare = r.temporary()
            let types: [GameEventScriptBytecodeTypeKind] = s.operation == "group" ? [.list, .map, .custom] : [.list]
            for type in types {
                r.emit(.checkType, compare, source, Int(type.rawValue))
                guardOK.append(r.emit(.jumpIfTrue, 0, compare))
            }
            invalid.append(r.emit(.jump))
            for i in guardOK { r.patch(i, target: r.code.count) }
        }
        let row: GesComponentRow?
        if let sourceExpression {
            row = try componentIterator(iterator, sourceExpression, &invalid, r, scope, guardedTerminal: prefix.isEmpty ? s : nil)
        } else {
            invalid.append(r.emit(.iteratorCreateOrJump, iterator, source))
            row = nil
        }
        let collection = ["filter", "select", "map", "group", "distinct", "order", "take", "drop", "draw"].contains(s.operation)
        var builder = 0
        var count = d
        var one = 0
        var seen = 0
        var sum = d
        var winner = 0
        var compare = 0
        switch s.operation {
        case "filter", "select", "map", "group", "distinct", "order", "take", "drop", "draw":
            builder = r.temporary()
            let create: Op = s.operation == "map" ? .mapBuilderCreate : s.operation == "group" ? .groupBuilderCreate : s.operation == "distinct" ? .distinctBuilderCreate : s.operation == "order" ? .orderBuilderCreate : .listBuilderCreate
            r.emit(create, builder)
        case "count", "average":
            r.emit(.loadInteger, count)
            one = r.temporary()
            r.emit(.loadInteger, one, payload: 1)
            if s.operation == "average" {
                seen = r.temporary()
                sum = r.temporary()
                r.emit(.loadFalse, seen)
                r.emit(.loadNothing, sum)
            }
        case "all", "any": r.emit(s.operation == "all" ? .loadTrue : .loadFalse, d)
        case "sum":
            seen = r.temporary()
            r.emit(.loadFalse, seen)
            r.emit(.loadNothing, sum)
        case "min", "max":
            seen = r.temporary()
            winner = r.temporary()
            compare = r.temporary()
            r.emit(.loadFalse, seen)
            r.emit(.loadNothing, d)
            r.emit(.loadNothing, winner)
        case "single":
            count = r.temporary()
            one = r.temporary()
            r.emit(.loadInteger, count)
            r.emit(.loadInteger, one, payload: 1)
            r.emit(.loadNothing, d)
        case "objectMatch": r.emit(.loadFalse, d)
        default: r.emit(.loadNothing, d)
        }
        let start = r.code.count
        var endJumps = [r.emit(.iteratorNext, row.map { list($0.components) } ?? item, iterator, flags: row == nil ? 0 : 32)]
        if let row { r.componentRows[item] = row }
        var nextJumps: [Int] = []
        var current = item
        for p in prefix {
            let child = GesScope(scope)
            bindComponents(p.names, fallback: p.name, current: current, r, child)
            if p.operation == "filter" { if scalarConstant(p.expressions[0]) != .boolean(true) { nextJumps.append(r.emit(.jumpIfNotTrue, 0, try expression(p.expressions[0], r, child))) } } else { current = try expression(p.expressions[0], r, child) }
        }
        let child = GesScope(scope)
        if ["filter", "map", "group", "distinct", "order", "first", "last", "single", "min", "max", "objectMatch", "take", "drop", "draw"].contains(s.operation) { materializeRow(current, r) }
        // Implicit aggregates consume the whole element, just like an identity projection.
        if s.expressions.isEmpty && ["sum", "average"].contains(s.operation) { materializeRow(current, r) }
        if !s.expressions.isEmpty { bindComponents(s.names, fallback: s.name, current: current, r, child) }
        let projected: Int
        if ["objectMatch", "take", "drop", "draw"].contains(s.operation) || ["count", "filter"].contains(s.operation) && s.expressions.first.flatMap(scalarConstant) == .boolean(true) {
            projected = current
        } else {
            projected = try s.expressions.first.map { try expression($0, r, child) } ?? current
        }
        switch s.operation {
        case "foreach": break
        case "filter", "select", "take", "drop", "draw":
            if s.operation == "filter" && s.expressions.first.flatMap(scalarConstant) != .boolean(true) { nextJumps.append(r.emit(.jumpIfNotTrue, 0, projected)) }
            r.emit(.listBuilderAdd, 0, builder, s.operation == "select" ? projected : current)
        case "map", "group", "distinct", "order":
            let value = s.operation == "map" && s.expressions.count > 1 ? try expression(s.expressions[1], r, child) : current
            let op: Op = s.operation == "map" ? .mapBuilderAdd : s.operation == "group" ? .groupBuilderAdd : s.operation == "distinct" ? .distinctBuilderAdd : .orderBuilderAdd
            r.emit(op, 0, builder, projected, payload: UInt64(value))
        case "any", "all":
            nextJumps.append(r.emit(s.operation == "all" ? .jumpIfTrue : .jumpIfNotTrue, 0, projected))
            r.emit(s.operation == "all" ? .loadFalse : .loadTrue, d)
            endJumps.append(r.emit(.jump))
        case "count":
            if !s.expressions.isEmpty && s.expressions.first.flatMap(scalarConstant) != .boolean(true) { nextJumps.append(r.emit(.jumpIfNotTrue, 0, projected)) }
            r.emit(.add, count, count, one)
        case "sum", "average":
            if s.operation == "average" { r.emit(.add, count, count, one) }
            let add = r.emit(.jumpIfTrue, 0, seen)
            numericCopy(sum, projected, r)
            r.emit(.loadTrue, seen)
            nextJumps.append(r.emit(.jump))
            r.patch(add, target: r.code.count)
            r.emit(.add, sum, sum, projected, flags: 64)
        case "min", "max":
            let compareJump = r.emit(.jumpIfTrue, 0, seen)
            r.emit(.move, d, current)
            r.emit(.move, winner, projected)
            r.emit(.loadTrue, seen)
            nextJumps.append(r.emit(.jump))
            r.patch(compareJump, target: r.code.count)
            r.emit(s.operation == "min" ? .less : .greater, compare, projected, winner)
            let update = r.emit(.jumpIfTrue, 0, compare)
            r.emit(.loadTrue, seen)
            nextJumps.append(r.emit(.jump))
            r.patch(update, target: r.code.count)
            r.emit(.move, d, current)
            r.emit(.move, winner, projected)
            r.emit(.loadTrue, seen)
        case "first", "last", "single":
            if !s.expressions.isEmpty { nextJumps.append(r.emit(.jumpIfNotTrue, 0, projected)) }
            if s.operation == "single" {
                let duplicate = r.emit(.jumpIfTrue, 0, count)
                r.emit(.move, d, current)
                r.emit(.add, count, count, one)
                nextJumps.append(r.emit(.jump))
                r.patch(duplicate, target: r.code.count)
                r.emit(.loadNothing, d)
                endJumps.append(r.emit(.jump))
            } else {
                r.emit(.move, d, current)
                if s.operation == "first" { endJumps.append(r.emit(.jump)) }
            }
        case "objectMatch":
            let match = try objectMatch(current, s.expressions[0], r, scope)
            nextJumps.append(r.emit(.jumpIfNotTrue, 0, match))
            r.emit(.loadTrue, d)
            endJumps.append(r.emit(.jump))
        default: throw error("compile.unsupportedConstruct", r.location, symbol: s.operation, phase: .compile)
        }
        r.componentRows.removeValue(forKey: item)
        for jump in nextJumps { r.patch(jump, target: r.code.count) }
        r.emit(.jump, 0, 0, start)
        for jump in endJumps { r.patch(jump, target: r.code.count) }
        r.emit(.iteratorClose, 0, iterator)
        if collection {
            let finish: Op =
                s.operation == "map"
                ? .mapBuilderFinish
                : s.operation == "group"
                    ? .groupBuilderFinish : s.operation == "distinct" ? .distinctBuilderFinish : s.operation == "order" ? (s.mode == "descending" ? .orderBuilderFinishDescending : .orderBuilderFinishAscending) : .listBuilderFinish
            if ["take", "drop", "draw"].contains(s.operation) {
                let collected = r.temporary()
                r.emit(finish, collected, builder)
                try slice(s, d, collected, r)
            } else {
                r.emit(finish, d, builder)
            }
        }
        var close: [Int] = []
        if s.operation == "sum" {
            close.append(r.emit(.jumpIfTrue, 0, seen))
            r.emit(.loadInteger, d)
        }
        if s.operation == "average" {
            let empty = r.emit(.jumpIfNotTrue, 0, count)
            r.emit(.divide, d, sum, count, flags: 64)
            close.append(r.emit(.jump))
            r.patch(empty, target: r.code.count)
            r.emit(.loadNothing, d)
        }
        for jump in close { r.patch(jump, target: r.code.count) }
        let done = r.emit(.jump)
        for jump in invalid { r.patch(jump, target: r.code.count) }
        r.emit(.loadNothing, d)
        r.patch(done, target: r.code.count)
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
        let loop = r.code.count
        let end = r.emit(.iteratorNext, item, iterator)
        if average { r.emit(.add, count, count, one) }
        r.emit(.add, accumulator, accumulator, item, flags: 64)
        r.emit(.jump, 0, 0, loop)
        r.patch(empty, target: r.code.count)
        r.emit(.iteratorClose, 0, iterator)
        r.emit(average ? .loadNothing : .loadInteger, d)
        let done1 = r.emit(.jump)
        r.patch(end, target: r.code.count)
        r.emit(.iteratorClose, 0, iterator)
        if average { r.emit(.divide, d, accumulator, count, flags: 64) }
        let done2 = r.emit(.jump)
        r.patch(invalid, target: r.code.count)
        r.emit(.loadNothing, d)
        r.patch(done1, target: r.code.count)
        r.patch(done2, target: r.code.count)
    }

    func objectMatch(_ value: Int, _ pattern: GesExpression, _ r: GesRoutine, _ scope: GesScope) throws -> Int {
        let result = r.temporary()
        r.emit(.loadTrue, result)
        guard case .map(let entries) = pattern.kind else { return result }
        var failures: [Int] = []
        for (name, expression) in entries {
            let member = r.temporary()
            r.emit(.memberAccess, member, text(name), value)
            let match: Int
            if case .map = expression.kind {
                match = try objectMatch(member, expression, r, scope)
            } else {
                match = r.temporary()
                r.emit(.equal, match, member, try self.expression(expression, r, scope))
            }
            failures.append(r.emit(.jumpIfNotTrue, 0, match))
        }
        let end = r.emit(.jump)
        for failure in failures { r.patch(failure, target: r.code.count) }
        r.emit(.loadFalse, result)
        r.patch(end, target: r.code.count)
        return result
    }
}
