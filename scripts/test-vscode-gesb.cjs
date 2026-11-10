// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const extension = path.join(__dirname, '../tools/editors/VS Code');
const { readGesb } = require(path.join(extension, 'gesb-reader'));
const { registerGesbViewer, renderBody } = require(path.join(extension, 'gesb-viewer'));

const u16 = value => { const b = Buffer.alloc(2); b.writeUInt16LE(value); return b; };
const u32 = value => { const b = Buffer.alloc(4); b.writeUInt32LE(value); return b; };
const text = value => Buffer.concat([u32(Buffer.byteLength(value)), Buffer.from(value)]);

function section(type, payload, { flags = 1, version = 1 } = {}) {
    return Buffer.concat([u16(type), u16(flags), u16(version), u16(0), u32(payload.length), payload]);
}

function binding(kind, id, name, args, { required = 0xFFFF, excluded = 0xFFFF, entry = 0xFFFF, registers = 0, depth = 0 } = {}) {
    return Buffer.concat([Buffer.from([kind, 0]), ...[id, name, args, required, excluded, entry, registers, depth, 0].map(u16)]);
}

function file(...sections) {
    const size = 16 + sections.reduce((sum, item) => sum + item.length, 0);
    return Buffer.concat([Buffer.from('GESB'), u16(1), u16(0), u32(16), u32(size), ...sections]);
}

const names = ['game.demo', 'Hit', 'damage', 'Log', 'twice', 'isDead', 'Unit', ':math.distance', ':Enemy', 'boss', 'minion', '_', '<b>'];

function sample() {
    const metadata = Buffer.concat([u16(0), u16(9), u16(2), u16(0), Buffer.from([7, 0, 0, 0, 0, 0, 0, 0])]);
    const strings = Buffer.concat([u32(names.length), ...names.map(text)]);
    const lists = Buffer.concat([u32(4), u32(1), u16(2), u32(1), u16(9), u32(1), u16(10), u32(1), u16(11)]);
    const binds = [
        binding(0x10, 0, 1, 0, { required: 1, excluded: 2, entry: 0, registers: 9, depth: 2 }),
        binding(0x11, 0, 3, 3, { entry: 2, registers: 4 }),
        binding(0x12, 0, 4, 3, { entry: 4 }),
        binding(0x13, 0, 5, 3, { entry: 5 }),
        binding(0x30, 0, 6, 0, { entry: 6 }),
        binding(0x20, 0, 7, 3),
        binding(0x31, 0, 8, 3),
        binding(0x21, 5, 12, 3),
    ];
    const code = Buffer.concat([u32(7), Buffer.alloc(7 * 16)]);
    const source = 'on Hit(damage) {\n  emit Log(damage)\n}\n';
    const mapEntries = [[1, 0, 3], [3, 0, 19]];
    const map = Buffer.concat([
        u32(1), text('demo.ges'), u32(Buffer.byteLength(source)), Buffer.alloc(32), u32(4), u32(0), u32(17), u32(36), u32(38),
        u32(mapEntries.length),
        ...mapEntries.flatMap(([start, id, offset]) => [u32(start), u32(1), u32(id), u32(offset), u32(1)]),
    ]);
    const archive = Buffer.concat([u32(1), u32(0), text('demo.ges'), text(source)]);
    const symbols = Buffer.concat([u32(1), text('damage'), u32(1), Buffer.from([1, 0]), u16(0), u32(0), u32(0), u32(1)]);
    const build = Buffer.concat([text('test.compiler'), text('1.2.3')]);
    return file(
        section(0x0001, metadata), section(0x0002, strings), section(0x0003, lists),
        section(0x0004, Buffer.concat([u32(binds.length), ...binds])), section(0x0010, code),
        section(0x0020, symbols, { flags: 0 }), section(0x0021, map, { flags: 0 }), section(0x0022, archive, { flags: 0 }),
        section(0x0030, build, { flags: 0 }), section(0x8000, Buffer.from([1, 2, 3]), { flags: 0 }),
    );
}

