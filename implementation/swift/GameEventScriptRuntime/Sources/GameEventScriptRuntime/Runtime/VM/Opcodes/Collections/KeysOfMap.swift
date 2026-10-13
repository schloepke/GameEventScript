// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeKeysOfMap(_ destinationRegister: Int, _ sourceRegister: Int) throws {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        guard let map = try registers[x].asMap ?? registers[x].externalMap() else { return setNothing(destination) }
        return setList(destination, map.keys)
    }
}
