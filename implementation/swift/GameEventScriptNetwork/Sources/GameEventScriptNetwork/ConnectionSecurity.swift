// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

private typealias Wire = GesNetworkWire

extension GesNetworkConnection {
    // The keys of an encrypted connection, allocated once it uses encryption. Owns its memory and clears it on release.
    final class Keys {
        let aead = GesNetworkAead()
        let sha = GesNetworkSha256()
        let ephemeral = Keys.bytes(GesNetworkX25519.keyBytes)
        let ephemeralPublic = Keys.bytes(GesNetworkX25519.keyBytes)
        let shared = Keys.bytes(GesNetworkX25519.keyBytes)
        let helloKey = Keys.bytes(GesNetworkAead.keyBytes)
        let welcomeKey = Keys.bytes(GesNetworkAead.keyBytes)
        let nonce = Keys.bytes(GesNetworkAead.nonceBytes)
        let zeroNonce = Keys.bytes(GesNetworkAead.nonceBytes)
        let suffix = Keys.bytes(8)
        let sendSecret = Keys.bytes(GesNetworkHkdf.hashBytes)
        let sendIv = Keys.bytes(GesNetworkAead.nonceBytes)
        let sendKey = Keys.bytes(GesNetworkAead.keyBytes)
        var sendEpoch: Int64 = -1
        let receiveSecret = Keys.bytes(GesNetworkHkdf.hashBytes)
        let receiveIv = Keys.bytes(GesNetworkAead.nonceBytes)
        let receiveKeys = Keys.bytes(3 * GesNetworkAead.keyBytes)
        var receiveEpochs: (Int64, Int64, Int64) = (-1, -1, -1)
        // The largest authenticated packet number; -1 before the first.
        var largest: Int64 = -1
        // The client's sealed hello through its token, for the transcript.
        let hello: UnsafeMutableRawBufferPointer
        var helloLength = 0
        // Holds a packet while it is opened, with room to rebuild a data packet's sequence in front of it.
        let buffer: UnsafeMutableRawBufferPointer

        init(maxPacketBytes: Int) {
            hello = Self.bytes(max(maxPacketBytes, Wire.minimumPacketBytes))
            buffer = Self.bytes(maxPacketBytes + 1)
        }

        deinit {
            for memory in [ephemeral, ephemeralPublic, shared, helloKey, welcomeKey, nonce, zeroNonce, suffix, sendSecret, sendIv, sendKey, receiveSecret, receiveIv, receiveKeys, hello, buffer] {
                memory.initializeMemory(as: UInt8.self, repeating: 0)
                memory.deallocate()
            }
        }

        private static func bytes(_ count: Int) -> UnsafeMutableRawBufferPointer {
            let memory = UnsafeMutableRawBufferPointer.allocate(byteCount: count, alignment: 8)
            memory.initializeMemory(as: UInt8.self, repeating: 0)
            return memory
        }

        func receiveKey(_ slot: Int) -> UnsafeMutableRawBufferPointer {
            UnsafeMutableRawBufferPointer(rebasing: receiveKeys[slot * GesNetworkAead.keyBytes..<(slot + 1) * GesNetworkAead.keyBytes])
        }

        func receiveEpoch(_ slot: Int) -> Int64 {
            switch slot {
            case 0: return receiveEpochs.0
            case 1: return receiveEpochs.1
            default: return receiveEpochs.2
            }
        }

        func setReceiveEpoch(_ slot: Int, _ epoch: Int64) {
            switch slot {
            case 0: receiveEpochs.0 = epoch
            case 1: receiveEpochs.1 = epoch
            default: receiveEpochs.2 = epoch
            }
        }

        // The slot that holds the receive key of the epoch; -1 when none does.
        func receiveSlot(_ epoch: Int64) -> Int {
            if receiveEpochs.0 == epoch { return 0 }
            if receiveEpochs.1 == epoch { return 1 }
            if receiveEpochs.2 == epoch { return 2 }
            return -1
        }
    }
}

extension GesNetworkConnection {
    // The security byte of a hello.
    private static let helloPlain: UInt8 = 0
    private static let helloKeyRequest: UInt8 = 1
    private static let helloSealedByte: UInt8 = 2

