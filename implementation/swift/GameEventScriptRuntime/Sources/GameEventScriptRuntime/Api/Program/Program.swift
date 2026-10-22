// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable, shared reference to a portable `.gesb` program.
/// Only validated readers and compilers create programs. Hosts share the same instance;
/// arrays returned to callers retain copy-on-write isolation from the program.
public final class GameEventScriptProgram: Sendable {
    /// Portable binary format version; independent of the application program version.
    public let formatVersion: UInt16
    /// Module identity, application version and resource requirements.
    public let metadata: GesProgramMetadataSegment
    /// Declared module identifier.
    public var moduleName: String { metadata.moduleName }
    /// Application-provided version carried by the Program metadata.
    public var programVersion: UInt64 { metadata.programVersion }
    /// Maximum simultaneous register requirement computed from executable bindings.
    public var requiredRegisterCount: UInt16 { metadata.requiredRegisterCount }
    /// Maximum acyclic nested-call depth required by this Program.
    public var requiredCallStackDepth: UInt16 { metadata.requiredCallStackDepth }
    /// UTF-8 constants addressed by UInt16 indexes.
    public let stringConstants: GesStringConstantSegment
    /// Shared lists of indexes used by bindings and instructions.
    public let uint16IndexLists: GesUInt16IndexListSegment
    /// Declarative callable and import entries; contains no host callbacks.
    public let bindings: GesBindingSegment
    /// Logical bytecode instructions in address order.
    public let code: GesCodeSegment
    /// Optional register names and lifetimes; nil when absent or not retained.
    public let debugSymbols: GesDebugSymbolsSegment?
    /// Optional bytecode-to-source mapping.
    public let sourceMap: GesSourceMapSegment?
    /// Optional original UTF-8 source documents.
    public let sourceArchive: GesSourceArchiveSegment?
    /// Optional compiler identity and version.
    public let buildMetadata: GesBuildMetadataSegment?
    /// Preserved opaque sections retained by the selected reader policy.
    public let opaqueSections: [GesOpaqueSection]

    init(
        formatVersion: UInt16,
        moduleName: String,
        programVersion: UInt64,
        requiredRegisterCount: UInt16,
        requiredCallStackDepth: UInt16,
        stringConstants: [String],
        uint16IndexLists: [[UInt16]],
        bindings: [GesBindingSegment.Entry],
        code: [GesBytecodeInstruction],
        debugSymbols: [GesDebugSymbol]?,
        sourceMap: GesSourceMapSegment?,
        sourceArchive: [GesSourceArchiveEntry]?,
        buildMetadata: GesBuildMetadataSegment?,
        opaqueSections: [GesOpaqueSection]
    ) {
        self.formatVersion = formatVersion
        metadata = GesProgramMetadataSegment(moduleName: moduleName, requiredRegisterCount: requiredRegisterCount, requiredCallStackDepth: requiredCallStackDepth, programVersion: programVersion)
        self.stringConstants = GesStringConstantSegment(values: stringConstants)
        self.uint16IndexLists = GesUInt16IndexListSegment(lists: uint16IndexLists)
        self.bindings = GesBindingSegment(entries: bindings)
        self.code = GesCodeSegment(instructions: code)
        self.debugSymbols = debugSymbols.map(GesDebugSymbolsSegment.init(symbols:))
        self.sourceMap = sourceMap
        self.sourceArchive = sourceArchive.map(GesSourceArchiveSegment.init(sources:))
        self.buildMetadata = buildMetadata
        self.opaqueSections = opaqueSections
    }
}

/// Module identity, application version and the Program's resource requirements.
public struct GesProgramMetadataSegment: Sendable, Equatable {
    /// Declared module identifier.
    public let moduleName: String
    /// Maximum simultaneous register requirement computed from executable bindings.
    public let requiredRegisterCount: UInt16
    /// Maximum acyclic nested-call depth required by this Program.
    public let requiredCallStackDepth: UInt16
    /// Application-provided version.
    public let programVersion: UInt64

