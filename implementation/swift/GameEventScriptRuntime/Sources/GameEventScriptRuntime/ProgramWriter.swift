// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Canonical little-endian `.gesb` encoding, independent of host state and memory layout.
public enum GameEventScriptProgramWriter {
    public static func encodedSize(_ program: GameEventScriptProgram) throws -> Int {
        try GameEventScriptProgramValidator.validate(program)
        var output = GesBinaryOutput(countOnly: true)
        try encode(program, &output, fileSize: 0)
        return output.count
    }
    public static func bytes(_ program: GameEventScriptProgram) throws -> [UInt8] {
        let size = try encodedSize(program)
        var output = GesBinaryOutput(capacity: size)
        try encode(program, &output, fileSize: size)
        return output.storage
    }
    /// Writes into existing storage, preserving it on validation or capacity failure.
    @discardableResult
    public static func write(_ program: GameEventScriptProgram, into destination: inout [UInt8]) throws -> Int {
        let size = try encodedSize(program)
        if destination.count < size {
            throw GameEventScriptProgramFormatError(.invalidPayloadLength, message: "Destination is too small")
        }
        let encoded = try bytes(program)
        destination.replaceSubrange(0..<size, with: encoded)
        return size
    }

    private static func encode(_ p: GameEventScriptProgram, _ out: inout GesBinaryOutput, fileSize: Int) throws {
        try out.raw(Array("GESB".utf8))
        try out.u16(1)
        try out.u16(0)
        try out.u32(16)
        try out.u32(UInt32(fileSize))
        guard let module = p.stringConstants.firstIndex(where: { GesText.scalarEqual($0, p.moduleName) }) else {
            throw GameEventScriptProgramValidator.failure(.invalidStringIndex)
        }
        try out.section(1, flags: 1, size: 16)
        try out.u16(UInt16(module))
        try out.u16(p.requiredRegisterCount)
        try out.u16(p.requiredCallStackDepth)
        try out.u16(0)
        try out.u64(p.programVersion)
        let stringsSize = 4 + p.stringConstants.reduce(0) { $0 + 4 + $1.utf8.count }
        try out.section(2, flags: 1, size: stringsSize)
        try out.length(p.stringConstants.count)
        for text in p.stringConstants { try out.string(text) }
        let listsSize = 4 + p.uint16IndexLists.reduce(0) { $0 + 4 + 2 * $1.count }
        try out.section(3, flags: 1, size: listsSize)
        try out.length(p.uint16IndexLists.count)
        for list in p.uint16IndexLists {
            try out.length(list.count)
            for value in list { try out.u16(value) }
        }
        func findList(_ values: [UInt16]) throws -> UInt16 {
            if let index = p.uint16IndexLists.firstIndex(of: values) { return UInt16(index) }
            if values.isEmpty { return .max }
            throw GameEventScriptProgramValidator.failure(.invalidListIndex)
        }
        try out.section(4, flags: 1, size: 4 + 20 * p.bindings.count)
        try out.length(p.bindings.count)
        for binding in p.bindings {
            try out.u8(binding.kind.rawValue)
            try out.u8(0)
            try out.u16(binding.id)
            try out.u16(binding.name)
            try out.u16(findList(binding.argumentNames))
            try out.u16(findList(binding.requiredTags))
            try out.u16(findList(binding.excludedTags))
            try out.u16(binding.entryAddress)
            try out.u16(binding.requiredRegisterCount)
            try out.u16(binding.requiredCallStackDepth)
            try out.u16(0)
        }
        try out.section(16, flags: 1, size: 4 + 16 * p.code.count)
        try out.length(p.code.count)
        for instruction in p.code {
            try out.u8(instruction.opcode.rawValue)
            try out.u8(instruction.unitAndFlags)
            try out.u16(instruction.word0)
            try out.u16(instruction.word1)
            try out.u16(instruction.word2)
            try out.u64(instruction.payload)
        }
        if let symbols = p.debugSymbols {
            var names: [String] = []
            for symbol in symbols where !names.contains(where: { GesText.scalarEqual($0, symbol.name) }) {
                names.append(symbol.name)
            }
            let size = 8 + 16 * symbols.count + names.reduce(0) { $0 + 4 + $1.utf8.count }
            try out.section(32, size: size)
            try out.length(names.count)
            for name in names { try out.string(name) }
            try out.length(symbols.count)
            for symbol in symbols {
                try out.u8(symbol.kind.rawValue)
                try out.u8(0)
                try out.u16(symbol.registerID)
                try out.length(names.firstIndex(where: { GesText.scalarEqual($0, symbol.name) })!)
                try out.u32(symbol.codeStart)
                try out.u32(symbol.codeLength)
            }
        }
        if let map = p.sourceMap {
            let size =
                8 + 20 * map.entries.count
                + map.sources.reduce(0) { $0 + 44 + $1.sourceName.utf8.count + 4 * $1.lineStartByteOffsets.count }
            try out.section(33, size: size)
            try out.length(map.sources.count)
            for source in map.sources {
                try out.string(source.sourceName)
                try out.u32(source.sourceByteLength)
                try out.raw(source.sha256)
                try out.length(source.lineStartByteOffsets.count)
                for line in source.lineStartByteOffsets { try out.u32(line) }
            }
            try out.length(map.entries.count)
            for entry in map.entries {
                try out.u32(entry.codeStart)
                try out.u32(entry.codeLength)
                try out.u32(entry.sourceID)
                try out.u32(entry.sourceStartByteOffset)
                try out.u32(entry.sourceByteLength)
            }
        }
        if let archive = p.sourceArchive {
            let size = 4 + archive.reduce(0) { $0 + 12 + $1.sourceName.utf8.count + $1.utf8Content.count }
            try out.section(34, size: size)
            try out.length(archive.count)
            for source in archive {
                try out.u32(source.sourceID)
                try out.string(source.sourceName)
                try out.length(source.utf8Content.count)
                try out.raw(source.utf8Content)
            }
        }
        if let build = p.buildMetadata {
            try out.section(48, size: 8 + build.compilerID.utf8.count + build.compilerVersion.utf8.count)
            try out.string(build.compilerID)
            try out.string(build.compilerVersion)
        }
        for section in p.opaqueSections.sorted(by: { $0.originalOrdinal < $1.originalOrdinal }) {
            try out.section(
                section.sectionType, flags: section.flags, size: section.rawPayload.count,
                version: section.sectionVersion)
            try out.raw(section.rawPayload)
        }
    }
}

