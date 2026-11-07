// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';

// Reads the structure of a .gesb file for display, as specs/BinaryFormat.md
// defines it. It does not validate a Program: only the runtimes do that. Damaged
// files yield what could be read and a list of problems, and never an exception.

const FILE_HEADER_SIZE = 16;
const SECTION_HEADER_SIZE = 12;
const NO_INDEX = 0xFFFF;
const MAX_PROBLEMS = 20;
// The default reader limits of specs/BinaryFormat.md. MAX_ENTRIES also bounds the
// source tables, which the format leaves open, because each one becomes a page element.
const MAX_SECTIONS = 256;
const MAX_ENTRIES = 65535;
const MAX_SOURCE_ARCHIVE = 32 * 1024 * 1024;
// Many bindings can share one long list; a signature never needs more for display.
const MAX_SHOWN_LIST_ENTRIES = 64;
const UTF8 = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true });

const SECTION_NAMES = new Map([
    [0x0001, 'ProgramMetadataSegment'],
    [0x0002, 'StringConstantSegment'],
    [0x0003, 'UInt16IndexListSegment'],
    [0x0004, 'BindingSegment'],
    [0x0010, 'CodeSegment'],
    [0x0020, 'DebugSymbolsSegment'],
    [0x0021, 'SourceMapSegment'],
    [0x0022, 'SourceArchiveSegment'],
    [0x0030, 'BuildMetadataSegment'],
    [0x0040, 'ReservedSignatureSegment'],
    [0xFFFE, 'NamedCustomSection'],
]);

const BINDING_KINDS = new Map([
    [0x10, 'MessageHandler'],
    [0x11, 'MessageNameHandler'],
    [0x12, 'Function'],
    [0x13, 'Predicate'],
    [0x20, 'ExtensionCall'],
    [0x21, 'OutboundMessage'],
    [0x30, 'Record'],
    [0x31, 'ExternalType'],
]);

function sectionName(type) {
    if (SECTION_NAMES.has(type)) return SECTION_NAMES.get(type);
    if (type >= 0x0041 && type <= 0x004F) return 'Reserved security section';
    if (type >= 0x8000 && type <= 0xFFFD) return 'Private section';
    return 'Unknown section';
}

class Payload {
    constructor(bytes, start, length) {
        this.view = new DataView(bytes.buffer, bytes.byteOffset + start, length);
        this.bytes = bytes.subarray(start, start + length);
        this.position = 0;
    }

    get remaining() { return this.bytes.length - this.position; }

    take(count) {
        if (count > this.remaining) throw new Error('The segment ends before its data does.');
        const position = this.position;
        this.position += count;
        return position;
    }

    u8() { return this.view.getUint8(this.take(1)); }

    u16() { return this.view.getUint16(this.take(2), true); }

    u32() { return this.view.getUint32(this.take(4), true); }

    u64() { return this.view.getBigUint64(this.take(8), true); }

    skip(count) { this.take(count); }

    // Checking a count against the remaining bytes bounds every later allocation.
    count(entrySize, limit = Infinity) {
        const count = this.u32();
        if (count * entrySize > this.remaining) throw new Error('The segment declares more entries than it holds.');
        if (count > limit) throw new Error(`The segment declares ${count} entries; the limit is ${limit}.`);
        return count;
    }

    text() {
        const length = this.u32();
        const start = this.take(length);
        return UTF8.decode(this.bytes.subarray(start, start + length));
    }

    end() {
        if (this.remaining !== 0) throw new Error('The segment holds data after its end.');
    }
}

function repeat(count, read) {
    const values = new Array(count);
    for (let index = 0; index < count; index++) values[index] = read();
    return values;
}

