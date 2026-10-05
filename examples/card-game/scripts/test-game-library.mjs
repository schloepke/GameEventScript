// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import { attachGameLibrary } from '../web/games.mjs';

class Element extends EventTarget {
  constructor(value = '') { super(); this.value = value; this.children = []; }
  append(child) { this.children.push(child); }
  replaceChildren() { this.children = []; }
}

const storage = new Map();
let storageFails = false;
globalThis.localStorage = {
  getItem: key => storage.get(key) ?? null,
  setItem: (key, value) => { if (storageFails) throw new Error('quota'); storage.set(key, value); },
};
globalThis.document = { createElement: () => new Element() };
const examples = [
  { id: 'blackjack', name: 'Blackjack', source: 'examples/blackjack.ges', players: 1, playerCounts: [1, 2, 3] },
  { id: 'mau-mau', name: 'Mau Mau', source: 'examples/mau-mau.ges' },
  { id: 'skat', name: 'Skat', source: 'examples/skat.ges', players: 3 },
];
let pendingFetch;
let deferFetch = false;
let failFetch = false;
globalThis.fetch = async url => {
  if (deferFetch && url !== './examples.json') await new Promise(resolve => { pendingFetch = resolve; });
  return { ok: !failFetch, json: async () => examples, text: async () => url };
};

async function attach() {
  const ui = { input: new Element(), select: new Element(), save: new Element(), status: new Element(), players: new Element('2'), starts: 0, errors: [] };
  await attachGameLibrary(ui.input, ui.select, ui.save, ui.status, ui.players, () => ui.starts++, error => ui.errors.push(error));
  return ui;
}

function edit(ui, source) {
  ui.input.value = source;
  ui.input.dispatchEvent(new Event('input'));
}

async function choose(ui, id) {
  ui.select.value = id;
  await ui.select.onchange();
}

storage.set('ges-card-lab.draft.v1', JSON.stringify({ version: 1, source: 'legacy draft', players: 4 }));
const ui = await attach();
assert.equal(ui.input.value, 'legacy draft');
assert.equal(ui.select.value, 'draft');
assert.equal(ui.players.value, '4');
ui.save.onclick();
edit(ui, 'work in progress');
assert.equal(ui.select.value, 'draft');
await choose(ui, 'skat');
assert.equal(ui.players.value, '3');
assert.equal(ui.input.value, 'examples/skat.ges');
await choose(ui, 'saved');
assert.equal(ui.input.value, 'legacy draft');
await choose(ui, 'draft');
assert.equal(ui.input.value, 'work in progress');
assert.equal(ui.players.value, '4');
ui.save.onclick();
edit(ui, 'later edits');
const restored = await attach();
assert.equal(restored.input.value, 'later edits');
await choose(restored, 'saved');
assert.equal(restored.input.value, 'work in progress');
await choose(restored, 'draft');
assert.equal(restored.input.value, 'later edits');

// A response arriving after an edit must not overwrite that edit.
deferFetch = true;
const loading = choose(restored, 'skat');
edit(restored, 'edited during fetch');
pendingFetch();
await loading;
assert.equal(restored.input.value, 'edited during fetch');
assert.equal(restored.select.value, 'draft');
deferFetch = false;
failFetch = true;
await choose(restored, 'skat');
assert.equal(restored.input.value, 'edited during fetch');
assert.equal(restored.select.value, 'draft');
assert.equal(restored.errors.length, 1);
failFetch = false;
storageFails = true;
edit(restored, 'memory only');
assert.match(restored.status.textContent, /Local saving is unavailable/);
await choose(restored, 'skat');
await choose(restored, 'draft');
assert.equal(restored.input.value, 'memory only');
console.log('Game library: migration, autosave, explicit Save, switching, reload, fetch races and storage failure passed.');

// Per-example seat counts include solo play and survive Draft/Save round trips.
storageFails = false;
await choose(restored, 'blackjack');
assert.equal(restored.players.value, '1');
assert.deepEqual(restored.players.children.map(option => option.value), ['1', '2', '3']);
restored.save.onclick();
const solo = await attach();
assert.equal(solo.players.value, '1');
assert.deepEqual(solo.players.children.map(option => option.value), ['1', '2', '3']);
edit(solo, 'custom blackjack');
await choose(solo, 'mau-mau');
assert.equal(solo.players.value, '2');
assert.deepEqual(solo.players.children.map(option => option.value), ['2', '3', '4']);
await choose(solo, 'draft');
assert.equal(solo.input.value, 'custom blackjack');
assert.equal(solo.players.value, '1');
console.log('Game library: solo counts, example limits, save/reload and draft switching passed.');