test('reads metadata, segments, bindings and sources', () => {
    const program = readGesb(sample());
    assert.deepEqual(program.problems, []);
    assert.equal(program.module, 'game.demo');
    assert.equal(program.programVersion, '7');
    assert.equal(program.registers, 9);
    assert.equal(program.callDepth, 2);
    assert.equal(program.instructions, 7);
    assert.equal(program.symbols, 1);
    assert.deepEqual(program.compiler, { compiler: 'test.compiler', version: '1.2.3' });
    assert.deepEqual(program.sections.map(item => item.type), [1, 2, 3, 4, 0x10, 0x20, 0x21, 0x22, 0x30, 0x8000]);
    assert.equal(program.sections.at(-1).name, 'Private section');
    assert.equal(program.sections.at(-1).length, 3);
    const hit = program.bindings[0];
    assert.equal(hit.kind, 'MessageHandler');
    assert.deepEqual(hit.arguments, ['damage']);
    assert.deepEqual(hit.requiredTags, ['boss']);
    assert.deepEqual(hit.excludedTags, ['minion']);
    assert.deepEqual([hit.entry, hit.registers, hit.callDepth], [0, 9, 2]);
    assert.deepEqual(program.bindings.at(-1).id, 5);
    assert.deepEqual(hit.location, { file: 'demo.ges', line: 1 });
    assert.deepEqual(program.bindings[1].location, { file: 'demo.ges', line: 2 });
    assert.equal(program.bindings[2].location, null);
    assert.equal(program.sources[0].name, 'demo.ges');
    assert.match(program.sources[0].content, /emit Log/);
});

test('renders the groups and escapes every value from the file', () => {
    const html = renderBody(readGesb(sample()), null);
    for (const title of ['Segments', 'Inbound messages', 'Outbound messages', 'Functions', 'Predicates', 'Records', 'Extensions', 'External types', 'Sources', 'Texts']) {
        assert.match(html, new RegExp('<h2>' + title + ' '));
    }
    assert.match(html, /Hit\(damage\)/);
    assert.match(html, /Log as message/);
    assert.match(html, /matching boss without minion/);
    assert.match(html, /:math\.distance\(_\)/);
    assert.match(html, /demo\.ges:2/);
    assert.match(html, /&lt;b&gt;\(_\)/);
    assert.ok(!html.includes('<b>'));
    assert.ok(!html.includes('<script'));
    assert.equal(html.match(/class="line"/g).length, 4);
});

