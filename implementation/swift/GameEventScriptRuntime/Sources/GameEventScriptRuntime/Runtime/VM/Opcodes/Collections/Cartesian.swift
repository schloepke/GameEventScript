// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptVirtualMachine {
    static func cartesian(_ state: GesVmState, _ destination: Int, _ left: GesValue, _ right: GesValue, _ budget: GesRuntimeBudget) {
        guard let a = left.listValue, let b = right.listValue else {
            state.setNothing(destination)
            return
        }
        let (count, overflow) = a.count.multipliedReportingOverflow(by: b.count)
        guard !overflow, budget.generated(count), count <= Int32.max, count == 0 || budget.generated(2) else {
            state.setNothing(destination)
            return
        }
        var result: [GesValue] = []
        result.reserveCapacity(count)
        for first in a { for second in b { result.append(.list([first, second])) } }
        state.setValue(destination, .list(result))
    }
}
