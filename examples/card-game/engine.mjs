// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { WASI, File, OpenFile, ConsoleStdout } from './vendor/index.js';

/** Instantiate one isolated, serial Swift rule host. The caller owns worker lifetime. */
export async function createEngine(binary) {
  const wasi = new WASI(
    [],
    [],
    [
      new OpenFile(new File([])),
      ConsoleStdout.lineBuffered((line) => console.log(line)),
      ConsoleStdout.lineBuffered((line) => console.error(line)),
    ],
    { debug: false },
  );
  const { instance } = await WebAssembly.instantiate(binary, {
    wasi_snapshot_preview1: wasi.wasiImport,
  });
  wasi.initialize(instance);
  const api = instance.exports;

  let currentActions = [];

  const decode = (length) => {
    if (length < 0 || length > api.memory.buffer.byteLength)
      throw new Error('Invalid response length');
    const result = JSON.parse(
      new TextDecoder().decode(new Uint8Array(api.memory.buffer, api.cardgame_output(), length)),
    );
    if (result.state) currentActions = result.state.actions;
    return result;
  };

  const upload = (source) => {
    const bytes = new TextEncoder().encode(source);
    if (bytes.length === 0 || bytes.length > 131072)
      throw new Error('GES source must contain 1–131072 UTF-8 bytes');
    const pointer = api.cardgame_alloc(bytes.length);
    if (!pointer) throw new Error('Cannot allocate source buffer');
    new Uint8Array(api.memory.buffer, pointer, bytes.length).set(bytes);
    return bytes.length;
  };

  return {
    highlight(source) {
      if (source === '') return { complete: true, spans: [] };
      return decode(api.cardgame_highlight(upload(source)));
    },

    check(source) {
      return decode(api.cardgame_check(upload(source || '\n')));
    },

    start(source, seed = crypto.getRandomValues(new Int32Array(1))[0], players = 2) {
      if (!Number.isInteger(seed) || seed < -2147483648 || seed > 2147483647)
        throw new Error('Seed must be an Int32');
      if (!Number.isInteger(players) || players < 1 || players > 4)
        throw new Error('Choose one to four players');
      return decode(api.cardgame_start(upload(source), seed, players));
    },

    act(player, action, revision) {
      const index = currentActions.findIndex(
        (offer) =>
          offer.kind === action.kind &&
          offer.card === (action.card ?? null) &&
          (action.id === undefined || offer.id === action.id),
      );
      if (
        ![player, revision, action.card ?? 0].every(
          (value) => Number.isInteger(value) && value >= -2147483648 && value <= 2147483647,
        )
      )
        throw new Error('Invalid action');
      return decode(api.cardgame_action(player, index, revision));
    },
  };
}
