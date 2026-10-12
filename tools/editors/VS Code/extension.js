// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';

const vscode = require('vscode');
const { execFile } = require('node:child_process');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');

// Split arguments without invoking a shell or expanding variables/globs.
function parseArguments(text) {
    const args = [];
    let value = '', quote = null, started = false;
    for (let index = 0; index < text.length; index++) {
        const char = text[index];
        const next = text[index + 1];
        if (char === '\\' && next && (next === quote || next === '\\' || (!quote && /[\s"']/.test(next)))) {
            value += next;
            index++;
            started = true;
        } else if (quote) {
            if (char === quote) quote = null;
            else value += char;
        } else if (char === '"' || char === "'") {
            quote = char;
            started = true;
        } else if (/\s/.test(char)) {
            if (started) args.push(value);
            value = '';
            started = false;
        } else {
            value += char;
            started = true;
        }
    }
    if (quote) throw new Error('Close the quoted argument.');
    if (started) args.push(value);
    return args;
}

async function activate(context) {
    const dumps = new Map();
    const sessions = new Map();
    let interactiveBusy = false;
    let lastArguments = '';
    const output = vscode.window.createOutputChannel('GES');
    let sequence = 0;
    context.subscriptions.push(output,
        vscode.window.onDidCloseTerminal(terminal => {
            for (const [source, session] of sessions) {
                if (session === terminal) sessions.delete(source);
            }
        }),
        { dispose() { for (const terminal of sessions.values()) terminal.dispose(); sessions.clear(); } },
        vscode.workspace.registerTextDocumentContentProvider('ges-dump', {
            provideTextDocumentContent: uri => dumps.get(uri.toString()) ?? '',
        }),
        vscode.workspace.onDidCloseTextDocument(document => {
            if (document.uri.scheme === 'ges-dump') dumps.delete(document.uri.toString());
        }));

    function invoke(cli, args, cwd, token) {
        return new Promise((resolve, reject) => {
            const child = execFile(cli, args, {
                cwd, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024,
                env: { ...process.env, NO_COLOR: '1' },
            }, (error, stdout, stderr) => {
                cancellation?.dispose();
                if (stderr) output.append(stderr);
                if (error) {
                    if (stdout) output.append(stdout);
                    reject(error);
                } else resolve(stdout);
            });
            const cancellation = token.onCancellationRequested(() => child.kill());
            if (token.isCancellationRequested) child.kill();
        });
    }

    async function execute(action) {
        if (!vscode.workspace.isTrusted) {
            throw new Error('Trust this workspace before running GES CLI commands.');
        }
        const document = vscode.window.activeTextEditor?.document;
        if (!document || document.languageId !== 'ges' || document.uri.scheme !== 'file') {
            throw new Error('Open a saved .ges source file first.');
        }
        let argumentsForRun = [];
        if (action === 'runWithArguments') {
            const text = await vscode.window.showInputBox({
                title: 'GES: Run with Arguments', value: lastArguments,
                prompt: 'Arguments for Main(args). Use quotes for spaces; no shell expansion.',
                placeHolder: 'hello "two words" 42',
                validateInput(value) { try { parseArguments(value); } catch (error) { return error.message; } },
            });
            if (text === undefined) return;
            argumentsForRun = parseArguments(text);
            lastArguments = text;
        }
        if (document.isDirty && !await document.save()) return;
        const source = document.uri.fsPath;
        const cwd = path.dirname(source);
        const cli = vscode.workspace.getConfiguration('ges', document.uri).get('cliPath', 'ges').trim();
        if (!cli) throw new Error('Set ges.cliPath to ges, dotnet-ges, or an absolute executable path.');

        if (action === 'startInteractive' || action === 'restartInteractive') {
            const existing = sessions.get(source);
            if (existing && action === 'startInteractive') {
                existing.show();
                return;
            }
            if (existing) {
                await new Promise(resolve => {
                    const listener = vscode.window.onDidCloseTerminal(terminal => {
                        if (terminal !== existing) return;
                        listener.dispose();
                        resolve();
                    });
                    existing.dispose();
                });
            }
            const terminal = vscode.window.createTerminal({
                name: `GES REPL: ${path.basename(source)}`,
                shellPath: cli, shellArgs: ['run', source, '--interactive', '--color'], cwd,
            });
            sessions.set(source, terminal);
            terminal.show();
            return;
        }

        if (action !== 'dump') {
            const args = action === 'runWithArguments' ? ['run', source, '--', ...argumentsForRun] : [action, source];
            const task = new vscode.Task(
                { type: 'process' },
                vscode.workspace.getWorkspaceFolder(document.uri) ?? vscode.TaskScope.Global,
                `GES ${action}: ${path.basename(source)}`, 'GES',
                new vscode.ProcessExecution(cli, args, { cwd, env: { NO_COLOR: '1' } }), []);
            task.presentationOptions = { reveal: vscode.TaskRevealKind.Always, panel: vscode.TaskPanelKind.Dedicated };
            await vscode.tasks.executeTask(task);
            return;
        }

        await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: 'GES: Creating dump', cancellable: true,
        }, async (_progress, token) => {
            const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'ges-vscode-'));
            try {
                const binary = path.join(directory, 'program.gesb');
                await invoke(cli, ['compile', source, '-o', binary], cwd, token);
                if (token.isCancellationRequested) return;
                const text = await invoke(cli, ['dump', binary], cwd, token);
                if (token.isCancellationRequested) return;
                const uri = vscode.Uri.from({
                    scheme: 'ges-dump', path: '/' + path.basename(source, path.extname(source)) + '.gesa',
                    query: String(++sequence),
                });
                dumps.set(uri.toString(), text);
                try {
                    const dump = await vscode.workspace.openTextDocument(uri);
                    await vscode.languages.setTextDocumentLanguage(dump, 'gesa');
                    await vscode.window.showTextDocument(dump, { preview: true });
                } catch (error) {
                    dumps.delete(uri.toString());
                    throw error;
                }
            } catch (error) {
                if (!token.isCancellationRequested) throw error;
            } finally {
                await fs.rm(directory, { recursive: true, force: true });
            }
        });
    }

    for (const action of ['check', 'run', 'compile', 'dump', 'runWithArguments', 'startInteractive', 'restartInteractive']) {
        context.subscriptions.push(vscode.commands.registerCommand(`ges.${action}`, async () => {
            const interactive = action === 'startInteractive' || action === 'restartInteractive';
            if (interactive && interactiveBusy) return;
            if (interactive) interactiveBusy = true;
            try {
                await execute(action);
            } catch (error) {
                output.appendLine(error.message);
                output.show(true);
                const message = error.code === 'ENOENT'
                    ? 'GES CLI not found. Install ges or dotnet-ges and configure ges.cliPath if needed.'
                    : `GES ${action} failed. ${error.message}`;
                await vscode.window.showErrorMessage(message);
            } finally {
                if (interactive) interactiveBusy = false;
            }
        }));
    }
    return { extendMarkdownIt: await require('./markdown-preview').createMarkdownExtension() };
}

module.exports = { activate, parseArguments };