private struct GesBinaryOutput {
    var storage: [UInt8] = []
    var count = 0
    let countOnly: Bool
    init(countOnly: Bool = false, capacity: Int = 0) {
        self.countOnly = countOnly
        if !countOnly { storage.reserveCapacity(capacity) }
    }
    mutating func u8(_ value: UInt8) throws {
        if count == Int(Int32.max) { throw GameEventScriptProgramValidator.failure(.sectionTooLarge) }
        count += 1
        if !countOnly { storage.append(value) }
    }
    mutating func u16(_ value: UInt16) throws {
        try u8(UInt8(truncatingIfNeeded: value))
        try u8(UInt8(truncatingIfNeeded: value >> 8))
    }
    mutating func u32(_ value: UInt32) throws {
        try u16(UInt16(truncatingIfNeeded: value))
        try u16(UInt16(truncatingIfNeeded: value >> 16))
    }
    mutating func u64(_ value: UInt64) throws {
        try u32(UInt32(truncatingIfNeeded: value))
        try u32(UInt32(truncatingIfNeeded: value >> 32))
    }
    mutating func length(_ value: Int) throws {
        if value < 0 || value > Int(Int32.max) { throw GameEventScriptProgramValidator.failure(.sectionTooLarge) }
        try u32(UInt32(value))
    }
    mutating func raw(_ bytes: [UInt8]) throws {
        if bytes.count > Int(Int32.max) - count { throw GameEventScriptProgramValidator.failure(.sectionTooLarge) }
        count += bytes.count
        if !countOnly { storage.append(contentsOf: bytes) }
    }
    mutating func string(_ text: String) throws {
        try length(text.utf8.count)
        try raw(Array(text.utf8))
    }
    mutating func section(_ type: UInt16, flags: UInt16 = 0, size: Int, version: UInt16 = 1) throws {
        try u16(type)
        try u16(flags)
        try u16(version)
        try u16(0)
        try length(size)
    }
}
