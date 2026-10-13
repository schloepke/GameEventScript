// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptVirtualMachine {
    static func spatial(_ s: GesVmState, destination: Int, start: Int, point: Bool) {
        var xyz = [0.0, 0.0, 0.0]
        var unit: GesUnit?
        if start == 0 && s.stageLength > 0 && s.staged(0).spatialValue != nil {
            if s.stageLength > 2 { return s.setNothing(destination) }
            let first = s.staged(0)
            xyz = [first.x, first.y, first.z]
            unit = first.unit
            if s.stageLength == 2 {
                let value = s.staged(1)
                if value.unit != unit { return s.setNothing(destination) }
                xyz[2] = value.asNumber
            }
        } else {
            for index in 0..<min(s.stageLength, max(0, 3 - start)) {
                let value = s.staged(index)
                if let unit, value.unit != unit { return s.setNothing(destination) }
                unit = value.unit
                xyz[start + index] = value.asNumber
            }
        }
        return point ? s.setPoint(destination, x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none) : s.setVector(destination, x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none)
    }
}