    // The bytes a sealed data packet adds: the type and the tag.
    private static let sealedOverhead = 1 + GesNetworkAead.tagBytes
    private static let epochShift: Int64 = 24
    private static let packetNumberLimit: Int64 = 1 << 62

    private static let labelHello = Array("ges hello".utf8)
    private static let labelWelcome = Array("ges welcome".utf8)
    private static let labelClient = Array("ges client".utf8)
    private static let labelServer = Array("ges server".utf8)
    private static let labelIv = Array("ges iv".utf8)
    private static let labelKey = Array("ges key".utf8)

    private var sessionKeys: Keys {
        if let keys { return keys }
        let created = Keys(maxPacketBytes: maxPacketBytes)
        keys = created
        return created
    }

    // ---- Ephemeral keys ----

    private func drawEphemeral(_ keys: Keys) {
        if testKeys {
            withUnsafeTemporaryAllocation(byteCount: 4 + 1 + 8 + 8, alignment: 1) { input in
                input[0] = 0x47
                input[1] = 0x45
                input[2] = 0x53
                input[3] = 0x4B
                input[4] = isClient ? 0 : 1
                for index in 0..<8 {
                    input[5 + index] = UInt8(truncatingIfNeeded: testKeySeed >> UInt64(8 * index))
                    input[13 + index] = UInt8(truncatingIfNeeded: ephemeralCount >> UInt64(8 * index))
                }
                keys.sha.reset()
                keys.sha.update(UnsafeRawBufferPointer(input))
                keys.sha.finish(into: keys.ephemeral)
            }
        } else {
            var generator = SystemRandomNumberGenerator()
            for word in 0..<GesNetworkX25519.keyBytes / 8 {
                let value: UInt64 = generator.next()
                for index in 0..<8 { keys.ephemeral[8 * word + index] = UInt8(truncatingIfNeeded: value >> UInt64(8 * index)) }
            }
        }

        ephemeralCount &+= 1
        GesNetworkX25519.publicKey(UnsafeRawBufferPointer(keys.ephemeral), into: keys.ephemeralPublic)
    }

    // Derives hello_key from es and the server's public key.
    private static func deriveHelloKey(_ keys: Keys, _ serverKey: [UInt8]) {
        withUnsafeTemporaryAllocation(byteCount: GesNetworkHkdf.hashBytes, alignment: 1) { prk in
            serverKey.withUnsafeBytes { GesNetworkHkdf.extract(salt: $0, ikm: UnsafeRawBufferPointer(keys.shared), into: prk) }
            labelHello.withUnsafeBytes { GesNetworkHkdf.expand(prk: UnsafeRawBufferPointer(prk), label: $0, into: keys.helloKey) }
            prk.initializeMemory(as: UInt8.self, repeating: 0)
        }
    }

