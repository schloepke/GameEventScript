// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Strict UTF-8 decoding without a platform framework or deployment-version dependency.
enum GesUtf8 {
    static func decode(_ bytes: [UInt8]) -> String? {
        let text = String(decoding: bytes, as: UTF8.self)
        return text.utf8.elementsEqual(bytes) ? text : nil
    }
}
