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
  path.join(root, 'examples/card-game/games/mau-mau/mau-mau.ges'),
  'utf8',
);
const game = await createEngine(binary);
assert.equal(game.start(source, 42).state.playerLayout, 'aroundTable');
for (const count of [1, 2, 3, 4]) {
  const below = source.replace('function board(players) be [', 'function board(players) be [playerLayout: #bottom,');
  const result = game.start(below, 42, count);
  assert.equal(result.state?.playerLayout, 'bottom', result.error);
  assert.equal(result.state.players.length, count);
}
for (const invalid of ['#unknown', "'bottom'", '42']) {
  const rules = source.replace('function board(players) be [', `function board(players) be [playerLayout: ${invalid},`);
  assert.ok(game.start(rules, 42).error);
}
const badgeSetup = (commands) => source.replace('on PrepareGame(players) {', `on PrepareGame(players) { ${commands}`);
for (const color of ['green', 'red', 'yellow', 'blue', 'gray']) {
  const result = game.start(badgeSetup(`emit PlayerBadge('Test', player: 0, color: #${color})`), 42);
  assert.equal(result.state?.playerBadgeColors[0], color, result.error);
}
for (const color of ['#unknown', "'red'", '42']) {
  assert.ok(game.start(badgeSetup(`emit PlayerBadge('Test', player: 0, color: ${color})`), 42).error);
}
assert.equal(game.start(badgeSetup("emit PlayerBadge('Red', player: 0, color: #red)\nemit PlayerBadge('Default', player: 0)"), 42).state.playerBadgeColors[0], 'green');
assert.equal(game.start(badgeSetup("emit PlayerBadge('Red', player: 0, color: #red)\nemit PlayerBadge('', player: 0)"), 42).state.playerBadgeColors[0], '');
assert.deepEqual(game.start(source, 42).state.playerBadgeColors, ['', '']);
for (const color of ['green', 'red', 'yellow', 'blue', 'gray']) {
  const result = game.start(badgeSetup(`emit ZoneBadge('Zone <&😀>', zone: #draw, color: #${color})
    emit TableBadge('Table <&😀>', color: #${color})`), 42);
  assert.equal(result.error, undefined, result.error);
  assert.equal(result.state.tableBadge, 'Table <&😀>');
  assert.equal(result.state.tableBadgeColor, color);
  const zone = result.state.zones.find(zone => zone.id === 'draw');
  assert.equal(zone.badge, 'Zone <&😀>');
  assert.equal(zone.badgeColor, color);
}
for (const command of ["ZoneBadge('X', zone: #missing)", "ZoneBadge('X', zone: 'draw')",
  "ZoneBadge('X', zone: #draw, color: #unknown)", "TableBadge('X', color: #unknown)",
  "ZoneBadge('X', zone: #draw, color: 'red')", "TableBadge('X', color: 'red')"]) {
  assert.ok(game.start(badgeSetup(`emit ${command}`), 42).error);
}
for (const label of ['Default', '']) {
  const result = game.start(badgeSetup(`emit ZoneBadge('Red', zone: #draw, color: #red)
    emit TableBadge('Red', color: #red)
    emit ZoneBadge('${label}', zone: #draw)
    emit TableBadge('${label}')`), 42);
  const zone = result.state.zones.find(zone => zone.id === 'draw');
  assert.equal(zone.badge, label);
  assert.equal(zone.badgeColor, label ? 'green' : '');
  assert.equal(result.state.tableBadge, label);
  assert.equal(result.state.tableBadgeColor, label ? 'green' : '');
}
const clearedBadges = game.start(source, 42).state;
assert.equal(clearedBadges.tableBadge, '');
assert.equal(clearedBadges.tableBadgeColor, '');
assert.ok(clearedBadges.zones.every(zone => zone.badge === '' && zone.badgeColor === ''));
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
// Each queued popup retains its own title; invalid titles fail at the host boundary.
const noticeSource = source.replace('on PrepareGame(players) {', `on PrepareGame(players) {
    emit Notice('Default message')
    emit Notice('Custom message', title: 'Rules')
    emit Notice('Next message', title: 'Bidding values')`);
