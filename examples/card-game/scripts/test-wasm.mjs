// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const web = path.join(root, 'artifacts/card-game/web');
const { createEngine } = await import(pathToFileURL(path.join(web, 'engine.mjs')));
const binary = await fs.readFile(path.join(web, 'card-game.wasm'));
const source = await fs.readFile(
  path.join(root, 'examples/card-game/games/mau-mau/rules.ges'),
  'utf8',
);
const game = await createEngine(binary);
// Exercise the actual Swift/Foundation highlighter, including UTF-16 astral characters.
const sample = "// 😀 comment\non Test(value) { emit Done(value: 'hello', count: 7) }";
const colored = game.highlight(sample);
assert.equal(colored.complete, true);
assert.ok(
  colored.spans.some(
    ([start, length, kind]) => kind === 'keyword' && sample.slice(start, start + length) === 'on',
  ),
);
assert.ok(colored.spans.some(([, , kind]) => kind === 'string'));
assert.ok(colored.spans.some(([, , kind]) => kind === 'number'));
assert.equal(game.highlight("on Test(value) { emit Done(value: 'unfinished").complete, true);
assert.deepEqual(game.highlight(''), { complete: true, spans: [] });
const { diagnosticRange } = await import(pathToFileURL(path.join(web, 'diagnostics.mjs')));
assert.deepEqual(game.check(source), { diagnostics: [] });
assert.deepEqual(game.check(''), { diagnostics: [] });
const broken = "// header\r\non Test() { emit Done(text: '😀', value: missing) }";
const checked = game.check(broken);
assert.ok(checked.diagnostics.length > 0);
const range = diagnosticRange(broken, checked.diagnostics[0]);
assert.equal(broken.slice(range.start, range.end), 'missing');
assert.equal(checked.diagnostics[0].line, 2);
const unfinished = 'on Test() {';
const eof = game.check(unfinished);
assert.ok(eof.diagnostics.length > 0);
const eofRange = diagnosticRange(unfinished, eof.diagnostics[0]);
assert.ok(eofRange && eofRange.end > eofRange.start);
const initial = game.start(source);
assert.equal(initial.error, undefined);
assert.deepEqual(
  initial.state.zones.map((zone) => zone.count),
  [21, 1, 5, 5],
);
assert.equal(initial.state.zones.find((zone) => zone.id === 'hand1').cards.length, 0);
assert.equal(initial.state.zones.find((zone) => zone.id === 'draw').cards.length, 0);
assert.deepEqual(game.check(source), { diagnostics: [] });
assert.ok(game.check(broken).diagnostics.length > 0);
const wrong = game.act(1, { kind: 'draw' }, 0);
assert.equal(wrong.accepted, false);
assert.deepEqual(wrong.state, initial.state);
const illegal = game.act(0, { kind: 'play', card: 2147483647 }, 0);
assert.equal(illegal.accepted, false);
assert.deepEqual(illegal.state, initial.state);
let totalActions = 0;
let recycled = false;
const specialCardsSeen = new Set();
for (let seed = 0; seed < 30; seed++) {
  let result = game.start(source, seed);
  assert.equal(result.error, undefined);
  for (let moves = 0; !result.state.finished && moves < 1000; moves++) {
    const before = result.state;
    const action = before.actions[0];
    const played = before.zones
      .find((zone) => zone.id === `hand${before.currentPlayer}`)
      .cards.find((card) => card.id === action.card);
    const affected = (before.currentPlayer + 1) % before.players.length;
    const affectedCount = before.zones.find((zone) => zone.id === `hand${affected}`).count;
    const drawingFromEmpty =
      action.kind === 'draw' && before.zones.find((zone) => zone.id === 'draw').count === 0;
    result = game.act(before.currentPlayer, action, before.revision);
    assert.equal(result.error, undefined, result.error);
    assert.equal(result.accepted, true);
    assert.equal(result.state.revision, before.revision + 1);
    if (
      !result.state.finished &&
      action.kind === 'play' &&
      ['7', '8'].includes(played.properties.rank)
    ) {
      specialCardsSeen.add(played.properties.rank);
      assert.ok(result.state.notice.startsWith(`${played.properties.rank}:`));
      assert.equal(result.state.currentPlayer, (before.currentPlayer + 2) % before.players.length);
      const available =
        before.zones.find((zone) => zone.id === 'draw').count +
        before.zones.find((zone) => zone.id === 'discard').count;
      const expectedExtra = played.properties.rank === '7' ? Math.min(2, available) : 0;
      assert.equal(
        result.state.zones.find((zone) => zone.id === `hand${affected}`).count,
        affectedCount + expectedExtra,
      );
    }
    assert.equal(
      result.state.zones.reduce((sum, zone) => sum + zone.count, 0),
      32,
    );
    if (drawingFromEmpty) {
      recycled = true;
      assert.deepEqual(
        result.state.zones.find((zone) => zone.id === 'discard').cards,
        before.zones.find((zone) => zone.id === 'discard').cards,
      );
    }
    const stale = game.act(result.state.currentPlayer, { kind: 'draw' }, before.revision);
    assert.equal(stale.accepted, false);
    assert.deepEqual(stale.state, result.state);
    totalActions++;
  }
  assert.equal(result.state.finished, true, `Seed ${seed} did not finish`);
}
assert.equal(recycled, true);
assert.deepEqual([...specialCardsSeen].sort(), ['7', '8']);
const changed = source
  .replace(
    'card.properties.suit = top.properties.suit or card.properties.rank = top.properties.rank',
    'true',
  )
  .replace('count: 5', 'count: 1');
const single = game.start(changed);
assert.equal(single.error, undefined);
const win = game.act(
  0,
  single.state.actions.find((action) => action.kind === 'play'),
  0,
);
assert.equal(win.state.winner, 0);
assert.equal(win.state.finished, true);
assert.ok(game.start('not valid GES').error);
assert.ok(game.act(0, { kind: 'draw' }, 0).error);
assert.equal(game.start(source).error, undefined);
assert.ok(game.start('on Setup(players) { emit Again() }\non Again() { emit Again() }').error);
console.log(
  `Wasm: 30 complete seeded games ${totalActions} accepted actions, recycling, rejections, stale revisions, source edits, restart, execution limits, Swift highlighting, and seven/eight effects passed.`,
);
