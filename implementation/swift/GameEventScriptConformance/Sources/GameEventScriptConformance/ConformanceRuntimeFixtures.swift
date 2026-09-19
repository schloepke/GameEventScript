// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// The fixed, language-neutral fixture catalog specified by Conformance/Environment.md.
struct RuntimeFixtures: GameEventScriptExtensionRegistry, GameEventScriptExternalTypeRegistry {
    var mismatch = false
    func resolve(_ reference: GameEventScriptExtensionReference) -> (any GameEventScriptExtensionFunction)? {
        let name = reference.extensionName + "." + reference.functionName
        let labels = reference.argumentLabels
        let unlabeled = labels.allSatisfy { $0 == "_" }
        let allowed: Bool
        switch name {
        case "math.floor", "test.vectorSum", "test.echo", "test.notify": allowed = labels.count == 1 && unlabeled
        case "math.max": allowed = !labels.isEmpty && unlabeled
        case "nav.shortestTurn": allowed = labels == ["from", "to"]
        case "nav.isNorth": allowed = labels.count == 1
        case "test.truth", "test.fail", "test.declaredFault", "test.failWithRandomScope",
            "test.declaredFaultWithRandomScope":
            allowed = labels.isEmpty
        default: allowed = false
        }
        return allowed ? FixtureFunction(name: name) : nil
    }
    func resolve(_ reference: GameEventScriptExternalTypeConstructorReference) throws -> (
        any GameEventScriptExternalTypeConstructor
    )? {
        if mismatch {
            return FixtureConstructor(
                definition: try .init(typeName: "Mismatch", parameters: [.init(name: "value", kind: .float)]))
        }
        for definition in try [Self.aim(), Self.probe()] {
            for constructor in definition.constructors where constructor.signatureID == reference.signatureID {
                return FixtureConstructor(definition: constructor)
            }
        }
        return nil
    }
    static func aim() throws -> GameEventScriptExternalTypeDefinition {
        let fields: [GameEventScriptExternalTypeFieldDefinition] = try [
            .init(name: "bearing", kind: .float, unit: .degree), .init(name: "range", kind: .float, unit: .meter),
            .init(name: "steps", kind: .float, unit: .meter), .init(name: "direction", kind: .vector, unit: .meter),
            .init(name: "checksum", kind: .float),
        ]
        return try .init(
            name: "Aim", fields: fields,
            constructors: [
                .init(
                    typeName: "Aim",
                    parameters: fields.prefix(4).map { try .init(name: $0.name, kind: $0.kind!, unit: $0.unit) })
            ])
    }
    static func probe() throws -> GameEventScriptExternalTypeDefinition {
        try .init(
            name: "CallbackProbe",
            fields: [
                .init(name: "failure", kind: .text), .init(name: "context", kind: .text),
                .init(name: "value", kind: .float),
            ],
            constructors: [
                .init(
                    typeName: "CallbackProbe",
                    parameters: [.init(name: "failure", kind: .text), .init(name: "context", kind: .text)])
            ])
    }
    static func fault(symbol: String, context: String) throws -> GameEventScriptExtensionFault {
        try .init(
            code: "test.callbackFault", message: "Configured external fault", symbol: symbol,
            programName: ["program", "both"].contains(context) ? "reported.program" : nil,
            handlerName: ["handler", "both"].contains(context) ? "Reported()" : nil)
    }
}
private struct FixtureFunction: GameEventScriptExtensionFunction {
    let name: String
    func invoke(_ call: GesExtensionCall) throws {
        let args = call.arguments
        switch name {
        case "math.floor": call.setFloat(args[0].asNumber.rounded(.down), unit: args[0].unit)
        case "math.max":
            var number = args[0].asNumber
            for index in 1..<args.count {
                let next = args[index].asNumber
                number = number.isNaN || next.isNaN ? .nan : max(number, next)
            }
            call.setFloat(number)
        case "nav.shortestTurn":
            call.setFloat(
                (args[1].asNumber - args[0].asNumber + 540).truncatingRemainder(dividingBy: 360) - 180, unit: .degree)
        case "nav.isNorth":
            let angle = (args[0].asNumber.truncatingRemainder(dividingBy: 360) + 360).truncatingRemainder(
                dividingBy: 360)
            call.setBoolean(angle <= 45 || angle >= 315)
        case "test.vectorSum":
            if args[0].kind == .vector { call.setFloat(args[0].x + args[0].y + args[0].z, unit: args[0].unit) }
        case "test.echo": call.setValue(args[0])
        case "test.notify":
            try call.context.emit("Effect", arguments: [.init(name: "value", value: args[0])])
            call.setValue(args[0])
        case "test.truth": call.setBoolean(true)
        default:
            if name.hasSuffix("WithRandomScope") {
                call.random.push(seed: 7)
                _ = call.random.nextInclusiveInteger(1, 100)
            }
            if name.contains("declaredFault") {
                throw try GameEventScriptExtensionFault(
                    code: "test.declaredFault", message: "Configured extension fault")
            }
            throw ConformanceExecutionError.invalidInput("Configured extension failure")
        }
    }
}
private struct FixtureConstructor: GameEventScriptExternalTypeConstructor {
    let definition: GameEventScriptExternalTypeConstructorDefinition
    func invoke(_ call: GesExternalTypeConstructorCall) throws {
        if definition.typeName == "Aim" {
            let args = call.arguments
            let sum = args[0].asNumber + args[1].asNumber + args[2].asNumber
            let checksum =
                sum.isFinite && sum >= -9_223_372_036_854_775_808 && sum < 9_223_372_036_854_775_808
                ? Double(Int64(sum)) : 0
            try call.setExternalValue(
                FixtureValue(
                    definition: RuntimeFixtures.aim(), fields: [args[0], args[1], args[2], args[3], .float(checksum)]))
        } else if definition.typeName == "CallbackProbe" {
            let failure = call.arguments[0].asText
            let context = call.arguments[1].asText
            if !["none", "program", "handler", "both"].contains(context) { return }
            if failure == "constructorUnexpected" {
                throw ConformanceExecutionError.invalidInput("Configured constructor failure")
            }
            if failure == "constructorDeclared" {
                throw try RuntimeFixtures.fault(symbol: "CallbackProbe", context: context)
            }
            if ["fieldUnexpected", "fieldDeclared"].contains(failure) {
                try call.setExternalValue(
                    FixtureValue(definition: RuntimeFixtures.probe(), fields: [.text(failure), .text(context)]))
            }
        }
    }
}
private final class FixtureValue: GameEventScriptExternalValue {
    let definition: GameEventScriptExternalTypeDefinition
    let fields: [GesValue]
    init(definition: GameEventScriptExternalTypeDefinition, fields: [GesValue]) {
        self.definition = definition
        self.fields = fields
    }
    func field(_ name: String) throws -> GesValue? {
        if definition.name == "CallbackProbe" && name == "value" {
            if fields[0].asText == "fieldUnexpected" {
                throw ConformanceExecutionError.invalidInput("Configured field failure")
            }
            throw try RuntimeFixtures.fault(symbol: "CallbackProbe.value", context: fields[1].asText)
        }
        guard let index = definition.fields.firstIndex(where: { $0.name == name }) else { return nil }
        return fields[index]
    }
}
