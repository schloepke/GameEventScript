// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptProgram {
    /// Decodes bounded, portable `.gesb` V1 bytes into a validated immutable Program that owns all retained data.
    ///
    /// - Throws: `GesProgramFormatError` with a stable code for malformed, unsupported, oversized or invalid input.
    public static func read(_ bytes: [UInt8], options: GesProgramReadOptions = .init()) throws -> GameEventScriptProgram {
        try GesProgramReader.read(bytes, options: options)
    }

    /// Validates this Program and computes its canonical encoded byte count without producing the binary buffer.
    ///
    /// - Throws: `GesProgramFormatError` for invalid Program data.
    public func encodedSize() throws -> Int { try GesProgramWriter.encodedSize(self) }

    /// Validates and encodes this Program as canonical little-endian `.gesb` V1 bytes.
    ///
    /// - Throws: `GesProgramFormatError` for invalid Program data.
    public func encode() throws -> [UInt8] { try GesProgramWriter.bytes(self) }

    /// Writes the canonical encoding into existing storage, preserving it on validation or capacity failure. Returns
    /// the number of bytes written.
    ///
    /// - Throws: `GesProgramFormatError` for invalid Program data or a too small destination.
    @discardableResult public func write(into destination: inout [UInt8]) throws -> Int { try GesProgramWriter.write(self, into: &destination) }

    /// Enforces the shared structural and semantic invariants required by the reader, writer and host linker.
    ///
    /// - Throws: `GesProgramFormatError` with a stable code when an invariant is violated.
    public func validate() throws { try GesProgramValidator.validate(self) }

    /// Dumps metadata, tables, binds and code as assembler-like GESA text.
    public func dump(includeInstructionAddresses: Bool = false) -> String { GesProgramDumper.dump(self, includeInstructionAddresses: includeInstructionAddresses) }
}
