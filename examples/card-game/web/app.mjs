// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { attachEditor } from './editor.mjs';

const byId = (id) => document.getElementById(id);
let worker,
  state,
  timer,
  request = 0,
  busy = false;
const symbols = { clubs: '♣', spades: '♠', hearts: '♥', diamonds: '♦' };

function lock(value) {
  busy = value;
  document.querySelectorAll('#board button, #choices button').forEach((button) => {
    button.disabled = value;
  });
}

function stop(message = 'Stopped. Choose “New game” to start again.') {
  worker?.terminate();
  worker = null;
  clearTimeout(timer);
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
  byId('status').textContent = 'Running rules …';
  const id = ++request;
  worker.postMessage({ id, ...payload });
  clearTimeout(timer);
  timer = setTimeout(
    () =>
      showError(
        'Time limit reached. Check the rules or your connection and restart.',
      ),
    payload.type === 'start' ? 120000 : 15000,
  );
}

function act(action) {
  if (!busy && state)
    send({ type: 'action', player: state.currentPlayer, action, revision: state.revision });
}

function render(next) {
  state = next;
  byId('board').replaceChildren();
  byId('choices').replaceChildren();
  for (const zone of state.zones) {
    const section = document.createElement('section');
    section.className = 'zone';
    const title = document.createElement('h2');
    const label =
      zone.id === 'draw'
        ? 'Draw pile'
        : zone.id === 'discard'
          ? 'Discard pile'
          : zone.owner !== null
            ? `${state.players[zone.owner]} · Hand`
            : zone.id;
    title.textContent = `${label} · ${zone.count} ${zone.count === 1 ? 'card' : 'cards'}`;
    section.append(title);
    const cards = document.createElement('div');
    cards.className = 'cards';
    for (const card of zone.cards) {
      const offer = state.actions.find(
        (action) => action.kind === 'play' && action.card === card.id,
      );
      const element = document.createElement(offer ? 'button' : 'div');
      element.className = `card ${['hearts', 'diamonds'].includes(card.properties.suit) ? 'red' : ''} ${offer ? 'playable' : ''}`;
      element.textContent = `${symbols[card.properties.suit] ?? card.properties.suit} ${card.properties.rank}`;
      const id = document.createElement('small');
      id.textContent = `#${card.id}`;
      element.append(id);
      if (offer) {
        element.setAttribute(
          'aria-label',
          `Play ${card.properties.suit} ${card.properties.rank}`,
        );
        element.onclick = () => act(offer);
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
    byId('board').append(section);
  }
  for (const offer of state.actions.filter((action) => action.kind !== 'play')) {
    const button = document.createElement('button');
    button.textContent = { draw: 'Draw a card', pass: 'Pass' }[offer.kind] ?? offer.kind;
    button.onclick = () => act(offer);
    byId('choices').append(button);
  }
  byId('status').textContent = state.finished
    ? state.winner === null
      ? 'Draw.'
      : `${state.players[state.winner]} wins!`
    : `${state.players[state.currentPlayer]} to play · Turn ${state.revision + 1}`;
  if (state.notice) byId('status').textContent += ` · ${state.notice}`;
  lock(state.finished || state.failed);
}

function restart() {
  const seed = Number(byId('seed').value);
  if (!Number.isInteger(seed) || seed < -2147483648 || seed > 2147483647) {
    byId('status').textContent = 'Seed must be an Int32 integer.';
    return;
  }
  stop();
  byId('error-dialog').close();
  byId('show-error').hidden = true;
  byId('error-message').textContent = '';
  const active = new Worker('./worker.mjs', { type: 'module' });
  worker = active;
  active.onmessage = ({ data }) => {
    if (active !== worker || data.id !== request) return;
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
  send({ type: 'start', source: byId('source').value, seed });
}

byId('show-error').onclick = () => byId('error-dialog').showModal();
byId('restart').onclick = restart;
byId('stop').onclick = () => stop();
try {
  const response = await fetch('./rules.ges');
  if (!response.ok) throw new Error(`Could not load rules: ${response.status}`);
  byId('source').value = await response.text();
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
