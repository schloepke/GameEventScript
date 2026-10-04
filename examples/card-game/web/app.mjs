// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { attachEditor } from './editor.mjs';
import { attachGameLibrary } from './games.mjs';
import { showLoading } from './loading.mjs';
import { createCardActionPicker } from './card-actions.mjs';

const byId = (id) => document.getElementById(id);
const cardActions = createCardActionPicker(act);
let worker,
  state,
  timer,
  request = 0,
  busy = false;
let seatLayoutFrame = 0;
const seatResize = new ResizeObserver(() => {
  cancelAnimationFrame(seatLayoutFrame);
  seatLayoutFrame = requestAnimationFrame(() => {
    const board = byId('board');
    for (const area of board.querySelectorAll('.seat-left, .seat-right')) {
      const content = area.querySelector('.player-content');
      const left = area.classList.contains('seat-left');
      // Rotation swaps axes: table height constrains card rows, their natural
      // height determines the horizontal space this player needs beside it.
      content.style.width = `${area.clientHeight}px`;
      const width = Math.max(184, Math.ceil(content.offsetHeight) + 2);
      board.style.setProperty(left ? '--left-seat-width' : '--right-seat-width', `${width}px`);
      content.style.transform = left
        ? `translateX(${width - 2}px) rotate(90deg)`
        : `translateY(${area.clientHeight}px) rotate(-90deg)`;
    }
  });
});
let noticeQueue = [];

function showNextNotice() {
  if (byId('notice-dialog').open || noticeQueue.length === 0) return;
  byId('notice-message').textContent = noticeQueue.shift();
  byId('notice-dialog').showModal();
}

byId('notice-dialog').addEventListener('close', showNextNotice);

const symbols = { clubs: '♣', spades: '♠', hearts: '♥', diamonds: '♦' };

function lock(value) {
  busy = value;
  if (value) cardActions.close();
  document.querySelectorAll('#board button').forEach((button) => {
    button.disabled = value;
  });
}

function stop(message = 'Stopped. Choose “New game” to start again.') {
  worker?.terminate();
  worker = null;
  clearTimeout(timer);
  byId('wasm-loading').hidden = true;
  lock(true);
  byId('status').textContent = message;
}

function showError(message) {
  stop('Game stopped after an error.');
  byId('error-message').textContent = String(message);
  byId('show-error').hidden = false;
  if (!byId('error-dialog').open) byId('error-dialog').showModal();
}

function send(payload) {
  if (!worker) return;
  lock(true);
  byId('status').textContent = payload.type === 'start' ? 'Preparing game …' : 'Running rules …';
  const id = ++request;
  worker.postMessage({ id, ...payload });
  clearTimeout(timer);
  timer = setTimeout(
    () => showError('Time limit reached. Check the rules or your connection and restart.'),
    payload.type === 'start' ? 120000 : 15000,
  );
}

function act(action) {
  if (!busy && state)
    send({ type: 'action', player: state.currentPlayer, action, revision: state.revision });
}

