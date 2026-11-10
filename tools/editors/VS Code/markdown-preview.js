// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';

const fs = require('node:fs/promises');
const path = require('node:path');
const textmate = require('vscode-textmate');
const oniguruma = require('vscode-oniguruma');

// Preview CSS uses a small semantic palette; token boundaries come exclusively
// from the same TextMate grammars used by the editor.
function tokenClass(scopes) {
    for (const scope of [...scopes].reverse()) {
        if (scope.startsWith('comment')) return 'comment';
        if (scope.startsWith('string')) return 'string';
        if (/^(keyword|storage)\./.test(scope)) return 'keyword';
        if (/^constant\./.test(scope)) return 'constant';
        if (/^(entity\.name|support)\./.test(scope)) return 'entity';
        if (/^variable\./.test(scope)) return 'variable';
    }
    return null;
}

async function createHighlighter() {
    const wasm = await fs.readFile(require.resolve('vscode-oniguruma/release/onig.wasm'));
    await oniguruma.loadWASM(wasm.buffer.slice(wasm.byteOffset, wasm.byteOffset + wasm.byteLength));
    const registry = new textmate.Registry({
        onigLib: Promise.resolve({
            createOnigScanner: patterns => new oniguruma.OnigScanner(patterns),
            createOnigString: text => new oniguruma.OnigString(text),
        }),
        async loadGrammar(scope) {
            const name = {
                'source.gameeventscript': 'GameEventScript',
                'source.gameeventscript.assembler': 'GameEventScriptAssembler',
                'source.gameeventscript.document': 'GameEventScriptDocument',
                'source.gameeventscript.schema': 'GameEventScriptSchema',
            }[scope];
            if (!name) return null;
            return JSON.parse(await fs.readFile(path.join(__dirname, 'syntaxes', name + '.tmLanguage.json'), 'utf8'));
        },
    });
    const grammars = {
        ges: await registry.loadGrammar('source.gameeventscript'),
        gesa: await registry.loadGrammar('source.gameeventscript.assembler'),
        gesd: await registry.loadGrammar('source.gameeventscript.document'),
        gess: await registry.loadGrammar('source.gameeventscript.schema'),
    };
    // Yields the highlighted lines, or null for a language without a grammar.
    return (code, language, escapeHtml) => {
        const name = (language || '').toLowerCase();
        const grammar = Object.hasOwn(grammars, name) ? grammars[name] : null;
        if (!grammar) return null;
        let stack = textmate.INITIAL;
        return code.split('\n').map(line => {
            const result = grammar.tokenizeLine(line, stack);
            stack = result.ruleStack;
            return result.tokens.map(token => {
                const escaped = escapeHtml(line.slice(token.startIndex, token.endIndex));
                const kind = tokenClass(token.scopes);
                return kind ? `<span class="ges-token-${kind}">${escaped}</span>` : escaped;
            }).join('');
        });
    };
}

function createMarkdownExtension(highlight) {
    return md => {
        const original = md.options.highlight;
        md.options.highlight = function (code, language, ...args) {
            const lines = highlight(code, language, md.utils.escapeHtml);
            if (!lines) return original ? original.call(this, code, language, ...args) : '';
            return lines.join('\n');
        };
        return md;
    };
}

module.exports = { createHighlighter, createMarkdownExtension };
