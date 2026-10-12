// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptVirtualMachine {
    static func range<Output: GesValueOutput>(_ from: GesValue, _ to: GesValue, _ step: GesValue, sink: Output) -> Output.Result {
        if let from = from.integerValue, let to = to.integerValue, let step = step.integerValue { return sink.integerRange(from: from, to: to, step: step) }
        if from.isNumeric && to.isNumeric && step.isNumeric { return sink.floatRange(from: from.asNumber, to: to.asNumber, step: step.asNumber) }
        return sink.nothing
    }
}
