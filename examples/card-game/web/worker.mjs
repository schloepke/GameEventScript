// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { createEngine } from './engine.mjs';

import { loadWasm } from './loading.mjs';

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
      const result =
        type === 'analyze'
          ? { ...game.highlight(source), ...game.check(source) }
          : type === 'dump'
            ? game.dump(source, sourceName)
            : type === 'highlight'
              ? game.highlight(source)
              : type === 'start'
                ? game.start(source, seed, players)
                : game.act(player, action, revision);
      self.postMessage({ id, ...result });
    } catch (error) {
      self.postMessage({ id, error: String(error) });
    }
  });
};
