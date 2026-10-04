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
for (const [tag, text] of [
  ['position: #center', "position: 'center'"],
  ['layout: #pile', "layout: 'pile'"],
  ['visibility: #hidden', "visibility: 'hidden'"],
  ["label: 'Draw pile'", 'label: #draw'],
]) {
  assert.ok(game.start(source.replace(tag, text)).error);
}
const initial = game.start(source, 42);
assert.equal(initial.error, undefined);
assert.equal(initial.state.currentPlayer, 0);
assert.equal(initial.state.actions.find((action) => action.kind === 'draw').zone, 'draw');
assert.ok(
  game.start(
    source.replace(
      "handler: Draw(action, player), zone: #draw",
      "handler: Draw(action, player), zone: #missing",
    ),
  ).error,
);
game.start(source, 42);
assert.deepEqual(game.start(source, 42).state, initial.state);
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
      ['draw', 'penalty'].includes(action.kind) && before.zones.find((zone) => zone.id === 'draw').count === 0;
    result = game.act(before.currentPlayer, action, before.revision);
    assert.equal(result.error, undefined, result.error);
    assert.equal(result.accepted, true);
    assert.equal(result.state.revision, before.revision + 1);
    if (!result.state.finished && ['play', 'stack'].includes(action.kind)) {
      if (played.properties.rank === '8') {
        specialCardsSeen.add('8');
        assert.equal(result.state.currentPlayer, (before.currentPlayer + 2) % before.players.length);
        assert.ok(result.state.notice.startsWith('8:'));
      } else if (played.properties.rank === '7') {
        specialCardsSeen.add('7');
        assert.equal(result.state.currentPlayer, affected);
        assert.equal(result.state.zones.find(z => z.id === `hand${affected}`).count, affectedCount);
        assert.ok(result.state.actions.some(a => a.kind === 'penalty'));
      } else if (played.properties.rank === 'J') {
        specialCardsSeen.add('J');
        assert.equal(result.state.currentPlayer, before.currentPlayer);
        assert.deepEqual(result.state.actions.filter(a => !a.global).map(a => a.kind), ['clubs', 'spades', 'hearts', 'diamonds']);
      }
    }
    if (action.kind === 'penalty' && before.zones.find(z => z.id === 'draw').count > 0) {
      assert.equal(result.state.currentPlayer, before.currentPlayer);
      assert.equal(result.state.turn, before.turn);
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
assert.deepEqual([...specialCardsSeen].sort(), ['7', '8', 'J']);
const changed = source
  .replace(/^function fits\(card, top\) be .*$/m, 'function fits(card, top) be true')
  .replace('[1, 1, 1, 1, 1]', '[1]');
const single = game.start(changed, 42);
assert.equal(single.error, undefined);
const win = game.act(
  0,
  single.state.actions.find((action) => action.kind === 'play'),
  0,
);
assert.deepEqual(win.state.winners, [0]);
assert.equal(win.state.finished, true);
assert.ok(game.start('not valid GES').error);
assert.ok(game.act(0, { kind: 'draw' }, 0).error);
assert.equal(game.start(source).error, undefined);
assert.ok(
  game.start('on PrepareGame(players) { emit Again() }\non Again() { emit Again() }').error,
);
console.log(
  `Wasm: 30 complete seeded games ${totalActions} accepted actions, recycling, rejections, stale revisions, source edits, restart, execution limits, Swift highlighting, and stacked sevens, penalty draws, jacks and eights passed.`,
);

const batches = game.start(
  source
    .replace('deck()[:shuffle]', 'deck()')
    .replace('[1, 1, 1, 1, 1]', '[3, 2, 3]')
    .replace('visibility: #owner', 'visibility: #public'),
  42,
  3,
);
assert.equal(batches.error, undefined);
assert.deepEqual(
  batches.state.zones
    .filter((zone) => zone.owner !== null)
    .map((zone) => zone.cards.map((card) => card.id)),
  [
    [32, 31, 30, 23, 22, 17, 16, 15],
    [29, 28, 27, 21, 20, 14, 13, 12],
    [26, 25, 24, 19, 18, 11, 10, 9],
  ],
);
assert.equal(batches.state.zones.find((zone) => zone.id === 'discard').cards[0].id, 8);

for (const players of [2, 3, 4]) {
  const state = game.start(source, 42, players).state;
  assert.equal(state.players.length, players);
  assert.equal(state.zones.find((z) => z.id === 'draw').count, 31 - players * 5);
  assert.deepEqual(
    state.zones.filter((z) => z.owner === null).map((z) => z.position),
    ['center', 'center'],
  );
}
const again =
  source.replace('on EndTurn(player) {', 'on MauMauEndTurn(player) {') +
  '\non EndTurn(player) { emit NextPlayersTurn(repeatTurnForPlayer: true) }';
const round = game.start(again, 42, 4);
const drawn = game.act(0, { kind: 'draw' }, round.state.revision);
assert.equal(drawn.state.currentPlayer, 0);
assert.ok(!drawn.state.actions.some((a) => a.kind === 'draw'));
assert.equal(drawn.state.actions.find((a) => a.kind === 'pass').area, 0);
assert.equal(game.act(0, { kind: 'pass' }, drawn.state.revision).state.currentPlayer, 0);
assert.throws(() => game.start(source, 42, 5), /two to four/);

const bulk = source
  .replace(
    "['7', '8', '9', '10', 'J', 'Q', 'K', 'A']",
    "['2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A']",
  )
  .replace(
    'deck()[:shuffle]',
    "(deck() | deck() | [[suit: 'joker', rank: 'Joker'], [suit: 'joker', rank: 'Joker'], [suit: 'joker', rank: 'Joker'], [suit: 'joker', rank: 'Joker']])[:shuffle]",
  );
const rummy = game.start(bulk, 42, 4);
assert.equal(rummy.error, undefined);
assert.equal(
  rummy.state.zones.reduce((sum, z) => sum + z.count, 0),
  108,
);
const customRules = source.replaceAll('action: #draw', 'action: #pick');
const customState = game.start(customRules, 42, 3).state;
assert.equal(game.act(0, { kind: 'pick' }, customState.revision).accepted, true);
console.log(
  'Lifecycle: 2–4 players, bulk 108-card deck, repeat-turn selection and custom action names passed.',
);

// Counters survive rejection; required actions finish only on their final success.
const countedRules = `
on PrepareGame(players) {  }
on BeginTurn(player) {
    emit Action(action: #work, label: 'Work twice', optional: false,
        finishTurn: true, consumable: 2, handler: Work(action, player), area: player)
}
on Work(action, player) { emit Complete(action: action) }
on EndTurn(player) {}
`;
assert.ok(game.start(countedRules.replace('area: player', 'area: 99'), 42).error);
let counted = game.start(countedRules, 42);
assert.equal(counted.error, undefined);
assert.equal(counted.state.actions[0].label, 'Work twice');
assert.equal(counted.state.actions[0].optional, false);
const activation = counted.state.actions[0].id;
counted = game.act(0, counted.state.actions[0], 0);
assert.equal(counted.state.currentPlayer, 0);
assert.equal(counted.state.actions[0].id, activation);
assert.equal(counted.state.actions[0].remaining, 1);
counted = game.act(0, counted.state.actions[0], 1);
assert.equal(counted.state.currentPlayer, 1);
assert.notEqual(counted.state.actions[0].id, activation);
let rejectedCount = game.start(
  countedRules.replace(
    'emit Complete(action: action)',
    "emit Reject(action: action, reason: 'No')",
  ),
  42,
);
const rejectionBefore = rejectedCount.state;
rejectedCount = game.act(0, rejectedCount.state.actions[0], 0);
assert.equal(rejectedCount.accepted, false);
assert.deepEqual(rejectedCount.state, rejectionBefore);
console.log(
  'Actions: typed callbacks, labels, counted consumption, rejection and fresh turn activations passed.',
);

const sharedWinners = `
on PrepareGame(players) {
  emit CreateGame(setup: [table: [[id: #trick, label: 'Trick', layout: #spread, visibility: #public, cards: ['7', '8', '9'][:select rank => [suit: 'clubs', rank: rank]]]], players: players[:select player => [id: player, zones: []]]])
}
on BeginTurn(player) {
  if player = 0 { emit NextPlayersTurn() } else {
    emit Action(action: #finish, label: 'Finish', optional: true, finishTurn: true, consumable: #auto, handler: Done(action, player), area: player)
  }
}
on Done(action, player) { emit Complete(action: action) }
on EndTurn(player) { if player = 1 emit EndGame() }
on EndGame(players) { emit Finish(winners: [0, 1]) }
`;
const spread = game.start(sharedWinners, 42);
assert.equal(spread.error, undefined);
assert.equal(spread.state.currentPlayer, 1);
assert.equal(spread.state.turn, 2);
assert.equal(spread.state.zones[0].cards.length, 3);
assert.equal(spread.state.zones[0].layout, 'spread');
const sharedEnd = game.act(1, spread.state.actions[0], spread.state.revision);
assert.equal(sharedEnd.error, undefined);
assert.equal(sharedEnd.state.finished, true);
assert.deepEqual(sharedEnd.state.winners, [0, 1]);
console.log('Automatic BeginTurn skips, public table spreads and multiple winners passed.');

const queuedGroups = `
on PrepareGame(players) {
  emit ActionGroup(spec: [group: #duty, player: 1, priority: 10, optional: false, exclusive: true, actions: [
    [action: #pay, label: 'Pay', consumable: 2, handler: Pay(action, player), area: 1],
    [action: #other, label: 'Other', handler: Other(action, player), area: 1]
  ]])
}
on BeginTurn(player) { emit Action(spec: [action: #next, label: 'Next', finishTurn: true, handler: Next(action, player), area: player]) }
on Pay(action, player) { emit Complete(action: action) }
on Other(action, player) { emit Reject(action: action, reason: 'Not now') }
on Next(action, player) { emit Complete(action: action) }
on EndTurn(player) {}
`;
let queued = game.start(queuedGroups, 42);
assert.equal(queued.error, undefined);
assert.deepEqual(queued.state.actions.map(a => a.kind), ['next']);
queued = game.act(0, queued.state.actions[0], queued.state.revision);
assert.deepEqual(queued.state.actions.map(a => a.kind), ['pay', 'other']);
assert.equal(queued.state.actions[0].group, 'duty');
assert.equal(queued.state.actions[0].priority, 10);
const refused = game.act(1, queued.state.actions[1], queued.state.revision);
assert.equal(refused.accepted, false);
assert.deepEqual(refused.state.actions.map(a => a.kind), ['pay', 'other']);
queued = game.act(1, queued.state.actions[0], queued.state.revision);
assert.deepEqual(queued.state.actions.map(a => a.kind), ['pay']);
assert.equal(queued.state.actions[0].remaining, 1);
queued = game.act(1, queued.state.actions[0], queued.state.revision);
assert.deepEqual(queued.state.actions.map(a => a.kind), ['next']);
queued = game.act(1, queued.state.actions[0], queued.state.revision);
queued = game.act(0, queued.state.actions[0], queued.state.revision);
assert.deepEqual(queued.state.actions.map(a => a.kind), ['next']);
console.log('Queued groups, priority gating, exclusive selection, rejection and cleanup passed.');