    // Derives the welcome key and both secrets from the transcript, es (in shared) and ee; false when ee is zero.
    private func deriveSessionKeys(
        _ keys: Keys,
        _ hello: UnsafeRawBufferPointer,
        _ serverEphemeral: UnsafeRawBufferPointer,
        _ peerEphemeral: UnsafeRawBufferPointer
    ) -> Bool {
        let size = GesNetworkX25519.keyBytes
        let valid = withUnsafeTemporaryAllocation(byteCount: 2 * size + 2 * GesNetworkHkdf.hashBytes, alignment: 1) { scratch -> Bool in
            let material = UnsafeMutableRawBufferPointer(rebasing: scratch[0..<2 * size])
            let transcript = UnsafeMutableRawBufferPointer(rebasing: scratch[2 * size..<2 * size + GesNetworkHkdf.hashBytes])
            let prk = UnsafeMutableRawBufferPointer(rebasing: scratch[2 * size + GesNetworkHkdf.hashBytes..<scratch.count])
            material.copyMemory(from: UnsafeRawBufferPointer(keys.shared))
            let valid = GesNetworkX25519.sharedSecret(UnsafeRawBufferPointer(keys.ephemeral), peerEphemeral, into: UnsafeMutableRawBufferPointer(rebasing: material[size..<2 * size]))
            keys.sha.reset()
            keys.sha.update(hello)
            keys.sha.update(serverEphemeral)
            keys.sha.finish(into: transcript)
            GesNetworkHkdf.extract(salt: UnsafeRawBufferPointer(transcript), ikm: UnsafeRawBufferPointer(material), into: prk)
            let secret = UnsafeRawBufferPointer(prk)
            Self.labelWelcome.withUnsafeBytes { GesNetworkHkdf.expand(prk: secret, label: $0, into: keys.welcomeKey) }
            Self.labelClient.withUnsafeBytes { client in
                Self.labelServer.withUnsafeBytes { server in
                    GesNetworkHkdf.expand(prk: secret, label: isClient ? client : server, into: keys.sendSecret)
                    GesNetworkHkdf.expand(prk: secret, label: isClient ? server : client, into: keys.receiveSecret)
                }
            }
            Self.labelIv.withUnsafeBytes { label in
                GesNetworkHkdf.expand(prk: UnsafeRawBufferPointer(keys.sendSecret), label: label, into: keys.sendIv)
                GesNetworkHkdf.expand(prk: UnsafeRawBufferPointer(keys.receiveSecret), label: label, into: keys.receiveIv)
            }
            scratch.initializeMemory(as: UInt8.self, repeating: 0)
            return valid
        }

        // A welcome that failed to authenticate may have derived keys before; none of them stays.
        keys.sendEpoch = -1
        keys.receiveEpochs = (-1, -1, -1)
        Self.updateReceiveKeys(keys, 0)
        keys.ephemeral.initializeMemory(as: UInt8.self, repeating: 0)
        keys.shared.initializeMemory(as: UInt8.self, repeating: 0)
        return valid
    }

    private static func deriveKey(_ keys: Keys, _ secret: UnsafeMutableRawBufferPointer, _ epoch: Int64, _ key: UnsafeMutableRawBufferPointer) {
        for index in 0..<8 { keys.suffix[index] = UInt8(truncatingIfNeeded: UInt64(bitPattern: epoch) >> UInt64(8 * index)) }
        labelKey.withUnsafeBytes { GesNetworkHkdf.expand(prk: UnsafeRawBufferPointer(secret), label: $0, suffix: UnsafeRawBufferPointer(keys.suffix), into: key) }
    }

    // Keeps the receive keys of the epochs next to the given one; derives only those not yet held.
    private static func updateReceiveKeys(_ keys: Keys, _ epoch: Int64) {
        var wanted = max(0, epoch - 1)
        while wanted <= epoch + 1 {
            if keys.receiveSlot(wanted) < 0 {
                var slot = 0
                while keys.receiveEpoch(slot) >= 0 && abs(keys.receiveEpoch(slot) - epoch) <= 1 { slot += 1 }
                deriveKey(keys, keys.receiveSecret, wanted, keys.receiveKey(slot))
                keys.setReceiveEpoch(slot, wanted)
            }
            wanted += 1
        }
    }

    private static func writeNonce(_ keys: Keys, _ iv: UnsafeMutableRawBufferPointer, _ number: Int64) {
        keys.nonce.copyMemory(from: UnsafeRawBufferPointer(iv))
        for index in 0..<8 { keys.nonce[index] ^= UInt8(truncatingIfNeeded: number >> Int64(8 * index)) }
    }

    // ---- Hello ----

    // The security byte of the client's next hello.
    private var helloSecurity: UInt8 { serverKey != nil ? Self.helloSealedByte : minimumSecurity == .none ? Self.helloPlain : Self.helloKeyRequest }

