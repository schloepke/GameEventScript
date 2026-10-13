// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVmState {
    @inline(never)
    func executeZip(_ target: Int, _ x: Int, _ y: Int) {
        registers.withUnsafeMutableBufferPointer { slots in
            guard let left = slots[x].listValue, let right = slots[y].listValue else { return slots[target].setNothing() }
            return slots[target].setList(zip(left, right).map { .map([.init(key: "left", value: $0.0), .init(key: "right", value: $0.1)]) })
        }
    }
}
