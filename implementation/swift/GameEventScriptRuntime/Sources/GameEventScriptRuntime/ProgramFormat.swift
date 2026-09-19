// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

public enum GameEventScriptProgramFormatErrorCode: Int, Sendable, CaseIterable {
    case invalidMagic = 1
    case unsupportedFormatVersion = 2
    case invalidHeaderFlags = 3
    case invalidHeaderSize = 4
    case fileSizeMismatch = 5
    case fileTooLarge = 6
    case tooManySections = 7
    case truncatedSectionHeader = 8
    case truncatedSectionPayload = 9
    case sectionTooLarge = 10
    case invalidSectionType = 11
    case invalidSectionFlags = 12
    case invalidSectionReserved = 13
    case unsupportedSectionVersion = 14
    case unsupportedCompression = 15
    case missingRequiredSection = 16
    case duplicateSection = 17
    case unknownRequiredSection = 18
    case invalidPayloadLength = 19
    case invalidUtf8 = 20
    case tooManyEntries = 21
    case invalidStringIndex = 22
    case invalidListIndex = 23
    case invalidBindingKind = 24
    case duplicateBindingId = 25
    case invalidEntryAddress = 26
    case invalidOpcode = 27
    case invalidOperand = 28
    case invalidJumpAddress = 29
    case invalidCallAddress = 30
    case cyclicCallGraph = 31
    case invalidResourceMetadata = 32
    case invalidDebugSymbol = 33
    case invalidSourceMap = 34
    case invalidSourceArchive = 35
    case sourceMetadataMismatch = 36
    case readerLimitExceeded = 37
    case invalidProgram = 38
}

/// A stable format error and its optional location in the encoded or parsed program.
public struct GameEventScriptProgramFormatError: Error, Sendable {
    public let code: GameEventScriptProgramFormatErrorCode
    public let byteOffset: Int?
    public let sectionType: UInt16?
    public let entryIndex: Int?
    public let message: String
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

public enum GameEventScriptProgramRetention: Sendable { case preserveAll, preserveKnown, runtimeOnly }

public struct GameEventScriptProgramReadLimits: Sendable {
    public var maxFileBytes = 64 * 1024 * 1024
    public var maxSectionCount = 256
    public var maxSourceArchiveBytes = 32 * 1024 * 1024
    public var maxRetainedOpaqueBytes = 16 * 1024 * 1024
    public var maxInstructions = 65535
    public var maxBindings = 65535
    public var maxStringEntries = 65535
    public var maxIndexLists = 65535
    public init() {}
}

public struct GameEventScriptProgramReadOptions: Sendable {
    public var retention: GameEventScriptProgramRetention
    public var limits: GameEventScriptProgramReadLimits
    public init(
        retention: GameEventScriptProgramRetention = .preserveAll, limits: GameEventScriptProgramReadLimits = .init()
    ) {
        self.retention = retention
        self.limits = limits
    }
}

/// Fixed framing constants for the portable V1 transport.
public enum GameEventScriptBinaryFormat {
    public static let version: UInt16 = 1
    public static let headerSize = 16
    public static let sectionHeaderSize = 12
    public static let instructionSize = 16
    public static let magic: [UInt8] = [0x47, 0x45, 0x53, 0x42]
}