const segmentReaders = new Map([
    [0x0001, payload => {
        const metadata = {
            moduleNameIndex: payload.u16(), registers: payload.u16(), callDepth: payload.u16(),
        };
        payload.skip(2);
        metadata.programVersion = payload.u64().toString();
        return metadata;
    }],
    [0x0002, payload => repeat(payload.count(4, MAX_ENTRIES), () => payload.text())],
    [0x0003, payload => repeat(payload.count(4, MAX_ENTRIES), () => {
        const count = payload.count(2);
        return repeat(count, () => payload.u16());
    })],
    [0x0004, payload => repeat(payload.count(20, MAX_ENTRIES), () => {
        const kind = payload.u8();
        payload.skip(1);
        const entry = {
            kind, id: payload.u16(), nameIndex: payload.u16(), argumentsList: payload.u16(), requiredList: payload.u16(),
            excludedList: payload.u16(), entry: payload.u16(), registers: payload.u16(), callDepth: payload.u16(),
        };
        payload.skip(2);
        return entry;
    })],
    [0x0010, payload => {
        const instructions = payload.count(16, MAX_ENTRIES);
        payload.skip(instructions * 16);
        return { instructions };
    }],
    [0x0020, payload => {
        // The page shows no symbol names, so they are neither decoded nor checked.
        const names = payload.count(4);
        for (let index = 0; index < names; index++) payload.skip(payload.u32());
        const symbols = payload.count(16);
        payload.skip(symbols * 16);
        return { symbols };
    }],
    [0x0021, payload => {
        const sources = repeat(payload.count(44, MAX_ENTRIES), () => {
            const name = payload.text();
            const byteLength = payload.u32();
            payload.skip(32);
            const lines = payload.count(4);
            return { name, byteLength, lineStarts: repeat(lines, () => payload.u32()) };
        });
        const mappings = repeat(payload.count(20), () => {
            const mapping = { codeStart: payload.u32(), codeLength: payload.u32(), source: payload.u32(), byteStart: payload.u32() };
            payload.skip(4);
            return mapping;
        });
        mappings.sort((a, b) => a.codeStart - b.codeStart);
        return { sources, mappings };
    }],
    [0x0022, payload => {
        if (payload.remaining > MAX_SOURCE_ARCHIVE) throw new Error('The segment exceeds the limit of 32 MiB.');
        return repeat(payload.count(12, MAX_ENTRIES), () => ({ id: payload.u32(), name: payload.text(), content: payload.text() }));
    }],
    [0x0030, payload => ({ compiler: payload.text(), version: payload.text() })],
]);

function lineOf(lineStarts, byteOffset) {
    let low = 0, high = lineStarts.length;
    while (low < high) {
        const middle = (low + high) >> 1;
        if (lineStarts[middle] <= byteOffset) low = middle + 1;
        else high = middle;
    }
    return low;
}

// The first mapped instruction of a routine names its source line: the entry
// itself usually is the compiler-generated register reservation.
function locate(address, nextAddress, sourceMap) {
    if (!sourceMap || address === NO_INDEX) return null;
    const mappings = sourceMap.mappings;
    let low = 0, high = mappings.length;
    while (low < high) {
        const middle = (low + high) >> 1;
        if (mappings[middle].codeStart < address) low = middle + 1;
        else high = middle;
    }
    const first = mappings[low];
    if (!first || first.codeStart >= nextAddress) return null;
    const source = sourceMap.sources[first.source];
    return source ? { file: source.name, line: lineOf(source.lineStarts, first.byteStart) } : null;
}

