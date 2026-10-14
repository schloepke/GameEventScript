// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    @inline(never)
    func executeOneWeighted(_ destinationRegister: Int, _ sourceRegister: Int, _ otherRegister: Int, _ context: GameEventScriptContext) {
        let destination = destinationRegister
        let x = frameStart + sourceRegister
        let y = frameStart + otherRegister
        let count = 1
        guard var items = registers[x].listValue, let weights = registers[y].listValue, weights.count >= items.count else { return setNothing(destination) }
        if items.isEmpty || count <= 0 { return setNothing(destination) }
        var weightsBuffer = weights.prefix(items.count).map(\.asNumber)
        if weightsBuffer.contains(where: { !$0.isFinite || $0 <= 0 }) { return setNothing(destination) }
        var total = weightsBuffer.reduce(0, +)
        var result = [GesValue](repeating: .nothing, count: min(count, items.count))
        for target in result.indices {
            if total <= 0 { break }
            let threshold = context.random.nextFloat(0, total)
            var cumulative = 0.0
            var selected = items.count - 1
            for index in items.indices {
                cumulative += weightsBuffer[index]
                if threshold < cumulative {
                    selected = index
                    break
                }
            }
            result[target] = items.remove(at: selected)
            total -= weightsBuffer.remove(at: selected)
        }
        return setValue(destination, result[0])
    }
}