    // Writes the security byte, the ephemeral key and the token from the position on; -1 when X25519 gave zero.
    func writeHelloSecurity(_ packet: UnsafeMutableRawBufferPointer, _ start: Int) -> Int {
        var position = start
        let security = helloSecurity
        packet[position] = security
        position += 1
        helloSealed = security == Self.helloSealedByte
        if security == Self.helloKeyRequest { return Wire.writeVarint(packet, position, 0) }
        if security == Self.helloPlain {
            position = Wire.writeVarint(packet, position, UInt32(token.count))
            for byte in token {
                packet[position] = byte
                position += 1
            }
            return position
        }

        let keys = sessionKeys
        let serverKey = serverKey!
        if ephemeralStale {
            drawEphemeral(keys)
            let valid = serverKey.withUnsafeBytes { GesNetworkX25519.sharedSecret(UnsafeRawBufferPointer(keys.ephemeral), $0, into: keys.shared) }
            if !valid { return -1 }
            Self.deriveHelloKey(keys, serverKey)
            ephemeralStale = false
        }

        UnsafeMutableRawBufferPointer(rebasing: packet[position..<position + GesNetworkX25519.keyBytes]).copyMemory(from: UnsafeRawBufferPointer(keys.ephemeralPublic))
        position = Wire.writeVarint(packet, position + GesNetworkX25519.keyBytes, UInt32(token.count + GesNetworkAead.tagBytes))
        for (index, byte) in token.enumerated() { packet[position + index] = byte }
        keys.aead.setKey(UnsafeRawBufferPointer(keys.helloKey))
        keys.aead.seal(
            nonce: UnsafeRawBufferPointer(keys.zeroNonce),
            aad: UnsafeRawBufferPointer(rebasing: packet[0..<position]),
            buffer: UnsafeMutableRawBufferPointer(rebasing: packet[position..<packet.count]),
            length: token.count
        )
        position += token.count + GesNetworkAead.tagBytes
        UnsafeMutableRawBufferPointer(rebasing: keys.hello[0..<position]).copyMemory(from: UnsafeRawBufferPointer(rebasing: packet[0..<position]))
        keys.helloLength = position
        return position
    }

    // Reads the security byte, the ephemeral key and the token of a hello and checks the end; false when it closed.
    @discardableResult
    func receiveHelloSecurity(_ packet: UnsafeRawBufferPointer, _ start: Int) -> Bool {
        var position = start
        if position >= packet.count { return fail(GesNetworkCodes.malformed) }
        let security = packet[position]
        position += 1
        if security > Self.helloSealedByte { return fail(GesNetworkCodes.malformed) }
        let ephemeral = position
        if security == Self.helloSealedByte {
            if packet.count - position < GesNetworkX25519.keyBytes { return fail(GesNetworkCodes.malformed) }
            position += GesNetworkX25519.keyBytes
        }

        var length: UInt32 = 0
        if Wire.readVarint(packet, &position, &length) != 1 || packet.count - position < Int(length) { return fail(GesNetworkCodes.malformed) }
        let valid: Bool
        switch security {
        case Self.helloPlain: valid = length <= UInt32(Wire.maximumTokenBytes)
        case Self.helloKeyRequest: valid = length == 0
        default: valid = length >= UInt32(GesNetworkAead.tagBytes) && length <= UInt32(Wire.maximumTokenBytes + GesNetworkAead.tagBytes)
        }
        if !valid { return fail(GesNetworkCodes.malformed) }
        let sealedStart = position
        let count = Int(length)
        position += count
        if !readHelloEnd(packet, position) { return false }
        guard let key else {
            if security != Self.helloPlain { return fail(GesNetworkCodes.insecure) }
            return acceptHelloToken(UnsafeRawBufferPointer(rebasing: packet[sealedStart..<sealedStart + count]))
        }

        if security != Self.helloSealedByte {
            retryPending = true
            return true
        }

        let keys = sessionKeys
        let clientEphemeral = UnsafeRawBufferPointer(rebasing: packet[ephemeral..<ephemeral + GesNetworkX25519.keyBytes])
        let sharedValid = key.privateKey.withUnsafeBytes { GesNetworkX25519.sharedSecret($0, clientEphemeral, into: keys.shared) }
        Self.deriveHelloKey(keys, key.publicKey)
        let token = UnsafeMutableRawBufferPointer(rebasing: keys.buffer[0..<count])
        token.copyMemory(from: UnsafeRawBufferPointer(rebasing: packet[sealedStart..<sealedStart + count]))
        keys.aead.setKey(UnsafeRawBufferPointer(keys.helloKey))
        if !keys.aead.open(nonce: UnsafeRawBufferPointer(keys.zeroNonce), aad: UnsafeRawBufferPointer(rebasing: packet[0..<sealedStart]), sealed: token) || !sharedValid {
            return fail(GesNetworkCodes.authenticationFailed)
        }

        drawEphemeral(keys)
        if !deriveSessionKeys(keys, UnsafeRawBufferPointer(rebasing: packet[0..<position]), UnsafeRawBufferPointer(keys.ephemeralPublic), clientEphemeral) {
            return fail(GesNetworkCodes.authenticationFailed)
        }

        encrypted = true
        return acceptHelloToken(UnsafeRawBufferPointer(rebasing: keys.buffer[0..<count - GesNetworkAead.tagBytes]))
    }

