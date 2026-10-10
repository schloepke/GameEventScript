// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';

class Element extends EventTarget {
  value = '';
  hidden = true;
  open = false;
  validity = { badInput: false };
  classList = { add() {}, remove() {} };
  showModal() { this.open = true; }
  close() {
    if (!this.open) return;
    this.open = false;
    this.dispatchEvent(new Event('close'));
  }
  focus() {}
}

const elements = new Map();
const element = id => {
  if (!elements.has(id)) elements.set(id, new Element());
  return elements.get(id);
};
element('players').value = '2';
const window = new EventTarget();
const calls = { starts: 0, refresh: 0, suspend: 0, reset: 0, workers: 0 };
const source = (await fs.readFile(new URL('../web/app.mjs', import.meta.url), 'utf8'))
  .replace(/^import .*;\n/gm, '');
await vm.runInNewContext(`(async () => { ${source} })()`, {
  document: { getElementById: element, querySelectorAll: () => [], body: new Element() },
  window,
  ResizeObserver: class { disconnect() {} },
  createCardActionPicker: () => ({ close() {} }),
  createWorkerClient: () => {
    calls.workers++;
    return {
      send: () => { calls.starts++; return new Promise(() => {}); },
      reset: () => calls.reset++,
    };
  },
  attachGameLibrary: async () => ({ examples: [] }),
  attachSearch() {},
  attachDump: () => ({ reset() {}, refresh() {} }),
  attachEditor: () => ({ focus() {}, refresh: () => calls.refresh++, suspend: () => calls.suspend++ }),
});
assert.equal(calls.starts, 1);
element('open-editor').onclick();
assert.equal(element('editor-dialog').open, true);
assert.equal(calls.refresh, 1);
window.dispatchEvent(new Event('pagehide'));
assert.equal(calls.suspend, 1);
assert.equal(calls.reset, 1);
const restored = () => Object.assign(new Event('pageshow'), { persisted: true });
window.dispatchEvent(restored());
assert.equal(calls.refresh, 2, 'Restoring an open editor restarts its analysis');
assert.equal(calls.starts, 1, 'Restoration must not restart gameplay behind the editor');
element('close-editor').onclick();
assert.equal(calls.starts, 2, 'Closing the editor starts a new game');
window.dispatchEvent(new Event('pagehide'));
window.dispatchEvent(restored());
assert.equal(calls.starts, 3, 'Restoring the board restarts the stopped game');
assert.equal(calls.workers, 1, 'All lifecycle paths share one worker client');
console.log('App lifecycle: fullscreen close, page suspension and back/forward restoration passed.');
