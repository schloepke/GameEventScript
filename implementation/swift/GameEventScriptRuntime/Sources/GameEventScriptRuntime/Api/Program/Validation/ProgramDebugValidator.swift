// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptProgramValidator {
    static func validateDebug(_ p: GameEventScriptProgram) throws {
        for (index, symbol) in (p.debugSymbols ?? []).enumerated() {
            if (symbol.name != "_" && !GesNames.identifier(symbol.name)) || symbol.codeLength == 0 || UInt64(symbol.codeStart) + UInt64(symbol.codeLength) > UInt64(p.code.count) || symbol.registerID == .max {
                throw failure(.invalidDebugSymbol, 32, index)
            }
        }
        if let map = p.sourceMap {
            for (index, source) in map.sources.enumerated() {
                if source.sourceID != index || source.sourceName.isEmpty || source.sha256.count != 32 || source.lineStartByteOffsets.first != 0 { throw failure(.invalidSourceMap, 33, index) }
                var previous: UInt32 = 0
                for (line, offset) in source.lineStartByteOffsets.enumerated() {
                    if offset > source.sourceByteLength || line > 0 && offset <= previous { throw failure(.invalidSourceMap, 33, index) }
                    previous = offset
                }
            }
            var end: UInt64 = 0
            for (index, entry) in map.entries.enumerated() {
                if Int(entry.sourceID) >= map.sources.count { throw failure(.invalidSourceMap, 33, index) }
                let source = map.sources[Int(entry.sourceID)]
                if entry.codeLength == 0 || entry.sourceByteLength == 0 || UInt64(entry.codeStart) < end || UInt64(entry.codeStart) + UInt64(entry.codeLength) > UInt64(p.code.count)
                    || UInt64(entry.sourceStartByteOffset) + UInt64(entry.sourceByteLength) > UInt64(source.sourceByteLength)
                {
                    throw failure(.invalidSourceMap, 33, index)
                }
                end = UInt64(entry.codeStart) + UInt64(entry.codeLength)
            }
        }
        if let archive = p.sourceArchive {
            var ids: Set<UInt32> = []
            for (index, source) in archive.enumerated() {
                if !ids.insert(source.sourceID).inserted || source.sourceName.isEmpty { throw failure(.invalidSourceArchive, 34, index) }
                guard let text = GesUtf8.decode(source.utf8Content) else { throw failure(.invalidUtf8, 34, index) }
                if text.unicodeScalars.first?.value == 0xfeff { throw failure(.invalidSourceArchive, 34, index) }
            }
        }
        if let map = p.sourceMap, let archive = p.sourceArchive {
            if map.sources.count != archive.count { throw failure(.sourceMetadataMismatch, 33) }
            let byID = Dictionary(uniqueKeysWithValues: archive.map { ($0.sourceID, $0) })
            for (index, metadata) in map.sources.enumerated() {
                guard let source = byID[metadata.sourceID], GesText.scalarEqual(source.sourceName, metadata.sourceName), source.utf8Content.count == metadata.sourceByteLength, GesSha256.digest(source.utf8Content) == metadata.sha256 else {
                    throw failure(.sourceMetadataMismatch, 33, index)
                }
                let bytes = source.utf8Content

                func boundary(_ offset: UInt32) -> Bool { offset == 0 || offset == bytes.count || Int(offset) < bytes.count && bytes[Int(offset)] & 0xc0 != 0x80 }

                if !metadata.lineStartByteOffsets.allSatisfy(boundary) { throw failure(.invalidSourceMap, 33, index) }
                var lines: [UInt32] = [0]
                var cursor = 0
                while cursor < bytes.count {
                    if bytes[cursor] == 13 {
                        cursor += 1
                        if cursor < bytes.count && bytes[cursor] == 10 { cursor += 1 }
                        lines.append(UInt32(cursor))
                    } else if bytes[cursor] == 10 {
                        cursor += 1
                        lines.append(UInt32(cursor))
                    } else {
                        cursor += 1
                    }
                }
                if lines != metadata.lineStartByteOffsets { throw failure(.sourceMetadataMismatch, 33, index) }
                for (mappingIndex, entry) in map.entries.enumerated() where entry.sourceID == metadata.sourceID {
                    if !boundary(entry.sourceStartByteOffset) || !boundary(entry.sourceStartByteOffset + entry.sourceByteLength) { throw failure(.invalidSourceMap, 33, mappingIndex) }
                }
            }
        }
        if let build = p.buildMetadata, build.compilerID.isEmpty || build.compilerVersion.isEmpty { throw failure(.invalidProgram, 48) }
    }

    static func validateOpaque(_ p: GameEventScriptProgram) throws {
        var ordinals: Set<Int> = []
        var known: Set<UInt16> = []
        for (index, section) in p.opaqueSections.enumerated() {
            let type = section.sectionType
            if type == 0 || type == .max || section.originalOrdinal < 0 || section.flags & 1 != 0 { throw failure(.unknownRequiredSection, type, index) }
            if section.flags & ~0x00f1 != 0 { throw failure(.invalidSectionFlags, type, index) }
            if [1, 2, 3, 4, 16].contains(type) { throw failure(.duplicateSection, type, index) }
            if type == 32 && p.debugSymbols != nil || type == 33 && p.sourceMap != nil || type == 34 && p.sourceArchive != nil || type == 48 && p.buildMetadata != nil { throw failure(.duplicateSection, type, index) }
            if [32, 33, 34, 48].contains(type) {
                if section.sectionVersion == 1 && section.flags & 0xf0 == 0 { throw failure(.invalidProgram, type, index) }
                if !known.insert(type).inserted { throw failure(.duplicateSection, type, index) }
            }
            if !ordinals.insert(section.originalOrdinal).inserted { throw failure(.invalidProgram, type, index) }
        }
    }
}