const noticeResult = game.start(noticeSource, 42);
assert.equal(noticeResult.error, undefined);
assert.deepEqual(noticeResult.state.notices, [
  { text: 'Default message', title: 'Game Notice' },
  { text: 'Custom message', title: 'Rules' },
  { text: 'Next message', title: 'Bidding values' },
]);
assert.ok(game.start(noticeSource.replace("title: 'Rules'", 'title: #rules'), 42).error);
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
  assert.ok(result.state.tableNotice);
  assert.ok(result.state.notices.some(notice => notice.text === result.state.tableNotice));
  assert.deepEqual(result.state.playerBadges, result.state.players.map((_, player) => result.state.winners.includes(player) ? 'Winner' : ''));
}
assert.equal(recycled, true);
assert.deepEqual([...specialCardsSeen].sort(), ['7', '8', 'J']);
const changed = source
  .replace(/^predicate fits\(card, top\) be[\s\S]*?(?=\n\n)/m, 'predicate fits(card, top) be true')
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
assert.throws(() => game.start(source, 42, 5), /one to four/);
assert.throws(() => game.start(source, 42, 0), /one to four/);

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
  :board.create(setup: [table: [[id: #trick, label: 'Trick', layout: #spread, visibility: #public, cards: ['7', '8', '9'][:select rank => [suit: 'clubs', rank: rank]]]], players: players[:select player => [id: player, zones: []]]])
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

// A second rule set exercises open tricks and a human-controlled round boundary.
const highCard = await fs.readFile(path.join(web, 'examples/high-card.ges'), 'utf8');
assert.deepEqual(game.check(highCard), { diagnostics: [] });
const highCardUnshuffled = highCard.replace(':shuffle', '');
assert.notEqual(highCardUnshuffled, highCard, 'Tie scenario must remove shuffling');
for (const players of [2, 3, 4]) {
  for (const shuffled of [false, true]) {
    let result = game.start(shuffled ? highCard : highCardUnshuffled, 42, players);
    assert.equal(result.error, undefined, result.error);
    const scores = Array(players).fill(0);
    for (let round = 1; round <= 13; round++) {
      assert.equal(result.state.round, round);
      for (let player = 0; player < players; player++) {
        assert.equal(result.state.currentPlayer, player);
        assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), ['reveal']);
        result = game.act(player, result.state.actions.find(action => !action.global), result.state.revision);
        assert.equal(result.error, undefined, result.error);
        assert.equal(result.accepted, true);
      }
      const trick = result.state.zones.find(zone => zone.id === 'trick').cards;
      assert.equal(trick.length, players);
      const winner = trick.reduce((best, card, index) =>
        Number(card.properties.value) > Number(trick[best].properties.value) ? index : best, 0);
      scores[winner] += players;
      if (!shuffled) assert.equal(winner, 0, 'First revealed card wins tied values');
      assert.equal(result.state.waitingForRound, true);
      assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), ['collect']);
      result = game.act(result.state.currentPlayer, result.state.actions.find(action => !action.global), result.state.revision);
      assert.equal(result.error, undefined, result.error);
      assert.equal(result.accepted, true);
      assert.equal(result.state.zones.find(zone => zone.id === 'trick').count, 0);
      for (let player = 0; player < players; player++) {
        assert.equal(result.state.zones.find(zone => zone.id === `won${player}`).count, scores[player]);
      }
      assert.equal(result.state.zones.reduce((total, zone) => total + zone.count, 0), 13 * players);
      assert.equal(result.state.finished, round === 13);
    }
    assert.ok(result.state.tableNotice);
    assert.ok(result.state.notices.some(notice => notice.text === result.state.tableNotice));
    assert.deepEqual(result.state.playerBadges, result.state.players.map((_, player) => result.state.winners.includes(player) ? 'Winner' : ''));
    assert.deepEqual(result.state.winners, scores.flatMap((score, player) =>
      score === Math.max(...scores) ? [player] : []));
  }
}
console.log('Highest Card: 2–4 players, 13 rounds, visible tricks, collection, scoring and first-reveal ties passed.');

const skat = await fs.readFile(path.join(web, 'examples/skat.ges'), 'utf8');
assert.deepEqual(game.check(skat), { diagnostics: [] });
const eyeValues = { A: 11, '10': 10, K: 4, Q: 3, J: 2, '9': 0, '8': 0, '7': 0 };
const skatColor = (card, mode) => mode !== 'null' && (card.properties.rank === 'J' || card.properties.suit === mode)
  ? 'trump' : card.properties.suit;
const skatStrength = (card, mode) => mode === 'null' ? ['7', '8', '9', '10', 'J', 'Q', 'K', 'A'].indexOf(card.properties.rank) : card.properties.rank === 'J'
  ? 10 + ['diamonds', 'hearts', 'spades', 'clubs'].indexOf(card.properties.suit)
  : ['7', '8', '9', 'Q', 'K', '10', 'A'].indexOf(card.properties.rank);
function skatAction(result, kind) {
  const action = result.state.actions.find(action => action.kind === kind);
  assert.ok(action, `Missing ${kind}: ${result.state.tableNotice}`);
  const next = game.act(result.state.currentPlayer, action, result.state.revision);
  assert.equal(next.error, undefined, next.error);
  assert.equal(next.accepted, true);
  return next;
}

function auction(result, declarer) {
  assert.equal(result.error, undefined, result.error);
  assert.equal(result.state.currentPlayer, 1);
  const choices = declarer === 0 ? ['bid', 'hold', 'passBid', 'passBid']
    : declarer === 1 ? ['bid', 'passBid', 'passBid'] : ['passBid', 'bid', 'passBid'];
  for (const kind of choices) result = skatAction(result, kind);
  assert.equal(result.state.currentPlayer, declarer);
  assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), ['takeSkat', 'hand']);
  return result;
}

