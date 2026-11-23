// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import XCTest

@testable import GameEventScriptNetwork

final class NetworkSha256Tests: XCTestCase {
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

    private func sha256(_ message: [UInt8], _ sha: GesNetworkSha256 = GesNetworkSha256()) -> String {
        var digest = [UInt8](repeating: 0, count: 32)
        sha.reset()
        message.withUnsafeBytes { sha.update($0) }
        digest.withUnsafeMutableBytes { sha.finish(into: $0) }
        return hex(digest)
    }

    private func hmac(_ key: [UInt8], _ message: [UInt8]) -> String {
        let hmac = key.withUnsafeBytes { GesNetworkHmacSha256(key: $0) }
        var digest = [UInt8](repeating: 0, count: 32)
        message.withUnsafeBytes { message in digest.withUnsafeMutableBytes { hmac.compute(message, into: $0) } }
        return hex(digest)
    }

    // FIPS 180-4 example vectors.
    func testSha256MatchesTheFipsVectors() {
        let sha = GesNetworkSha256()
        XCTAssertEqual(sha256(Array("abc".utf8), sha), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")
        XCTAssertEqual(
            sha256(Array("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq".utf8), sha),
            "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1"
        )
        XCTAssertEqual(sha256([], sha), "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")
    }

    // RFC 4231 test cases 1 to 4, 6 and 7; case 5 truncates the output.
    func testHmacSha256MatchesTheRfc4231Vectors() {
        XCTAssertEqual(
            hmac([UInt8](repeating: 0x0b, count: 20), Array("Hi There".utf8)),
            "b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7"
        )
        XCTAssertEqual(
            hmac(Array("Jefe".utf8), Array("what do ya want for nothing?".utf8)),
            "5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843"
        )
        XCTAssertEqual(
            hmac([UInt8](repeating: 0xaa, count: 20), [UInt8](repeating: 0xdd, count: 50)),
            "773ea91e36800e46854db8ebd09181a72959098b3ef8c122d9635514ced565fe"
        )
        XCTAssertEqual(
            hmac(unhex("0102030405060708090a0b0c0d0e0f10111213141516171819"), [UInt8](repeating: 0xcd, count: 50)),
            "82558a389a443c0ea4cc819899f2083a85f0faa3e578f8077a2e3ff46729665b"
        )
        XCTAssertEqual(
            hmac([UInt8](repeating: 0xaa, count: 131), Array("Test Using Larger Than Block-Size Key - Hash Key First".utf8)),
            "60e431591ee0b67f0d8a26aacbf5b77f8e0bc6213728c5140546040f0ee37f54"
        )
        XCTAssertEqual(
            hmac(
                [UInt8](repeating: 0xaa, count: 131),
                Array("This is a test using a larger than block-size key and a larger than block-size data. The key needs to be hashed before being used by the HMAC algorithm.".utf8)
            ),
            "9b09ffa71b942fcb27635fbcd5b0e944bfdc63644f0713938a7f51535c3a35e2"
        )
    }

    func testHmacReusesItsStateAcrossMessages() {
        let key = [UInt8](repeating: 0x0b, count: 20)
        let hmac = key.withUnsafeBytes { GesNetworkHmacSha256(key: $0) }
        var digest = [UInt8](repeating: 0, count: 32)
        for _ in 0..<3 {
            Array("Hi There".utf8).withUnsafeBytes { message in digest.withUnsafeMutableBytes { hmac.compute(message, into: $0) } }
            XCTAssertEqual(hex(digest), "b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7")
        }
    }
}
