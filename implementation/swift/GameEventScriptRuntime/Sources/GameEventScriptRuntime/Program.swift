// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable, portable `.gesb` program. Only validated readers and compilers create programs.
public struct GameEventScriptProgram: Sendable {
    /// Portable binary format version; independent of the application program version.
    public let formatVersion: UInt16
    /// Declared module identifier.
    public let moduleName: String
    /// Application-provided version carried by the Program metadata.
    public let programVersion: UInt64
    /// Maximum simultaneous register requirement computed from executable bindings.
    public let requiredRegisterCount: UInt16
    /// Maximum acyclic nested-call depth required by this Program.
    public let requiredCallStackDepth: UInt16
    /// Decoded UTF-8 constants addressed by UInt16 indexes.
    public let stringConstants: [String]
    /// Shared lists of indexes used by bindings and instructions.
    public let uint16IndexLists: [[UInt16]]
    /// Declarative callable and import entries; contains no host callbacks.
    public let bindings: [GameEventScriptBinding]
    /// Logical bytecode instructions in address order.
    public let code: [GameEventScriptBytecodeInstruction]
    /// Optional register names and lifetimes; nil when absent or not retained.
    public let debugSymbols: [GameEventScriptDebugSymbol]?
    /// Optional bytecode-to-source mapping.
    public let sourceMap: GameEventScriptSourceMap?
    /// Optional original UTF-8 source documents.
    public let sourceArchive: [GameEventScriptSourceArchiveEntry]?
    /// Optional compiler identity and version.
    public let buildMetadata: GameEventScriptBuildMetadata?
    /// Preserved opaque sections retained by the selected reader policy.
    public let opaqueSections: [GameEventScriptOpaqueSection]
}

/// A logical instruction; its encoding never depends on native memory layout.
public struct GameEventScriptBytecodeInstruction: Sendable, Equatable {
    /// Operation identifier determining operand interpretation.
    public let opcode: GameEventScriptBytecodeOpCode
    /// Low five bits encode the unit; upper bits encode instruction flags.
    public let unitAndFlags: UInt8
    /// First UInt16 operand word, commonly the destination register.
    public let word0: UInt16
    /// Second UInt16 operand word; interpretation depends on the opcode.
    public let word1: UInt16
    /// Third UInt16 operand word; interpretation depends on the opcode.
    public let word2: UInt16
    /// Raw 64-bit immediate payload or four packed UInt16 operands.
    public let payload: UInt64

    /// Creates logical instruction data without validation; the Program validator checks operands and references.
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
    /// Second operand word reinterpreted as signed Int16.
    public var signedWord1: Int16 { Int16(bitPattern: word1) }
    /// Third operand word reinterpreted as signed Int16.
    public var signedWord2: Int16 { Int16(bitPattern: word2) }
    /// Payload bits reinterpreted as signed Int64.
    public var integer: Int64 { Int64(bitPattern: payload) }
    /// Payload bits reinterpreted as IEEE 754 binary64.
    public var float: Double { Double(bitPattern: payload) }
    /// Payload bits 0...15 interpreted as UInt16.
    public var a: UInt16 { UInt16(truncatingIfNeeded: payload) }
    /// Payload bits 16...31 interpreted as UInt16.
    public var b: UInt16 { UInt16(truncatingIfNeeded: payload >> 16) }
    /// Payload bits 32...47 interpreted as UInt16.
    public var c: UInt16 { UInt16(truncatingIfNeeded: payload >> 32) }
    /// Payload bits 48...63 interpreted as UInt16.
    public var d: UInt16 { UInt16(truncatingIfNeeded: payload >> 48) }
    /// Decoded degree, meter or second unit; other codes yield none. Validation rejects invalid encodings.
    public var unit: GesUnit {
        switch unitAndFlags & 0x1f {
        case 1: .degree
        case 2: .meter
        case 3: .second
        default: .none
        }
    }
    /// Whether the instruction requests Boolean predicate normalization (flag 0x20).
    public var normalizeResultAsPredicate: Bool { unitAndFlags & 0x20 != 0 }
}

/// A declarative binding; callbacks and execution state never belong to a Program.
public struct GameEventScriptBinding: Sendable {
    /// Binding role: executable declaration, message signature or host import.
    public let kind: GameEventScriptBinaryBindKind
    /// Binding identifier within its kind.
    public let id: UInt16
    /// Index of the binding name in the string table.
    public let name: UInt16
    /// Ordered string indexes for parameter labels.
    public let argumentNames: [UInt16]
    /// String indexes of tags required by this handler.
    public let requiredTags: [UInt16]
    /// String indexes of tags that exclude this handler.
    public let excludedTags: [UInt16]
    /// Executable entry instruction, or UInt16.max for a non-executable binding.
    public let entryAddress: UInt16
    /// Maximum active registers required by this callable, including arguments.
    public let requiredRegisterCount: UInt16
    /// Maximum nested-call depth reachable from this callable.
    public let requiredCallStackDepth: UInt16

