// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import { attachEditor } from '../web/editor.mjs';

class Element extends EventTarget {
  value = '';
  classList = { add() {} };
  replaceChildren() {}
  removeAttribute() {}
}
const elements = new Map();
const byId = id => {
  if (!elements.has(id)) elements.set(id, new Element());
  return elements.get(id);
};
globalThis.document = { getElementById: byId };
globalThis.ResizeObserver = class { observe() {} };
let options, current, change;
const input = new Element();
input.value = 'on Main() {}';
const cm = {
  on(name, handler) { if (name === 'change') change = handler; },
  getValue() { return current; },
  setValue(value) { current = value; change(); },
  clearHistory() {},
  setOption() {},
  getInputField() { return new Element(); },
  refresh() {},
};
globalThis.window = { CodeMirror: {
  defineMode() {},
  fromTextArea(input, config) { current = input.value; options = config; return cm; },
} };
const editor = attachEditor(input, new Element(), new Element(), new Element(), { send() { throw new Error('Inactive editor must not compile'); } });
assert.equal(options.extraKeys.Esc, false, 'Escape must reach the native dialog instead of CodeMirror singleSelection');
assert.equal(options.lineNumbers, true);
assert.equal(options.tabSize, 4);
let notifications = 0;
input.addEventListener('input', () => notifications++);
current = 'on Changed() {}';
change();
assert.equal(input.value, current, 'Full document reaches autosave and gameplay');
assert.equal(notifications, 1);
input.value = 'on Loaded() {}';
input.dispatchEvent(new Event('input'));
assert.equal(current, input.value, 'Loading a source updates the editing document');
assert.equal(notifications, 2, 'Programmatic loads must not emit recursive change events');
editor.suspend();
console.log('Editor adapter: Escape passthrough and bidirectional source synchronization passed.');
