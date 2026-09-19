// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Bounded, fileless decoding of a complete `.gesb` V1 image.
public enum GameEventScriptProgramReader {
    public static func read(_ bytes: [UInt8], options: GameEventScriptProgramReadOptions = .init()) throws
        -> GameEventScriptProgram
    {
        let limits = options.limits
        guard limits.maxFileBytes >= 16, limits.maxSectionCount >= 1, limits.maxSourceArchiveBytes >= 0,
            limits.maxRetainedOpaqueBytes >= 0,
            [limits.maxInstructions, limits.maxBindings, limits.maxStringEntries, limits.maxIndexLists].allSatisfy({
                (0...65535).contains($0)
            })
        else {
            throw GameEventScriptProgramFormatError(.readerLimitExceeded, message: "Invalid reader limits")
        }
        if bytes.count > limits.maxFileBytes { throw GameEventScriptProgramFormatError(.fileTooLarge) }
        if bytes.count < 16 { throw GameEventScriptProgramFormatError(.invalidHeaderSize) }
        var header = GesBinaryCursor(bytes: bytes[...])
        guard try header.bytes(4) == Array("GESB".utf8) else { throw GameEventScriptProgramFormatError(.invalidMagic) }
        guard try header.u16() == 1 else {
            throw GameEventScriptProgramFormatError(.unsupportedFormatVersion, byteOffset: 4)
        }
        guard try header.u16() == 0 else { throw GameEventScriptProgramFormatError(.invalidHeaderFlags, byteOffset: 6) }
        guard try header.u32() == 16 else { throw GameEventScriptProgramFormatError(.invalidHeaderSize, byteOffset: 8) }
        guard try header.u32() == bytes.count else {
            throw GameEventScriptProgramFormatError(.fileSizeMismatch, byteOffset: 12)
        }
        let required: Set<UInt16> = [1, 2, 3, 4, 16]
        let known = required.union([32, 33, 34, 48])
        var sections: [UInt16: GesBinaryCursor] = [:]
        var seen: Set<UInt16> = []
        var opaque: [GameEventScriptOpaqueSection] = []
        var opaqueBytes = 0
        var offset = 16
        var ordinal = 0
        while offset < bytes.count {
            if ordinal >= limits.maxSectionCount {
                throw GameEventScriptProgramFormatError(.tooManySections, byteOffset: offset)
            }
            if bytes.count - offset < 12 {
                throw GameEventScriptProgramFormatError(.truncatedSectionHeader, byteOffset: offset)
            }
            var cursor = GesBinaryCursor(bytes: bytes[offset...])
            let type = try cursor.u16()
            let flags = try cursor.u16()
            let version = try cursor.u16()
            let reserved = try cursor.u16()
            let length = Int(try cursor.u32())
            func fail(_ code: GameEventScriptProgramFormatErrorCode, _ delta: Int = 0)
                -> GameEventScriptProgramFormatError
            {
                .init(code, byteOffset: offset + delta, sectionType: type)
            }
            if type == 0 || type == .max { throw fail(.invalidSectionType) }
            if flags & ~0x00f1 != 0 { throw fail(.invalidSectionFlags, 2) }
            if reserved != 0 { throw fail(.invalidSectionReserved, 6) }
            if length > Int(Int32.max) || length > bytes.count - offset - 12 { throw fail(.truncatedSectionPayload, 8) }
            if known.contains(type), !seen.insert(type).inserted { throw fail(.duplicateSection) }
            if required.contains(type), flags & 1 == 0 { throw fail(.invalidSectionFlags, 2) }
            if !required.contains(type), flags & 1 != 0 {
                throw fail(.unknownRequiredSection, known.contains(type) ? 2 : 0)
            }
            if required.contains(type), version != 1 { throw fail(.unsupportedSectionVersion, 4) }
            if required.contains(type), flags & 0xf0 != 0 { throw fail(.unsupportedCompression, 2) }
            let payload = bytes[(offset + 12)..<(offset + 12 + length)]
            if known.contains(type), version == 1, flags & 0xf0 == 0 {
                sections[type] = GesBinaryCursor(bytes: payload, section: type)
            } else if options.retention == .preserveAll {
                opaqueBytes += length
                if opaqueBytes > limits.maxRetainedOpaqueBytes { throw fail(.readerLimitExceeded) }
                opaque.append(
                    .init(
                        sectionType: type, flags: flags, sectionVersion: version, rawPayload: Array(payload),
                        originalOrdinal: ordinal))
            }
            offset += 12 + length
            ordinal += 1
        }
        func section(_ type: UInt16) throws -> GesBinaryCursor {
            guard let result = sections[type] else {
                throw GameEventScriptProgramFormatError(.missingRequiredSection, sectionType: type)
            }
            return result
        }
        var stringsReader = try section(2)
        var strings: [String] = []
        for index in 0..<(try stringsReader.count(limits.maxStringEntries)) {
            strings.append(try stringsReader.string(index))
        }
        try stringsReader.end()
        var listsReader = try section(3)
        var lists: [[UInt16]] = []
        for index in 0..<(try listsReader.count(limits.maxIndexLists)) {
            let length = try listsReader.length()
            try listsReader.requireElements(length, width: 2, entry: index)
            var list: [UInt16] = []
            list.reserveCapacity(length)
            for _ in 0..<length { list.append(try listsReader.u16()) }
            lists.append(list)
        }
        try listsReader.end()
        var metadata = try section(1)
        if metadata.remaining != 16 { throw metadata.error(.invalidPayloadLength) }
        let module = Int(try metadata.u16())
        let registers = try metadata.u16()
        let depth = try metadata.u16()
        if try metadata.u16() != 0 { throw metadata.error(.invalidSectionReserved, offset: metadata.offset - 2) }
        let version = try metadata.u64()
        if module >= strings.count { throw metadata.error(.invalidStringIndex, offset: metadata.bytes.startIndex) }
        var bindingsReader = try section(4)
        let bindCount = try bindingsReader.count(limits.maxBindings)
        if bindingsReader.remaining != bindCount * 20 { throw bindingsReader.error(.invalidPayloadLength) }
        var bindings: [GameEventScriptBinding] = []
        func list(_ index: UInt16, entry: Int) throws -> [UInt16] {
            if index == .max { return [] }
            guard Int(index) < lists.count else {
                throw bindingsReader.error(.invalidListIndex, offset: bindingsReader.bytes.startIndex, entry: entry)
            }
            return lists[Int(index)]
        }
        for index in 0..<bindCount {
            let rawKind = try bindingsReader.u8()
            if try bindingsReader.u8() != 0 {
                throw bindingsReader.error(.invalidSectionFlags, offset: bindingsReader.offset - 1, entry: index)
            }
            let id = try bindingsReader.u16()
            let name = try bindingsReader.u16()
            let argsIndex = try bindingsReader.u16()
            let requiredIndex = try bindingsReader.u16()
            let excludedIndex = try bindingsReader.u16()
            let args = try list(argsIndex, entry: index)
            let tags = try list(requiredIndex, entry: index)
            let excluded = try list(excludedIndex, entry: index)
            let address = try bindingsReader.u16()
            let registers = try bindingsReader.u16()
            let depth = try bindingsReader.u16()
            if try bindingsReader.u16() != 0 {
                throw bindingsReader.error(.invalidSectionReserved, offset: bindingsReader.offset - 2, entry: index)
            }
            guard let kind = GameEventScriptBinaryBindKind(rawValue: rawKind) else {
                throw bindingsReader.error(.invalidBindingKind, entry: index)
            }
            bindings.append(
                .init(
                    kind: kind, name: name, argumentNames: args, entryAddress: address, id: id,
                    requiredTags: tags, excludedTags: excluded, requiredRegisterCount: registers,
                    requiredCallStackDepth: depth))
        }
        var codeReader = try section(16)
        let instructionCount = try codeReader.count(limits.maxInstructions)
        if codeReader.remaining != instructionCount * 16 { throw codeReader.error(.invalidPayloadLength) }
        var code: [GameEventScriptBytecodeInstruction] = []
        for index in 0..<instructionCount {
            let raw = try codeReader.u8()
            guard let opcode = GameEventScriptBytecodeOpCode(rawValue: raw) else {
                throw codeReader.error(.invalidOpcode, entry: index)
            }
            code.append(
                try .init(
                    opcode: opcode, unitAndFlags: codeReader.u8(), word0: codeReader.u16(), word1: codeReader.u16(),
                    word2: codeReader.u16(), payload: codeReader.u64()))
        }
        var debug: [GameEventScriptDebugSymbol]?
        var sourceMap: GameEventScriptSourceMap?
        var archive: [GameEventScriptSourceArchiveEntry]?
        var build: GameEventScriptBuildMetadata?
        if options.retention != .runtimeOnly {
            if var reader = sections[32] {
                var names: [String] = []
                for index in 0..<(try reader.count(65535)) { names.append(try reader.string(index)) }
                let count = try reader.count(65535)
                if reader.remaining != count * 16 { throw reader.error(.invalidPayloadLength) }
                var symbols: [GameEventScriptDebugSymbol] = []
                for index in 0..<count {
                    let raw = try reader.u8()
                    if try reader.u8() != 0 {
                        throw reader.error(.invalidSectionReserved, offset: reader.offset - 1, entry: index)
                    }
                    let register = try reader.u16()
                    let name = Int(try reader.u32())
                    let start = try reader.u32()
                    let length = try reader.u32()
                    if name >= names.count {
                        throw reader.error(.invalidStringIndex, offset: reader.offset - 12, entry: index)
                    }
                    guard let kind = GameEventScriptDebugSymbolKind(rawValue: raw) else {
                        throw reader.error(.invalidDebugSymbol, entry: index)
                    }
                    symbols.append(
                        .init(kind: kind, registerID: register, name: names[name], codeStart: start, codeLength: length)
                    )
                }
                try reader.end()
                debug = symbols
            }
            if var reader = sections[33] {
                var sources: [GameEventScriptSourceMapSource] = []
                for index in 0..<(try reader.count(65535)) {
                    let name = try reader.string(index)
                    let length = try reader.u32()
                    let hash = try reader.bytes(32, entry: index)
                    let count = try reader.count(Int(Int32.max))
                    try reader.requireElements(count, width: 4, entry: index)
                    var lines: [UInt32] = []
                    for _ in 0..<count { lines.append(try reader.u32()) }
                    sources.append(
                        .init(
                            sourceID: UInt32(index), sourceName: name, sourceByteLength: length, sha256: hash,
                            lineStartByteOffsets: lines))
                }
                let count = try reader.count(Int(Int32.max))
                if count > reader.remaining / 20 || reader.remaining != count * 20 {
                    throw reader.error(.invalidPayloadLength)
                }
                var entries: [GameEventScriptSourceMapEntry] = []
                for _ in 0..<count {
                    entries.append(
                        try .init(
                            codeStart: reader.u32(), codeLength: reader.u32(), sourceID: reader.u32(),
                            sourceStartByteOffset: reader.u32(), sourceByteLength: reader.u32()))
                }
                try reader.end()
                sourceMap = .init(sources: sources, entries: entries)
            }
            if var reader = sections[34] {
                var sources: [GameEventScriptSourceArchiveEntry] = []
                var total = 0
                for index in 0..<(try reader.count(65535)) {
                    let id = try reader.u32()
                    let name = try reader.string(index)
                    let length = try reader.length()
                    total += length
                    if total > limits.maxSourceArchiveBytes { throw reader.error(.readerLimitExceeded, entry: index) }
                    let content = try reader.bytes(length, entry: index)
                    guard GesUtf8.decode(content) != nil else {
                        throw reader.error(.invalidUtf8, offset: reader.offset - length, entry: index)
                    }
                    sources.append(.init(sourceID: id, sourceName: name, utf8Content: content))
                }
                try reader.end()
                archive = sources
            }
            if var reader = sections[48] {
                build = try .init(compilerID: reader.string(0), compilerVersion: reader.string(1))
                try reader.end()
            }
        }
        let program = GameEventScriptProgram(
            formatVersion: 1, moduleName: strings[module], programVersion: version,
            requiredRegisterCount: registers, requiredCallStackDepth: depth, stringConstants: strings,
            uint16IndexLists: lists,
            bindings: bindings, code: code, debugSymbols: debug, sourceMap: sourceMap, sourceArchive: archive,
            buildMetadata: build, opaqueSections: opaque)
        try GameEventScriptProgramValidator.validate(program)
        return program
    }
}

