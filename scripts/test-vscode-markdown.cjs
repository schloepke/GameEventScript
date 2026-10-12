// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';
// Use an extracted VSIX or the staging directory from package-vscode.sh.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const root = process.env.GES_VSCODE_EXTENSION_ROOT || path.join(__dirname, '../artifacts/editors/vscode-package');
const { createRequire } = require('node:module');
const load = createRequire(path.join(root, 'package.json'));
const tm = load('vscode-textmate');
const onig = load('vscode-oniguruma');
const { createMarkdownExtension } = load('./markdown-preview');
const escapeHtml = text => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const ready = createMarkdownExtension();

test('preview highlights both grammars, preserves text, escapes HTML, delegates other languages', async () => {
    const extend = await ready;
    const md = { options: { highlight: (code, lang) => 'original:' + lang + ':' + code }, utils: { escapeHtml } };
    assert.equal(extend(md), md);
    for (const [lang, text] of [
        ['ges', 'on Main() { emit ConsoleOut("<script>&hello</script>") }\n'],
        ['GESA', '.region "Code"\nLoadInteger r0, #42\n.source-line "test.ges" 1 | emit ConsoleOut("hello")\n'],
    ]) {
        const html = md.options.highlight(text, lang);
        assert.match(html, /class="ges-token-/);
        assert.equal(html.replace(/<span class="ges-token-[a-z]+">|<\/span>/g, ''), escapeHtml(text));
        assert.ok(!html.includes('<script>'));
    }
    for (const lang of ['js', '', 'constructor']) {
        assert.equal(md.options.highlight('unchanged', lang), 'original:' + lang + ':unchanged');
    }
    const plain = { options: {}, utils: { escapeHtml } };
    extend(plain);
    assert.equal(plain.options.highlight('plain', 'unknown'), '');
});

test('editor injection highlights fences and stops at matching closers', async () => {
    await ready;
    const injection = JSON.parse(fs.readFileSync(path.join(root, 'syntaxes/Markdown.tmLanguage.json')));
    // Use VS Code's full grammar when supplied, otherwise a focused host grammar
    // with a competing generic fence to verify injection precedence.
    const markdown = process.env.GES_MARKDOWN_GRAMMAR
        ? JSON.parse(fs.readFileSync(process.env.GES_MARKDOWN_GRAMMAR))
        : { scopeName: 'text.html.markdown', patterns: [{
            begin: '^(`{3,}|~{3,}).*$', end: '^\\1.*$',
            name: 'markup.fenced_code.block.markdown', contentName: 'meta.embedded.block.unknown',
        }] };
    const registry = new tm.Registry({
        onigLib: Promise.resolve({ createOnigScanner: p => new onig.OnigScanner(p), createOnigString: s => new onig.OnigString(s) }),
        getInjections: scope => scope === 'text.html.markdown' ? [injection.scopeName] : [],
        loadGrammar: async scope => {
            if (scope === markdown.scopeName) return markdown;
            if (scope === injection.scopeName) return injection;
            const name = { 'source.gameeventscript': 'GameEventScript', 'source.gameeventscript.assembler': 'GameEventScriptAssembler' }[scope];
            return name ? JSON.parse(fs.readFileSync(path.join(root, 'syntaxes', name + '.tmLanguage.json'))) : null;
        },
    });
    const grammar = await registry.loadGrammar(markdown.scopeName);
    for (const [open, code, close, expected] of [
        ['```ges', 'emit ConsoleOut(42)', '```', 'keyword'],
        ['~~~GESA', 'LoadInteger r0, #42', '~~~~', 'keyword'],
        ['````ges metadata', 'emit ConsoleOut(42)', '`````', 'keyword'],
    ]) {
        let result = grammar.tokenizeLine(open, tm.INITIAL);
        result = grammar.tokenizeLine(code, result.ruleStack);
        assert.ok(result.tokens.some(t => t.scopes.some(s => s.startsWith(expected))), open);
        result = grammar.tokenizeLine(close, result.ruleStack);
        result = grammar.tokenizeLine('plain prose', result.ruleStack);
        assert.ok(result.tokens.every(t => !t.scopes.some(s => s.includes('embedded') || s.includes('fenced_code'))), close);
    }
    let result = grammar.tokenizeLine('```javascript', tm.INITIAL);
    result = grammar.tokenizeLine('```ges', result.ruleStack);
    assert.ok(result.tokens.every(t => !t.scopes.includes('meta.embedded.block.ges.markdown')));
    registry.dispose();
});
