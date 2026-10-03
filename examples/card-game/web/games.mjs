// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

const draftKey = 'ges-card-lab.draft.v1';

/** Restore the current draft and offer explicit loading of bundled examples. */
export async function attachGameLibrary(input, select, loadButton, status, undoButton) {
  let draft = null;
  let previous = null;
  let storageReadable = true;
  try {
    const stored = localStorage.getItem(draftKey);
    if (stored !== null) {
      const parsed = JSON.parse(stored);
      if (parsed.version !== 1 || typeof parsed.source !== 'string') {
        throw new Error('Invalid draft');
      }
      draft = parsed;
    }
  } catch {
    storageReadable = false;
  }

  const response = await fetch('./examples.json');
  if (!response.ok) throw new Error('Could not load the example list.');
  const examples = await response.json();
  if (
    !Array.isArray(examples) ||
    examples.length === 0 ||
    examples.some(
      (item) =>
        typeof item.id !== 'string' ||
        typeof item.name !== 'string' ||
        typeof item.source !== 'string' ||
        !/^examples\/[a-z0-9-]+\.ges$/.test(item.source),
    )
  ) {
    throw new Error('Invalid example list.');
  }
  for (const example of examples) {
    const option = document.createElement('option');
    option.value = example.id;
    option.textContent = example.name;
    select.append(option);
  }
  select.value = examples.some((item) => item.id === draft?.exampleId)
    ? draft.exampleId
    : examples[0].id;

  async function readExample(example) {
    const result = await fetch(example.source);
    if (!result.ok) throw new Error('Could not load the example. Your code is unchanged.');
    return result.text();
  }

  function save() {
    try {
      localStorage.setItem(
        draftKey,
        JSON.stringify({
          version: 1,
          source: input.value,
          exampleId: select.value,
        }),
      );
      status.textContent = 'Saved in this browser.';
    } catch {
      status.textContent = 'Local saving is unavailable. Keep a copy of your code before leaving.';
    }
  }

  input.value = draft !== null ? draft.source : await readExample(examples[0]);
  status.textContent =
    draft !== null
      ? 'Saved draft restored from this browser.'
      : storageReadable
        ? 'Edits are saved automatically in this browser.'
        : 'The saved draft could not be read. Keep a copy of your code.';
  input.addEventListener('input', save);

  loadButton.disabled = false;
  loadButton.onclick = async () => {
    const example = examples.find((item) => item.id === select.value);
    if (!example) return;
    loadButton.disabled = true;
    // If editing continues during the fetch, do not overwrite those new changes.
    const before = input.value;
    try {
      const source = await readExample(example);
      if (input.value !== before) {
        status.textContent = 'Code changed while loading. Choose Load example again to replace it.';
        return;
      }
      previous = before;
      input.value = source;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      undoButton.hidden = false;
      status.textContent += ' Example loaded; choose New game to apply it.';
    } catch (error) {
      status.textContent = error.message;
    } finally {
      loadButton.disabled = false;
    }
  };

  undoButton.onclick = () => {
    if (previous === null) return;
    input.value = previous;
    previous = null;
    undoButton.hidden = true;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  };
}
