// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

const libraryKey = 'ges-card-lab.library.v2';
const legacyKey = 'ges-card-lab.draft.v1';

/** Keep one autosaved draft and one explicit saved copy, independent of examples. */
export async function attachGameLibrary(input, select, saveButton, status, playerSelect, onSelect, onError) {
  const response = await fetch('./examples.json');
  if (!response.ok) throw new Error('Could not load the game list.');
  const examples = await response.json();
  if (!Array.isArray(examples) || !examples.length || examples.some(item =>
    typeof item.id !== 'string' || ['draft', 'saved'].includes(item.id) ||
    typeof item.name !== 'string' || typeof item.source !== 'string' ||
    !/^examples\/[a-z0-9-]+\.ges$/.test(item.source) ||
    (item.players !== undefined && ![2, 3, 4].includes(item.players)))) {
    throw new Error('Invalid game list.');
  }
  const validSlot = slot => slot && typeof slot.source === 'string' && [2, 3, 4].includes(slot.players);
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
    select.replaceChildren();
    for (const entry of [...examples, { id: 'draft', name: 'Draft · autosaved' }, { id: 'saved', name: 'Saved' }]) {
      const option = document.createElement('option');
      option.value = entry.id;
      option.textContent = entry.name;
      option.disabled = ['draft', 'saved'].includes(entry.id) && !library[entry.id];
      select.append(option);
    }
    select.value = library.selected;
  }

  async function load(id) {
    if (id === 'draft' || id === 'saved') {
      if (!library[id]) throw new Error('This slot is empty.');
      return { ...library[id] };
    }
    const example = examples.find(item => item.id === id);
    if (!example) throw new Error('Unknown game.');
    const result = await fetch(example.source);
    if (!result.ok) throw new Error('Could not load the game. Your code is unchanged.');
    return { source: await result.text(), players: example.players ?? Number(playerSelect.value) };
  }

  function apply(slot) {
    applying = true;
    input.value = slot.source;
    playerSelect.value = String(slot.players);
    input.dispatchEvent(new Event('input', { bubbles: true }));
    applying = false;
  }

  function autosave() {
    if (applying) return;
    generation++;
    library.draft = { source: input.value, players: Number(playerSelect.value) };
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
  playerSelect.addEventListener('change', autosave);
  select.disabled = false;
  saveButton.disabled = false;
  select.onchange = async () => {
    const id = select.value;
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
  };
  saveButton.onclick = () => {
    generation++;
    library.saved = { source: input.value, players: Number(playerSelect.value) };
    library.selected = 'saved';
    options();
    persist('Saved slot updated. Further edits go to Draft.');
  };
}
