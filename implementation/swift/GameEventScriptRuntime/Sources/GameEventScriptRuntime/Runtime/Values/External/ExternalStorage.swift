// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

final class GesExternalStorage: Hashable {
    let value: any GameEventScriptExternalValue
    let definition: GameEventScriptExternalTypeDefinition
    private var cachedMap: GesValueMap?

    init(value: any GameEventScriptExternalValue) {
        self.value = value
        definition = value.definition
    }

    static func == (lhs: GesExternalStorage, rhs: GesExternalStorage) -> Bool { lhs === rhs }

    func hash(into hasher: inout Hasher) { hasher.combine(ObjectIdentifier(self)) }

    func field(_ name: String) throws -> GesValue? {
        guard let field = definition.fields.first(where: { $0.name == name }) else { return nil }
        do { return try value.field(name).map { GesExternalNames.coerce($0, kind: field.kind, unit: field.unit) } } catch let fault as GameEventScriptExtensionFault { throw fault } catch let fault as GesRuntimeError { throw fault } catch {
            throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.externalFieldAccessFailed", symbol: definition.name + "." + name, technicalDetails: String(describing: error)))
        }
    }

    func map() throws -> GesValueMap {
        if let cachedMap { return cachedMap }
        var entries: [GesMapEntry] = []
        for field in definition.fields {
            do { if let result = try self.field(field.name) { entries.append(.init(key: field.name, value: result)) } } catch let fault as GameEventScriptExtensionFault { throw fault } catch let error as GesRuntimeError { throw error } catch {
                throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: "runtime.externalFieldAccessFailed", symbol: definition.name + "." + field.name, technicalDetails: String(describing: error)))
            }
        }
        let map = GesValueMap(entries)
        cachedMap = map
        return map
    }
}