function readGesb(bytes) {
    const program = {
        fileSize: bytes.length, formatVersion: null, sections: [], problems: [], module: null, programVersion: null,
        registers: null, callDepth: null, compiler: null, texts: [], listCount: 0, bindings: [], instructions: null,
        symbols: null, sources: [], mappedSources: 0,
    };
    const problem = (message, offset = null) => {
        if (program.problems.length > MAX_PROBLEMS || program.problems.some(known => known.message === message)) return;
        program.problems.push(program.problems.length === MAX_PROBLEMS ? { message: 'Further problems are not listed.', offset: null } : { message, offset });
    };
    if (bytes.length < FILE_HEADER_SIZE) {
        problem('The file is too short for a .gesb header.', 0);
        return program;
    }
    const header = new DataView(bytes.buffer, bytes.byteOffset, bytes.length);
    if (String.fromCharCode(...bytes.subarray(0, 4)) !== 'GESB') {
        problem('The file does not start with GESB.', 0);
        return program;
    }
    program.formatVersion = header.getUint16(4, true);
    if (program.formatVersion !== 1) {
        problem(`This viewer reads format version 1, not ${program.formatVersion}.`, 4);
        return program;
    }
    if (header.getUint16(6, true) !== 0) problem('The header flags are not zero.', 6);
    if (header.getUint32(8, true) !== FILE_HEADER_SIZE) problem('The header size is not 16.', 8);
    if (header.getUint32(12, true) !== bytes.length) problem(`The header declares ${header.getUint32(12, true)} bytes, but the file has ${bytes.length}.`, 12);

    const contents = new Map();
    let readAll = true;
    for (let offset = FILE_HEADER_SIZE; offset < bytes.length;) {
        if (bytes.length - offset < SECTION_HEADER_SIZE) {
            problem('The file ends inside a segment header.', offset);
            break;
        }
        if (program.sections.length === MAX_SECTIONS) {
            problem(`The file has more than ${MAX_SECTIONS} segments; the rest is not shown.`, offset);
            readAll = false;
            break;
        }
        const section = {
            offset, type: header.getUint16(offset, true), flags: header.getUint16(offset + 2, true),
            version: header.getUint16(offset + 4, true), length: header.getUint32(offset + 8, true),
        };
        section.name = sectionName(section.type);
        section.required = (section.flags & 1) !== 0;
        const payloadStart = offset + SECTION_HEADER_SIZE;
        if (section.length > bytes.length - payloadStart) {
            problem(`${section.name} declares ${section.length} bytes, more than the file holds.`, offset);
            section.length = bytes.length - payloadStart;
            section.truncated = true;
        }
        program.sections.push(section);
        offset = payloadStart + section.length;
        const read = segmentReaders.get(section.type);
        if (!read) continue;
        section.unreadable = true;
        if (section.truncated) continue;
        if (contents.has(section.type)) problem(`${section.name} appears more than once.`, section.offset);
        else if (section.version !== 1) problem(`${section.name} has version ${section.version}, which this viewer does not read.`, section.offset);
        else if ((section.flags & 0x00F0) !== 0) problem(`${section.name} is compressed, which this viewer does not read.`, section.offset);
        else {
            try {
                const payload = new Payload(bytes, payloadStart, section.length);
                const content = read(payload);
                payload.end();
                contents.set(section.type, content);
                section.unreadable = false;
            } catch (error) {
                problem(`${section.name}: ${error.message}`, section.offset);
            }
        }
    }
    for (const type of [0x0001, 0x0002, 0x0003, 0x0004, 0x0010]) {
        if (readAll && !program.sections.some(section => section.type === type)) problem(`${sectionName(type)} is missing.`);
    }

    program.texts = contents.get(0x0002) ?? [];
    const lists = contents.get(0x0003) ?? [];
    program.listCount = lists.length;
    const text = index => {
        if (index < program.texts.length) return program.texts[index];
        problem(`A reference names text ${index}, which does not exist.`);
        return `?${index}`;
    };
    const textLists = new Map();
    const textList = index => {
        if (index === NO_INDEX) return [];
        if (index >= lists.length) {
            problem(`A reference names list ${index}, which does not exist.`);
            return [];
        }
        if (!textLists.has(index)) {
            const shown = lists[index].slice(0, MAX_SHOWN_LIST_ENTRIES).map(text);
            if (lists[index].length > MAX_SHOWN_LIST_ENTRIES) shown.push('…');
            textLists.set(index, shown);
        }
        return textLists.get(index);
    };
    const metadata = contents.get(0x0001);
    if (metadata) {
        program.module = text(metadata.moduleNameIndex);
        program.programVersion = metadata.programVersion;
        program.registers = metadata.registers;
        program.callDepth = metadata.callDepth;
    }
    program.compiler = contents.get(0x0030) ?? null;
    program.instructions = contents.get(0x0010)?.instructions ?? null;
    program.symbols = contents.get(0x0020)?.symbols ?? null;
    const sourceMap = contents.get(0x0021) ?? null;

    const entries = contents.get(0x0004) ?? [];
    const addresses = [...new Set(entries.map(entry => entry.entry).filter(address => address !== NO_INDEX))].sort((a, b) => a - b);
    const nextAddresses = new Map(addresses.map((address, index) => [address, addresses[index + 1] ?? Infinity]));
    program.bindings = entries.map(entry => ({
        kind: BINDING_KINDS.get(entry.kind) ?? `0x${entry.kind.toString(16).padStart(2, '0')}`,
        id: entry.id === NO_INDEX ? null : entry.id,
        name: text(entry.nameIndex), arguments: textList(entry.argumentsList),
        requiredTags: textList(entry.requiredList), excludedTags: textList(entry.excludedList),
        entry: entry.entry === NO_INDEX ? null : entry.entry,
        registers: entry.registers, callDepth: entry.callDepth,
        location: locate(entry.entry, nextAddresses.get(entry.entry), sourceMap),
    }));

    // A file without an archive can still name its sources through the map, whose
    // table index is the SourceId.
    const archive = contents.get(0x0022) ?? [];
    const archived = new Set(archive.map(source => source.id));
    program.sources = archive.map(source => ({ name: source.name, content: source.content }));
    if (sourceMap) {
        program.mappedSources = sourceMap.sources.length;
        sourceMap.sources.forEach((source, id) => {
            if (!archived.has(id)) program.sources.push({ name: source.name, byteLength: source.byteLength, content: null });
        });
    }
    return program;
}

module.exports = { readGesb };
