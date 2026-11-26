// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    private static let entryKeys = ["key", "value"]

    func executeEntriesOfMap(_ destinationRegister: Int, _ sourceRegister: Int) throws {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        guard let map = try registers[x].asMap ?? registers[x].externalMap() else { return setNothing(destination) }

        return setList(destination, map.keyStorage.indices.map { .map(GesValueMap(canonicalKeys: Self.entryKeys, values: [map.keyValue(at: $0), map.valueStorage[$0]])) })
    }
}
