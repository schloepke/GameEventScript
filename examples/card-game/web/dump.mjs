// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/** Show compiler output as selectable GESA text without editing the source or game. */
export function attachDump(input, engine, getSourceName = () => 'my-game.ges', focusSource = () => input.focus()) {
  const byId = id => document.getElementById(id);
  const button = byId('show-dump');
  const view = byId('dump-view');
  const code = byId('dump-code');
  const status = byId('dump-status');
  const sourceElements = ['source-editor', 'highlight-status', 'diagnostics'].map(byId);
  const kinds = new Set(['plain', 'keyword', 'builtin', 'identifier', 'message', 'type', 'tag',
    'constant', 'number', 'string', 'comment', 'symbol', 'function', 'module', 'label', 'register']);
  let revision = 0;

  function paint(text, spans) {
    let previousEnd = 0;
    for (const [start, length, kind] of spans) {
      if (!Number.isInteger(start) || !Number.isInteger(length) || start < previousEnd || length < 0 ||
          start + length > text.length || !kinds.has(kind)) throw new Error('Invalid GESA highlight range');
      previousEnd = start + length;
    }
    const fragment = document.createDocumentFragment();
    let spanIndex = 0;

    function appendRange(target, start, end) {
      let offset = start;
      while (spanIndex < spans.length) {
        const [tokenStart, length, kind] = spans[spanIndex];
        if (tokenStart >= end) break;
        const tokenEnd = tokenStart + length;
        if (tokenEnd <= offset) { spanIndex++; continue; }
        target.append(document.createTextNode(text.slice(offset, Math.max(offset, tokenStart))));
        const token = document.createElement('span');
        token.className = `syntax-${kind}`;
        token.textContent = text.slice(Math.max(offset, tokenStart), Math.min(end, tokenEnd));
        target.append(token);
        offset = Math.min(end, tokenEnd);
        if (tokenEnd > end) break;
        spanIndex++;
      }
      target.append(document.createTextNode(text.slice(offset, end)));
    }

    const lines = text.split('\n');
    if (lines.at(-1) === '') lines.pop();
    const regions = new Map();
    for (let index = 0; index < lines.length; index++) {
      const match = /^\.region ("(?:[^"\\]|\\.)*")$/.exec(lines[index]);
      if (!match) continue;
      // Archived GES may contain region-like text. The generated closing marker
      // is the last exact match; skip the body when looking for further regions.
      const end = lines.lastIndexOf(`.region-end ${match[1]}`);
      if (end <= index) continue;
      regions.set(index, { end, name: match[1].slice(1, -1), folded: match[1] !== '"Code"' });
      index = end;
    }
    const rows = [];
    let offset = 0;
    for (let index = 0; index < lines.length; index++) {
      const row = document.createElement('div');
      row.className = 'dump-line';
      const gutter = document.createElement('span');
      gutter.className = 'dump-gutter';
      const number = document.createElement('span');
      number.className = 'dump-line-number';
      number.textContent = String(index + 1);
      number.setAttribute('aria-hidden', 'true');
      gutter.append(number);
      const region = regions.get(index);
      if (region) {
        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'dump-fold';
        const update = () => {
          toggle.textContent = region.folded ? '▸' : '▾';
          toggle.setAttribute('aria-expanded', String(!region.folded));
          toggle.setAttribute('aria-label', `${region.folded ? 'Expand' : 'Collapse'} ${region.name}`);
          for (let child = index + 1; child <= region.end; child++) rows[child].hidden = region.folded;
        };
        toggle.onclick = () => { region.folded = !region.folded; update(); };
        region.update = update;
        gutter.append(toggle);
      }
      const content = document.createElement('span');
      content.className = 'dump-line-content';
      appendRange(content, offset, offset + lines[index].length);
      offset += lines[index].length + 1;
      row.append(gutter, content);
      rows.push(row);
      fragment.append(row);
    }
    for (const region of regions.values()) region.update();
    code.replaceChildren(fragment);
  }

  async function refresh() {
    if (view.hidden) return;
    const ticket = ++revision;
    const source = input.value;
    code.replaceChildren();
    status.textContent = 'Compiling GESA dump …';
    try {
      const result = await engine.send({ type: 'dump', source, sourceName: getSourceName() });
      if (ticket !== revision || view.hidden || input.value !== source) return;
      if (result.diagnostics.length) {
        code.textContent = result.diagnostics.map(item =>
          `${item.line ? `Line ${item.line}, column ${item.column}: ` : ''}${item.code} — ${item.message}`).join('\n');
        status.textContent = 'Compilation failed. Return to Source view to correct the code.';
        return;
      }
      paint(result.dump, result.complete ? result.spans : []);
      code.scrollTop = 0;
      code.scrollLeft = 0;
      status.textContent = result.complete
        ? 'GESA dump · Read-only · Compiled from the current editor source.'
        : 'GESA dump · Read-only · Syntax highlighting unavailable for this dump.';
    } catch (error) {
      if (ticket !== revision || view.hidden) return;
      status.textContent = `Could not create dump: ${error.message}`;
    }
  }

  function reset() {
    revision++;
    view.hidden = true;
    code.replaceChildren();
    sourceElements.forEach(element => { element.hidden = false; });
    button.textContent = 'Dump view';
    button.setAttribute('aria-pressed', 'false');
  }

  button.onclick = () => {
    if (!view.hidden) {
      reset();
      focusSource();
      return;
    }
    view.hidden = false;
    sourceElements.forEach(element => { element.hidden = true; });
    button.textContent = 'Source view';
    button.setAttribute('aria-pressed', 'true');
    refresh();
    code.focus();
  };
  input.addEventListener('input', refresh);
  byId('check-source').addEventListener('click', refresh);
  return { reset, refresh };
}
