// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import XCTest

@testable import GameEventScriptNetwork

#if canImport(CryptoKit)
    import CryptoKit
#endif

// Checks the cipher suite against conformance/fixtures/Network/crypto.json, which scripts/test-network-crypto.py
// verifies against an independent oracle, and against the platform implementation of the same suite where one exists.
final class NetworkCryptoTests: XCTestCase {
    nonisolated(unsafe) private static let fixture: [String: Any] = {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("conformance/fixtures/Network/crypto.json")
        let data = try! Data(contentsOf: url)
        return try! JSONSerialization.jsonObject(with: data) as! [String: Any]
    }()

    private func cases(_ section: String) -> [[String: Any]] { Self.fixture[section] as! [[String: Any]] }

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

    private func bytes(_ testCase: [String: Any], _ name: String) -> [UInt8] { unhex(testCase[name] as! String) }

    private func scalarMultiply(_ scalar: [UInt8], _ u: [UInt8]) -> [UInt8] {
        var result = [UInt8](repeating: 0, count: 32)
        scalar.withUnsafeBytes { s in u.withUnsafeBytes { p in result.withUnsafeMutableBytes { GesNetworkX25519.scalarMultiply(s, p, into: $0) } } }
        return result
    }

    private func sharedSecret(_ scalar: [UInt8], _ u: [UInt8]) -> (Bool, [UInt8]) {
        var result = [UInt8](repeating: 0, count: 32)
        let valid = scalar.withUnsafeBytes { s in u.withUnsafeBytes { p in result.withUnsafeMutableBytes { GesNetworkX25519.sharedSecret(s, p, into: $0) } } }
        return (valid, result)
    }

    private func seal(_ aead: GesNetworkAead, key: [UInt8], nonce: [UInt8], aad: [UInt8], plaintext: [UInt8]) -> [UInt8] {
        var buffer = plaintext + [UInt8](repeating: 0, count: 16)
        key.withUnsafeBytes { aead.setKey($0) }
        nonce.withUnsafeBytes { n in aad.withUnsafeBytes { a in buffer.withUnsafeMutableBytes { aead.seal(nonce: n, aad: a, buffer: $0, length: plaintext.count) } } }
        return buffer
    }

    private func open(_ aead: GesNetworkAead, key: [UInt8], nonce: [UInt8], aad: [UInt8], sealed: inout [UInt8]) -> Bool {
        key.withUnsafeBytes { aead.setKey($0) }
        return nonce.withUnsafeBytes { n in aad.withUnsafeBytes { a in sealed.withUnsafeMutableBytes { aead.open(nonce: n, aad: a, sealed: UnsafeRawBufferPointer($0), into: $0) } } }
    }

    private func hkdf(salt: [UInt8], ikm: [UInt8], info: [UInt8], length: Int) -> [UInt8] {
        var prk = [UInt8](repeating: 0, count: 32)
        salt.withUnsafeBytes { s in ikm.withUnsafeBytes { i in prk.withUnsafeMutableBytes { GesNetworkHkdf.extract(salt: s, ikm: i, into: $0) } } }
        var okm = [UInt8](repeating: 0, count: length)
        prk.withUnsafeBytes { p in info.withUnsafeBytes { i in okm.withUnsafeMutableBytes { GesNetworkHkdf.expand(prk: p, label: i, into: $0) } } }
        return okm
    }

    func testX25519MatchesTheFixture() {
        for testCase in cases("x25519") {
            XCTAssertEqual(hex(scalarMultiply(bytes(testCase, "scalar"), bytes(testCase, "u"))), testCase["result"] as? String, testCase["name"] as? String ?? "")
        }
    }

    func testX25519IteratesLikeRfc7748() {
        var k = [UInt8](repeating: 0, count: 32)
        k[0] = 9
        var u = k
        for iteration in 1...1000 {
            let next = scalarMultiply(k, u)
            u = k
            k = next
            if iteration == 1 { XCTAssertEqual(hex(k), "422c8e7a6227d7bca1350b3e2bb7279f7897b87bb6854b783c60e80311ae3079") }
        }
        XCTAssertEqual(hex(k), "684cf59ba83309552800ef566f2f4d3c1c3887c49360e3875f2eb94d99532c51")
    }

    func testSharedSecretRejectsLowOrderKeys() {
        let scalar = unhex("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a")
        XCTAssertFalse(sharedSecret(scalar, [UInt8](repeating: 0, count: 32)).0)
        var one = [UInt8](repeating: 0, count: 32)
        one[0] = 1
        XCTAssertFalse(sharedSecret(scalar, one).0)
        let (valid, secret) = sharedSecret(scalar, unhex("de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f"))
        XCTAssertTrue(valid)
        XCTAssertEqual(hex(secret), "4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742")
    }

