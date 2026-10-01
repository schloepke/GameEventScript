// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { createEngine } from './engine.mjs';

const engine = fetch('./card-game.wasm')
  .then((response) => {
    if (!response.ok) throw new Error(`Wasm download failed: ${response.status}`);
    return response.arrayBuffer();
  })
  .then(createEngine);
let serial = Promise.resolve();

self.onmessage = (event) => {
  serial = serial.then(async () => {
    const { id, type, source, seed, player, action, revision } = event.data;
    try {
      const game = await engine;
      const result =
        type === 'analyze'
          ? { ...game.highlight(source), ...game.check(source) }
          : type === 'highlight'
            ? game.highlight(source)
            : type === 'start'
              ? game.start(source, seed)
              : game.act(player, action, revision);
      self.postMessage({ id, ...result });
    } catch (error) {
      self.postMessage({ id, error: String(error) });
    }
  });
};