const standardCards = ['diamonds', 'hearts', 'spades', 'clubs'].flatMap((suit, suitIndex) =>
  ['7', '8', '9', 'Q', 'K', '10', 'A', 'J'].map((rank, rankIndex) =>
    ({ id: suitIndex * 8 + rankIndex + 1, properties: { suit, rank } })));
const cardKey = card => `${card.properties.suit}:${card.properties.rank}`;
const leaders = new Set();
let followTrump = false, followSuit = false, discardFreely = false;
const modes = ['clubs', 'spades', 'hearts', 'diamonds', 'grand', 'null'];
for (const mode of modes) for (const handGame of [false, true]) for (let seed = 0; seed < 10; seed++) {
  let result = game.start(skat, seed, 3);
  assert.equal(result.error, undefined, result.error);
  assert.deepEqual(result.state.zones.map(zone => zone.count), [0, 2, 0, 0, 10, 0, 10, 0, 10, 0]);
  const solo = seed % 3;
  result = auction(result, solo);
  const soloHand = `hand${solo}`;
  const opponents = [0, 1, 2].filter(player => player !== solo);
  const choose = kind => { result = skatAction(result, kind); };
  assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), ['takeSkat', 'hand']);
  choose(handGame ? 'hand' : 'takeSkat');
  if (!handGame) {
    assert.equal(result.state.zones.find(zone => zone.id === soloHand).count, 12);
    assert.equal(result.state.zones.find(zone => zone.id === 'skat').count, 0);
    assert.ok(result.state.actions.filter(action => !action.global).every(action => action.kind === 'discard'));
    choose('discard');
    assert.equal(result.state.zones.find(zone => zone.id === soloHand).count, 11);
    assert.ok(result.state.actions.filter(action => !action.global).every(action => action.kind === 'discard'));
    choose('discard');
  }
  assert.equal(result.state.zones.find(zone => zone.id === soloHand).count, 10);
  assert.equal(result.state.zones.find(zone => zone.id === 'skat').count, 2);
  assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), modes);
  choose(mode);
  if (handGame || mode === 'null') choose('closed');
  assert.equal(result.state.currentPlayer, 0);
  const scores = [0, 0, 0];
  const seen = new Set();
  const ownCards = new Set();
  const seenFaces = new Set();
  let leader = 0;
  let previousTrick = [];
  for (let trickNumber = 1; trickNumber <= 10; trickNumber++) {
    leaders.add(leader);
    for (let seat = 0; seat < 3; seat++) {
      let state = result.state;
      const player = (leader + seat) % 3;
      assert.equal(state.actions.some(action => action.kind === 'viewLastTrick'), trickNumber > 1);
      if (seed === 0 && trickNumber > 1) {
        const before = state;
        for (let repeat = 0; repeat < 2; repeat++) {
          result = skatAction(result, 'viewLastTrick');
          state = result.state;
          assert.equal(state.currentPlayer, player);
          assert.equal(state.turn, before.turn);
          assert.equal(state.round, before.round);
          assert.deepEqual(state.zones, before.zones);
          assert.deepEqual(state.actions, before.actions);
          assert.equal(state.tableNotice, before.tableNotice);
          const symbols = { clubs: '♣', spades: '♠', hearts: '♥', diamonds: '♦' };
          const expected = `Last trick · won by Player ${leader + 1}` + previousTrick.map(card => ` · ${symbols[card.properties.suit]} ${card.properties.rank}`).join('');
          assert.ok(state.notices.some(notice => notice.text === expected), JSON.stringify({expected, notices: state.notices}));
        }
      }
      assert.equal(state.currentPlayer, player);
      const cards = state.zones.find(zone => zone.id === `hand${player}`).cards;
      const trick = state.zones.find(zone => zone.id === 'trick').cards;
      const following = trick.length ? cards.filter(card => skatColor(card, mode) === skatColor(trick[0], mode)) : [];
      const legal = following.length ? following : cards;
      if (trick.length) {
        if (!following.length) discardFreely = true;
        else if (skatColor(trick[0], mode) === 'trump') followTrump = true;
        else followSuit = true;
      }
      const plays = state.actions.filter(action => action.kind === 'play');
      assert.deepEqual(plays.map(action => action.card).sort((a, b) => a - b), legal.map(card => card.id).sort((a, b) => a - b));
      const forbidden = cards.find(card => !legal.includes(card));
      if (forbidden) {
        const rejected = game.act(player, { kind: 'play', card: forbidden.id }, state.revision);
        assert.equal(rejected.accepted, false);
        assert.deepEqual(rejected.state, state);
      }
      // Vary choices without depending on the rule's stored rank/power metadata.
      const action = plays[(seed + trickNumber + seat) % plays.length];
      result = game.act(player, action, state.revision);
      assert.equal(result.error, undefined, result.error);
      assert.equal(result.accepted, true);
      seen.add(action.card);
      const key = cardKey(cards.find(card => card.id === action.card));
      seenFaces.add(key);
      if (player === solo) ownCards.add(key);
    }
    const trick = result.state.zones.find(zone => zone.id === 'trick').cards;
    assert.equal(trick.length, 3);
    const winningIndex = trick.reduce((best, card, index) => {
      if (skatColor(card, mode) === skatColor(trick[best], mode)) return skatStrength(card, mode) > skatStrength(trick[best], mode) ? index : best;
      return skatColor(card, mode) === 'trump' ? index : best;
    }, 0);
    const winner = (leader + winningIndex) % 3;
    previousTrick = trick;
    scores[winner] += trick.reduce((sum, card) => sum + eyeValues[card.properties.rank], 0);
    assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), ['collect']);
    result = game.act(result.state.currentPlayer, result.state.actions[0], result.state.revision);
    assert.equal(result.error, undefined, result.error);
    assert.equal(result.accepted, true);
    assert.equal(result.state.zones.find(zone => zone.id === 'trick').count, 0);
    assert.equal(result.state.zones.reduce((sum, zone) => sum + zone.count, 0), 32);
    assert.equal(result.state.finished, trickNumber === 10 || (mode === 'null' && winner === solo));
    leader = winner;
    if (result.state.finished) break;
  }
  if (mode !== 'null' || result.state.winners.includes(solo)) assert.equal(seen.size, 30);
  const skatEyes = 120 - scores.reduce((sum, score) => sum + score, 0);
  const declarer = scores[solo] + skatEyes;
  if (mode === 'null') {
    const success = result.state.zones.find(zone => zone.id === `won${solo}`).count === 0;
    assert.deepEqual(result.state.winners, success ? [solo] : opponents);
    assert.ok(result.state.tableNotice.endsWith(`Score ${(success ? 1 : -2) * (handGame ? 35 : 23)}`));
  } else {
    assert.deepEqual(result.state.winners, declarer >= 61 ? [solo] : opponents);
    assert.ok(result.state.tableNotice.includes(`${declarer}: ${120 - declarer} eyes`));
    // Reconstruct the declarer's original twelve cards, including the hidden skat.
    for (const card of standardCards) if (!seenFaces.has(cardKey(card))) ownCards.add(cardKey(card));
    const trumps = standardCards.filter(card => skatColor(card, mode) === 'trump')
      .sort((a, b) => skatStrength(b, mode) - skatStrength(a, mode));
    const withTop = ownCards.has(cardKey(trumps[0]));
    const firstBreak = trumps.findIndex(card => ownCards.has(cardKey(card)) !== withTop);
    const matadors = firstBreak < 0 ? trumps.length : firstBreak;
    const tricks = result.state.zones.find(zone => zone.id === `won${solo}`).count / 3;
    const levels = 1 + Number(handGame) + Number(declarer <= 30 || declarer >= 90) + Number(tricks === 0 || tricks === 10);
    const value = { clubs: 12, spades: 11, hearts: 10, diamonds: 9, grand: 24 }[mode] * (matadors + levels);
    assert.ok(result.state.tableNotice.endsWith(`Score ${(declarer >= 61 ? 1 : -2) * value}`), JSON.stringify({ mode, handGame, seed, solo, ownCards: [...ownCards], matadors, levels, value, notice: result.state.tableNotice }));
  }
  assert.ok(result.state.notices.some(notice => notice.text === result.state.tableNotice));
  assert.deepEqual(result.state.playerBadges, result.state.players.map((_, player) => result.state.winners.includes(player) ? 'Winner' : ''));
}
assert.deepEqual([...leaders].sort(), [0, 1, 2]);
assert.ok(followTrump && followSuit && discardFreely);
for (const players of [2, 4]) {
  const result = game.start(skat, 42, players);
  assert.equal(result.error, undefined, result.error);
  assert.equal(result.state.finished, true);
  assert.deepEqual(result.state.winners, []);
  assert.match(result.state.notice, /exactly three players/);
}
console.log('Skat: 120 games, all three declarers, bidding/holding, all modes, Hand/skat exchange, follow-suit, trick leaders, scoring and player-count guard passed.');