    func testPoly1305MatchesTheFixtureWholeAndInPieces() {
        let poly = GesNetworkPoly1305()
        var tag = [UInt8](repeating: 0, count: 16)
        for testCase in cases("poly1305") {
            let name = testCase["name"] as? String ?? ""
            let key = bytes(testCase, "key")
            let message = bytes(testCase, "message")
            key.withUnsafeBytes { poly.start(key: $0) }
            message.withUnsafeBytes { poly.update($0) }
            tag.withUnsafeMutableBytes { poly.finish(into: $0) }
            XCTAssertEqual(hex(tag), testCase["tag"] as? String, name)
            key.withUnsafeBytes { poly.start(key: $0) }
            message.withUnsafeBytes { all in
                var offset = 0
                while offset < all.count {
                    poly.update(UnsafeRawBufferPointer(rebasing: all[offset..<min(offset + 7, all.count)]))
                    offset += 7
                }
            }
            tag.withUnsafeMutableBytes { poly.finish(into: $0) }
            XCTAssertEqual(hex(tag), testCase["tag"] as? String, name)
        }
    }

    func testAeadMatchesTheFixtureAndRejectsChanges() {
        let aead = GesNetworkAead()
        for testCase in cases("aead") {
            let name = testCase["name"] as? String ?? ""
            let plaintext = bytes(testCase, "plaintext")
            var sealed = seal(aead, key: bytes(testCase, "key"), nonce: bytes(testCase, "nonce"), aad: bytes(testCase, "aad"), plaintext: plaintext)
            XCTAssertEqual(hex(sealed), testCase["sealed"] as? String, name)
            XCTAssertTrue(open(aead, key: bytes(testCase, "key"), nonce: bytes(testCase, "nonce"), aad: bytes(testCase, "aad"), sealed: &sealed), name)
            XCTAssertEqual(Array(sealed[0..<plaintext.count]), plaintext, name)
        }
        for testCase in cases("aeadRejected") {
            let original = bytes(testCase, "sealed")
            var copy = original
            XCTAssertFalse(open(aead, key: bytes(testCase, "key"), nonce: bytes(testCase, "nonce"), aad: bytes(testCase, "aad"), sealed: &copy), testCase["name"] as? String ?? "")
            XCTAssertEqual(copy, original)
        }
    }

    func testHkdfMatchesTheFixture() {
        for testCase in cases("hkdf") {
            let okm = hkdf(salt: bytes(testCase, "salt"), ikm: bytes(testCase, "ikm"), info: bytes(testCase, "info"), length: testCase["length"] as! Int)
            XCTAssertEqual(hex(okm), testCase["okm"] as? String, testCase["name"] as? String ?? "")
        }
    }

    // The platform implementations of the same suite agree on random inputs: a port may use either.
    func testTheSuiteAgreesWithThePlatform() throws {
        #if canImport(CryptoKit)
            guard #available(macOS 11.0, iOS 14.0, *) else { throw XCTSkip("CryptoKit's HKDF needs macOS 11.") }
            var generator = SystemRandomNumberGenerator()

            func random(_ count: Int) -> [UInt8] { (0..<count).map { _ in UInt8.random(in: 0...255, using: &generator) } }

            let aead = GesNetworkAead()
            for _ in 0..<200 {
                let key = random(32)
                let nonce = random(12)
                let aad = random(Int.random(in: 0..<40))
                let plaintext = random(Int.random(in: 0..<1300))
                let box = try ChaChaPoly.seal(plaintext, using: SymmetricKey(data: key), nonce: ChaChaPoly.Nonce(data: nonce), authenticating: aad)
                XCTAssertEqual(seal(aead, key: key, nonce: nonce, aad: aad, plaintext: plaintext), Array(box.ciphertext) + Array(box.tag))

                let info = random(Int.random(in: 0..<30))
                let length = Int.random(in: 1..<100)
                let expected = HKDF<SHA256>.deriveKey(inputKeyMaterial: SymmetricKey(data: key), salt: aad, info: info, outputByteCount: length)
                XCTAssertEqual(hkdf(salt: aad, ikm: key, info: info, length: length), expected.withUnsafeBytes { Array($0) })

                let privateKey = random(32)
                let peer = Curve25519.KeyAgreement.PrivateKey()
                var publicKey = [UInt8](repeating: 0, count: 32)
                privateKey.withUnsafeBytes { p in publicKey.withUnsafeMutableBytes { GesNetworkX25519.publicKey(p, into: $0) } }
                let platform = try Curve25519.KeyAgreement.PrivateKey(rawRepresentation: privateKey)
                XCTAssertEqual(publicKey, Array(platform.publicKey.rawRepresentation))
                let shared = try peer.sharedSecretFromKeyAgreement(with: Curve25519.KeyAgreement.PublicKey(rawRepresentation: publicKey))
                XCTAssertEqual(sharedSecret(privateKey, Array(peer.publicKey.rawRepresentation)).1, shared.withUnsafeBytes { Array($0) })
            }
        #else
            throw XCTSkip("The platform has no CryptoKit.")
        #endif
    }
}
