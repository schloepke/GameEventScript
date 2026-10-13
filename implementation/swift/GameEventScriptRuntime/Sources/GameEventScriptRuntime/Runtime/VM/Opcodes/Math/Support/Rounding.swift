// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesMath {
    @inline(__always)
    static func performFloorCeilTruncateRoundHalfEvenRoundHalfUpRoundHalfDown<Output: GesValueOutput>(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext, _ a: GesValue, _ b: GesValue, sink: Output) throws
        -> Output.Result
    {
        if !a.isNumeric { return sink.nothing }
        let n = a.asNumber
        let result: Double
        switch i.opcode {
        case .floor: result = n.rounded(.down)
        case .ceil: result = n.rounded(.up)
        case .truncate: result = n.rounded(.towardZero)
        case .roundHalfEven: result = n.rounded(.toNearestOrEven)
        case .roundHalfUp: result = n.rounded(.toNearestOrAwayFromZero)
        default:
            let truncated = n.rounded(.towardZero)
            result = Swift.abs(n - truncated) <= 0.5 ? truncated : n.rounded(.toNearestOrAwayFromZero)
        }
        return sink.integer(GesNumber.saturatedInteger(result))
    }
}
