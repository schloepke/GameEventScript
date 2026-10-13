// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeZip(_ target: Int, _ x: Int, _ y: Int) {
        guard let left = registers[x].listValue, let right = registers[y].listValue else { return registers[target].setNothing() }
        return registers[target].setList(zip(left, right).map { .map([.init(key: "left", value: $0.0), .init(key: "right", value: $0.1)]) })
    }
}