function render(next) {
  cardActions.close();
  state = next;
  seatResize.disconnect();
  cancelAnimationFrame(seatLayoutFrame);
  byId('board').style.removeProperty('--left-seat-width');
  byId('board').style.removeProperty('--right-seat-width');
  byId('board').replaceChildren();
  byId('board').dataset.players = String(state.players.length);
  const table = document.createElement('section');
  table.className = 'table-area';
  for (const side of ['left', 'right']) {
    if (
      state.zones.some(
        (zone) =>
          zone.owner === null &&
          (side === 'left' ? ['nw', 'w', 'sw'] : ['ne', 'e', 'se']).includes(zone.position),
      )
    ) {
      table.classList.add(`has-${side}`);
    }
  }
  table.setAttribute('aria-label', 'Table');
  const tableTitle = document.createElement('h2');
  tableTitle.textContent = 'Table';
  tableTitle.className = 'area-heading';
  const areaHeadings = new Map([['table', tableTitle]]);
  table.append(tableTitle);
  const tableStatus = document.createElement('div');
  tableStatus.className = 'table-status';
  tableStatus.setAttribute('role', 'status');
  tableStatus.textContent = state.tableNotice;
  tableStatus.hidden = !state.tableNotice;
  table.append(tableStatus);
  const tableRows = new Map();
  for (const position of ['nw', 'n', 'ne', 'w', 'center', 'e', 'sw', 's', 'se']) {
    const slot = document.createElement('div');
    slot.className = `table-slot slot-${position}`;
    table.append(slot);
    for (const row of state.rows.filter((row) => row.owner === null && row.position === position)) {
      const element = document.createElement('div');
      element.className = 'table-row';
      tableRows.set(`${position}:${row.index}`, element);
      slot.append(element);
    }
  }
  byId('board').append(table);
  const playerRows = new Map();
  state.players.forEach((name, index) => {
    const area = document.createElement('section');
    const seat = (state.players.length === 2 ? ['bottom', 'top'] : ['bottom', 'left', 'top', 'right'])[index];
    area.className = `player-area player-${index} seat-${seat}${index === state.currentPlayer ? ' active-player' : ''}`;
    const content = document.createElement('div');
    content.className = 'player-content';
    area.append(content);
    const heading = document.createElement('h2');
    heading.className = 'area-heading';
    areaHeadings.set(index, heading);
    heading.textContent = `${name}${index === state.currentPlayer ? (state.waitingForRound ? ' · Prepare next round' : ' · Your turn') : ''}`;
    content.append(heading);
    for (const row of state.rows.filter((row) => row.owner === index)) {
      const element = document.createElement('div');
      element.className = `player-row align-${row.position}`;
      playerRows.set(`${index}:${row.index}`, element);
      content.append(element);
    }
    byId('board').append(area);
    if (seat === 'left' || seat === 'right') {
      seatResize.observe(area);
      seatResize.observe(content);
    }
  });
  const zoneActions = new Map();
  const zoneHeadings = new Map();
  for (const action of state.actions) {
    if (action.zone && action.card === null && !zoneActions.has(action.zone)) {
      zoneActions.set(action.zone, action);
    }
  }
  for (const zone of state.zones) {
    const section = document.createElement('section');
    section.className = `zone zone-${zone.layout} position-${zone.position}`;
    const zoneAction = zoneActions.get(zone.id);
    if (zoneAction) {
      const trigger = document.createElement('button');
      trigger.className = 'zone-action';
      trigger.type = 'button';
      trigger.setAttribute('aria-label', `${zoneAction.label}: ${zone.label}`);
      trigger.title = zoneAction.label;
      trigger.onclick = () => act(zoneAction);
      section.append(trigger);
    }
    const title = document.createElement('h2');
    title.textContent = zone.label;
    zoneHeadings.set(zone.id, title);
    section.append(title);
    const cards = document.createElement('div');
    cards.className = 'cards';
    for (const card of zone.layout === 'pile' ? zone.cards.slice(-1) : zone.cards) {
      const offers = state.actions.filter((action) => action.card === card.id);
      const offer = offers[0];
      const element = document.createElement(offer ? 'button' : 'div');
      element.className = `card ${['hearts', 'diamonds'].includes(card.properties.suit) ? 'red' : ''} ${offer ? 'playable' : ''}`;
      element.textContent = `${symbols[card.properties.suit] ?? card.properties.suit} ${card.properties.rank}`;
      const id = document.createElement('small');
      id.textContent = `#${card.id}`;
      element.append(id);
      if (zone.layout === 'pile') {
        const count = document.createElement('span');
        count.className = 'pile-count';
        count.textContent = String(zone.count);
        count.setAttribute('aria-label', `${zone.count} cards`);
        element.append(count);
      }
      if (offer) {
        element.setAttribute(
          'aria-label',
          `${offers.length > 1 ? 'Choose action' : offer.label}: ${card.properties.suit} ${card.properties.rank}`,
        );
        if (offers.length > 1) {
          element.setAttribute('aria-haspopup', 'dialog');
          element.setAttribute('aria-expanded', 'false');
        }
        element.onclick = () => {
          if (busy) return;
          if (offers.length === 1) act(offer);
          else cardActions.open(element, offers);
        };
      }
      cards.append(element);
    }
    if (zone.count > 0 && zone.cards.length === 0) {
      const back = document.createElement('div');
      back.className = 'card back';
      back.textContent = String(zone.count);
      cards.append(back);
    }
    section.append(cards);
    if (!zone.count) {
      const empty = document.createElement('div');
      empty.className = 'empty-pile';
      empty.textContent = 'Empty';
      cards.append(empty);
    }
    (zone.owner === null
      ? tableRows.get(`${zone.position}:${zone.row}`)
      : playerRows.get(`${zone.owner}:${zone.row}`)
    ).append(section);
  }
  for (const offer of state.actions.filter(
    (action) => action.card === null && zoneActions.get(action.zone) !== action,
  )) {
    const button = document.createElement('button');
    button.textContent =
      offer.label +
      (offer.remaining > 1 ? ` (${offer.remaining} remaining)` : '') +
      (offer.optional ? '' : offer.group ? ' · required alternative' : ' · required');
    button.onclick = () => act(offer);
    button.className = 'area-action';
    (offer.zone ? zoneHeadings.get(offer.zone) : areaHeadings.get(offer.area)).append(button);
  }
  byId('status').textContent = state.finished
    ? state.winners.length === 0
      ? 'Draw.'
      : `${state.winners.map((player) => state.players[player]).join(" & ")} ${state.winners.length === 1 ? "wins" : "win"}!`
    : state.waitingForRound
      ? `Round ${state.round} complete · Prepare the next round`
      : `${state.players[state.currentPlayer]} to play · Turn ${state.turn}`;
  noticeQueue.push(...state.notices);
  showNextNotice();
  lock(state.finished || state.failed);
}

