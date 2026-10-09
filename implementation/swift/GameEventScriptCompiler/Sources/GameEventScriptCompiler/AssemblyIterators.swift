// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCompiler {
    func assemblyIteratorMode(_ operand: GesExpression) throws -> UInt8 {
        let name = try assemblyText(operand)
        guard let mode: UInt8 = ["normal": 0, "union": 1, "intersect": 2, "difference": 3, "lockstep": 4, "cartesian": 5, "entries": 6][name] else { throw assemblyFailure(operand.location, "Unknown iterator mode.") }
        return mode
    }

    func assemblyIteratorSignature(_ line: GesAssemblyLine) throws -> String? {
        if line.name == "IteratorNext", line.operands.count == 3, case .list(let targets) = line.operands[0].kind {
            if targets.isEmpty { throw assemblyFailure(line.location, "Component targets cannot be empty.") }
            var names: Set<String> = []
            let iterator = try assemblyName(line.operands[1])
            for target in targets {
                let name = try assemblyName(target)
                if !names.insert(name).inserted || name == iterator { throw assemblyFailure(line.location, "Targets must be distinct and cannot overwrite the iterator.", name) }
            }
            return "Wrl"
        }
        let create = line.name == "IteratorCreate" && line.operands.count == 3
        let branch = line.name == "IteratorCreateOrJump" && line.operands.count == 4
        if !create && !branch { return nil }
        let mode = try assemblyIteratorMode(line.operands.last!)
        if (1...5).contains(mode) {
            guard case .list(let sources) = line.operands[1].kind, sources.count >= 2 else { throw assemblyFailure(line.location, "Compound iterators require at least two sources.") }
            return branch ? "welt" : "wet"
        }
        return branch ? "wrlt" : "wrt"
    }

    func assemblyWrites(_ role: Character, _ operand: GesExpression) -> [GesExpression] {
        if role == "w" { return [operand] }
        if role == "W", case .list(let targets) = operand.kind { return targets }
        return []
    }

    func assemblyIterator(_ line: GesAssemblyLine, _ registers: [Int], _ r: GesRoutine, _ scope: GesScope, _ patches: inout [(Int, String)]) throws -> Bool {
        guard try assemblyIteratorSignature(line) != nil else { return false }
        if line.name == "IteratorNext", case .list(let targets) = line.operands[0].kind {
            let values = try targets.map { scope.get(try assemblyName($0))! }
            let instruction = r.emit(.iteratorNext, list(values), registers[1], flags: 32)
            patches.append((instruction, try assemblyName(line.operands[2])))
            return true
        }
        let mode = try assemblyIteratorMode(line.operands.last!)
        let sources: Int
        if (1...5).contains(mode), case .list(let values) = line.operands[1].kind {
            sources = try list(
                values.map { value in
                    if case .name(let name) = value.kind { return scope.get(name)! }
                    let target = r.local()
                    return try expression(value, r, scope, destination: target)
                }
            )
        } else {
            sources = registers[1]
        }
        let branch = line.name == "IteratorCreateOrJump"
        let instruction = r.emit(branch ? .iteratorCreateOrJump : .iteratorCreate, registers[0], sources, flags: mode << 5)
        if branch { patches.append((instruction, try assemblyName(line.operands[2]))) }
        return true
    }
}