// Give the declarer ten low cards to exercise a successful full-length Null game.
const nullWinner = skat.replace('        :board.setstate(key: #trickNumber, value: 1)', `
        for zone in [#hand0, #hand1, #hand2, #skat] {
            :board.movecards(source: zone, destination: #deck, cards: :board.cards(zone: zone)[:select card => card.id])
        }
        let low be :board.cards(zone: #deck)[:filter card where card.properties.rank = '7' or card.properties.rank = '8' or (card.properties.rank = '9' and (card.properties.suit = 'clubs' or card.properties.suit = 'spades'))]
        :board.movecards(source: #deck, destination: #hand0, cards: low[:select card => card.id])
        :board.take(source: #deck, destination: #hand1, count: 10)
        :board.take(source: #deck, destination: #hand2, count: 10)
        :board.take(source: #deck, destination: #skat, count: 2)
        :board.setstate(key: #trickNumber, value: 1)`);
let nullResult = auction(game.start(nullWinner, 42, 3), 0);
for (const kind of ['hand', 'null', 'closed']) {
  assert.equal(nullResult.error, undefined, nullResult.error);
  nullResult = game.act(0, nullResult.state.actions.find(action => action.kind === kind), nullResult.state.revision);
}
let nullPlays = 0;
while (!nullResult.state.finished && nullPlays <= 30) {
  const state = nullResult.state;
  const action = state.actions[0];
  if (action.kind === 'play') nullPlays++;
  nullResult = game.act(state.currentPlayer, action, state.revision);
  assert.equal(nullResult.error, undefined, nullResult.error);
}
assert.equal(nullPlays, 30);
assert.deepEqual(nullResult.state.winners, [0]);
assert.match(nullResult.state.tableNotice, /Player 1 wins.*No tricks taken.*Score 35$/);
console.log('Null: declarer wins after ten tricks without taking any, independently of card eyes.');

