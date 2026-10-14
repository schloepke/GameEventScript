// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum MessageNames {
    static func trim(_ input: String) -> String {
        let scalars = input.unicodeScalars
        var start = scalars.startIndex
        var end = scalars.endIndex
        while start < end && isSpace(scalars[start].value) { scalars.formIndex(after: &start) }
        while start < end {
            let previous = scalars.index(before: end)
            if !isSpace(scalars[previous].value) { break }
            end = previous
        }
        return String(scalars[start..<end])
    }

    private static func isSpace(_ scalar: UInt32) -> Bool { scalar == 32 || scalar == 9 }
}
