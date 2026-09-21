// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Stable classifications for invalid portable Program data or binary framing.
public enum GameEventScriptProgramFormatErrorCode: Int, Sendable, CaseIterable {
    /// The input does not begin with the GESB magic bytes.
    case invalidMagic = 1
    /// The file uses an unsupported binary format version.
    case unsupportedFormatVersion = 2
    /// The file header contains unsupported flag bits.
    case invalidHeaderFlags = 3
    /// The header size differs from the V1 framing size.
    case invalidHeaderSize = 4
    /// The declared file size differs from the input length.
    case fileSizeMismatch = 5
    /// The file exceeds the permitted format size.
    case fileTooLarge = 6
    /// The section count exceeds the permitted bound.
    case tooManySections = 7
    /// The input ends inside a section header.
    case truncatedSectionHeader = 8
    /// The input ends before a section payload is complete.
    case truncatedSectionPayload = 9
    /// A section payload exceeds its permitted size.
    case sectionTooLarge = 10
    /// A section uses an invalid type identifier.
    case invalidSectionType = 11
    /// A section contains unsupported flag bits.
    case invalidSectionFlags = 12
    /// A section has a nonzero reserved field.
    case invalidSectionReserved = 13
    /// A known section uses an unsupported version.
    case unsupportedSectionVersion = 14
    /// A section requests unsupported compression.
    case unsupportedCompression = 15
    /// A required V1 section is missing.
    case missingRequiredSection = 16
    /// A section that must be unique occurs more than once.
    case duplicateSection = 17
    /// An unknown section is marked required.
    case unknownRequiredSection = 18
    /// A payload length is inconsistent with its encoded data.
    case invalidPayloadLength = 19
    /// A stored string or source document is not valid UTF-8.
    case invalidUtf8 = 20
    /// A table exceeds its indexable entry count.
    case tooManyEntries = 21
    /// A string-table reference is out of bounds.
    case invalidStringIndex = 22
    /// An index-list reference is out of bounds.
    case invalidListIndex = 23
    /// A binding uses an unknown kind.
    case invalidBindingKind = 24
    /// A binding identifier is duplicated within its kind.
    case duplicateBindingId = 25
    /// An executable entry address is invalid.
    case invalidEntryAddress = 26
    /// An instruction uses an unknown opcode.
    case invalidOpcode = 27
    /// An instruction operand violates its opcode contract.
    case invalidOperand = 28
    /// A branch target is not a valid instruction address.
    case invalidJumpAddress = 29
    /// A call target is not a valid callable entry.
    case invalidCallAddress = 30
    /// Executable calls form a forbidden cycle.
    case cyclicCallGraph = 31
    /// Declared register or call-depth requirements are invalid.
    case invalidResourceMetadata = 32
    /// A debug symbol has an invalid register, name or lifetime.
    case invalidDebugSymbol = 33
    /// Source mappings contain invalid identities, spans or ordering.
    case invalidSourceMap = 34
    /// An archived source document is malformed.
    case invalidSourceArchive = 35
    /// Source metadata does not match the archived source bytes.
    case sourceMetadataMismatch = 36
    /// Reading would exceed a configured resource limit.
    case readerLimitExceeded = 37
    /// Program data violates a structural or semantic invariant.
    case invalidProgram = 38
}

/// A stable format error and its optional location in the encoded or parsed program.
public struct GameEventScriptProgramFormatError: Error, Sendable {
    /// Stable error classification for machine-readable handling.
    public let code: GameEventScriptProgramFormatErrorCode
    /// Zero-based encoded byte offset when known.
    public let byteOffset: Int?
    /// Raw section identifier when the failure belongs to a section.
    public let sectionType: UInt16?
    /// Zero-based entry index when known.
    public let entryIndex: Int?
    /// Human-readable explanation; not a stable comparison key.
    public let message: String
    /// Creates a format error with optional encoded and logical location context.
    public init(
        _ code: GameEventScriptProgramFormatErrorCode, message: String = "Invalid GES program",
        byteOffset: Int? = nil, sectionType: UInt16? = nil, entryIndex: Int? = nil
    ) {
        self.code = code
        self.message = message
        self.byteOffset = byteOffset
        self.sectionType = sectionType
        self.entryIndex = entryIndex
    }
}

/// Controls which optional sections the binary reader retains.
public enum GameEventScriptProgramRetention: Sendable {
    /// Retains known optional sections and permitted opaque sections.
    case preserveAll
    /// Retains known sections and discards opaque optional sections.
    case preserveKnown
    /// Retains executable Program data and discards optional debug, source and provenance data.
    case runtimeOnly
}

/// Independent resource limits applied while decoding untrusted binary inputs.
public struct GameEventScriptProgramReadLimits: Sendable {
    /// Maximum input bytes; defaults to 64 MiB.
    public var maxFileBytes = 64 * 1024 * 1024
    /// Maximum number of sections; defaults to 256.
    public var maxSectionCount = 256
    /// Maximum cumulative retained source archive bytes; defaults to 32 MiB.
    public var maxSourceArchiveBytes = 32 * 1024 * 1024
    /// Maximum cumulative retained opaque payload bytes; defaults to 16 MiB.
    public var maxRetainedOpaqueBytes = 16 * 1024 * 1024
    /// Maximum instruction count; defaults to 65535.
    public var maxInstructions = 65535
    /// Maximum binding count; defaults to 65535.
    public var maxBindings = 65535
    /// Maximum string-table entry count; defaults to 65535.
    public var maxStringEntries = 65535
    /// Maximum shared index-list count; defaults to 65535.
    public var maxIndexLists = 65535
    /// Creates the standard bounded reader limits.
    public init() {}
}

/// Binary-reader retention policy and resource limits.
public struct GameEventScriptProgramReadOptions: Sendable {
    /// Which optional data survives reading.
    public var retention: GameEventScriptProgramRetention
    /// Resource limits enforced during decoding.
    public var limits: GameEventScriptProgramReadLimits
    /// Creates reader options; defaults to preserving all supported data with standard limits.
    public init(
        retention: GameEventScriptProgramRetention = .preserveAll, limits: GameEventScriptProgramReadLimits = .init()
    ) {
        self.retention = retention
        self.limits = limits
    }
}

/// Fixed framing constants for the portable V1 transport.
public enum GameEventScriptBinaryFormat {
    /// Current portable format version, V1.
    public static let version: UInt16 = 1
    /// File header size in bytes (16).
    public static let headerSize = 16
    /// Section header size in bytes (12).
    public static let sectionHeaderSize = 12
    /// Canonical instruction width in bytes (16).
    public static let instructionSize = 16
    /// ASCII GESB framing bytes.
    public static let magic: [UInt8] = [0x47, 0x45, 0x53, 0x42]
}
