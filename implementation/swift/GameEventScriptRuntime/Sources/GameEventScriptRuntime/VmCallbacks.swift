// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesCallbacks {
    static func extensionCall(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        let destination = Int(i.word0)
        let registers = s.list(i.word2)
        guard let binding = s.linked!.extensionBindings[i.word1], let function = s.linked!.extensions[i.word1], binding.argumentNames.count == registers.count else {
            s.set(destination, .nothing)
            s.fail("runtime.invalidExtensionBinding")
            return
        }
        let call = s.extensionCall
        let boundary = c.random.markBoundary()
        call.begin(c, .init(state: s, indexes: registers))
        defer {
            let fault = c.endBoundary(boundary)
            if s.error == nil { if fault == .boundaryUnderflow { s.fail("runtime.randomStackUnderflow") } else if fault == .unbalanced { s.fail("runtime.randomScopeImbalance") } }
            call.end()
        }
        do { try function.invoke(call) } catch let fault as GameEventScriptExtensionFault {
            s.fail(fault.diagnostic)
            throw fault
        } catch let fault as GesRuntimeError {
            s.fail(fault.diagnostic)
            throw fault
        } catch {
            s.set(destination, .nothing)
            s.fail("runtime.extensionCallFailed", symbol: s.text(binding.name), error: error)
            return
        }
        s.set(destination, i.normalizeResultAsPredicate && call.result.kind != .boolean ? .nothing : call.result)
    }

    static func constructor(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        let destination = Int(i.word0)
        guard let binding = s.linked!.constructorBindings[i.word1], let constructor = s.linked!.constructors[i.word1] else {
            s.set(destination, .nothing)
            s.fail("runtime.invalidExternalTypeBinding")
            return
        }
        let labels = s.list(i.word2).map(s.text)
        guard labels.count == s.stageLength, labels.count == binding.argumentNames.count, labels.count == constructor.definition.parameters.count, !labels.contains("_") else {
            s.set(destination, .nothing)
            return
        }
        var arguments: [GesValue] = []
        for parameter in constructor.definition.parameters {
            guard let index = labels.firstIndex(of: parameter.name) else {
                s.set(destination, .nothing)
                return
            }
            let value = try convert(s.staged(index), to: parameter.typeName, c)
            if value.isNothing {
                s.set(destination, .nothing)
                return
            }
            arguments.append(GesExternalNames.coerce(value, kind: parameter.kind, unit: parameter.unit))
        }
        let call = s.constructorCall
        call.begin(arguments: .init(arguments), typeName: constructor.definition.typeName)
        defer { call.end() }
        do { try constructor.invoke(call) } catch let fault as GameEventScriptExtensionFault { throw fault } catch let fault as GesRuntimeError { throw fault } catch {
            s.set(destination, .nothing)
            s.fail("runtime.externalConstructorFailed", symbol: constructor.definition.typeName, error: error)
            return
        }
        s.set(destination, call.result)
    }

    static func convert(_ value: GesValue, to name: String, _ c: GameEventScriptContext) throws -> GesValue {
        let unit: GesUnit?
        switch name {
        case "Quantity(m)", "meter": unit = .meter
        case "Quantity(s)", "second": unit = .second
        case "Quantity(°)", "degree": unit = .degree
        default: unit = nil
        }
        if let unit { return GesCasts.unit(value, unit) }
        if name == "numeric" { return GesCasts.number(value) }
        let kind: GameEventScriptBytecodeTypeKind
        switch name {
        case "Nothing": kind = .nothing
        case "Boolean": kind = .boolean
        case "Number": kind = .float
        case "Percentage": kind = .percentage
        case "Tag": kind = .tag
        case "Text": kind = .text
        case "Vector": kind = .vector
        case "Point": kind = .point
        case "Range": kind = .range
        case "Message": kind = .message
        case "Handler": kind = .handler
        case "List": kind = .list
        case "Map": kind = .map
        case "Dice": kind = .dice
        default: return .nothing
        }
        return try GesCasts.cast(value, kind, c)
    }
}
