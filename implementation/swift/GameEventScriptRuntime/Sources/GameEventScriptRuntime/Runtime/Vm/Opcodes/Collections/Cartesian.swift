// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeCartesian(_ destination: Int, _ left: GesValue, _ right: GesValue, _ budget: GesRuntimeBudget) {
        guard let a = left.listValue, let b = right.listValue else {
            setNothing(destination)
            return
        }
        let (count, overflow) = a.count.multipliedReportingOverflow(by: b.count)
        guard !overflow, budget.generated(count), count <= Int32.max, count == 0 || budget.generated(2) else {
            setNothing(destination)
            return
        }
        var result: [GesValue] = []
        result.reserveCapacity(count)
        for first in a { for second in b { result.append(.list([first, second])) } }
        setValue(destination, .list(result))
    }
}
