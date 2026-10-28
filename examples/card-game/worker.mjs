// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { createEngine } from './engine.mjs?v=d66c3db5f1b2a923af6b7380b7b6c8a7f52c4561fd4f3a19f9cb12b6dc4313ca';

import { loadWasm } from './loading.mjs?v=d66c3db5f1b2a923af6b7380b7b6c8a7f52c4561fd4f3a19f9cb12b6dc4313ca';

let engine;

function getEngine() {
  if (!engine) {
    const report = (progress) => self.postMessage({ type: 'loading', ...progress });
    engine = loadWasm(report).then(async (binary) => {
      report({ phase: 'initialize' });
      const game = await createEngine(binary);
      report({ phase: 'ready' });
      return game;
    }).catch(error => {
      engine = undefined;
      report({ phase: 'failed' });
      throw error;
    });
  }
  return engine;
}

let serial = Promise.resolve();

self.onmessage = (event) => {
  serial = serial.then(async () => {
    const { id, type, source, sourceName, seed, players, player, action, revision } = event.data;
    try {
      const game = await getEngine();
      let result;
      switch (type) {
        case 'analyze': result = { ...game.highlight(source), ...game.check(source) }; break;
        case 'dump': result = game.dump(source, sourceName); break;
        case 'highlight': result = game.highlight(source); break;
        case 'start': result = game.start(source, seed, players); break;
        case 'action': result = game.act(player, action, revision); break;
        default: throw new Error(`Unknown worker request: ${type}. Reload the page to update the application.`);
      }
      self.postMessage({ id, ...result });
    } catch (error) {
      self.postMessage({ id, error: String(error) });
    }
  });
};
