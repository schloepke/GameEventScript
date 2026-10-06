// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    func assemblySend(_ line: GesAssemblyLine) -> (publish: Bool, result: Bool, delayed: Bool, tags: Bool)? {
        let name = line.name
        if ["EmitInstant", "EmitAfter", "PublishInstant", "PublishAfter"].contains(name) {
            let delayed = name.hasSuffix("After")
            return (name.hasPrefix("Publish"), true, delayed, line.operands.count == (delayed ? 4 : 3))
        }
        if ["EmitMessage", "EmitMessageWithTags", "EmitMessageValue", "EmitMessageValueWithTags", "PublishMessage", "PublishMessageWithTags", "PublishMessageValue", "PublishMessageValueWithTags"].contains(name) {
            return (name.hasPrefix("Publish"), false, false, name.hasSuffix("WithTags"))
        }
        return nil
    }

    func assemblySignature(_ line: GesAssemblyLine) throws -> String {
        if line.name == "emit" || line.name == "publish" { return String(repeating: "e", count: line.operands.count) }
        if let send = assemblySend(line) {
            let count = (send.result ? 2 : 1) + (send.delayed ? 1 : 0) + (send.tags ? 1 : 0)
            if count != line.operands.count { throw assemblyFailure(line.location, "Incorrect send operand count.", line.name) }
            return (send.result ? "w" : "") + String(repeating: "e", count: count - (send.result ? 1 : 0))
        }
        if ["Call", "CallExternal", "CreateList", "CreateMap", "CreateRecord", "CreateExternalType", "CreateVector", "CreatePoint", "LoadMessage", "LoadHandler", "BindHandler", "ConstructData"].contains(line.name) {
            if line.operands.count != 2 { throw assemblyFailure(line.location, "Symbolic instruction requires a destination and value.", line.name) }
            let valid: Bool
            switch (line.name, line.operands[1].kind) {
            case ("Call", .call), ("BindHandler", .call), ("CallExternal", .extensionCall), ("CreateList", .list), ("CreateMap", .map), ("LoadMessage", .message), ("LoadHandler", .handler): valid = true
            case ("CreateVector", .constructor(let type, _)): valid = type == "vector"
            case ("CreatePoint", .constructor(let type, _)): valid = type == "point"
            case ("CreateRecord", .constructor(let type, _)): valid = records[type] != nil
            case ("CreateExternalType", .constructor(let type, _)): valid = try records[type] == nil && type.first?.isUppercase == true && catalog?.resolve(type) != nil
            case ("ConstructData", .constructor(let type, let args)):
                valid = ["record", "series"].contains(type) || ["number", "range", "message", "nothing", "percentage", "boolean", "text", "tag", "list", "map", "dice", "handler"].contains(type) && (args.count != 1 || args[0].label != "_")
            default: valid = false
            }
            if !valid { throw assemblyFailure(line.location, "Operand does not match symbolic instruction.", line.name) }
            return "we"
        }
        switch line.name {
        case "LoadInteger" where line.operands.count == 3: return "wiq"
        case "LoadFloat" where line.operands.count == 3: return "wfq"
        case "Cast", "CheckType": return "wry"
        case "CastUnit", "CheckUnit": return "wrq"
        case "CreateSeries": return "wz"
        case "RandomPush": return "r"
        case "RandomPushConstant": return "i"
        case "RandomPop": return ""
        case "HasPattern", "TakePattern": return line.operands.count == 5 ? "wrpsr" : "wrps"
        case "SplitText": return line.operands.count == 2 ? "wr" : "wrr"
        default:
            guard let entry = Self.assemblyInstructions[line.name] else { throw assemblyFailure(line.location, "Unsupported assembly instruction.", line.name) }
            return entry.1
        }
    }

    func assemblySymbolicReads(_ e: GesExpression, handlerTarget: Bool = false) throws -> [GesExpression] {
        var operands: [GesExpression]
        switch e.kind {
        case .call(let name, let args): operands = (handlerTarget ? [.init(.name(name), e.location)] : []) + args.map(\.value)
        case .extensionCall(_, _, let args), .constructor(_, let args), .message(_, let args): operands = args.map(\.value)
        case .handler: operands = []
        case .list(let items): operands = items
        case .map(let entries): operands = entries.map(\.1)
        default: operands = [e]
        }
        for operand in operands {
            if case .name = operand.kind { continue }
            if !assemblyScalar(operand) { throw assemblyFailure(operand.location, "Symbolic arguments must be scalar literals or names.") }
        }
        return operands
    }

    func assemblySymbolic(_ line: GesAssemblyLine, _ regs: [Int], _ r: GesRoutine, _ scope: GesScope) throws -> Bool {
        if let send = assemblySend(line) {
            var offset = send.result ? 1 : 0
            let delay = send.delayed ? line.operands[offset] : nil
            if send.delayed { offset += 1 }
            let message = line.operands[offset]
            offset += 1
            var tags: [GesExpression] = []
            if send.tags {
                guard case .list(let items) = line.operands[offset].kind else { throw assemblyFailure(line.location, "Send tags require a literal list.") }
                tags = items
            }
            if !send.result {
                let valid: Bool
                if line.name.hasSuffix("Value") || line.name.hasSuffix("ValueWithTags") { if case .name = message.kind { valid = true } else { valid = false } } else { if case .message = message.kind { valid = true } else { valid = false } }
                if !valid { throw assemblyFailure(line.location, "Message opcode and operand disagree.", line.name) }
            }
            if send.result {
                _ = try expression(.init(.send(send.publish, message, tags, delay), line.location), r, scope, destination: regs[0])
            } else {
                try statements([.init(kind: .publish(send.publish, message, tags), location: line.location)], r, scope)
            }
            return true
        }
        if line.name == "emit" || line.name == "publish" {
            try statements([.init(kind: .publish(line.name == "publish", line.operands[0], Array(line.operands.dropFirst())), location: line.location)], r, scope)
            return true
        }
        if ["Call", "CallExternal", "CreateList", "CreateMap", "CreateRecord", "CreateExternalType", "CreateVector", "CreatePoint", "LoadMessage", "LoadHandler", "BindHandler", "ConstructData"].contains(line.name) {
            if line.name == "Call", case .call(let name, let args) = line.operands[1].kind {
                let key = signature(name, args.map(\.label))
                guard let definition = definitions[key] else { throw error("compile.unresolvedSymbol", line.location, symbol: name, phase: .compile) }
                let values = try args.map { try expression($0.value, r, scope) }
                stage(values, r)
                let instruction = r.emit(.call, regs[0], flags: definition.kind == "predicate" ? 0x20 : 0)
                r.calls.append((instruction, key))
                r.dependencies.insert(key)
                return true
            }
            _ = try expression(line.operands[1], r, scope, destination: regs[0])
            return true
        }
        return false
    }

    func assemblyType(_ e: GesExpression) throws -> Int {
        let name = try assemblyText(e)
        let types: [String: GameEventScriptBytecodeTypeKind] = [
            "nothing": .nothing, "boolean": .boolean, "percentage": .percentage, "vector": .vector, "point": .point, "series": .series, "tag": .tag, "text": .text, "list": .list, "range": .range, "message": .message, "handler": .handler, "map": .map,
            "dice": .dice,
        ]
        if name == "Number" { return Int(GameEventScriptBytecodeTypeKind.float.rawValue) }
        guard let type = types[name.lowercased()] else { throw assemblyFailure(e.location, "Unknown assembly type.", name) }
        return Int(type.rawValue)
    }

    func assemblyUnit(_ e: GesExpression) throws -> UInt8 {
        switch try assemblyText(e) {
        case "none": return 0
        case "degree": return 1
        case "m", "meter": return 2
        case "s", "second": return 3
        default: throw assemblyFailure(e.location, "Unknown assembly unit.")
        }
    }

    func assemblyPattern(_ e: GesExpression) throws -> Int {
        switch try assemblyText(e) {
        case "countAny": return 0
        case "countFace": return 1
        case "fullHouse": return 2
        case "straight": return 3
        default: throw assemblyFailure(e.location, "Unknown assembly pattern.")
        }
    }

    func assemblySeries(_ e: GesExpression) throws -> Int {
        switch try assemblyText(e) {
        case "fibonacci": return 1
        case "factorial": return 2
        default: throw assemblyFailure(e.location, "Unknown assembly series.")
        }
    }

    func assemblySpecial(_ line: GesAssemblyLine, _ regs: [Int], _ r: GesRoutine) throws -> Bool {
        let args = line.operands
        switch line.name {
        case "LoadInteger" where args.count == 3: r.emit(.loadInteger, regs[0], payload: UInt64(bitPattern: try assemblyInteger(args[1])), flags: try assemblyUnit(args[2]))
        case "LoadFloat" where args.count == 3: r.emit(.loadFloat, regs[0], payload: try assemblyNumber(args[1]).bitPattern, flags: try assemblyUnit(args[2]))
        case "Cast", "CheckType": r.emit(line.name == "Cast" ? .cast : .checkType, regs[0], regs[1], try assemblyType(args[2]))
        case "CastUnit", "CheckUnit": r.emit(line.name == "CastUnit" ? .castUnit : .checkUnit, regs[0], regs[1], flags: try assemblyUnit(args[2]))
        case "CreateSeries": r.emit(.createSeries, regs[0], 0, try assemblySeries(args[1]))
        case "RandomPush": r.emit(.randomPush, 0, regs[0])
        case "RandomPushConstant": r.emit(.randomPushConstant, payload: UInt64(bitPattern: try assemblyInteger(args[0])))
        case "RandomPop": r.emit(.randomPop)
        case "HasPattern", "TakePattern":
            let pattern = try assemblyPattern(args[2])
            let count = try assemblyInteger(args[3], 0, Int64(Int16.max))
            if (pattern == 1) != (args.count == 5) { throw assemblyFailure(line.location, "Only countFace takes a face operand.") }
            r.emit(line.name == "HasPattern" ? .hasPattern : .takePattern, regs[0], regs[1], Int(count), payload: UInt64(pattern) | UInt64(args.count == 5 ? regs[4] : 0) << 16)
        case "SplitText": r.emit(.splitText, regs[0], regs[1], args.count == 3 ? regs[2] : 0, payload: args.count == 2 ? 1 : 0)
        default: return false
        }
        return true
    }
}
