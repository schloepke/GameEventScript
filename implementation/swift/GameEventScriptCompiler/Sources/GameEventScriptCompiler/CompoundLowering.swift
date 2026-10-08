// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

struct GesComponentRow {
    let components: [Int]
    let entry: Bool
}

extension GesCompiler {
    func knownNonList(_ e: GesExpression, _ r: GesRoutine, _ scope: GesScope) -> Bool {
        switch e.kind {
        case .literal(let value): return value.kind != .list
        case .name(let name): return scope.get(name).map { r.nonListBindings.contains($0) } ?? false
        case .binary(let op, _, _): return ["/", "div", "mod", "rem", "^"].contains(op)
        default: return false
        }
    }

    func isCombinedSource(_ e: GesExpression) -> Bool {
        if case .combined = e.kind { return true }
        if case .selector(_, let s) = e.kind { return s.operation == "entries" }
        return false
    }

    func materializeRow(_ current: Int, _ r: GesRoutine) {
        guard let row = r.componentRows[current] else { return }
        stage(row.components, r)
        if row.entry { r.emit(.createMap, current, list([text("key"), text("value")])) } else { r.emit(.createList, current) }
    }

    func bindComponents(_ names: [String], fallback: String, current: Int, _ r: GesRoutine, _ scope: GesScope) {
        if names.count < 2 {
            materializeRow(current, r)
            scope.values[fallback] = current
            return
        }
        if let row = r.componentRows[current] {
            for (index, name) in names.enumerated() {
                if index < row.components.count {
                    scope.values[name] = row.components[index]
                } else {
                    let missing = r.temporary()
                    r.emit(.loadNothing, missing)
                    scope.values[name] = missing
                }
            }
            return
        }
        let values = r.temporary()
        let check = r.temporary()
        let targets = names.map { name in
            let target = r.temporary()
            scope.values[name] = target
            return target
        }
        r.emit(.checkType, check, current, Int(GameEventScriptBytecodeTypeKind.map.rawValue))
        let map = r.emit(.jumpIfTrue, 0, check)
        r.emit(.move, values, current)
        r.emit(.checkType, check, current, Int(GameEventScriptBytecodeTypeKind.list.rawValue))
        let list = r.emit(.jumpIfTrue, 0, check)
        r.emit(.move, targets[0], current)
        for target in targets.dropFirst() { r.emit(.loadNothing, target) }
        let done = r.emit(.jump)
        r.patch(map, target: r.code.count)
        r.emit(.valuesOfMap, values, current)
        r.patch(list, target: r.code.count)
        for (index, target) in targets.enumerated() { r.emit(.indexAccess, target, index + 1, values) }
        r.patch(done, target: r.code.count)
    }

    func componentIterator(_ iterator: Int, _ e: GesExpression, _ invalid: inout [Int], _ r: GesRoutine, _ scope: GesScope) throws -> GesComponentRow? {
        if case .selector(let target, let selection) = e.kind, selection.operation == "entries" {
            invalid.append(r.emit(.iteratorCreateOrJump, iterator, try expression(target, r, scope), flags: 6 << 5))
            return .init(components: [r.temporary(), r.temporary()], entry: true)
        }
        guard case .combined(let operation, let expressions) = e.kind else { return nil }
        let mode: UInt8 = ["union": 1, "intersect": 2, "difference": 3, "lockstep": 4, "cartesian": 5][operation]!
        var sources: [Int] = []
        var failed: [Int] = []
        let validation = r.temporary()
        for (index, value) in expressions.enumerated() {
            let source = try expression(value, r, scope)
            sources.append(source)
            if mode >= 4 {
                failed.append(r.emit(.iteratorCreateOrJump, validation, source))
                r.emit(.iteratorClose, 0, validation)
            } else if index == 0 {
                sourceGuard(source, &failed, r)
            } else {
                failed.append(r.emit(.iteratorCreateOrJump, iterator, list(sources), flags: mode << 5))
                if index + 1 < expressions.count { r.emit(.iteratorClose, 0, iterator) }
            }
        }
        if mode >= 4 { failed.append(r.emit(.iteratorCreateOrJump, iterator, list(sources), flags: mode << 5)) }
        let ready = r.emit(.jump)
        for jump in failed { r.patch(jump, target: r.code.count) }
        r.emit(.loadNothing, iterator)
        invalid.append(r.emit(.jump))
        r.patch(ready, target: r.code.count)
        return mode >= 4 ? .init(components: expressions.map { _ in r.temporary() }, entry: false) : nil
    }

    func sourceGuard(_ value: Int, _ invalid: inout [Int], _ r: GesRoutine) {
        let check = r.temporary()
        var valid: [Int] = []
        for type in [GameEventScriptBytecodeTypeKind.list, .map, .dice] {
            r.emit(.checkType, check, value, Int(type.rawValue))
            valid.append(r.emit(.jumpIfTrue, 0, check))
        }
        invalid.append(r.emit(.jump))
        for jump in valid { r.patch(jump, target: r.code.count) }
    }

    func combinedCollection(_ operation: String, _ sources: [GesExpression], _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        if operation == "cartesian" || operation == "lockstep" {
            let e = GesExpression(.combined(operation, sources), r.location)
            try pipeline(0, [], .init(operation: "select", name: "item", expressions: [.init(.name("item"), r.location)]), d, r, scope, sourceExpression: e)
            return
        }
        var invalid: [Int] = []
        var current = try expression(sources[0], r, scope)
        let check = r.temporary()
        for source in sources.dropFirst() {
            sourceGuard(current, &invalid, r)
            let right = try expression(source, r, scope)
            let result = r.temporary()
            r.emit(operation == "union" ? .union : operation == "intersect" ? .intersect : .subtract, result, current, right)
            r.emit(.checkType, check, result, Int(GameEventScriptBytecodeTypeKind.nothing.rawValue))
            invalid.append(r.emit(.jumpIfTrue, 0, check))
            current = result
        }
        r.emit(.move, d, current)
        let done = r.emit(.jump)
        for jump in invalid { r.patch(jump, target: r.code.count) }
        r.emit(.loadNothing, d)
        r.patch(done, target: r.code.count)
    }
}
