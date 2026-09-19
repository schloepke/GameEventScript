// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable, portable `.gesb` program. Only validated readers and compilers create programs.
public struct GameEventScriptProgram: Sendable {
    public let formatVersion: UInt16
    public let moduleName: String
    public let programVersion: UInt64
    public let requiredRegisterCount: UInt16
    public let requiredCallStackDepth: UInt16
    public let stringConstants: [String]
    public let uint16IndexLists: [[UInt16]]
    public let bindings: [GameEventScriptBinding]
    public let code: [GameEventScriptBytecodeInstruction]
    public let debugSymbols: [GameEventScriptDebugSymbol]?
    public let sourceMap: GameEventScriptSourceMap?
    public let sourceArchive: [GameEventScriptSourceArchiveEntry]?
    public let buildMetadata: GameEventScriptBuildMetadata?
    public let opaqueSections: [GameEventScriptOpaqueSection]
}

/// A logical instruction; its encoding never depends on native memory layout.
public struct GameEventScriptBytecodeInstruction: Sendable, Equatable {
    public let opcode: GameEventScriptBytecodeOpCode
    public let unitAndFlags: UInt8
    public let word0: UInt16
    public let word1: UInt16
    public let word2: UInt16
    public let payload: UInt64

    public init(
        opcode: GameEventScriptBytecodeOpCode, unitAndFlags: UInt8 = 0,
        word0: UInt16 = 0, word1: UInt16 = 0, word2: UInt16 = 0, payload: UInt64 = 0
    ) {
        self.opcode = opcode
        self.unitAndFlags = unitAndFlags
        self.word0 = word0
        self.word1 = word1
        self.word2 = word2
        self.payload = payload
    }
    public var signedWord1: Int16 { Int16(bitPattern: word1) }
    public var signedWord2: Int16 { Int16(bitPattern: word2) }
    public var integer: Int64 { Int64(bitPattern: payload) }
    public var float: Double { Double(bitPattern: payload) }
    public var a: UInt16 { UInt16(truncatingIfNeeded: payload) }
    public var b: UInt16 { UInt16(truncatingIfNeeded: payload >> 16) }
    public var c: UInt16 { UInt16(truncatingIfNeeded: payload >> 32) }
    public var d: UInt16 { UInt16(truncatingIfNeeded: payload >> 48) }
    public var unit: GesUnit {
        switch unitAndFlags & 0x1f {
        case 1: .degree
        case 2: .meter
        case 3: .second
        default: .none
        }
    }
    public var normalizeResultAsPredicate: Bool { unitAndFlags & 0x20 != 0 }
}

/// A declarative binding; callbacks and execution state never belong to a Program.
public struct GameEventScriptBinding: Sendable {
    public let kind: GameEventScriptBinaryBindKind
    public let id: UInt16
    public let name: UInt16
    public let argumentNames: [UInt16]
    public let requiredTags: [UInt16]
    public let excludedTags: [UInt16]
    public let entryAddress: UInt16
    public let requiredRegisterCount: UInt16
    public let requiredCallStackDepth: UInt16

    public init(
        kind: GameEventScriptBinaryBindKind, name: UInt16, argumentNames: [UInt16] = [],
        entryAddress: UInt16 = .max, id: UInt16 = .max, requiredTags: [UInt16] = [],
        excludedTags: [UInt16] = [], requiredRegisterCount: UInt16 = 0, requiredCallStackDepth: UInt16 = 0
    ) {
        self.kind = kind
        self.id = id
        self.name = name
        self.argumentNames = argumentNames
        self.requiredTags = requiredTags
        self.excludedTags = excludedTags
        self.entryAddress = entryAddress
        self.requiredRegisterCount = requiredRegisterCount
        self.requiredCallStackDepth = requiredCallStackDepth
    }
    var isExecutable: Bool { [.messageHandler, .messageNameHandler, .function, .predicate, .record].contains(kind) }
    var isHandler: Bool { kind == .messageHandler || kind == .messageNameHandler }
}

public enum GameEventScriptDebugSymbolKind: UInt8, Sendable {
    case parameter = 1
    case local = 2
}

public struct GameEventScriptDebugSymbol: Sendable {
    public let kind: GameEventScriptDebugSymbolKind
    public let registerID: UInt16
    public let name: String
    public let codeStart: UInt32
    public let codeLength: UInt32
    public init(
        kind: GameEventScriptDebugSymbolKind, registerID: UInt16, name: String, codeStart: UInt32, codeLength: UInt32
    ) {
        self.kind = kind
        self.registerID = registerID
        self.name = name
        self.codeStart = codeStart
        self.codeLength = codeLength
    }
}

public struct GameEventScriptSourceMapSource: Sendable {
    public let sourceID: UInt32
    public let sourceName: String
    public let sourceByteLength: UInt32
    public let sha256: [UInt8]
    public let lineStartByteOffsets: [UInt32]
    public init(
        sourceID: UInt32, sourceName: String, sourceByteLength: UInt32, sha256: [UInt8], lineStartByteOffsets: [UInt32]
    ) {
        self.sourceID = sourceID
        self.sourceName = sourceName
        self.sourceByteLength = sourceByteLength
        self.sha256 = sha256
        self.lineStartByteOffsets = lineStartByteOffsets
    }
}

public struct GameEventScriptSourceMapEntry: Sendable {
    public let codeStart: UInt32
    public let codeLength: UInt32
    public let sourceID: UInt32
    public let sourceStartByteOffset: UInt32
    public let sourceByteLength: UInt32
    public init(
        codeStart: UInt32, codeLength: UInt32, sourceID: UInt32, sourceStartByteOffset: UInt32, sourceByteLength: UInt32
    ) {
        self.codeStart = codeStart
        self.codeLength = codeLength
        self.sourceID = sourceID
        self.sourceStartByteOffset = sourceStartByteOffset
        self.sourceByteLength = sourceByteLength
    }
}

public struct GameEventScriptSourceMap: Sendable {
    public let sources: [GameEventScriptSourceMapSource]
    public let entries: [GameEventScriptSourceMapEntry]
    public init(sources: [GameEventScriptSourceMapSource], entries: [GameEventScriptSourceMapEntry]) {
        self.sources = sources
        self.entries = entries
    }
}

public struct GameEventScriptSourceArchiveEntry: Sendable {
    public let sourceID: UInt32
    public let sourceName: String
    public let utf8Content: [UInt8]
    public init(sourceID: UInt32, sourceName: String, utf8Content: [UInt8]) {
        self.sourceID = sourceID
        self.sourceName = sourceName
        self.utf8Content = utf8Content
    }
    public var text: String { String(decoding: utf8Content, as: UTF8.self) }
}

public struct GameEventScriptBuildMetadata: Sendable {
    public let compilerID: String
    public let compilerVersion: String
    public init(compilerID: String, compilerVersion: String) {
        self.compilerID = compilerID
        self.compilerVersion = compilerVersion
    }
}

public struct GameEventScriptOpaqueSection: Sendable {
    public let sectionType: UInt16
    public let flags: UInt16
    public let sectionVersion: UInt16
    public let rawPayload: [UInt8]
    public let originalOrdinal: Int
    public init(sectionType: UInt16, flags: UInt16, sectionVersion: UInt16, rawPayload: [UInt8], originalOrdinal: Int) {
        self.sectionType = sectionType
        self.flags = flags
        self.sectionVersion = sectionVersion
        self.rawPayload = rawPayload
        self.originalOrdinal = originalOrdinal
    }
}