struct GesBinaryCursor {
    let bytes: ArraySlice<UInt8>
    let section: UInt16?
    var offset: Int
    init(bytes: ArraySlice<UInt8>, section: UInt16? = nil) {
        self.bytes = bytes
        self.section = section
        offset = bytes.startIndex
    }
    var remaining: Int { bytes.endIndex - offset }
    func error(_ code: GameEventScriptProgramFormatErrorCode, offset: Int? = nil, entry: Int? = nil)
        -> GameEventScriptProgramFormatError
    {
        .init(code, byteOffset: offset ?? self.offset, sectionType: section, entryIndex: entry)
    }
    func requireElements(_ count: Int, width: Int = 1, entry: Int? = nil) throws {
        if count < 0 || count > remaining / width { throw error(.truncatedSectionPayload, entry: entry) }
    }
    mutating func u8() throws -> UInt8 {
        try requireElements(1)
        defer { offset += 1 }
        return bytes[offset]
    }
    mutating func u16() throws -> UInt16 {
        let low = try u8()
        return try UInt16(low) | (UInt16(u8()) << 8)
    }
    mutating func u32() throws -> UInt32 {
        let low = try u16()
        return try UInt32(low) | (UInt32(u16()) << 16)
    }
    mutating func u64() throws -> UInt64 {
        let low = try u32()
        return try UInt64(low) | (UInt64(u32()) << 32)
    }
    mutating func count(_ maximum: Int) throws -> Int {
        let value = Int(try u32())
        if value > maximum { throw error(.tooManyEntries, offset: offset - 4) }
        return value
    }
    mutating func length() throws -> Int {
        let value = Int(try u32())
        if value > Int(Int32.max) { throw error(.sectionTooLarge, offset: offset - 4) }
        return value
    }
    mutating func bytes(_ count: Int, entry: Int? = nil) throws -> [UInt8] {
        try requireElements(count, entry: entry)
        defer { offset += count }
        return Array(bytes[offset..<(offset + count)])
    }
    mutating func string(_ entry: Int? = nil) throws -> String {
        let length = try length()
        let raw = try bytes(length, entry: entry)
        guard let text = GesUtf8.decode(raw) else { throw error(.invalidUtf8, offset: offset - length, entry: entry) }
        return text
    }
    func end() throws { if remaining != 0 { throw error(.invalidPayloadLength) } }
}
