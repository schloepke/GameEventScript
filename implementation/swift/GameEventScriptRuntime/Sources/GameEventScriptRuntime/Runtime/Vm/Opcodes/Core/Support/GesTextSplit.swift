// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesTextSplit {
    static func whitespace(_ scalar: Unicode.Scalar) -> Bool {
        let v = scalar.value
        return (9...13).contains(v) || [0x20, 0x85, 0xA0, 0x1680, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000].contains(v) || (0x2000...0x200A).contains(v)
    }

    static func split<Output: GesValueOutput>(_ input: GesValue, _ delimiter: GesValue, whitespace: Bool, sink: Output) -> Output.Result {
        guard input.kind == .text, let text = input.textValue else { return sink.nothing }
        if whitespace {
            return sink.list(text.unicodeScalars.split(whereSeparator: Self.whitespace).map { .text(String(String.UnicodeScalarView($0))) })
        }
        guard delimiter.kind == .text, let separator = delimiter.textValue, !separator.isEmpty else { return sink.nothing }
        // Scalar arrays avoid Swift's canonical-equivalence and grapheme-cluster matching.
        let scalars = Array(text.unicodeScalars)
        let needle = Array(separator.unicodeScalars)
        var start = 0
        var index = 0
        var result: [GesValue] = []

        func segment(_ end: Int) -> GesValue { end == start ? .nothing : .text(String(String.UnicodeScalarView(scalars[start..<end]))) }

        while index + needle.count <= scalars.count {
            if scalars[index..<(index + needle.count)].elementsEqual(needle) {
                result.append(segment(index))
                index += needle.count
                start = index
            } else {
                index += 1
            }
        }
        result.append(segment(scalars.count))
        return sink.list(result)
    }
}