test('highlighted sources keep one element per line', () => {
    const html = renderBody(readGesb(sample()), (code, language, escape) => {
        assert.equal(language, 'ges');
        return code.split('\n').map(line => `<span class="ges-token-keyword">${escape(line)}</span>`);
    });
    assert.equal(html.match(/class="line"/g).length, 4);
    assert.match(html, /ges-token-keyword">on Hit/);
});

test('files with no source information say so', () => {
    const program = readGesb(fs.readFileSync(path.join(__dirname, '../examples/cli-example/example.gesb')));
    assert.deepEqual(program.problems, []);
    assert.equal(program.module, 'ges.cli.example');
    assert.equal(program.sources.length, 0);
    assert.deepEqual(program.bindings.filter(item => item.kind === 'OutboundMessage').map(item => item.name), ['ConsoleOut', 'ConsoleOut', 'ConsoleOut', 'ConsoleErr']);
    assert.match(renderBody(program, null), /no source information/);
});

test('damaged input reports problems and never throws', () => {
    const valid = sample();
    for (const [bytes, expected] of [
        [Buffer.alloc(0), /too short/],
        [Buffer.from('NOPE0000000000000000'), /GESB/],
        [Buffer.concat([Buffer.from('GESB'), u16(2), u16(0), u32(16), u32(16)]), /version 1/],
        [valid.subarray(0, 30), /declares/],
        [file(section(0x0002, Buffer.from([0xFF, 0xFF, 0xFF, 0xFF]))), /more entries/],
        [file(section(0x0002, Buffer.concat([u32(1), u32(1), Buffer.from([0xFF])]))), /StringConstantSegment/],
        [file(section(0x0002, Buffer.concat([u32(0), Buffer.from([1])]))), /after its end/],
        [file(section(0x0002, u32(0), { version: 2 })), /version 2/],
        [file(section(0x0002, u32(0), { flags: 0x11 })), /compressed/],
        [file(section(0x0002, u32(0)), section(0x0002, u32(0))), /more than once/],
        [file(section(0x0001, Buffer.alloc(16))), /text 0/],
        [file(section(0x0004, Buffer.concat([u32(1), binding(0x10, 0, 0, 4)])), section(0x0002, Buffer.concat([u32(1), text('x')]))), /list 4/],
    ]) {
        const program = readGesb(bytes);
        assert.ok(program.problems.some(problem => expected.test(problem.message)), expected + ': ' + JSON.stringify(program.problems));
        assert.match(renderBody(program, null), /<h2>Problems<\/h2>/);
    }
    for (let length = 0; length < valid.length; length += 7) renderBody(readGesb(valid.subarray(0, length)), null);
});

function mappedFile(bindings, mappings) {
    const map = Buffer.alloc(61 + mappings.length * 20);
    map.writeUInt32LE(1, 0);
    map.writeUInt32LE(1, 4);
    map.write('a', 8);
    map.writeUInt32LE(2, 45);
    map.writeUInt32LE(0, 49);
    map.writeUInt32LE(1, 53);
    map.writeUInt32LE(mappings.length, 57);
    mappings.forEach(([codeStart, byteStart], index) => {
        map.writeUInt32LE(codeStart, 61 + index * 20);
        map.writeUInt32LE(byteStart, 61 + index * 20 + 12);
    });
    return file(section(0x0002, Buffer.concat([u32(1), text('x')])), section(0x0004, Buffer.concat([u32(bindings.length), ...bindings])), section(0x0021, map, { flags: 0 }));
}

test('a routine takes the line of its first mapping, in any file order', () => {
    const bindings = [binding(0x12, 0, 0, 0xFFFF, { entry: 10 }), binding(0x12, 1, 0, 0xFFFF, { entry: 20 }), binding(0x12, 2, 0, 0xFFFF, { entry: 30 })];
    const program = readGesb(mappedFile(bindings, [[25, 1], [12, 0], [11, 1], [29, 0]]));
    assert.equal(program.sections.find(item => item.type === 0x0021).unreadable, false);
    assert.deepEqual(program.bindings.map(item => item.location?.line), [2, 2, undefined]);
});

test('many bindings and mappings do not take quadratic time', () => {
    const count = 60000;
    const bindings = Array.from({ length: count }, (_, index) => binding(0x12, index, 0, 0xFFFF, { entry: index }));
    const mappings = Array.from({ length: count }, () => [70000, 0]);
    const started = Date.now();
    const program = readGesb(mappedFile(bindings, mappings));
    assert.equal(program.bindings.length, count);
    assert.equal(program.sections.find(item => item.type === 0x0021).unreadable, false);
    assert.ok(Date.now() - started < 2000, 'took ' + (Date.now() - started) + ' ms');
});

test('the reader limits of the format apply', () => {
    const sections = Array.from({ length: 300 }, () => section(0x8000, Buffer.alloc(0), { flags: 0 }));
    const tooManySections = readGesb(file(...sections));
    assert.equal(tooManySections.sections.length, 256);
    assert.ok(tooManySections.problems.some(problem => /more than 256 segments/.test(problem.message)));
    for (const [type, entrySize] of [[0x0002, 4], [0x0003, 4], [0x0004, 20], [0x0010, 16]]) {
        const program = readGesb(file(section(type, Buffer.concat([u32(65536), Buffer.alloc(65536 * entrySize)]))));
        assert.ok(program.problems.some(problem => /the limit is 65535/.test(problem.message)), 'type ' + type);
    }
    const archive = readGesb(file(section(0x0022, Buffer.alloc(32 * 1024 * 1024 + 1), { flags: 0 })));
    assert.ok(archive.problems.some(problem => /32 MiB/.test(problem.message)));
});

test('one long shared list costs one resolution and stays short on the page', () => {
    const huge = 'h'.repeat(1000);
    const strings = Buffer.concat([u32(1), text(huge)]);
    const lists = Buffer.concat([u32(1), u32(100), Buffer.alloc(200)]);
    const bindings = Array.from({ length: 2 }, (_, index) => binding(0x10, index, 0, 0, { required: 0, entry: index }));
    const program = readGesb(file(section(0x0002, strings), section(0x0003, lists), section(0x0004, Buffer.concat([u32(2), ...bindings]))));
    assert.equal(program.bindings[0].arguments.length, 65);
    assert.equal(program.bindings[0].arguments.at(-1), '…');
    assert.equal(program.bindings[0].arguments, program.bindings[1].arguments);
    const html = renderBody(program, null);
    assert.ok(!html.includes(huge));
    assert.equal(html.match(/<td class="code">(.*?)<\/td>/)[1].length <= 201 * 6, true);
});

test('sources are joined by id, not by name', () => {
    const entry = (name, size) => Buffer.concat([text(name), u32(size), Buffer.alloc(32), u32(1), u32(0)]);
    const map = Buffer.concat([u32(2), entry('same.ges', 1), entry('same.ges', 2), u32(0)]);
    const archive = Buffer.concat([u32(1), u32(0), text('same.ges'), text('x')]);
    const program = readGesb(file(section(0x0021, map, { flags: 0 }), section(0x0022, archive, { flags: 0 })));
    assert.deepEqual(program.sources.map(source => source.content), ['x', null]);
    assert.equal(program.mappedSources, 2);
});

test('an unreadable segment does not look empty', () => {
    const broken = Buffer.concat([u32(1), u32(1), Buffer.from([0xFF])]);
    const html = renderBody(readGesb(file(section(0x0002, broken), section(0x0021, Buffer.from([1]), { flags: 0 }))), null);
    assert.ok(!html.includes('0 texts'));
    assert.ok(!html.includes('0 sources'));
    assert.match(html, /StringConstantSegment<\/td>.*?Not readable/);
    assert.match(html, /source information of this file is not readable/);
    assert.ok(!html.includes('no source information'));
});

test('a file that is no readable program shows no invented structure', () => {
    for (const bytes of [Buffer.alloc(5), Buffer.from('NOPE0000000000000000')]) {
        const html = renderBody(readGesb(bytes), null);
        assert.ok(!html.includes('File header'));
        assert.ok(!html.includes('source information'));
        assert.match(html, /<h2>Problems<\/h2>/);
    }
    const newer = renderBody(readGesb(Buffer.concat([Buffer.from('GESB'), u16(2), u16(0), u32(16), u32(16)])), null);
    assert.match(newer, /Format version 2/);
    assert.ok(!newer.includes('source information'));
});

test('source text has limits across the whole file', () => {
    const source = 'a\n'.repeat(400 * 1024);
    const names = Array.from({ length: 6 }, (_, index) => 'file' + index);
    const archive = Buffer.concat([u32(names.length), ...names.flatMap((name, index) => [u32(index), text(name), text(source)])]);
    let highlighted = 0;
    const html = renderBody(readGesb(file(section(0x0022, archive, { flags: 0 }))), code => {
        highlighted += code.length;
        return code.split('\n');
    });
    assert.ok(highlighted > 0 && highlighted <= 1024 * 1024, 'highlighted ' + highlighted);
    assert.equal(html.match(/<pre class="source">/g).length, 5);
    assert.match(html, /file5<\/code>, 800\.0 KB: too large to show here/);
});

test('the source column shows a short file name and the sources keep the path', () => {
    const named = name => {
        const map = Buffer.concat([u32(1), text(name), u32(1), Buffer.alloc(32), u32(1), u32(0), u32(1), u32(0), u32(1), u32(0), u32(0), u32(1)]);
        const strings = Buffer.concat([u32(1), text('x')]);
        const binds = Buffer.concat([u32(2), binding(0x12, 0, 0, 0xFFFF, { entry: 0 }), binding(0x12, 1, 0, 0xFFFF, { entry: 1 })]);
        return renderBody(readGesb(file(section(0x0002, strings), section(0x0004, binds), section(0x0021, map, { flags: 0 }))), null);
    };
    for (const name of ['/Users/someone/game/main.ges', 'C:\\game\\main.ges']) {
        const html = named(name);
        assert.match(html, /<td class="code">main\.ges:1<\/td>/);
        assert.ok(html.includes(`<code>${name}</code>`));
    }
    const long = named('n'.repeat(1024 * 1024));
    assert.ok(long.length < 10000, 'page has ' + long.length + ' characters');
});

test('sources with the same file name stay apart', () => {
    const entry = name => Buffer.concat([text(name), u32(1), Buffer.alloc(32), u32(1), u32(0)]);
    const mapping = (start, source) => [u32(start), u32(1), u32(source), u32(0), u32(1)];
    const map = Buffer.concat([u32(3), entry('/game/a/main.ges'), entry('/game/b/main.ges'), entry('/game/other.ges'), u32(3), ...mapping(0, 0), ...mapping(1, 1), ...mapping(2, 2)]);
    const binds = Buffer.concat([u32(3), ...[0, 1, 2].map(index => binding(0x12, index, 0, 0xFFFF, { entry: index }))]);
    const program = readGesb(file(section(0x0002, Buffer.concat([u32(1), text('x')])), section(0x0004, binds), section(0x0021, map, { flags: 0 })));
    const html = renderBody(program, null);
    for (const label of ['/game/a/main.ges:1', '/game/b/main.ges:1', 'other.ges:1']) assert.ok(html.includes(`<td class="code">${label}</td>`), label);
    assert.ok(!html.includes('<td class="code">main.ges:1</td>'));
    assert.deepEqual(program.bindings[0].location, { file: '/game/a/main.ges', line: 1 });
});

test('long paths keep their file name and surrogate pairs', () => {
    const map = name => Buffer.concat([u32(1), text(name), u32(1), Buffer.alloc(32), u32(1), u32(0), u32(0)]);
    for (const name of ['/' + 'd'.repeat(500) + '/main.ges', '/' + '😀'.repeat(300) + '.ges', '/' + '😀'.repeat(300) + 'x.ges']) {
        const html = renderBody(readGesb(file(section(0x0021, map(name), { flags: 0 }))), null);
        const shown = html.match(/<code>(.*?)<\/code>/)[1];
        assert.ok(shown.startsWith('…') && shown.endsWith('.ges') && shown.length <= 200, shown.length + '');
        assert.ok(!/[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/.test(shown));
    }
});

test('a cut problem list says so', () => {
    const sections = Array.from({ length: 30 }, (_, index) => section(0x0001, Buffer.alloc(0), { version: index + 2 }));
    const program = readGesb(file(...sections));
    assert.equal(program.problems.length, 21);
    assert.equal(program.problems.at(-1).message, 'Further problems are not listed.');
    assert.match(renderBody(program, null), /Further problems are not listed/);
    assert.ok(readGesb(sample()).problems.every(problem => !/Further/.test(problem.message)));
});

test('a damaged archive is not reported as a missing embedding', () => {
    const map = Buffer.concat([u32(1), text('a.ges'), u32(1), Buffer.alloc(32), u32(1), u32(0), u32(0)]);
    const damaged = renderBody(readGesb(file(section(0x0021, map, { flags: 0 }), section(0x0022, Buffer.from([1, 2, 3]), { flags: 0 }))), null);
    assert.match(damaged, /its embedded text is not readable/);
    assert.ok(!damaged.includes('does not embed'));
    assert.match(renderBody(readGesb(file(section(0x0021, map, { flags: 0 }))), null), /does not embed this source/);
});

test('a source the tokenizer cannot handle stays readable and leaves the rest highlighted', () => {
    const names = ['first', 'second'];
    const archive = Buffer.concat([u32(2), ...names.flatMap((name, index) => [u32(index), text(name), text(name + ' body')])]);
    const html = renderBody(readGesb(file(section(0x0022, archive, { flags: 0 }))), code => {
        if (code.startsWith('first')) throw new Error('tokenizer failed');
        return [`<span class="ges-token-keyword">${code}</span>`];
    });
    assert.match(html, /<span class="line">first body<\/span>/);
    assert.match(html, /ges-token-keyword">second body/);
    assert.ok(!html.includes('tokenizer failed'));
});

test('segments beyond the limit do not count as missing', () => {
    const many = Array.from({ length: 256 }, () => section(0x8000, Buffer.alloc(0), { flags: 0 }));
    const messages = readGesb(file(...many, section(0x0010, u32(0)))).problems.map(problem => problem.message);
    assert.deepEqual(messages, ['The file has more than 256 segments; the rest is not shown.']);
});

test('debug symbol names are neither decoded nor needed', () => {
    const debug = Buffer.concat([u32(2), u32(1), Buffer.from([0xFF]), text('x'), u32(0)]);
    const program = readGesb(file(section(0x0020, debug, { flags: 0 })));
    assert.equal(program.symbols, 0);
    assert.equal(program.sections[0].unreadable, false);
    const truncated = readGesb(file(section(0x0020, Buffer.concat([u32(1), u32(9), Buffer.from([1]), u32(0)]), { flags: 0 })));
    assert.equal(truncated.sections[0].unreadable, true);
});

test('shortening keeps surrogate pairs whole', () => {
    const lone = /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/;
    for (const prefix of [199, 198]) {
        const value = 'a'.repeat(prefix) + '😀'.repeat(3);
        const html = renderBody(readGesb(file(section(0x0002, Buffer.concat([u32(1), text(value)])))), null);
        assert.ok(!lone.test(html), 'prefix ' + prefix);
        assert.ok(html.includes('…'));
    }
});

test('the first shown source is open, also when an earlier one is too large', () => {
    const large = 'a'.repeat(4 * 1024 * 1024 + 1);
    const archive = Buffer.concat([u32(3), u32(0), text('large'), text(large), u32(1), text('second'), text('b'), u32(2), text('third'), text('c')]);
    const html = renderBody(readGesb(file(section(0x0022, archive, { flags: 0 }))), null);
    assert.match(html, /large<\/code>.*too large to show/);
    assert.match(html, /<details open><summary><code>second</);
    assert.match(html, /<details><summary><code>third</);
});

test('an unknown binding kind stays visible', () => {
    const strings = Buffer.concat([u32(1), text('x')]);
    const program = readGesb(file(section(0x0002, strings), section(0x0004, Buffer.concat([u32(1), binding(0x7F, 0, 0, 0xFFFF)]))));
    assert.equal(program.bindings[0].kind, '0x7f');
    assert.match(renderBody(program, null), /Other bindings/);
});

test('every checked-in binary is readable', () => {
    const root = path.join(__dirname, '..');
    const directories = ['conformance/fixtures/GesbV1', 'conformance/fixtures/Release030', 'examples/cli-example', 'examples/compute-benchmark'];
    let count = 0;
    for (const directory of directories) {
        for (const name of fs.readdirSync(path.join(root, directory)).filter(item => item.endsWith('.gesb'))) {
            renderBody(readGesb(fs.readFileSync(path.join(root, directory, name))), null);
            count++;
        }
    }
    assert.ok(count > 100);
});

function viewerHarness({ bytes = sample(), size = bytes.length, name = 'demo.gesb', readFile = async () => bytes, stat = async () => ({ size }), highlighter = null } = {}) {
    const listeners = {};
    const disposed = [];
    let provider, options;
    const disposable = name => ({ dispose() { disposed.push(name); } });
    const vscode = {
        Uri: { joinPath: (base, ...parts) => ({ ...base, path: base.path + '/' + parts.join('/'), toString() { return this.path; } }) },
        RelativePattern: class { constructor(base, pattern) { this.base = base; this.pattern = pattern; } },
        window: { registerCustomEditorProvider(view, value, viewOptions) { provider = { view, value }; options = viewOptions; return disposable('provider'); } },
        workspace: {
            fs: { stat: (...args) => stat(...args), readFile: (...args) => readFile(...args) },
            createFileSystemWatcher(pattern) {
                listeners.pattern = pattern.pattern;
                return { pattern, ...disposable('watcher'),
                    onDidDelete(callback) { listeners.delete = callback; return disposable('delete'); },
                    onDidChange(callback) { listeners.change = callback; return disposable('change'); },
                    onDidCreate(callback) { listeners.create = callback; return disposable('create'); } };
            },
        },
    };
    registerGesbViewer(vscode, { extensionUri: { path: '/extension' } }, Promise.resolve(highlighter));
    let html = '', closed = false;
    const panel = {
        webview: {
            cspSource: 'test-csp', asWebviewUri: uri => uri, options: null,
            get html() { return html; },
            set html(value) {
                if (closed) throw new Error('Webview is disposed');
                html = value;
            },
        },
        onDidDispose(callback) { listeners.dispose = () => { closed = true; callback(); }; },
    };
    const document = { uri: { path: '/folder/' + name } };
    return { provider, options, panel, document, listeners, disposed, setBytes(value) { bytes = value; } };
}

test('the editor runs no script, restricts its resources and follows file changes', async () => {
    const h = viewerHarness();
    assert.equal(h.provider.view, 'gameeventscript.gesbViewer');
    assert.equal(h.options.supportsMultipleEditorsPerDocument, true);
    await h.provider.value.resolveCustomEditor(await h.provider.value.openCustomDocument(h.document.uri), h.panel);
    assert.equal(h.panel.webview.options.enableScripts, false);
    assert.deepEqual(h.panel.webview.options.localResourceRoots, [{ path: '/extension' }]);
    assert.match(h.panel.webview.html, /default-src 'none'; style-src test-csp;/);
    assert.ok(!h.panel.webview.html.includes('<style'));
    assert.match(h.panel.webview.html, /game\.demo/);
    h.setBytes(Buffer.from('broken'));
    await h.listeners.change();
    assert.match(h.panel.webview.html, /too short/);
    h.listeners.dispose();
    assert.deepEqual(h.disposed.sort(), ['change', 'create', 'delete', 'watcher']);
});

test('a deleted file does not stay on the page as if it were current', async () => {
    let missing = false;
    const h = viewerHarness({ stat: async () => {
        if (missing) throw new Error('The file is gone.');
        return { size: 1 };
    } });
    await h.provider.value.resolveCustomEditor(h.document, h.panel);
    assert.match(h.panel.webview.html, /game\.demo/);
    missing = true;
    await h.listeners.delete();
    assert.match(h.panel.webview.html, /The file is gone/);
    assert.ok(!h.panel.webview.html.includes('game.demo'));
});

test('a missing file is reported in plain words, other errors unchanged', async () => {
    for (const [error, expected] of [
        [Object.assign(new Error('raw text'), { code: 'FileNotFound' }), /The file does not exist/],
        [Object.assign(new Error('raw text'), { code: 'ENOENT' }), /The file does not exist/],
        [new Error("Error: ENOENT: no such file or directory, stat '/a.gesb'"), /The file does not exist/],
        [new Error('EntryNotFound (FileSystemError): /a.gesb'), /The file does not exist/],
        [new Error('NoPermissions <b>'), /NoPermissions &lt;b&gt;/],
    ]) {
        const h = viewerHarness({ stat: async () => { throw error; } });
        await h.provider.value.resolveCustomEditor(h.document, h.panel);
        assert.match(h.panel.webview.html, expected);
        assert.ok(!h.panel.webview.html.includes('ENOENT'));
        h.listeners.dispose();
    }
});

test('a change the starting watcher missed appears through the second look', async t => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    let mtime = 1;
    const h = viewerHarness({ stat: async () => ({ size: 1, mtime }) });
    await h.provider.value.resolveCustomEditor(h.document, h.panel);
    assert.match(h.panel.webview.html, /game\.demo/);
    h.setBytes(Buffer.from('broken'));
    mtime = 2;
    t.mock.timers.tick(1000);
    await new Promise(setImmediate);
    await new Promise(setImmediate);
    assert.match(h.panel.webview.html, /too short/);
    h.listeners.dispose();
});

test('the second look renders nothing for an unchanged file or a closed panel', async t => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    let reads = 0;
    const unchanged = viewerHarness({ stat: async () => ({ size: 1, mtime: 1 }), readFile: async () => { reads++; return sample(); } });
    await unchanged.provider.value.resolveCustomEditor(unchanged.document, unchanged.panel);
    t.mock.timers.tick(1000);
    await new Promise(setImmediate);
    assert.equal(reads, 1);
    unchanged.listeners.dispose();

    let mtime = 1, stats = 0;
    const closed = viewerHarness({ stat: async () => { stats++; return { size: 1, mtime }; } });
    await closed.provider.value.resolveCustomEditor(closed.document, closed.panel);
    closed.listeners.dispose();
    mtime = 2;
    t.mock.timers.tick(1000);
    await new Promise(setImmediate);
    assert.equal(stats, 1);
});

test('closing the panel while a file is read leaves no failing write', async () => {
    let finishRead;
    const h = viewerHarness({ readFile: () => new Promise(resolve => { finishRead = resolve; }) });
    const resolved = h.provider.value.resolveCustomEditor(h.document, h.panel);
    await new Promise(setImmediate);
    h.listeners.dispose();
    finishRead(sample());
    await resolved;
    assert.equal(h.panel.webview.html, '');
});

test('a read that a newer one overtook is not parsed or rendered', async () => {
    const reads = [];
    let highlighted = 0;
    const h = viewerHarness({
        readFile: () => new Promise(resolve => { reads.push(resolve); }),
        highlighter: code => { highlighted++; return code.split('\n'); },
    });
    const first = h.provider.value.resolveCustomEditor(h.document, h.panel);
    await new Promise(setImmediate);
    const second = h.listeners.change();
    await new Promise(setImmediate);
    assert.equal(reads.length, 2);
    reads[0](sample());
    await first;
    assert.equal(h.panel.webview.html, '');
    assert.equal(highlighted, 0);
    reads[1](sample());
    await second;
    assert.equal(highlighted, 1);
    assert.match(h.panel.webview.html, /game\.demo/);
});

test('glob characters in the file name match themselves', async () => {
    const h = viewerHarness({ name: 'a[1]{b}*?.gesb' });
    await h.provider.value.resolveCustomEditor(h.document, h.panel);
    assert.equal(h.listeners.pattern, 'a[[]1[]][{]b[}][*][?].gesb');
});

test('oversized files are refused before reading', async () => {
    const h = viewerHarness({ bytes: null, size: 64 * 1024 * 1024 + 1 });
    await h.provider.value.resolveCustomEditor(h.document, h.panel);
    assert.match(h.panel.webview.html, /the limit is 64\.0 MB/);
});