    // Checks what follows a hello's token: nothing on a stream or message transport, zero padding to at least 1200
    // bytes on a datagram transport. False when it closed the connection.
    private func readHelloEnd(_ packet: UnsafeRawBufferPointer, _ position: Int) -> Bool {
        if !datagram { return position == packet.count || fail(GesNetworkCodes.malformed) }
        if packet.count < Wire.minimumPacketBytes { return fail(GesNetworkCodes.malformed) }
        for index in position..<packet.count where packet[index] != 0 { return fail(GesNetworkCodes.malformed) }
        return true
    }

    private func acceptHelloToken(_ token: UnsafeRawBufferPointer) -> Bool {
        state = .awaitAccept
        if !addEvent(.connecting, -1, token, .pending, "", "", false) { fail(GesNetworkCodes.queueFull) }
        return true
    }

    // ---- Retry ----

    func writeRetry(_ packet: UnsafeMutableRawBufferPointer) -> Int {
        packet[0] = Wire.retry
        packet[1] = 0
        packet[2] = UInt8(GesNetworkX25519.keyBytes)
        for (index, byte) in key!.publicKey.enumerated() { packet[3 + index] = byte }
        return 3 + GesNetworkX25519.keyBytes
    }

    func receiveRetry(_ packet: UnsafeRawBufferPointer) {
        var position = 1
        var cookieLength: UInt32 = 0
        if Wire.readVarint(packet, &position, &cookieLength) != 1 || cookieLength > UInt32(Wire.maximumCookieBytes) || packet.count - position < Int(cookieLength) {
            fail(GesNetworkCodes.malformed)
            return
        }

        let cookieBytes = UnsafeRawBufferPointer(rebasing: packet[position..<position + Int(cookieLength)])
        position += cookieBytes.count
        var keyLength: UInt32 = 0
        if Wire.readVarint(packet, &position, &keyLength) != 1 || (keyLength != 0 && keyLength != UInt32(GesNetworkX25519.keyBytes)) || packet.count - position != Int(keyLength)
            || (cookieLength == 0 && keyLength == 0)
        {
            fail(GesNetworkCodes.malformed)
            return
        }

        if !datagram {
            // One retry answers a hello that is not sealed, with a key and without a cookie.
            if cookieLength != 0 || keyLength == 0 || retryAccepted || helloSealed {
                fail(GesNetworkCodes.malformed)
                return
            }

            retryAccepted = true
        }

        if cookieLength != 0 && !cookieBytes.elementsEqual(cookie) {
            cookie = Array(cookieBytes)
            ephemeralStale = true
        }

        if keyLength != 0 && serverKey == nil {
            if minimumSecurity == .verified {
                fail(GesNetworkCodes.unverified)
                return
            }

            serverKey = Array(UnsafeRawBufferPointer(rebasing: packet[position..<packet.count]))
            ephemeralStale = true
        }

        helloSent = false
        helloInterval = Self.helloRepeatInterval
    }

    // ---- Welcome ----

