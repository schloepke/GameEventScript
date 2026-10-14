// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func createIterator(_ destination: Int, _ value: GesValue, _ c: GameEventScriptContext, checkRangeLimit: Bool = false) {
        var value = value
        if checkRangeLimit, let count = value.integerRangeValue?.count ?? value.floatRangeValue?.count, !c.budget.range(count) { value = .integerRange(from: 0, to: 0, step: 0) }
        if let iterator = GesIterator(value) { setIterator(destination, iterator) } else { setNothing(destination) }
    }
}
