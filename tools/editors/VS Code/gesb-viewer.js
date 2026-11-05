// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';

const { readGesb } = require('./gesb-reader');

// The default reader limit of specs/BinaryFormat.md.
const MAX_FILE_SIZE = 64 * 1024 * 1024;
// Per file: tokenizing and rendering block the extension host.
const MAX_HIGHLIGHTED_SOURCES = 1024 * 1024;
const MAX_SHOWN_SOURCES = 4 * 1024 * 1024;
const MAX_TEXT_LENGTH = 200;
const WATCHER_START_TIME = 1000;

const escapeHtml = text => String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

function shortened(text) {
    if (text.length <= MAX_TEXT_LENGTH) return text;
    // The cut must not separate the two halves of a surrogate pair.
    const splitsPair = (text.charCodeAt(MAX_TEXT_LENGTH - 1) & 0xFC00) === 0xD800;
    return text.slice(0, splitsPair ? MAX_TEXT_LENGTH - 1 : MAX_TEXT_LENGTH) + '…';
}

function formatSize(bytes) {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const hex = (value, width) => '0x' + value.toString(16).toUpperCase().padStart(width, '0');

const plural = (count, one, many = one + 's') => `${count} ${count === 1 ? one : many}`;

// Keeps the end of a path, which names the file.
function shortenedPath(path) {
    if (path.length <= MAX_TEXT_LENGTH) return path;
    const start = path.length - MAX_TEXT_LENGTH + 1;
    const splitsPair = (path.charCodeAt(start) & 0xFC00) === 0xDC00;
    return '…' + path.slice(splitsPair ? start + 1 : start);
}

// Shortening each text first keeps one huge text from being joined for every row.
const joined = texts => texts.map(shortened).join(', ');

const literalGlob = name => name.replace(/[[\]{}*?]/g, '[$&]');

function signature(binding) {
    const name = shortened(binding.name);
    if (binding.kind === 'MessageNameHandler') return `${name} as message`;
    return `${name}(${joined(binding.arguments)})`;
}

function filter(binding) {
    const parts = [];
    if (binding.requiredTags.length > 0) parts.push('matching ' + joined(binding.requiredTags));
    if (binding.excludedTags.length > 0) parts.push('without ' + joined(binding.excludedTags));
    return parts.join(' ');
}

const fileName = name => name.slice(Math.max(name.lastIndexOf('/'), name.lastIndexOf('\\')) + 1);

// Sources compiled from the editor carry absolute paths, so the table names a file
// by itself unless another source of the program has the same file name.
function withSourceLabels(bindings) {
    const names = new Map();
    for (const { location } of bindings) {
        if (location && !names.has(location.file)) names.set(location.file, fileName(location.file));
    }
    const uses = new Map();
    for (const name of names.values()) uses.set(name, (uses.get(name) ?? 0) + 1);
    return bindings.map(binding => {
        if (!binding.location) return { ...binding, source: '' };
        const { file, line } = binding.location;
        const name = names.get(file);
        return { ...binding, source: `${shortenedPath(uses.get(name) > 1 ? file : name)}:${line}` };
    });
}

function table(columns, rows) {
    const head = columns.map(column => `<th${column.numeric ? ' class="number"' : ''}>${escapeHtml(column.title)}</th>`).join('');
    const body = rows.map(row => '<tr>' + columns.map(column => {
        const classes = [column.numeric && 'number', column.code && 'code'].filter(Boolean).join(' ');
        return `<td${classes ? ` class="${classes}"` : ''}>${escapeHtml(column.value(row))}</td>`;
    }).join('') + '</tr>').join('\n');
    return `<div class="table"><table><thead><tr>${head}</tr></thead><tbody>\n${body}\n</tbody></table></div>`;
}

const signatureColumn = { title: 'Signature', code: true, value: row => shortened(signature(row)) };
const entryColumn = { title: 'Entry', numeric: true, value: row => row.entry ?? '' };
const sourceColumn = { title: 'Source', code: true, value: row => row.source };
const executableColumns = [entryColumn, sourceColumn];
// Only handlers declare resource requirements.
const handlerColumns = [
    entryColumn,
    { title: 'Registers', numeric: true, value: row => row.registers },
    { title: 'Call depth', numeric: true, value: row => row.callDepth },
    sourceColumn,
];

function bindingGroups(allBindings) {
    const bindings = withSourceLabels(allBindings);
    const known = ['MessageHandler', 'MessageNameHandler', 'OutboundMessage', 'Function', 'Predicate', 'Record', 'ExtensionCall', 'ExternalType'];
    const groups = [
        ['Inbound messages', ['MessageHandler', 'MessageNameHandler'], [signatureColumn, { title: 'Tags', code: true, value: row => shortened(filter(row)) }, ...handlerColumns]],
        ['Outbound messages', ['OutboundMessage'], [signatureColumn, { title: 'Id', numeric: true, value: row => row.id ?? '' }]],
        ['Functions', ['Function'], [signatureColumn, ...executableColumns]],
        ['Predicates', ['Predicate'], [signatureColumn, ...executableColumns]],
        ['Records', ['Record'], [signatureColumn, ...executableColumns]],
        ['Extensions', ['ExtensionCall'], [signatureColumn]],
        ['External types', ['ExternalType'], [signatureColumn]],
    ];
    const sections = groups.map(([title, kinds, columns]) => {
        const rows = bindings.filter(binding => kinds.includes(binding.kind));
        return rows.length === 0 ? '' : `<section><h2>${title} <span class="count">${rows.length}</span></h2>\n${table(columns, rows)}</section>`;
    });
    const others = bindings.filter(binding => !known.includes(binding.kind));
    if (others.length > 0) {
        sections.push(`<section><h2>Other bindings <span class="count">${others.length}</span></h2>\n${table([{ title: 'Kind', code: true, value: row => row.kind }, signatureColumn], others)}</section>`);
    }
    return sections.filter(Boolean).join('\n');
}

function summary(section, program) {
    if (section.unreadable) return 'Not readable';
    switch (section.type) {
        case 0x0001: return program.module === null ? '' : `Module ${shortened(program.module)}`;
        case 0x0002: return plural(program.texts.length, 'text');
        case 0x0003: return plural(program.listCount, 'list');
        case 0x0004: return plural(program.bindings.length, 'binding');
        case 0x0010: return program.instructions === null ? '' : plural(program.instructions, 'instruction');
        case 0x0020: return program.symbols === null ? '' : plural(program.symbols, 'symbol');
        case 0x0021: return plural(program.mappedSources, 'source');
        case 0x0022: return plural(program.sources.filter(source => source.content !== null).length, 'embedded source');
        case 0x0030: return program.compiler ? `${shortened(program.compiler.compiler)} ${shortened(program.compiler.version)}` : '';
        default: return '';
    }
}

function segments(program) {
    if (program.formatVersion === null) return '';
    const share = bytes => program.fileSize === 0 ? '' : `${(bytes * 100 / program.fileSize).toFixed(1)} %`;
    const rows = [
        { offset: 0, name: 'File header', type: '', size: 16, flags: '', content: `Format version ${program.formatVersion ?? '?'}` },
        ...program.sections.map(section => ({
            offset: section.offset, name: section.name, type: hex(section.type, 4), size: section.length,
            flags: section.required ? 'required' : 'optional', content: summary(section, program),
        })),
    ];
    const columns = [
        { title: 'Offset', numeric: true, value: row => hex(row.offset, 8) },
        { title: 'Segment', value: row => row.name },
        { title: 'Type', code: true, value: row => row.type },
        { title: 'Size', numeric: true, value: row => formatSize(row.size) },
        { title: 'Share', numeric: true, value: row => share(row.size) },
        { title: 'Use', value: row => row.flags },
        { title: 'Content', value: row => row.content },
    ];
    return `<section><h2>Segments <span class="count">${program.sections.length}</span></h2>\n${table(columns, rows)}</section>`;
}

function sourceLines(content, highlight, budget) {
    const text = content.replace(/\r\n?/g, '\n');
    let lines = null;
    if (highlight && text.length <= budget.highlighted) {
        budget.highlighted -= text.length;
        // A source the tokenizer cannot handle stays readable as plain text.
        try { lines = highlight(text, 'ges', escapeHtml); } catch { lines = null; }
    }
    lines ??= text.split('\n').map(escapeHtml);
    return lines.map(line => `<span class="line">${line}</span>`).join('');
}

function sources(program, highlight) {
    if (program.formatVersion !== 1) return '';
    const unreadable = type => program.sections.some(section => section.type === type && section.unreadable);
    if (program.sources.length === 0) {
        return `<section><h2>Sources</h2>\n<p class="note">${unreadable(0x0021) || unreadable(0x0022) ? 'The source information of this file is not readable.' : 'This file has no source information.'}</p></section>`;
    }
    const budget = { highlighted: MAX_HIGHLIGHTED_SOURCES, shown: MAX_SHOWN_SOURCES };
    const missing = unreadable(0x0022) ? 'its embedded text is not readable' : 'the file does not embed this source';
    let opened = false;
    const items = program.sources.map(source => {
        if (source.content === null) {
            return `<p class="note"><code>${escapeHtml(shortenedPath(source.name))}</code>, ${formatSize(source.byteLength)}: ${missing}.</p>`;
        }
        const size = formatSize(new TextEncoder().encode(source.content).length);
        if (source.content.length > budget.shown) {
            return `<p class="note"><code>${escapeHtml(shortenedPath(source.name))}</code>, ${size}: too large to show here.</p>`;
        }
        budget.shown -= source.content.length;
        const open = !opened;
        opened = true;
        return `<details${open ? ' open' : ''}><summary><code>${escapeHtml(shortenedPath(source.name))}</code> <span class="count">${size}</span></summary>\n<pre class="source">${sourceLines(source.content, highlight, budget)}</pre></details>`;
    });
    return `<section><h2>Sources <span class="count">${program.sources.length}</span></h2>\n${items.join('\n')}</section>`;
}

function texts(program) {
    if (program.texts.length === 0) return '';
    const rows = program.texts.map((text, index) => ({ index, text }));
    const content = table([
        { title: 'Index', numeric: true, value: row => row.index },
        { title: 'Text', code: true, value: row => shortened(row.text) },
    ], rows);
    return `<section><details><summary><h2>Texts <span class="count">${rows.length}</span></h2></summary>\n${content}</details></section>`;
}

function problems(program) {
    if (program.problems.length === 0) return '';
    const items = program.problems.map(problem => `<li>${escapeHtml(problem.message)}${problem.offset === null ? '' : ` <span class="count">at ${hex(problem.offset, 8)}</span>`}</li>`);
    return `<section class="problems"><h2>Problems</h2>\n<ul>${items.join('')}</ul></section>`;
}

function facts(program) {
    const values = [
        ['Size', formatSize(program.fileSize)],
        ['Program version', program.programVersion],
        ['Registers', program.registers],
        ['Call depth', program.callDepth],
        ['Compiler', program.compiler && `${shortened(program.compiler.compiler)} ${shortened(program.compiler.version)}`],
    ].filter(([, value]) => value !== null);
    return `<dl>${values.map(([name, value]) => `<div><dt>${name}</dt><dd>${escapeHtml(value)}</dd></div>`).join('')}</dl>`;
}

function renderBody(program, highlight) {
    const title = program.module === null ? 'GES binary' : shortened(program.module);
    return [`<h1>${escapeHtml(title)}</h1>`, facts(program), problems(program), segments(program), bindingGroups(program.bindings), sources(program, highlight), texts(program)]
        .filter(Boolean).join('\n');
}

function page(webview, styles, body) {
    const links = styles.map(uri => `<link rel="stylesheet" href="${escapeHtml(webview.asWebviewUri(uri).toString())}">`).join('');
    return `<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource};">
<meta name="viewport" content="width=device-width, initial-scale=1">${links}</head><body>
${body}
</body></html>`;
}

// VS Code reports a missing file by code or, depending on the provider, only in the message.
const isMissingFile = error => error.code === 'FileNotFound' || error.code === 'ENOENT' || /\b(ENOENT|EntryNotFound)\b/.test(error.message);

// The page runs no script and every value is escaped, because a binary file is
// untrusted input. This keeps the viewer usable in untrusted workspaces.
function registerGesbViewer(vscode, context, highlighter) {
    const styles = ['markdown-preview.css', 'gesb-viewer.css'].map(name => vscode.Uri.joinPath(context.extensionUri, name));
    const provider = {
        openCustomDocument: uri => ({ uri, dispose() {} }),
        async resolveCustomEditor(document, panel) {
            panel.webview.options = { enableScripts: false, localResourceRoots: [context.extensionUri] };
            let sequence = 0;
            let readTime = null;
            const render = async () => {
                const current = ++sequence;
                readTime = null;
                let body;
                try {
                    const { size, mtime } = await vscode.workspace.fs.stat(document.uri);
                    readTime = mtime;
                    if (size > MAX_FILE_SIZE) throw new Error(`The file has ${formatSize(size)}; the limit is ${formatSize(MAX_FILE_SIZE)}.`);
                    const bytes = await vscode.workspace.fs.readFile(document.uri);
                    const highlight = await highlighter;
                    // Only the latest read reaches the synchronous parsing and rendering.
                    if (current !== sequence) return;
                    body = renderBody(readGesb(bytes), highlight);
                } catch (error) {
                    body = `<section class="problems"><h2>Problems</h2>\n<p>${isMissingFile(error) ? 'The file does not exist. This page updates when it returns.' : escapeHtml(error.message)}</p></section>`;
                }
                if (current === sequence) panel.webview.html = page(panel.webview, styles, body);
            };
            // A recompilation shows up without reopening the file.
            const directory = vscode.Uri.joinPath(document.uri, '..');
            const name = document.uri.path.slice(document.uri.path.lastIndexOf('/') + 1);
            const watcher = vscode.workspace.createFileSystemWatcher(new vscode.RelativePattern(directory, literalGlob(name)));
            const subscriptions = [watcher, watcher.onDidChange(render), watcher.onDidCreate(render), watcher.onDidDelete(render)];
            let disposed = false;
            let recheck = null;
            panel.onDidDispose(() => {
                disposed = true;
                clearTimeout(recheck);
                // A render still reading the file must not touch the closed panel.
                sequence++;
                for (const subscription of subscriptions) subscription.dispose();
            });
            await render();
            // A watcher takes a moment to start and misses a change right after opening.
            if (!disposed) {
                recheck = setTimeout(async () => {
                    const { mtime } = await vscode.workspace.fs.stat(document.uri).then(stat => stat, () => ({ mtime: null }));
                    if (!disposed && mtime !== readTime) await render();
                }, WATCHER_START_TIME);
            }
        },
    };
    return vscode.window.registerCustomEditorProvider('gameeventscript.gesbViewer', provider, {
        supportsMultipleEditorsPerDocument: true,
    });
}

module.exports = { registerGesbViewer, renderBody };