    func writeSealedWelcome(_ packet: UnsafeMutableRawBufferPointer) -> Int {
        let keys = keys!
        let fields = 1 + GesNetworkX25519.keyBytes
        packet[0] = Wire.welcome
        UnsafeMutableRawBufferPointer(rebasing: packet[1..<fields]).copyMemory(from: UnsafeRawBufferPointer(keys.ephemeralPublic))
        let length = writeWelcomeFields(packet, fields) - fields
        keys.aead.setKey(UnsafeRawBufferPointer(keys.welcomeKey))
        keys.aead.seal(
            nonce: UnsafeRawBufferPointer(keys.zeroNonce),
            aad: UnsafeRawBufferPointer(rebasing: packet[0..<fields]),
            buffer: UnsafeMutableRawBufferPointer(rebasing: packet[fields..<packet.count]),
            length: length
        )
        sealing = true
        return fields + length + GesNetworkAead.tagBytes
    }

    // Opens a sealed welcome and reads its fields; false when it does not authenticate.
    func receiveSealedWelcome(_ packet: UnsafeRawBufferPointer) -> Bool {
        let fields = 1 + GesNetworkX25519.keyBytes
        guard let keys, packet.count >= fields + GesNetworkAead.tagBytes else { return false }
        // The ephemeral key and es stay until a welcome authenticates; the attempt works on copies.
        return withUnsafeTemporaryAllocation(byteCount: 2 * GesNetworkX25519.keyBytes, alignment: 1) { saved -> Bool in
            let ephemeral = UnsafeMutableRawBufferPointer(rebasing: saved[0..<GesNetworkX25519.keyBytes])
            let shared = UnsafeMutableRawBufferPointer(rebasing: saved[GesNetworkX25519.keyBytes..<saved.count])
            ephemeral.copyMemory(from: UnsafeRawBufferPointer(keys.ephemeral))
            shared.copyMemory(from: UnsafeRawBufferPointer(keys.shared))
            let serverEphemeral = UnsafeRawBufferPointer(rebasing: packet[1..<fields])
            let valid = deriveSessionKeys(keys, UnsafeRawBufferPointer(rebasing: keys.hello[0..<keys.helloLength]), serverEphemeral, serverEphemeral)
            let plain = UnsafeMutableRawBufferPointer(rebasing: keys.buffer[0..<packet.count - fields])
            plain.copyMemory(from: UnsafeRawBufferPointer(rebasing: packet[fields..<packet.count]))
            keys.aead.setKey(UnsafeRawBufferPointer(keys.welcomeKey))
            if !valid || !keys.aead.open(nonce: UnsafeRawBufferPointer(keys.zeroNonce), aad: UnsafeRawBufferPointer(rebasing: packet[0..<fields]), sealed: plain) {
                keys.ephemeral.copyMemory(from: UnsafeRawBufferPointer(ephemeral))
                keys.shared.copyMemory(from: UnsafeRawBufferPointer(shared))
                saved.initializeMemory(as: UInt8.self, repeating: 0)
                return false
            }

            saved.initializeMemory(as: UInt8.self, repeating: 0)
            encrypted = true
            sealing = true
            acceptWelcome(UnsafeRawBufferPointer(rebasing: plain[0..<plain.count - GesNetworkAead.tagBytes]), 0)
            return true
        }
    }

    // ---- Sealed packets ----

    // Writes a data packet sealed: the plain writer fills the bytes from offset 1, which are then rearranged into
    // type 6, the sequence, and the inner packet without its sequence. 0 when nothing is due.
    func writeSealedData(_ packet: UnsafeMutableRawBufferPointer, _ limit: Int) -> Int {
        let number = sentCount
        let inner = UnsafeMutableRawBufferPointer(rebasing: packet[1..<packet.count])
        let length = datagram ? writeDatagramData(inner, limit - Self.sealedOverhead) : writeData(inner, limit - Self.sealedOverhead)
        if length == 0 { return 0 }
        let flags = packet[2]
        packet[0] = Wire.sealed
        packet[1] = packet[3]
        packet[2] = packet[4]
        packet[3] = Wire.data
        packet[4] = flags
        seal(packet, number, length - 2)
        return length + Self.sealedOverhead
    }

