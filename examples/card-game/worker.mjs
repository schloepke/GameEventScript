// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { createEngine } from './engine.mjs?v=8acb810cd16a62b6c4ddf6d5db218c29244ce65ee5d1b9cdb456c00673cdf215';

import { loadWasm } from './loading.mjs?v=8acb810cd16a62b6c4ddf6d5db218c29244ce65ee5d1b9cdb456c00673cdf215';

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
