// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import { createWorkerClient } from '../web/worker-client.mjs';

const workers = [];
globalThis.Worker = class {
  messages = [];
  constructor() { workers.push(this); }
  postMessage(data) { this.messages.push(data); }
  terminate() { this.terminated = true; }
  reply(data) { this.onmessage({ data }); }
};
const progress = [];
const client = createWorkerClient(value => progress.push(value));
const start = client.send({ type: 'start' });
const analyze = client.send({ type: 'analyze' });
assert.equal(workers.length, 1);
const worker = workers[0];
const [gameRequest, editorRequest] = worker.messages;
assert.notEqual(gameRequest.id, editorRequest.id);
worker.reply({ type: 'loading', phase: 'ready' });
worker.reply({ id: editorRequest.id, diagnostics: [] });
worker.reply({ id: gameRequest.id, state: { revision: 1 } });
assert.deepEqual((await analyze).diagnostics, []);
assert.equal((await start).state.revision, 1);
const restart = client.send({ type: 'start' });
assert.equal(workers.length, 1, 'New game reuses the same WASM worker');
worker.reply({ id: worker.messages.at(-1).id, error: 'Bad source' });
await assert.rejects(restart, /Bad source/);
const pending = client.send({ type: 'analyze' });
client.reset();
await assert.rejects(pending, /stopped/);
assert.equal(worker.terminated, true);
const recovered = client.send({ type: 'start' });
worker.reply({ type: 'loading', phase: 'download' });
assert.equal(progress.at(-1).loaded, 0, 'Old worker progress is ignored');
workers[1].reply({ id: workers[1].messages[0].id, state: {} });
await recovered;
const timeout = client.send({ type: 'analyze' }, 1);
await assert.rejects(timeout, /Time limit/);
assert.equal(workers[1].terminated, true);
console.log('Shared worker: routing, reuse, errors, reset, stale messages and timeout passed.');