    /// Creates binding data. Names and tag values are string-table indexes; omitted IDs and entry addresses use
    /// UInt16.max. The Program validator enforces consistency.
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

/// Role of a named register in an executable binding.
public enum GameEventScriptDebugSymbolKind: UInt8, Sendable {
    /// A preloaded callable argument.
    case parameter = 1
    /// A local lexical binding.
    case local = 2
}

/// A register name and the instruction interval over which it is valid.
public struct GameEventScriptDebugSymbol: Sendable {
    /// Whether the register names a parameter or a local.
    public let kind: GameEventScriptDebugSymbolKind
    /// Zero-based register index in the active frame.
    public let registerID: UInt16
    /// Source-level binding name.
    public let name: String
    /// First instruction address in the symbol lifetime.
    public let codeStart: UInt32
    /// Number of instructions covered by the symbol lifetime.
    public let codeLength: UInt32
    /// Creates debug-symbol data with a register and half-open instruction lifetime.
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

/// Source-document identity, checksum and line boundaries used by debug mappings.
public struct GameEventScriptSourceMapSource: Sendable {
    /// Compiler-assigned document identifier.
    public let sourceID: UInt32
    /// Display name of the original source input.
    public let sourceName: String
    /// Length of the complete source document in UTF-8 bytes.
    public let sourceByteLength: UInt32
    /// SHA-256 bytes of the original UTF-8 source.
    public let sha256: [UInt8]
    /// Zero-based UTF-8 byte offset of each line start.
    public let lineStartByteOffsets: [UInt32]
    /// Creates source identity metadata; the Program validator checks checksum size and line boundaries.
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

/// Mapping from a half-open instruction interval to a UTF-8 source span.
public struct GameEventScriptSourceMapEntry: Sendable {
    /// First instruction address covered by this mapping.
    public let codeStart: UInt32
    /// Number of covered instructions.
    public let codeLength: UInt32
    /// Identifier of the source document.
    public let sourceID: UInt32
    /// Zero-based UTF-8 byte offset of the source span.
    public let sourceStartByteOffset: UInt32
    /// Length of the source span in UTF-8 bytes.
    public let sourceByteLength: UInt32
    /// Creates instruction-to-source mapping data; the Program validator checks all bounds.
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

/// Source identities and mappings retained in the optional SourceMap section.
public struct GameEventScriptSourceMap: Sendable {
    /// Source-document identities in stored order.
    public let sources: [GameEventScriptSourceMapSource]
    /// Instruction-to-source mappings in stored order.
    public let entries: [GameEventScriptSourceMapEntry]
    /// Creates an immutable source map from document identities and mappings.
    public init(sources: [GameEventScriptSourceMapSource], entries: [GameEventScriptSourceMapEntry]) {
        self.sources = sources
        self.entries = entries
    }
}

/// Original UTF-8 contents of one embedded compiler input.
public struct GameEventScriptSourceArchiveEntry: Sendable {
    /// Compiler-assigned source identifier.
    public let sourceID: UInt32
    /// Display name of the source input.
    public let sourceName: String
    /// Original UTF-8 document bytes.
    public let utf8Content: [UInt8]
    /// Creates archive data; the Program validator verifies UTF-8 and corresponding source metadata.
    public init(sourceID: UInt32, sourceName: String, utf8Content: [UInt8]) {
        self.sourceID = sourceID
        self.sourceName = sourceName
        self.utf8Content = utf8Content
    }
    /// Decodes the archived UTF-8 bytes as text.
    public var text: String { String(decoding: utf8Content, as: UTF8.self) }
}

/// Compiler provenance recorded separately from executable Program data.
public struct GameEventScriptBuildMetadata: Sendable {
    /// Identifier of the producing compiler.
    public let compilerID: String
    /// Version string reported by that compiler.
    public let compilerVersion: String
    /// Creates compiler provenance metadata.
    public init(compilerID: String, compilerVersion: String) {
        self.compilerID = compilerID
        self.compilerVersion = compilerVersion
    }
}

/// Uninterpreted section retained for lossless rewriting under the reader retention policy.
public struct GameEventScriptOpaqueSection: Sendable {
    /// Raw UInt16 section identifier.
    public let sectionType: UInt16
    /// Raw section flag bits.
    public let flags: UInt16
    /// Version of this section payload.
    public let sectionVersion: UInt16
    /// Uninterpreted encoded payload bytes.
    public let rawPayload: [UInt8]
    /// Zero-based section position in the original input.
    public let originalOrdinal: Int
    /// Creates retained section data; the Program validator checks permitted framing and flag values.
    public init(sectionType: UInt16, flags: UInt16, sectionVersion: UInt16, rawPayload: [UInt8], originalOrdinal: Int) {
        self.sectionType = sectionType
        self.flags = flags
        self.sectionVersion = sectionVersion
        self.rawPayload = rawPayload
        self.originalOrdinal = originalOrdinal
    }
}