function restart() {
  const seedInput = byId('seed');
  const seed = seedInput.value === '' ? undefined : Number(seedInput.value);
  if (
    seedInput.validity.badInput ||
    (seed !== undefined && (!Number.isInteger(seed) || seed < -2147483648 || seed > 2147483647))
  ) {
    byId('status').textContent = 'Seed must be an Int32 integer.';
    return;
  }
  stop();
  noticeQueue = [];
  byId('notice-dialog').close();
  byId('error-dialog').close();
  byId('show-error').hidden = true;
  byId('error-message').textContent = '';
  const active = new Worker('./worker.mjs', { type: 'module' });
  worker = active;
  showLoading(byId('wasm-loading'), { phase: 'download', loaded: 0 });
  active.onmessage = ({ data }) => {
    if (active !== worker) return;
    if (data.type === 'loading') {
      showLoading(byId('wasm-loading'), data);
      return;
    }
    if (data.id !== request) return;
    byId('wasm-loading').hidden = true;
    clearTimeout(timer);
    if (data.error) {
      showError(data.error);
      return;
    }
    render(data.state);
    if (data.accepted === false) byId('status').textContent = data.reason;
  };
  active.onerror = (event) => {
    if (active === worker) showError(`Worker error: ${event.message}`);
  };
  send({
    type: 'start',
    source: byId('source').value,
    seed,
    players: Number(byId('players').value),
  });
}

byId('show-help').onclick = () => byId('help-dialog').showModal();

byId('show-error').onclick = () => byId('error-dialog').showModal();
byId('restart').onclick = restart;
byId('stop').onclick = () => stop();
try {
  await attachGameLibrary(
    byId('source'),
    byId('example'),
    byId('load-example'),
    byId('save-status'),
    byId('undo-example'),
    byId('players'),
  );
  let editorAttached = false;
  byId('source')
    .closest('details')
    .addEventListener('toggle', (event) => {
      if (!event.currentTarget.open || editorAttached) return;
      editorAttached = true;
      attachEditor(
        byId('source'),
        byId('source-colors'),
        byId('source-editor'),
        byId('highlight-status'),
      );
    });
  byId('restart').disabled = false;
  restart();
} catch (error) {
  showError(error);
}