    /// Creates metadata from already validated values.
    public init(moduleName: String, requiredRegisterCount: UInt16, requiredCallStackDepth: UInt16, programVersion: UInt64) {
        self.moduleName = moduleName
        self.requiredRegisterCount = requiredRegisterCount
        self.requiredCallStackDepth = requiredCallStackDepth
        self.programVersion = programVersion
    }
}

/// UTF-8 string constants addressed by UInt16 index. The binary layout is exposed as slices into one UTF-8 buffer.
public struct GesStringConstantSegment: Sendable, RandomAccessCollection {
    /// One constant's byte range within `data`.
    public struct SliceEntry: Sendable, Equatable {
        /// First byte offset in `data`.
        public let start: Int
        /// UTF-8 byte length.
        public let length: Int

        /// Creates a byte range.
        public init(start: Int, length: Int) {
            self.start = start
            self.length = length
        }
    }

    // Decoded constants; the VM and linker address them directly.
    let values: [String]

    init(values: [String]) { self.values = values }

    /// Creates a segment from UTF-8 slices; malformed UTF-8 is decoded with replacement characters.
    public init(slices: [SliceEntry], data: [UInt8]) {
        values = slices.map { String(decoding: data[$0.start..<($0.start + $0.length)], as: UTF8.self) }
    }

    /// Byte ranges of all constants in index order.
    public var slices: [SliceEntry] {
        var start = 0
        return values.map { value in
            defer { start += value.utf8.count }
            return SliceEntry(start: start, length: value.utf8.count)
        }
    }

    /// Concatenated UTF-8 bytes of all constants.
    public var data: [UInt8] { values.flatMap { Array($0.utf8) } }

    /// UTF-8 byte length of the constant at `index`.
    public func resolveSize(_ index: UInt16) -> Int { values[Int(index)].utf8.count }

    /// The decoded constant at `index`.
    public func resolve(_ index: UInt16) -> String { values[Int(index)] }

    /// The first constant index.
    public var startIndex: Int { 0 }

    /// The index after the last constant.
    public var endIndex: Int { values.count }

    /// The decoded constant at `index`.
    public subscript(index: Int) -> String { values[index] }
}

/// One resolved UInt16 index list.
public struct GesUInt16IndexList: Sendable, RandomAccessCollection {
    // The segment's lists share storage; the offset is derived on demand so Programs need no extra index table.
    private let lists: [[UInt16]]
    private let index: Int

    init(lists: [[UInt16]], index: Int) {
        self.lists = lists
        self.index = index
    }

    private var values: [UInt16] { lists[index] }

    /// First element offset in the segment's `data`.
    public var start: Int { lists[..<index].reduce(0) { $0 + $1.count } }

    /// Number of elements.
    public var length: Int { values.count }

    /// The first element position.
    public var startIndex: Int { 0 }

    /// The position after the last element.
    public var endIndex: Int { values.count }

    /// The element at `index`, relative to this list.
    public subscript(index: Int) -> UInt16 { values[index] }
}

/// Shared UInt16 index lists used by bindings and instructions.
public struct GesUInt16IndexListSegment: Sendable, RandomAccessCollection {
    /// One list's element range within `data`.
    public struct SliceEntry: Sendable, Equatable {
        /// First element offset in `data`.
        public let start: Int
        /// Element count.
        public let length: Int

        /// Creates an element range.
        public init(start: Int, length: Int) {
            self.start = start
            self.length = length
        }
    }

    // Lists as stored arrays; the VM reads them directly.
    let lists: [[UInt16]]

    init(lists: [[UInt16]]) { self.lists = lists }

    /// Creates a segment from element slices into one shared buffer.
    public init(slices: [SliceEntry], data: [UInt16]) {
        self.init(lists: slices.map { Array(data[$0.start..<($0.start + $0.length)]) })
    }

    /// Element ranges of all lists in index order.
    public var slices: [SliceEntry] {
        var start = 0
        return lists.map { list in
            defer { start += list.count }
            return SliceEntry(start: start, length: list.count)
        }
    }

    /// Concatenated elements of all lists.
    public var data: [UInt16] { lists.flatMap { $0 } }

    /// The list at `index`.
    public func resolve(_ index: UInt16) -> GesUInt16IndexList { self[Int(index)] }

    /// The first list index.
    public var startIndex: Int { 0 }

