// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import XCTest

@testable import GameEventScriptNetwork

// Checks both directions against conformance/fixtures/Network/deflate.json, which scripts/test-network-deflate.py
// generates with an independent Python encoder and zlib's verdicts.
final class NetworkDeflateTests: XCTestCase {
    private func fixture() -> [String: Any] {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("conformance/fixtures/Network/deflate.json")
        let data = try! Data(contentsOf: url)
        return try! JSONSerialization.jsonObject(with: data) as! [String: Any]
    }

    private func hex(_ bytes: [UInt8]) -> String {
        let digits = Array("0123456789abcdef")
        var text = ""
        for byte in bytes {
            text.append(digits[Int(byte >> 4)])
            text.append(digits[Int(byte & 15)])
        }
        return text
    }

    private func unhex(_ text: String) -> [UInt8] {
        let characters = Array(text.utf8)
        return stride(from: 0, to: characters.count, by: 2).map { UInt8(String(decoding: characters[$0..<$0 + 2], as: UTF8.self), radix: 16)! }
    }

    private func sha256(_ bytes: [UInt8]) -> String {
        let sha = GesNetworkSha256()
        var digest = [UInt8](repeating: 0, count: 32)
        bytes.withUnsafeBytes { sha.update($0) }
        digest.withUnsafeMutableBytes { sha.finish(into: $0) }
        return hex(digest)
    }

    private func generate(_ spec: [String: Any]) -> [UInt8] {
        let length = spec["length"] as! Int
        let alphabet = spec["alphabet"] as! Int
        let repeatCount = spec["repeat"] as? Int ?? 0
        var state = Int64(spec["seed"] as! Int)
        var data = [UInt8](repeating: 0, count: length + repeatCount)
        for index in 0..<length {
            state = (state &* 1_103_515_245 &+ 12345) % (1 << 31)
            data[index] = UInt8(((state >> 16) & 0xff) % Int64(alphabet))
        }
        for index in 0..<repeatCount { data[length + index] = data[index] }
        return data
    }

    private func compress(_ encoder: GesNetworkDeflater, _ data: [UInt8], capacity: Int) -> (Int, [UInt8]) {
        var output = [UInt8](repeating: 0, count: capacity)
        let length = data.withUnsafeBytes { input in output.withUnsafeMutableBytes { encoder.compress(input, into: $0) } }
        return (length, output)
    }

    private func inflate(_ decoder: GesNetworkInflater, _ stream: [UInt8], size: Int) -> (Bool, [UInt8]) {
        var output = [UInt8](repeating: 0, count: size)
        let accepted = stream.withUnsafeBytes { input in output.withUnsafeMutableBytes { decoder.inflate(input, into: $0) } }
        return (accepted, output)
    }

    func testEncoderProducesTheReferenceBytesAndStopsAtTheOutputLimit() {
        let encoder = GesNetworkDeflater()
        let decoder = GesNetworkInflater()
        for testCase in fixture()["encode"] as! [[String: Any]] {
            let name = testCase["name"] as! String
            let data = generate(testCase["data"] as! [String: Any])
            let expectedLength = testCase["deflateLength"] as! Int
            let (length, output) = compress(encoder, data, capacity: expectedLength + 16)
            XCTAssertEqual(expectedLength, length, name)
            let stream = Array(output[0..<expectedLength])
            XCTAssertEqual(testCase["deflateSha256"] as? String, sha256(stream), name)
            if let expected = testCase["deflate"] as? String { XCTAssertEqual(expected, hex(stream), name) }
            XCTAssertEqual(expectedLength, compress(encoder, data, capacity: expectedLength).0, name)
            XCTAssertEqual(-1, compress(encoder, data, capacity: expectedLength - 1).0, name)
            let (accepted, restored) = inflate(decoder, stream, size: data.count)
            XCTAssertTrue(accepted, name)
            XCTAssertEqual(data, restored, name)
        }
    }

    func testDecoderAcceptsExactlyTheStreamsZlibAccepts() {
        let decoder = GesNetworkInflater()
        for testCase in fixture()["inflate"] as! [[String: Any]] {
            let name = testCase["name"] as! String
            let (accepted, output) = inflate(decoder, unhex(testCase["stream"] as! String), size: testCase["size"] as! Int)
            guard let expected = testCase["sha256"] as? String else {
                XCTAssertFalse(accepted, name)
                continue
            }
            XCTAssertTrue(accepted, name)
            XCTAssertEqual(expected, sha256(output), name)
        }
    }
}
