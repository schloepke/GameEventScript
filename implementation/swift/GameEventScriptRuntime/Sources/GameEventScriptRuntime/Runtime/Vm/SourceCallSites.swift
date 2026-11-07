// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Locates the source of the instructions that use one import binding, for link diagnostics. Each distinct source range
/// counts once, ordered by source ID, start and length. The most specific SourceMap entry covering an instruction
/// gives its range; lines come from the SourceMap and Unicode-scalar columns from the archived source, so a Program
/// without a SourceArchive yields lines only and one without a SourceMap no location at all.
enum GesSourceCallSites {
    static func locate(_ program: GameEventScriptProgram, _ opcode: GesBytecodeOpCode, _ bindID: UInt16) -> [GesSourceLocation] {
        guard let sourceMap = program.sourceMap else { return [] }
        var ranges: [GesSourceMapEntry] = []
        for (address, instruction) in program.code.instructions.enumerated() where instruction.opcode == opcode && instruction.word1 == bindID {
            guard let entry = innermost(sourceMap, UInt32(address)) else { continue }
            if !ranges.contains(where: { $0.sourceID == entry.sourceID && $0.sourceStartByteOffset == entry.sourceStartByteOffset && $0.sourceByteLength == entry.sourceByteLength }) {
                ranges.append(entry)
            }
        }
        ranges.sort { ($0.sourceID, $0.sourceStartByteOffset, $0.sourceByteLength) < ($1.sourceID, $1.sourceStartByteOffset, $1.sourceByteLength) }
        return ranges.compactMap { location(program, sourceMap, $0) }
    }

    // The shortest entry that covers the address, the first one among equally long entries.
    static func innermost(_ sourceMap: GesSourceMapSegment, _ address: UInt32) -> GesSourceMapEntry? {
        var best: GesSourceMapEntry?
        for entry in sourceMap.entries where address >= entry.codeStart && address - entry.codeStart < entry.codeLength {
            if best.map({ entry.sourceByteLength < $0.sourceByteLength }) ?? true { best = entry }
        }
        return best
    }

    static func location(_ program: GameEventScriptProgram, _ sourceMap: GesSourceMapSegment, _ range: GesSourceMapEntry) -> GesSourceLocation? {
        guard let source = sourceMap.sources.last(where: { $0.sourceID == range.sourceID }) else { return nil }
        let archived = program.sourceArchive?.sources.last { $0.sourceID == range.sourceID }
        let end = range.sourceStartByteOffset + range.sourceByteLength
        let (line, lineStart) = self.line(source, range.sourceStartByteOffset)
        let (endLine, endLineStart) = self.line(source, end)
        return GesSourceLocation(
            sourceName: source.sourceName,
            line: line,
            column: archived.map { column($0.utf8Content, lineStart, range.sourceStartByteOffset) },
            endLine: endLine,
            endColumn: archived.map { column($0.utf8Content, endLineStart, end) },
            moduleName: program.moduleName,
            sourceID: range.sourceID
        )
    }

    // The one-based line of a byte offset and the offset where that line starts.
    static func line(_ source: GesSourceMapSource, _ offset: UInt32) -> (line: Int, start: UInt32) {
        let lines = source.lineStartByteOffsets
        var index = 0
        while index + 1 < lines.count && lines[index + 1] <= offset { index += 1 }
        return (index + 1, lines.isEmpty ? 0 : lines[index])
    }

    // The one-based Unicode-scalar column of a byte offset; a leading byte order mark is not counted.
    static func column(_ content: [UInt8], _ lineStart: UInt32, _ offset: UInt32) -> Int {
        var start = Int(lineStart)
        if start == 0 && content.starts(with: [0xEF, 0xBB, 0xBF]) { start = 3 }
        var column = 1
        var index = start
        while index < Int(offset) && index < content.count {
            if content[index] & 0xC0 != 0x80 { column += 1 }
            index += 1
        }
        return column
    }
}