// Forehand can accept 18 after both passes, or pass the deal without a winner.
for (const kind of ['play18', 'passDeal']) {
  let result = game.start(skat, 42, 3);
  for (const step of ['passBid', 'passBid', kind]) result = skatAction(result, step);
  assert.equal(result.state.finished, kind === 'passDeal');
  if (kind === 'passDeal') {
    assert.deepEqual(result.state.winners, []);
    assert.match(result.state.tableNotice, /All players passed.*Score 0/);
    assert.deepEqual(result.state.playerBadges, ['', '', '']);
  } else assert.equal(result.state.currentPlayer, 0);
}

function bidTo(source, value) {
  let result = game.start(source, 42, 3);
  for (let count = 0; count < 100; count++) {
    const label = result.state.actions.find(action => action.kind === 'bid')?.label;
    assert.ok(label, `No bid available before ${value}`);
    const offered = Number(label.match(/\d+/)[0]);
    assert.ok(offered <= value, `${value} is not a legal bid`);
    result = skatAction(result, 'bid');
    result = skatAction(result, 'hold');
    if (offered === value) {
      if (value === 264) assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), ['passBid']);
      result = skatAction(result, 'passBid');
      return skatAction(result, 'passBid');
    }
  }
  assert.fail('Bidding did not terminate');
}

function fixedSkat(own, hidden, base = skat) {
  assert.equal(own.length, 10);
  assert.equal(hidden.length, 2);
  assert.equal(new Set([...own, ...hidden]).size, 12);
  return base.replace('cards: deck()[:shuffle]', 'cards: deck()').replace('        :board.setstate(key: #trickNumber, value: 1)', `
        for zone in [#hand0, #hand1, #hand2, #skat] {
            :board.movecards(source: zone, destination: #deck, cards: :board.cards(zone: zone)[:select card => card.id])
        }
        :board.movecards(source: #deck, destination: #hand0, cards: [${own}])
        :board.movecards(source: #deck, destination: #skat, cards: [${hidden}])
        :board.take(source: #deck, destination: #hand1, count: 10)
        :board.take(source: #deck, destination: #hand2, count: 10)
        :board.setstate(key: #trickNumber, value: 1)`);
}

function finishSkat(result) {
  for (let moves = 0; !result.state.finished && moves < 40; moves++) {
    result = skatAction(result, result.state.actions[0].kind);
  }
  assert.equal(result.state.finished, true);
  return result;
}

// All eleven diamond trumps belong to the declarer, including the hidden skat.
// With the top ten in hand, all ten tricks must be won: these are known scores,
// independent of the implementation's rank, matador and scoring calculations.
const topTen = [32, 24, 16, 8, 7, 6, 5, 4, 3, 2];
const winningSkat = fixedSkat(topTen, [1, 15]);
for (const [announcement, score] of [['closed', 135], ['schneider', 144], ['schwarz', 153], ['ouvert', 162]]) {
  let result = auction(game.start(winningSkat, 42, 3), 0);
  for (const kind of ['hand', 'diamonds', announcement]) result = skatAction(result, kind);
  if (announcement === 'ouvert') {
    assert.equal(result.state.zones.find(zone => zone.id === 'hand0').count, 0);
    assert.equal(result.state.zones.find(zone => zone.id === 'open').cards.length, 10);
    result = skatAction(result, 'play');
    assert.equal(result.state.currentPlayer, 1);
    assert.equal(result.state.zones.find(zone => zone.id === 'open').cards.length, 9);
  } else assert.equal(result.state.zones.find(zone => zone.id === 'open').count, 0);
  result = finishSkat(result);
  assert.deepEqual(result.state.winners, [0]);
  assert.match(result.state.tableNotice, /With 11/);
  assert.ok(result.state.tableNotice.endsWith(`Score ${score}`), result.state.tableNotice);
}

// Clubs jack in the unseen skat still makes this 'with eleven', never 'without one'.
let hiddenTop = auction(game.start(fixedSkat([24, 16, 8, 7, 6, 5, 4, 3, 2, 15], [32, 1]), 42, 3), 0);
for (const kind of ['hand', 'diamonds', 'closed']) hiddenTop = skatAction(hiddenTop, kind);
hiddenTop = finishSkat(hiddenTop);
assert.deepEqual(hiddenTop.state.winners, [0]);
assert.match(hiddenTop.state.tableNotice, /With 11.*Score 135$/);

