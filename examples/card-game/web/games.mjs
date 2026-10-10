// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

const libraryKey = 'ges-card-lab.library.v2';
const legacyKey = 'ges-card-lab.draft.v1';
const validCounts = counts => Array.isArray(counts) && counts.length > 0 &&
  new Set(counts).size === counts.length && counts.every(count => [1, 2, 3, 4].includes(count));

/** Keep one autosaved draft and one explicit saved copy, independent of examples. */
export async function attachGameLibrary(input, select, saveButton, status, playerSelect, onSelect, onError, editorSelect) {
  const response = await fetch('./examples.json', { cache: 'no-cache' });
  if (!response.ok) throw new Error('Could not load the game list.');
  const examples = await response.json();
  if (!Array.isArray(examples) || !examples.length || examples.some(item =>
    typeof item.id !== 'string' || ['draft', 'saved'].includes(item.id) ||
    typeof item.name !== 'string' || typeof item.source !== 'string' ||
    !/^examples\/[a-z0-9-]+\.ges$/.test(item.source) ||
    (item.playerCounts !== undefined && !validCounts(item.playerCounts)) ||
    (item.players !== undefined && !(item.playerCounts ?? [2, 3, 4]).includes(item.players)))) {
    throw new Error('Invalid game list.');
  }
  const validSlot = slot => slot && typeof slot.source === 'string' && [1, 2, 3, 4].includes(slot.players) &&
    (slot.playerCounts === undefined || (validCounts(slot.playerCounts) && slot.playerCounts.includes(slot.players)));
  let playerCounts = [2, 3, 4];
  let library = { version: 2, selected: examples[0].id, draft: null, saved: null };
  let storageWarning = '';
  try {
    const stored = localStorage.getItem(libraryKey);
    if (stored !== null) {
      const value = JSON.parse(stored);
      if (value.version !== 2 || (value.draft !== null && !validSlot(value.draft)) ||
          (value.saved !== null && !validSlot(value.saved))) throw new Error('Invalid library');
      library = value;
    } else {
      const legacy = JSON.parse(localStorage.getItem(legacyKey) ?? 'null');
      if (legacy?.version === 1 && typeof legacy.source === 'string') {
        library.draft = { source: legacy.source, players: [2, 3, 4].includes(legacy.players) ? legacy.players : 2 };
        library.selected = 'draft';
      }
    }
  } catch {
    storageWarning = 'Stored code could not be read. Keep a copy before leaving.';
  }
  let generation = 0;
  let applying = false;

  function persist(message) {
    try {
      localStorage.setItem(libraryKey, JSON.stringify(library));
      status.textContent = storageWarning || message;
    } catch {
      status.textContent = 'Local saving is unavailable. Keep a copy before leaving; slots work only until reload.';
    }
  }

  function options() {
    const entries = [...examples, { id: 'draft', name: 'Draft · autosaved' }, { id: 'saved', name: 'Saved' }];
    for (const target of [select, editorSelect].filter(Boolean)) {
      const previous = target.value;
      target.replaceChildren();
      for (const entry of entries) {
        const option = document.createElement('option');
        option.value = entry.id;
        option.textContent = entry.name;
        option.disabled = ['draft', 'saved'].includes(entry.id) && !library[entry.id];
        target.append(option);
      }
      const available = entries.some(entry => entry.id === previous &&
        (!['draft', 'saved'].includes(entry.id) || library[entry.id]));
      target.value = target === editorSelect && available ? previous : library.selected;
    }
  }

  async function load(id) {
    if (id === 'draft' || id === 'saved') {
      if (!library[id]) throw new Error('This slot is empty.');
      return { ...library[id] };
    }
    const example = examples.find(item => item.id === id);
    if (!example) throw new Error('Unknown game.');
    const result = await fetch(example.source, { cache: 'no-cache' });
    if (!result.ok) throw new Error('Could not load the game. Your code is unchanged.');
    const counts = example.playerCounts ?? [2, 3, 4];
    const requested = example.players ?? Number(playerSelect.value);
    return { source: await result.text(), players: counts.includes(requested) ? requested : counts[0], playerCounts: counts };
  }

  function apply(slot) {
    applying = true;
    input.value = slot.source;
    input.setSelectionRange(0, 0);
    input.scrollTop = 0;
    input.scrollLeft = 0;
    playerCounts = slot.playerCounts ?? [1, 2, 3, 4];
    playerSelect.replaceChildren();
    for (const count of playerCounts) {
      const option = document.createElement('option');
      option.value = String(count);
      option.textContent = String(count);
      playerSelect.append(option);
    }
    playerSelect.value = String(slot.players);
    input.dispatchEvent(new Event('input', { bubbles: true }));
    applying = false;
  }

  function autosave() {
    if (applying) return;
    generation++;
    library.draft = { source: input.value, players: Number(playerSelect.value), playerCounts: [...playerCounts] };
    library.selected = 'draft';
    options();
    persist('Draft autosaved. Save keeps a separate copy in Saved.');
  }

  if (!examples.some(item => item.id === library.selected) && !library[library.selected]) {
    library.selected = examples[0].id;
  }
  apply(await load(library.selected));
  options();
  status.textContent = storageWarning || 'Edits are autosaved to Draft. Save replaces the Saved slot.';
  input.addEventListener('input', autosave);
  playerSelect.addEventListener('change', () => {
    autosave();
    onSelect();
  });
  select.disabled = false;
  saveButton.disabled = false;
  async function choose(id) {
    const ticket = ++generation;
    try {
      const slot = await load(id);
      if (ticket !== generation) return;
      apply(slot);
      library.selected = id;
      options();
      persist('Game loaded. Your Draft and Saved slots are retained.');
      onSelect();
    } catch (error) {
      if (ticket !== generation) return;
      select.value = library.selected;
      status.textContent = error.message;
      onError(error.message);
    }
  }

  select.onchange = () => choose(select.value);
  saveButton.onclick = () => {
    generation++;
    library.saved = { source: input.value, players: Number(playerSelect.value), playerCounts: [...playerCounts] };
    library.selected = 'saved';
    options();
    persist('Saved slot updated. Further edits go to Draft.');
  };
  return { examples, select: choose };
}
