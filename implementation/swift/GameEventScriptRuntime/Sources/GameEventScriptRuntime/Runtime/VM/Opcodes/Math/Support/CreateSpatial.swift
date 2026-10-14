// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func createSpatial(destination: Int, start: Int, point: Bool) {
        var xyz = [0.0, 0.0, 0.0]
        var unit: GesUnit?
        if start == 0 && stageLength > 0 && staged(0).spatialValue != nil {
            if stageLength > 2 { return setNothing(destination) }
            let first = staged(0)
            xyz = [first.x, first.y, first.z]
            unit = first.unit
            if stageLength == 2 {
                let value = staged(1)
                if value.unit != unit { return setNothing(destination) }
                xyz[2] = value.asNumber
            }
        } else {
            for index in 0..<min(stageLength, max(0, 3 - start)) {
                let value = staged(index)
                if let unit, value.unit != unit { return setNothing(destination) }
                unit = value.unit
                xyz[start + index] = value.asNumber
            }
        }
        return point ? setPoint(destination, x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none) : setVector(destination, x: xyz[0], y: xyz[1], z: xyz[2], unit: unit ?? .none)
    }
}