let overbid = bidTo(winningSkat, 140);
for (const kind of ['hand', 'diamonds', 'closed']) overbid = skatAction(overbid, kind);
overbid = finishSkat(overbid);
assert.deepEqual(overbid.state.winners, [1, 2]);
assert.match(overbid.state.tableNotice, /120: 0 eyes.*Overbid.*Score -288$/);

// The two bidding duels can replace both holders, with no second chance to rejoin.
let rearhand = game.start(skat, 42, 3);
for (const kind of ['bid', 'passBid', 'bid', 'passBid']) rearhand = skatAction(rearhand, kind);
assert.equal(rearhand.state.currentPlayer, 2);
assert.match(rearhand.state.tableNotice, /Player 3 declares at 20/);
for (const kind of ['hand', 'grand', 'ouvert']) rearhand = skatAction(rearhand, kind);
assert.equal(rearhand.state.currentPlayer, 0);
assert.equal(rearhand.state.zones.find(zone => zone.id === 'open').cards.length, 10);
assert.equal(rearhand.state.zones.find(zone => zone.id === 'hand2').count, 0);
finishSkat(rearhand);

// Fixed-value Null offers must cover the bid; 264 is the last possible bid.
for (const [bid, handGame, expected] of [[24, false, ['ouvert']], [36, true, ['ouvert']], [48, false, []], [60, true, []], [264, true, []]]) {
  let result = bidTo(skat, bid);
  result = skatAction(result, handGame ? 'hand' : 'takeSkat');
  if (!handGame) for (let count = 0; count < 2; count++) result = skatAction(result, 'discard');
  assert.equal(result.state.actions.some(action => action.kind === 'null'), expected.length > 0);
  if (expected.length) {
    result = skatAction(result, 'null');
    assert.deepEqual(result.state.actions.filter(action => !action.global).map(action => action.kind), expected);
    result = skatAction(result, 'ouvert');
    assert.equal(result.state.zones.find(zone => zone.id === 'open').cards.length, 10);
    result = finishSkat(result);
    const value = handGame ? 59 : 46;
    assert.ok(result.state.tableNotice.endsWith(`Score ${(result.state.winners.includes(0) ? 1 : -2) * value}`));
  }
}
console.log('Skat: all-pass, duel replacement, bid limits, public ouvert, hidden matadors, known Hand/Schneider/Schwarz/ouvert values, Null values and overbidding passed.');

// Announced targets can lose even with more than 60 eyes. Charge at least
// the promised level; do not fall back to an ordinary won Hand game.
for (const [announcement, score] of [['schneider', -144], ['schwarz', -192], ['ouvert', -216]]) {
  let result = auction(game.start(skat, 42, 3), 0);
  for (const kind of ['hand', 'clubs', announcement]) result = skatAction(result, kind);
  result = finishSkat(result);
  assert.deepEqual(result.state.winners, [1, 2]);
  if (announcement !== 'ouvert') assert.match(result.state.tableNotice, /71: 49 eyes/);
  assert.ok(result.state.tableNotice.endsWith(`Score ${score}`), result.state.tableNotice);
}

// All four fixed Null values, also on successful games. Discard exactly the
// two picked-up skat cards so the deliberately weak starting hand is retained.
for (const handGame of [false, true]) for (const open of [false, true]) {
  let result = auction(game.start(nullWinner, 42, 3), 0);
  result = skatAction(result, handGame ? 'hand' : 'takeSkat');
  if (!handGame) {
    const pickedUp = result.state.zones.find(zone => zone.id === 'hand0').cards.slice(-2);
    for (const card of pickedUp) {
      const action = result.state.actions.find(action => action.card === card.id);
      result = game.act(0, action, result.state.revision);
      assert.equal(result.error, undefined, result.error);
      assert.equal(result.accepted, true);
    }
  }
  result = skatAction(result, 'null');
  result = skatAction(result, open ? 'ouvert' : 'closed');
  result = finishSkat(result);
  assert.deepEqual(result.state.winners, [0]);
  const score = handGame ? (open ? 59 : 35) : (open ? 46 : 23);
  assert.ok(result.state.tableNotice.endsWith(`Score ${score}`), result.state.tableNotice);
}
console.log('Skat: failed announcements above 60 eyes and all four successful fixed-value Null variants passed.');

// The global reference remains available through bidding, preparation and play.
let reference = game.start(skat, 42, 3);
for (const step of [null, 'bid', 'hold', 'passBid', 'passBid', 'hand', 'grand', 'closed']) {
  if (step) reference = skatAction(reference, step);
  const before = reference.state;
  for (let repeat = 0; repeat < 2; repeat++) {
    reference = skatAction(reference, 'bidValues');
    assert.equal(reference.state.currentPlayer, before.currentPlayer);
    assert.equal(reference.state.turn, before.turn);
    assert.equal(reference.state.round, before.round);
    assert.deepEqual(reference.state.zones, before.zones);
    assert.deepEqual(reference.state.actions, before.actions);
    assert.equal(reference.state.tableNotice, before.tableNotice);
    assert.match(reference.state.notice, /BIDDING VALUES\n18, 20, 22, 23, 24/);
    assert.match(reference.state.notice, /Null Ouvert Hand: 59/);
    assert.match(reference.state.notice, /192 \/ 216 \/ 240 \/ 264/);
  }
}
console.log('Skat: reusable global bidding reference preserves actions, cards and turn state.');