    /// The index after the last list.
    public var endIndex: Int { lists.count }

    /// The list at `index`.
    public subscript(index: Int) -> GesUInt16IndexList { GesUInt16IndexList(lists: lists, index: index) }
}

/// A logical instruction; its encoding never depends on native memory layout.
public struct GesBytecodeInstruction: Sendable, Equatable {
    /// Operation identifier determining operand interpretation.
    public let opcode: GesBytecodeOpCode
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
    public init(opcode: GesBytecodeOpCode, unitAndFlags: UInt8 = 0, word0: UInt16 = 0, word1: UInt16 = 0, word2: UInt16 = 0, payload: UInt64 = 0) {
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
    public var unit: GesQuantityUnit { decodedUnit }
    // Inlined into VM dispatch; the public accessor shares this single mapping.
    var decodedUnit: GesQuantityUnit {
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

/// Declarative callable and import entries in binding-id order.
public struct GesBindingSegment: Sendable, RandomAccessCollection {
    /// A declarative binding; callbacks and execution state never belong to a Program.
    public struct Entry: Sendable {
        /// Binding role: executable declaration, message signature or host import.
        public let kind: GesBinaryBindKind
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
            kind: GesBinaryBindKind,
            name: UInt16,
            argumentNames: [UInt16] = [],
            entryAddress: UInt16 = .max,
            id: UInt16 = .max,
            requiredTags: [UInt16] = [],
            excludedTags: [UInt16] = [],
            requiredRegisterCount: UInt16 = 0,
            requiredCallStackDepth: UInt16 = 0
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

    /// All entries in binding-id order.
    public let entries: [Entry]

    /// Creates a segment from entries in binding-id order.
    public init(entries: [Entry]) { self.entries = entries }

    /// Number of entries as encoded in the binary section header.
    public var entryCount: UInt16 { UInt16(truncatingIfNeeded: entries.count) }

    /// The first binding id.
    public var startIndex: Int { 0 }

    /// The binding id after the last entry.
    public var endIndex: Int { entries.count }

    /// The entry with binding id `index`.
    public subscript(index: Int) -> Entry { entries[index] }
}

/// Logical bytecode instructions in address order.
public struct GesCodeSegment: Sendable, RandomAccessCollection {
    /// All instructions in address order.
    public let instructions: [GesBytecodeInstruction]

    /// Creates a segment from instructions in address order.
    public init(instructions: [GesBytecodeInstruction]) { self.instructions = instructions }

    /// Number of instructions.
    public var length: Int { instructions.count }

    /// The first instruction address.
    public var startIndex: Int { 0 }

    /// The address after the last instruction.
    public var endIndex: Int { instructions.count }

    /// The instruction at `index`.
    public subscript(index: Int) -> GesBytecodeInstruction { instructions[index] }
}

/// Role of a named register in an executable binding.
public enum GesDebugSymbolKind: UInt8, Sendable {
    /// A preloaded callable argument.
    case parameter = 1
    /// A local lexical binding.
    case local = 2
}

/// A register name and the instruction interval over which it is valid.
public struct GesDebugSymbol: Sendable {
    /// Whether the register names a parameter or a local.
    public let kind: GesDebugSymbolKind
    /// Zero-based register index in the active frame.
    public let registerID: UInt16
    /// Source-level binding name.
    public let name: String
    /// First instruction address in the symbol lifetime.
    public let codeStart: UInt32
    /// Number of instructions covered by the symbol lifetime.
    public let codeLength: UInt32

    /// Creates debug-symbol data with a register and half-open instruction lifetime.
    public init(kind: GesDebugSymbolKind, registerID: UInt16, name: String, codeStart: UInt32, codeLength: UInt32) {
        self.kind = kind
        self.registerID = registerID
        self.name = name
        self.codeStart = codeStart
        self.codeLength = codeLength
    }
}

/// Source-document identity, checksum and line boundaries used by debug mappings.
public struct GesSourceMapSource: Sendable {
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
    public init(sourceID: UInt32, sourceName: String, sourceByteLength: UInt32, sha256: [UInt8], lineStartByteOffsets: [UInt32]) {
        self.sourceID = sourceID
        self.sourceName = sourceName
        self.sourceByteLength = sourceByteLength
        self.sha256 = sha256
        self.lineStartByteOffsets = lineStartByteOffsets
    }
}

/// Mapping from a half-open instruction interval to a UTF-8 source span.
public struct GesSourceMapEntry: Sendable {
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
    public init(codeStart: UInt32, codeLength: UInt32, sourceID: UInt32, sourceStartByteOffset: UInt32, sourceByteLength: UInt32) {
        self.codeStart = codeStart
        self.codeLength = codeLength
        self.sourceID = sourceID
        self.sourceStartByteOffset = sourceStartByteOffset
        self.sourceByteLength = sourceByteLength
    }
}

/// Source identities and mappings retained in the optional SourceMap section.
public struct GesSourceMapSegment: Sendable {
    /// Source-document identities in stored order.
    public let sources: [GesSourceMapSource]
    /// Instruction-to-source mappings in stored order.
    public let entries: [GesSourceMapEntry]

    /// Creates an immutable source map from document identities and mappings.
    public init(sources: [GesSourceMapSource], entries: [GesSourceMapEntry]) {
        self.sources = sources
        self.entries = entries
    }
}

/// Original UTF-8 contents of one embedded compiler input.
public struct GesSourceArchiveEntry: Sendable {
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

/// Optional register names and lifetimes.
public struct GesDebugSymbolsSegment: Sendable, RandomAccessCollection {
    /// Debug symbols in section order.
    public let symbols: [GesDebugSymbol]

    /// Creates a segment from symbols in section order.
    public init(symbols: [GesDebugSymbol]) { self.symbols = symbols }

    /// The first position.
    public var startIndex: Int { 0 }

    /// The position after the last element.
    public var endIndex: Int { symbols.count }

    /// The element at `index`.
    public subscript(index: Int) -> GesDebugSymbol { symbols[index] }
}

/// Optional original UTF-8 source documents.
public struct GesSourceArchiveSegment: Sendable, RandomAccessCollection {
    /// Archived documents in source-id order.
    public let sources: [GesSourceArchiveEntry]

    /// Creates a segment from documents in source-id order.
    public init(sources: [GesSourceArchiveEntry]) { self.sources = sources }

    /// The first position.
    public var startIndex: Int { 0 }

    /// The position after the last element.
    public var endIndex: Int { sources.count }

    /// The element at `index`.
    public subscript(index: Int) -> GesSourceArchiveEntry { sources[index] }
}

/// Compiler provenance recorded separately from executable Program data.
public struct GesBuildMetadataSegment: Sendable {
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

/// Section header flag bits of the `.gesb` V1 container.
public struct GesSectionFlags: OptionSet, Sendable, Hashable {
    /// Raw UInt16 flag bits as encoded in the section header.
    public let rawValue: UInt16

    /// Creates flags from raw bits; the Program validator rejects unsupported combinations.
    public init(rawValue: UInt16) { self.rawValue = rawValue }

    /// No flags.
    public static let none: Self = []
    /// The section is required for correct interpretation.
    public static let required = Self(rawValue: 0x0001)
    /// Bits reserved for the payload compression method.
    public static let compressionMask = Self(rawValue: 0x00f0)
}

/// Uninterpreted section retained for lossless rewriting under the reader retention policy.
public struct GesOpaqueSection: Sendable {
    /// Raw UInt16 section identifier.
    public let sectionType: UInt16
    /// Section flag bits.
    public let flags: GesSectionFlags
    /// Version of this section payload.
    public let sectionVersion: UInt16
    /// Uninterpreted encoded payload bytes.
    public let rawPayload: [UInt8]
    /// Zero-based section position in the original input.
    public let originalOrdinal: Int

    /// Creates retained section data; the Program validator checks permitted framing and flag values.
    public init(sectionType: UInt16, flags: GesSectionFlags, sectionVersion: UInt16, rawPayload: [UInt8], originalOrdinal: Int) {
        self.sectionType = sectionType
        self.flags = flags
        self.sectionVersion = sectionVersion
        self.rawPayload = rawPayload
        self.originalOrdinal = originalOrdinal
    }
}
