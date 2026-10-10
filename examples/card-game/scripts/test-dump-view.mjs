// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import { attachDump } from '../web/dump.mjs';

class Element extends EventTarget {
  hidden = false;
  value = '';
  children = [];
  textContent = '';
  attributes = {};
  append(...items) { this.children.push(...items); }
  replaceChildren(...items) { this.children = items; this.textContent = ''; }
  setAttribute(name, value) { this.attributes[name] = value; }
  focus() {}
}
const elements = new Map();
const byId = id => {
  if (!elements.has(id)) elements.set(id, new Element());
  return elements.get(id);
};
globalThis.document = {
  getElementById: byId,
  createElement: tag => Object.assign(new Element(), { tag }),
  createDocumentFragment: () => new Element(),
  createTextNode: text => ({ textContent: text }),
};
byId('dump-view').hidden = true;
const pending = [];
const engine = { send(payload) { return new Promise((resolve, reject) => pending.push({payload, resolve, reject})); } };
const input = new Element();
input.value = 'original';
const view = attachDump(input, engine, () => 'skat.ges');
byId('show-dump').onclick();
assert.equal(pending[0].payload.source, 'original');
assert.equal(pending[0].payload.sourceName, 'skat.ges');
assert.equal(byId('source-editor').hidden, true);
assert.equal(byId('dump-view').hidden, false);
input.value = 'changed';
input.dispatchEvent(new Event('input'));
pending[0].resolve({ diagnostics: [], dump: 'stale', complete: true, spans: [] });
await Promise.resolve();
assert.equal(byId('dump-code').children.length, 0);
pending[1].resolve({ diagnostics: [], dump: 'r0 <script>', complete: true, spans: [[0, 2, 'register']] });
await Promise.resolve();
const painted = byId('dump-code').children[0].children[0].children[1].children;
assert.equal(painted.map(item => item.textContent).join(''), 'r0 <script>');
assert.equal(painted[1].className, 'syntax-register');
assert.equal(input.value, 'changed');
byId('check-source').dispatchEvent(new Event('click'));
pending[2].resolve({ diagnostics: [{ line: 1, column: 2, code: 'compile.error', message: 'Invalid source' }] });
await Promise.resolve();
assert.match(byId('dump-code').textContent, /compile.error/);
assert.equal(byId('dump-code').children.length, 0, 'Failed compilation clears previous dump');
byId('check-source').dispatchEvent(new Event('click'));
view.reset();
pending[3].resolve({ diagnostics: [], dump: 'late', complete: true, spans: [] });
await Promise.resolve();
assert.equal(byId('dump-view').hidden, true);
assert.equal(byId('source-editor').hidden, false);
assert.equal(byId('dump-code').children.length, 0);
assert.equal(byId('show-dump').attributes['aria-pressed'], 'false');
console.log('Dump view: source selection, safe highlighted text, diagnostics, stale results and close passed.');

byId('show-dump').onclick();
const foldedDump = '.region "Source: skat.ges"\n\n.segment source "skat.ges"\n\non Test() {}\n.region-end "Source: skat.ges"\n.region "Code"\n.source-line "skat.ges" 1 | on Test() {}\nReturnVoid\n.region-end "Code"\n.region "Text"\n.text "hello"\n.region-end "Text"\n';
pending[4].resolve({ diagnostics: [], dump: foldedDump, complete: true, spans: [[foldedDump.indexOf('ReturnVoid'), 10, 'keyword']] });
await Promise.resolve();
const rows = byId('dump-code').children[0].children;
const sourceToggle = rows[0].children[0].children[1];
const codeToggle = rows[6].children[0].children[1];
assert.equal(sourceToggle.attributes['aria-expanded'], 'false');
assert.ok(rows[1].hidden);
assert.equal(codeToggle.attributes['aria-expanded'], 'true');
assert.equal(rows[7].hidden, false);
assert.equal(rows[7].children[0].children[0].textContent, '8', 'Line numbers retain original dump positions');
assert.ok(rows[7].children[1].children.some(item => item.textContent.includes('.source-line "skat.ges" 1 | on Test() {}')));
assert.ok(rows[8].children[1].children.some(item => item.className === 'syntax-keyword' && item.textContent === 'ReturnVoid'));
sourceToggle.onclick();
assert.equal(rows[1].hidden, false);
codeToggle.onclick();
assert.equal(rows[7].hidden, true);
assert.equal(rows[6].hidden, false);
assert.equal(sourceToggle.attributes['aria-expanded'], 'true');
codeToggle.onclick();
assert.equal(rows[8].hidden, false);
console.log('Dump gutter: original line numbers, independent region toggles and syntax highlighting passed.');

assert.equal(rows[10].children[0].children[1].attributes['aria-expanded'], 'false');
assert.equal(rows[11].hidden, true, 'Non-Code regions start folded');