const blackjack = await fs.readFile(path.join(web, 'examples/blackjack.ges'), 'utf8');
assert.deepEqual(game.check(blackjack), { diagnostics: [] });
assert.equal(game.start(blackjack, 42, 1).state.playerLayout, 'bottom');
const bjCards = (state, player) => state.zones.find(zone => zone.id === `hand${player}`).cards;
const bjDealer = state => state.zones.find(zone => zone.id === 'dealer').cards;
function bjTotal(cards) {
  let total = cards.reduce((sum, card) => sum + (card.properties.rank === 'A' ? 11 : ['J', 'Q', 'K'].includes(card.properties.rank) ? 10 : Number(card.properties.rank)), 0);
  let aces = cards.filter(card => card.properties.rank === 'A').length;
  while (total > 21 && aces-- > 0) total -= 10;
  return total;
}
const bjNatural = cards => cards.length === 2 && bjTotal(cards) === 21;
function bjResult(cards, bank) {
  if (bjTotal(cards) > 21) return 'Loss';
  if (bjNatural(bank)) return bjNatural(cards) ? 'Push' : 'Loss';
  if (bjNatural(cards) || bjTotal(bank) > 21 || bjTotal(cards) > bjTotal(bank)) return 'Win';
  return bjTotal(cards) === bjTotal(bank) ? 'Push' : 'Loss';
}
function bjAct(result, kind) {
  const action = result.state.actions.find(action => action.kind === kind);
  assert.ok(action, `Missing Blackjack action ${kind}`);
  const next = game.act(result.state.currentPlayer, action, result.state.revision);
  assert.equal(next.error, undefined, next.error);
  assert.equal(next.accepted, true);
  return next;
}
function checkBlackjack(result) {
  const state = result.state;
  assert.equal(result.error, undefined, result.error);
  assert.equal(state.finished, true);
  assert.equal(state.zones.reduce((sum, zone) => sum + zone.count, 0), 52);
  assert.equal(state.zones.find(zone => zone.id === 'hole').count, 0);
  const bank = bjDealer(state);
  const live = state.players.some((_, player) => bjTotal(bjCards(state, player)) <= 21 && !bjNatural(bjCards(state, player)));
  if (live) assert.ok(bjTotal(bank) >= 17);
  const winners = [];
  state.players.forEach((_, player) => {
    const result = bjResult(bjCards(state, player), bank);
    assert.ok(state.playerBadges[player].startsWith(`${result} · `));
    assert.equal(state.playerBadgeColors[player], {Win: 'green', Loss: 'red', Push: 'gray'}[result]);
    assert.ok(state.tableNotice.includes(`— ${result}`));
    if (result === 'Win') winners.push(player);
  });
  assert.deepEqual(state.winners, winners);
  assert.deepEqual(state.notices, []);
  const results = state.players.map((_, player) => bjResult(bjCards(state, player), bank));
  const dealerWins = results.filter(result => result === 'Loss').length;
  const dealerLosses = results.filter(result => result === 'Win').length;
  const dealerPushes = results.filter(result => result === 'Push').length;
  const count = results.length;
  const dealerLabel = dealerWins === count ? 'Win' : dealerLosses === count ? 'Loss' : dealerPushes === count ? 'Push'
    : `${dealerWins} wins · ${dealerLosses} losses · ${dealerPushes} pushes`;
  const dealerZone = state.zones.find(zone => zone.id === 'dealer');
  assert.ok(dealerZone.badge.startsWith(`${dealerLabel} · `));
  assert.equal(dealerZone.badgeColor, dealerWins === count ? 'green' : dealerLosses === count ? 'red' : dealerPushes === count ? 'gray' : 'yellow');
}
for (const players of [1, 2, 3]) for (let seed = 0; seed < 30; seed++) {
  let result = game.start(blackjack, seed, players);
  assert.equal(result.error, undefined, result.error);
  let previous = 0;
  for (let moves = 0; !result.state.finished && moves < 52; moves++) {
    const state = result.state;
    assert.ok(state.currentPlayer >= previous);
    previous = state.currentPlayer;
    assert.deepEqual(state.actions.filter(action => !action.global).map(action => action.kind), ['hit', 'stand']);
    assert.equal(state.zones.find(zone => zone.id === 'hole').count, 1);
    assert.deepEqual(state.zones.find(zone => zone.id === 'hole').cards, []);
    assert.equal(bjDealer(state).length, 1);
    const total = bjTotal(bjCards(state, state.currentPlayer));
    result = bjAct(result, total < 17 ? 'hit' : 'stand');
  }
  checkBlackjack(result);
}