    func writeSealedClose(_ packet: UnsafeMutableRawBufferPointer) -> Int {
        let number = sentCount
        sentCount += 1
        let length = writeClose(UnsafeMutableRawBufferPointer(rebasing: packet[3..<packet.count]))
        packet[0] = Wire.sealed
        Wire.writeUInt16(packet, 1, UInt16(truncatingIfNeeded: number))
        seal(packet, number, length)
        return 3 + length + GesNetworkAead.tagBytes
    }

    // Seals the inner packet of the given length behind the 3 header bytes.
    private func seal(_ packet: UnsafeMutableRawBufferPointer, _ number: Int64, _ length: Int) {
        let keys = keys!
        let epoch = number >> Self.epochShift
        if epoch != keys.sendEpoch {
            Self.deriveKey(keys, keys.sendSecret, epoch, keys.sendKey)
            keys.sendEpoch = epoch
        }

        Self.writeNonce(keys, keys.sendIv, number)
        keys.aead.setKey(UnsafeRawBufferPointer(keys.sendKey))
        keys.aead.seal(
            nonce: UnsafeRawBufferPointer(keys.nonce),
            aad: UnsafeRawBufferPointer(rebasing: packet[0..<3]),
            buffer: UnsafeMutableRawBufferPointer(rebasing: packet[3..<packet.count]),
            length: length
        )
    }

    // Opens a sealed packet and handles its inner packet. One that does not authenticate is dropped on a datagram
    // transport and closes the connection on the others.
    func receiveSealed(_ packet: UnsafeRawBufferPointer) {
        let keys = keys!
        let number = !datagram ? keys.largest + 1 : packet.count < 3 ? -1 : Self.packetNumber(keys, Wire.readUInt16(packet, 1))
        let slot = number < 0 ? -1 : keys.receiveSlot(number >> Self.epochShift)
        let length = packet.count - 3 - GesNetworkAead.tagBytes
        var opened = false
        if length > 0 && slot >= 0 {
            Self.writeNonce(keys, keys.receiveIv, number)
            let sealed = UnsafeMutableRawBufferPointer(rebasing: keys.buffer[2..<packet.count - 1])
            sealed.copyMemory(from: UnsafeRawBufferPointer(rebasing: packet[3..<packet.count]))
            keys.aead.setKey(UnsafeRawBufferPointer(keys.receiveKey(slot)))
            opened = keys.aead.open(nonce: UnsafeRawBufferPointer(keys.nonce), aad: UnsafeRawBufferPointer(rebasing: packet[0..<3]), sealed: sealed)
        }

        if !opened {
            if !datagram { fail(GesNetworkCodes.authenticationFailed) }
            return
        }

        lastReceive = now
        if number > keys.largest {
            if number >> Self.epochShift != max(0, keys.largest) >> Self.epochShift { Self.updateReceiveKeys(keys, number >> Self.epochShift) }
            keys.largest = number
        }

        let buffer = keys.buffer
        switch buffer[2] {
        case Wire.close:
            receiveClose(UnsafeRawBufferPointer(rebasing: buffer[2..<2 + length]))
        case Wire.data where length >= 2:
            // Rebuilds the plain data packet in front of the inner one: type, flags and the sequence.
            buffer[0] = Wire.data
            buffer[1] = buffer[3]
            buffer[2] = packet[1]
            buffer[3] = packet[2]
            let data = UnsafeRawBufferPointer(rebasing: buffer[0..<length + 2])
            if datagram { receiveDatagramData(data) } else { receiveData(data) }
        default:
            fail(GesNetworkCodes.malformed)
        }
    }

    // The packet number of a received sealed datagram: the number closest to the expected one with the same low 16
    // bits (RFC 9000 appendix A.3); -1 when it would reach 2^62.
    private static func packetNumber(_ keys: Keys, _ sequence: UInt16) -> Int64 {
        let expected = keys.largest + 1
        let window: Int64 = 1 << 16
        var candidate = (expected & ~(window - 1)) | Int64(sequence)
        if candidate <= expected - window / 2 && candidate < packetNumberLimit - window {
            candidate += window
        } else if candidate > expected + window / 2 && candidate >= window {
            candidate -= window
        }
        return candidate < packetNumberLimit ? candidate : -1
    }
}
