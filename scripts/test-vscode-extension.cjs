// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../tools/editors/VS Code/extension.js'), 'utf8');

function harness({ trusted = true, save = true, failure = null, input = undefined } = {}) {
    const calls = [], tasks = [], errors = [], shown = [], commands = new Map();
    const terminals = [], terminalListeners = new Set();
    const disposable = { dispose() {} };
    let provider, close;
    const document = {
        languageId: 'ges', isDirty: true,
        uri: { scheme: 'file', fsPath: path.resolve('a directory', "cards ' $(touch injected).ges") },
        async save() { calls.push('save'); return save; },
    };
    const vscode = {
        workspace: {
            isTrusted: trusted,
            registerTextDocumentContentProvider(_scheme, value) { provider = value; return disposable; },
            onDidCloseTextDocument(callback) { close = callback; return disposable; },
            getConfiguration() { return { get() { return '/a directory/ges'; } }; },
            getWorkspaceFolder() { return undefined; },
            async openTextDocument(uri) { return { uri, text: provider.provideTextDocumentContent(uri) }; },
        },
        window: {
            onDidCloseTerminal(callback) { terminalListeners.add(callback); return { dispose() { terminalListeners.delete(callback); } }; },
            createTerminal(options) {
                const terminal = { options, disposed: false, show() {}, dispose() {
                    this.disposed = true;
                    for (const listener of [...terminalListeners]) listener(this);
                } };
                terminals.push(terminal);
                return terminal;
            },
            async showInputBox() { return input; },
            activeTextEditor: { document },
            createOutputChannel() { return { append() {}, appendLine() {}, show() {}, dispose() {} }; },
            async withProgress(_options, callback) {
                return callback({}, { isCancellationRequested: false, onCancellationRequested() { return disposable; } });
            },
            async showTextDocument(doc) { shown.push(doc); },
            async showErrorMessage(message) { errors.push(message); },
        },
        languages: { async setTextDocumentLanguage(doc, language) { doc.languageId = language; } },
        commands: { registerCommand(name, fn) { commands.set(name, fn); return disposable; } },
        Task: class { constructor(...args) { this.args = args; } },
        ProcessExecution: class { constructor(...args) { this.args = args; } },
        TaskScope: { Global: 1 }, TaskRevealKind: { Always: 1 }, TaskPanelKind: { Dedicated: 1 },
        ProgressLocation: { Notification: 1 },
        tasks: { async executeTask(task) { tasks.push(task); } },
        Uri: { from(value) { return { ...value, toString() { return `${this.scheme}:${this.path}?${this.query}`; } }; } },
    };
    const child = { execFile(cli, args, options, callback) {
        calls.push({ cli, args, options });
        setImmediate(() => callback(args[0] === failure ? new Error('compile rejected') : null,
            args[0] === 'dump' ? '.region "Code"\nReturnVoid\n' : '', ''));
        return { kill() {} };
    } };
    const module = { exports: {} };
    vm.runInNewContext(source, { module, process, require(name) {
        if (name === './markdown-preview') return { async createMarkdownExtension() { return md => md; } };
        return name === 'vscode' ? vscode : name === 'node:child_process' ? child : require(name);
    } });
    const ready = module.exports.activate({ subscriptions: [] });
    return { calls, tasks, errors, shown, document, terminals, parseArguments: module.exports.parseArguments,
        provider: () => provider, close: doc => close(doc),
        run: async action => { await ready; return commands.get('ges.' + action)(); } };
}

test('check, run and compile save first and pass literal paths to process tasks', async () => {
    for (const action of ['check', 'run', 'compile']) {
        const h = harness();
        await h.run(action);
        assert.equal(h.calls[0], 'save');
        const execution = h.tasks[0].args[4];
        // Direct process tasks use VS Code's built-in type, not an unregistered provider.
        assert.equal(h.tasks[0].args[0].type, 'process');
        assert.equal(execution.args[0], '/a directory/ges');
        assert.deepEqual(Array.from(execution.args[1]), [action, h.document.uri.fsPath]);
        assert.equal(execution.args[2].cwd, path.dirname(h.document.uri.fsPath));
        assert.equal(h.errors.length, 0);
    }
});

test('save cancellation and untrusted workspaces never execute CLI', async () => {
    for (const options of [{ save: false }, { trusted: false }]) {
        for (const action of ['run', 'dump']) {
            const h = harness(options);
            await h.run(action);
            assert.equal(h.tasks.length, 0);
            assert.equal(h.calls.filter(x => typeof x === 'object').length, 0);
        }
    }
});

test('dump compiles first, opens GESA through readonly provider, cleans temporary files', async () => {
    const h = harness();
    await h.run('dump');
    const compile = h.calls[1], dump = h.calls[2];
    assert.equal(compile.args[0], 'compile');
    assert.equal(compile.args[1], h.document.uri.fsPath);
    assert.equal(compile.args[2], '-o');
    assert.deepEqual(Array.from(dump.args), ['dump', compile.args[3]]);
    assert.equal(fs.existsSync(path.dirname(compile.args[3])), false);
    assert.equal(h.shown[0].languageId, 'gesa');
    assert.match(h.shown[0].text, /ReturnVoid/);
    h.close(h.shown[0]);
    assert.equal(h.provider().provideTextDocumentContent(h.shown[0].uri), '');
});

test('compile failure never dumps stale output and still cleans temporary files', async () => {
    const h = harness({ failure: 'compile' });
    await h.run('dump');
    assert.equal(h.calls.length, 2);
    assert.equal(h.shown.length, 0);
    assert.equal(h.errors.length, 1);
    assert.equal(fs.existsSync(path.dirname(h.calls[1].args[3])), false);
});

test('arguments preserve quoting, empty values and literal shell expressions', async () => {
    const h = harness({ input: `hello "two words" '' --color '$(touch injected)' C:\\games\\test` });
    await h.run('runWithArguments');
    assert.deepEqual(Array.from(h.tasks[0].args[4].args[1]), [
        'run', h.document.uri.fsPath, '--', 'hello', 'two words', '', '--color', '$(touch injected)', 'C:\\games\\test',
    ]);
    assert.throws(() => h.parseArguments('"unfinished'), /quoted/);
    const cancelled = harness();
    await cancelled.run('runWithArguments');
    assert.equal(cancelled.tasks.length, 0);
    assert.equal(cancelled.calls.length, 0);
});

test('interactive start reuses session; restart disposes old process; closed sessions can start again', async () => {
    const h = harness();
    await h.run('startInteractive');
    const first = h.terminals[0];
    assert.deepEqual(Array.from(first.options.shellArgs), ['run', h.document.uri.fsPath, '--interactive', '--color']);
    assert.equal(first.options.shellPath, '/a directory/ges');
    await h.run('startInteractive');
    assert.equal(h.terminals.length, 1);
    await h.run('restartInteractive');
    assert.equal(first.disposed, true);
    assert.equal(h.terminals.length, 2);
    h.terminals[1].dispose();
    await h.run('startInteractive');
    assert.equal(h.terminals.length, 3);
});

test('interactive commands do not run after failed save or without trust', async () => {
    for (const options of [{ save: false }, { trusted: false }]) {
        const h = harness(options);
        await h.run('startInteractive');
        await h.run('restartInteractive');
        assert.equal(h.terminals.length, 0);
    }
});