function fixedBlackjack(ranks, players = 1) {
  const pool = ['clubs', 'spades', 'hearts', 'diamonds'].flatMap(suit =>
    ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K'].map(rank => ({ suit, rank })));
  const drawn = ranks.map(rank => {
    const index = pool.findIndex(card => card.rank === rank);
    assert.ok(index >= 0);
    return pool.splice(index, 1)[0];
  });
  const ordered = [...drawn, ...pool].reverse();
  const literal = '[' + ordered.map(({ suit, rank }) => `[suit: '${suit}', rank: '${rank}', value: ${rank === 'A' ? 1 : ['J', 'Q', 'K'].includes(rank) ? 10 : Number(rank)}]`).join(', ') + ']';
  const result = game.start(blackjack.replace('cards: deck()[:shuffle]', `cards: ${literal}`), 42, players);
  assert.equal(result.error, undefined, result.error);
  return result;
}

// Draw order for a single player is player, dealer up, player, dealer hole.
for (const scenario of [
  { cards: ['10', 'A', '8', '6'], actions: ['stand'], result: ['Win'], bank: 17, count: 2 },
  { cards: ['A', '9', '6', '8', '10'], actions: ['hit', 'stand'], result: ['Push'], bank: 17, count: 2 },
  { cards: ['A', '9', 'A', '7', 'A', '8', '5'], actions: ['hit', 'hit'], result: ['Push'], bank: 21, count: 3 },
  { cards: ['A', 'A', 'K', 'Q'], actions: [], result: ['Push'], bank: 21, count: 2 },
  { cards: ['10', 'A', '9', 'K'], actions: [], result: ['Loss'], bank: 21, count: 2 },
  { cards: ['A', '9', 'K', '7'], actions: [], result: ['Win'], bank: 16, count: 2 },
  { cards: ['A', '10', '6', 'K', '8', '5', '10'], actions: ['stand'], result: ['Win', 'Loss'], bank: 21, count: 3 },
  { cards: ['10', '10', '10', '9', '8', '6', 'K', '6'], actions: ['hit', 'stand'], result: ['Loss', 'Win'], bank: 22, count: 3 },
]) {
  let result = fixedBlackjack(scenario.cards, scenario.result.length);
  for (const action of scenario.actions) result = bjAct(result, action);
  checkBlackjack(result);
  assert.deepEqual(result.state.playerBadges.map(badge => badge.split(' · ')[0]), scenario.result);
  assert.equal(bjTotal(bjDealer(result.state)), scenario.bank);
  assert.equal(bjDealer(result.state).length, scenario.count);
}
const tooMany = game.start(blackjack, 42, 4);
assert.equal(tooMany.error, undefined, tooMany.error);
assert.equal(tooMany.state.finished, true);
assert.deepEqual(tooMany.state.winners, []);
assert.match(tooMany.state.notice, /one to three players/);
console.log('Blackjack: 90 deals with 1–3 players, ace conversion, soft 17, naturals, pushes, dealer reveal, bust precedence and individual outcomes passed.');

// Explicit button captions are separate from click actions, with the same rule dispatch.
const buttonRules = `
on PrepareGame(players) {
  :board.create(setup: [table: [[id: #trick, label: 'Trick', layout: #spread,
    cards: [[suit: 'clubs', rank: 'A']]]], players: players[:select player => [id: player, zones: []]]])
}
on BeginTurn(player) {
  emit Action(spec: [action: #pile, button: 'Collect', zone: #trick, handler: Do(action, player)])
  emit Action(spec: [action: #card, button: 'Inspect', cards: :board.cards(zone: #trick)[:select card => card.id], handler: Inspect(action, player, card)])
  emit Action(spec: [action: #click, label: 'Play', cards: :board.cards(zone: #trick)[:select card => card.id], handler: Inspect(action, player, card)])
  emit Action(action: #area, button: 'Info', optional: true, finishTurn: false, consumable: #never, handler: Do(action, player), area: #table)
}
on Do(action, player) { emit Complete(action: action) }
on Inspect(action, player, card) { emit Complete(action: action) }
`;
let buttons = game.start(buttonRules, 42, 2);
assert.equal(buttons.error, undefined);
assert.deepEqual(buttons.state.actions.map(({ kind, button }) => [kind, button]), [
  ['pile', true], ['card', true], ['click', false], ['area', true],
]);
for (const caption of ["label: 'Collect', button: 'Collect'", "button: #collect"]) {
  assert.ok(game.start(buttonRules.replace("button: 'Collect'", caption), 42, 2).error);
}
buttons = game.start(buttonRules, 42, 2);
for (const kind of ['card', 'pile', 'area']) {
  buttons = game.act(buttons.state.currentPlayer, buttons.state.actions.find(action => action.kind === kind), buttons.state.revision);
  assert.equal(buttons.error, undefined);
  assert.equal(buttons.accepted, true);
}
console.log('Explicit zone, card and area buttons: presentation, caption validation and dispatch passed.');
