// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

final class GesExternalStorage: Hashable {
    let value: any GesExternalValue
    let definition: GesExternalTypeDefinition

    init(value: any GesExternalValue) {
        self.value = value
        definition = value.definition
    }

    static func == (lhs: GesExternalStorage, rhs: GesExternalStorage) -> Bool { lhs === rhs }

    func hash(into hasher: inout Hasher) { hasher.combine(ObjectIdentifier(self)) }

    func field(_ name: String) throws -> GesValue? {
        guard let field = definition.fields.first(where: { GesText.scalarEqual($0.name, name) }) else { return nil }
        return try read(field)
    }

    // Runs every getter in declaration order, so the first failure is deterministic, and keeps nothing.
    func map() throws -> GesValueMap {
        let fields = definition.fields
        var values = [GesValue](repeating: .nothing, count: fields.count)
        var absent: [Bool]?
        for index in fields.indices {
            let position = definition.canonicalPositions[index]
            if let value = try read(fields[index]) {
                values[position] = value
            } else {
                if absent == nil { absent = [Bool](repeating: false, count: fields.count) }
                absent![position] = true
            }
        }
        guard let absent else { return GesValueMap(canonicalKeys: definition.canonicalKeys, values: values) }
        var presentKeys: [String] = []
        var presentValues: [GesValue] = []
        for index in fields.indices where !absent[index] {
            presentKeys.append(definition.canonicalKeys[index])
            presentValues.append(values[index])
        }
        return GesValueMap(canonicalKeys: presentKeys, values: presentValues)
    }

    private func read(_ field: GesExternalTypeFieldDefinition) throws -> GesValue? {
        do { return try value.field(field.name).map { GesExternalTypeNames.coerce($0, kind: field.kind, unit: field.unit) } } catch let fault as any GesFatalRuntimeError { throw fault } catch {
            throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.externalFieldAccessFailed", symbol: definition.name + "." + field.name, technicalDetails: String(describing: error)))
        }
    }
}
