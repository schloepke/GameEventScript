// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    func assemblyFailure(_ location: GameEventScriptSourceLocation, _ message: String, _ symbol: String? = nil) -> GameEventScriptCompileError {
        error("compile.invalidAssembly", location, symbol: symbol, phase: .compile, message: message)
    }

    func assemblyName(_ expression: GesExpression) throws -> String {
        guard case .name(let name) = expression.kind else { throw assemblyFailure(expression.location, "Expected a register or label name.") }
        return name
    }

    func assemblyText(_ expression: GesExpression) throws -> String {
        guard case .literal(let value) = expression.kind, value.kind == .text || value.kind == .tag, let text = value.textValue else { throw assemblyFailure(expression.location, "Expected literal text or tag.") }
        return text
    }

    func assemblyInteger(_ expression: GesExpression, _ minimum: Int64 = Int64.min, _ maximum: Int64 = Int64.max) throws -> Int64 {
        guard case .literal(let value) = expression.kind, !value.hasUnit, let integer = value.integerValue, integer >= minimum, integer <= maximum else {
            throw assemblyFailure(expression.location, "Expected an exact integer within the operand range.")
        }
        return integer
    }

    func assemblyNumber(_ expression: GesExpression) throws -> Double {
        guard case .literal(let value) = expression.kind, !value.hasUnit, value.kind == .integer || value.kind == .float else { throw assemblyFailure(expression.location, "Expected a numeric literal.") }
        return value.asNumber
    }

    func assemblyScalar(_ expression: GesExpression) -> Bool {
        guard case .literal(let value) = expression.kind else { return false }
        return [.nothing, .boolean, .integer, .float, .percentage, .text, .tag].contains(value.kind)
    }

    func assembly(_ block: GesAssembly, _ r: GesRoutine, _ outer: GesScope, destination: Int? = nil) throws {
        let scope = GesScope(outer)
        var writable: Set<String> = []
        var outputs: Set<String> = []
        var locals: Set<String> = []
        if let output = block.output {
            if let existing = outer.get(output), existing != destination { throw assemblyFailure(block.location, "Assembly result cannot shadow an input binding.", output) }
            writable.insert(output)
            outputs.insert(output)
            scope.values[output] = destination!
        }
        for declaration in block.declarations {
            if !writable.insert(declaration.name).inserted || outer.get(declaration.name) != nil { throw assemblyFailure(block.location, "Assembly binding shadows an existing binding.", declaration.name) }
            if declaration.export { outputs.insert(declaration.name) } else { locals.insert(declaration.name) }
            scope.values[declaration.name] = r.local()
        }
        var labels: [String: Int] = [:]
        for (index, line) in block.lines.enumerated() where line.label {
            if labels.updateValue(index, forKey: line.name) != nil { throw assemblyFailure(line.location, "Duplicate assembly label.", line.name) }
        }
        var signatures: [[Character]] = []
        for line in block.lines {
            if !block.allowSend && (line.name == "emit" || line.name == "publish" || line.name.hasPrefix("Emit") || line.name.hasPrefix("Publish")) {
                throw assemblyFailure(line.location, "Expression assembly cannot send messages.", line.name)
            }
            let signature = Array(try line.label ? "" : assemblySignature(line))
            signatures.append(signature)
            if signature.count != line.operands.count { throw assemblyFailure(line.location, "Invalid assembly operand count.", line.name) }
            for (index, operand) in line.operands.enumerated() {
                switch signature[index] {
                case "w":
                    let name = try assemblyName(operand)
                    if !writable.contains(name) { throw assemblyFailure(line.location, "Outer bindings are read-only.", name) }
                    if assemblyResource(assemblyResultKind(line, [:])) && !locals.contains(name) { throw assemblyFailure(line.location, "Resources require a temporary.", name) }
                case "r", "e":
                    let reads = signature[index] == "e" ? try assemblySymbolicReads(operand, handlerTarget: line.name == "BindHandler") : [operand]
                    for read in reads {
                        if case .name(let name) = read.kind {
                            if scope.get(name) == nil { throw error("compile.unresolvedSymbol", read.location, symbol: name, phase: .compile) }
                        } else if !assemblyScalar(read) {
                            throw assemblyFailure(read.location, "Only scalar literals and names are register operands.")
                        }
                    }
                    if signature[index] == "r", assemblyExpectedResource(line.name, index) != nil { _ = try assemblyName(operand) }
                case "l":
                    let name = try assemblyName(operand)
                    if labels[name] == nil { throw assemblyFailure(line.location, "Unknown assembly label.", name) }
                case "i": _ = try assemblyInteger(operand)
                case "s": _ = try assemblyInteger(operand, Int64(Int16.min), Int64(Int16.max))
                case "u": _ = try assemblyInteger(operand, 0, Int64(UInt16.max))
                case "f": _ = try assemblyNumber(operand)
                case "t": _ = try assemblyText(operand)
                case "y": _ = try assemblyType(operand)
                case "q": _ = try assemblyUnit(operand)
                case "p": _ = try assemblyPattern(operand)
                case "z": _ = try assemblySeries(operand)
                default: throw assemblyFailure(line.location, "Unknown operand kind.")
                }
            }
        }
        try assemblyFlow(block, signatures, labels, writable, outputs, locals)
        r.hasAssembly = true
        var addresses: [String: Int] = [:]
        var patches: [(Int, String)] = []
        for (index, line) in block.lines.enumerated() {
            r.location = line.location
            if line.label {
                addresses[line.name] = r.code.count
                continue
            }
            var registers = [Int](repeating: 0, count: line.operands.count)
            for (operandIndex, operand) in line.operands.enumerated() where signatures[index][operandIndex] == "w" || signatures[index][operandIndex] == "r" {
                if case .name(let name) = operand.kind {
                    registers[operandIndex] = scope.get(name)!
                } else {
                    let d = r.local()
                    _ = try expression(operand, r, scope, destination: d)
                    registers[operandIndex] = d
                }
            }
            if try assemblySymbolic(line, registers, r, scope) { continue }
            if try assemblySpecial(line, registers, r) { continue }
            let (op, _, slots) = Self.assemblyInstructions[line.name]!
            var fields = [UInt16](repeating: 0, count: 7)
            var payload: UInt64?
            for (operandIndex, operand) in line.operands.enumerated() {
                let kind = signatures[index][operandIndex]
                var value = 0
                switch kind {
                case "w", "r": value = registers[operandIndex]
                case "l": patches.append((r.code.count, try assemblyName(operand)))
                case "i": payload = UInt64(bitPattern: try assemblyInteger(operand))
                case "f": payload = try assemblyNumber(operand).bitPattern
                case "s", "u": value = Int(try assemblyInteger(operand))
                case "t": value = text(try assemblyText(operand))
                default: break
                }
                if slots[operandIndex] < 7 { fields[slots[operandIndex]] = UInt16(truncatingIfNeeded: value) }
            }
            let packed = UInt64(fields[3]) | UInt64(fields[4]) << 16 | UInt64(fields[5]) << 32 | UInt64(fields[6]) << 48
            r.emit(op, Int(fields[0]), Int(fields[1]), Int(fields[2]), payload: payload ?? packed)
        }
        for (instruction, label) in patches { r.patch(instruction, target: addresses[label]!) }
        for declaration in block.declarations where declaration.export { outer.values[declaration.name] = scope.values[declaration.name] }
    }

    func assemblyFlow(_ block: GesAssembly, _ signatures: [[Character]], _ labels: [String: Int], _ writable: Set<String>, _ outputs: Set<String>, _ locals: Set<String>) throws {
        var states = [[String: String]?](repeating: nil, count: block.lines.count + 1)
        var depths = [Int?](repeating: nil, count: states.count)
        states[0] = [:]
        depths[0] = 0
        var pending = [0]
        var cursor = 0
        while cursor < pending.count {
            let index = pending[cursor]
            cursor += 1
            if index == block.lines.count { continue }
            var state = states[index]!
            let line = block.lines[index]
            let signature = signatures[index]
            var depth = depths[index]!
            if line.name == "RandomPush" || line.name == "RandomPushConstant" { depth += 1 }
            if line.name == "RandomPop" {
                depth -= 1
                if depth < 0 { throw assemblyFailure(line.location, "Cannot pop an outer random scope.") }
            }
            for (i, kind) in signature.enumerated() where kind == "w" { state[try assemblyName(line.operands[i])] = assemblyResultKind(line, state) }
            if line.name == "IteratorClose" { state[try assemblyName(line.operands[0])] = "closed" }
            if line.name.hasSuffix("BuilderFinish") || line.name.hasSuffix("BuilderFinishAscending") || line.name.hasSuffix("BuilderFinishDescending") { state[try assemblyName(line.operands[1])] = "closed" }

            func merge(_ target: Int) throws {
                if let known = depths[target], known != depth { throw assemblyFailure(line.location, "Random scopes must balance at joins.") }
                depths[target] = depth
                guard var previous = states[target] else {
                    states[target] = state
                    pending.append(target)
                    return
                }
                for (name, kind) in state where assemblyResource(kind) && previous[name] == nil { throw assemblyFailure(line.location, "Resource is not live on every incoming path.", name) }
                var changed = false
                for (name, old) in previous {
                    guard let kind = state[name] else {
                        if assemblyResource(old) { throw assemblyFailure(line.location, "Resource is not live on every incoming path.", name) }
                        previous.removeValue(forKey: name)
                        changed = true
                        continue
                    }
                    if old != kind {
                        if assemblyResource(old) || assemblyResource(kind) { throw assemblyFailure(line.location, "Incompatible resource states.", name) }
                        let merged = old == "closed" || kind == "closed" ? "closed" : "value"
                        if old != merged {
                            previous[name] = merged
                            changed = true
                        }
                    }
                }
                if changed {
                    states[target] = previous
                    pending.append(target)
                }
            }

            if line.name == "IteratorCreateOrJump" {
                try merge(index + 1)
                state[try assemblyName(line.operands[0])] = "value"
                try merge(labels[assemblyName(line.operands[2])]!)
            } else {
                if line.name != "Jump" { try merge(index + 1) }
                for (i, kind) in signature.enumerated() where kind == "l" { try merge(labels[assemblyName(line.operands[i])]!) }
            }
        }
        for (index, line) in block.lines.enumerated() {
            guard let state = states[index] else { continue }
            for (i, kind) in signatures[index].enumerated() {
                if kind == "r" || kind == "e" {
                    let reads = kind == "e" ? try assemblySymbolicReads(line.operands[i], handlerTarget: line.name == "BindHandler") : [line.operands[i]]
                    for read in reads {
                        guard case .name(let name) = read.kind else { continue }
                        if writable.contains(name) && state[name] == nil { throw assemblyFailure(line.location, "Register is not initialized on every path.", name) }
                        if let actual = state[name] {
                            if actual == "closed" { throw assemblyFailure(line.location, "Resource has already been closed.", name) }
                            if assemblyResource(actual) && assemblyExpectedResource(line.name, i) != actual { throw assemblyFailure(line.location, "Internal values cannot escape or be copied.", name) }
                        }
                        if let expected = assemblyExpectedResource(line.name, i), state[name] != expected { throw assemblyFailure(line.location, "Wrong resource lifecycle state.", name) }
                    }
                } else if kind == "w" {
                    let name = try assemblyName(line.operands[i])
                    if let old = state[name], assemblyResource(old) { throw assemblyFailure(line.location, "Cannot overwrite a live resource.", name) }
                    if assemblyResource(assemblyResultKind(line, state)) && !locals.contains(name) { throw assemblyFailure(line.location, "Resources require a temporary.", name) }
                }
            }
        }
        if let exit = states.last! {
            if depths.last! != 0 { throw assemblyFailure(block.location, "Random scopes must balance at exit.") }
            for (name, kind) in exit where assemblyResource(kind) { throw assemblyFailure(block.location, "Resource is live at block exit.", name) }
            if block.predicate, let output = block.output, let kind = exit[output], kind != "boolean" { throw assemblyFailure(block.location, "Predicate must return Boolean or Nothing.", output) }
            for name in outputs where exit[name] == nil { throw assemblyFailure(block.location, "Output is not initialized on every exit.", name) }
        }
    }

    func assemblyResource(_ kind: String) -> Bool { kind == "iterator" || kind.hasSuffix("Builder") }

    func assemblyExpectedResource(_ opcode: String, _ operand: Int) -> String? {
        if opcode == "IteratorNext" && operand == 1 || opcode == "IteratorClose" && operand == 0 { return "iterator" }
        if ["ListBuilder", "MapBuilder", "DistinctBuilder", "GroupBuilder", "OrderBuilder"].contains(where: { opcode.hasPrefix($0) }),
            opcode.hasSuffix("Add") && operand == 0 || (opcode.hasSuffix("Finish") || opcode.hasSuffix("FinishAscending") || opcode.hasSuffix("FinishDescending")) && operand == 1
        {
            return String(opcode.prefix(while: { $0 != "B" })) + "Builder"
        }
        return nil
    }

    func assemblyBooleanOperand(_ operand: GesExpression, _ state: [String: String]) -> Bool {
        if case .literal(let value) = operand.kind { return value.kind == .nothing || value.kind == .boolean }
        if case .name(let name) = operand.kind { return state[name] == "boolean" }
        return false
    }

    func assemblyResultKind(_ line: GesAssemblyLine, _ state: [String: String]) -> String {
        if ["IteratorCreate", "IteratorCreateOrJump", "CreateRangeIterator", "CreateRangeIteratorWithStep", "CreateRangeIteratorShort"].contains(line.name) { return "iterator" }
        if line.name.hasSuffix("BuilderCreate") { return String(line.name.dropLast(6)) }
        if line.name == "Cast", case .literal(let type) = line.operands[2].kind, ["boolean", "nothing"].contains(type.textValue?.lowercased() ?? "") { return "boolean" }
        if line.name == "Call", case .call(let name, let args) = line.operands[1].kind, definitions[signature(name, args.map(\.label))]?.kind == "predicate" { return "boolean" }
        if line.name == "Move" {
            if assemblyBooleanOperand(line.operands[1], state) { return "boolean" }
            if case .name(let name) = line.operands[1].kind { return state[name] ?? "value" }
        }
        if line.name == "Not" && assemblyBooleanOperand(line.operands[1], state) { return "boolean" }
        if ["And", "Or", "Xor", "Implies"].contains(line.name) && assemblyBooleanOperand(line.operands[1], state) && assemblyBooleanOperand(line.operands[2], state) { return "boolean" }
        if [
            "LoadTrue", "LoadFalse", "LoadNothing", "Equal", "NotEqual", "Less", "Greater", "LessOrEqual", "GreaterOrEqual", "HasValue", "IsEmpty", "CheckInteger", "CheckFractional", "CheckNumeric", "CheckType", "CheckCustomType", "CheckUnit",
            "Contains", "ContainsAny", "ContainsAll", "HasAny", "HasAll", "HasPattern", "StartsWith", "EndsWith",
        ].contains(line.name) {
            return "boolean"
        }
        return "value"
    }
}
