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
  let documentText = '', documentRows = [], documentRegions = new Map(), marks = [];

  function clearMatch() {
    const parents = new Set(marks.map(mark => mark.parentNode).filter(Boolean));
    for (const mark of marks) mark.replaceWith(...mark.childNodes);
    for (const parent of parents) parent.normalize();
    marks = [];
  }

  function changed() { view.dispatchEvent(new Event('dumpchange')); }

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
    documentRegions = regions;
    documentRows = [];
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
      documentRows.push({ start: offset - lines[index].length - 1, end: offset - 1, content });
      row.append(gutter, content);
      rows.push(row);
      fragment.append(row);
    }
    for (const region of regions.values()) region.update();
    code.replaceChildren(fragment);
    documentText = text;
  }

  async function refresh() {
    if (view.hidden) return;
    const ticket = ++revision;
    const source = input.value;
    clearMatch(); documentText = ''; documentRows = []; documentRegions = new Map();
    code.replaceChildren();
    changed();
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
      changed();
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
    clearMatch(); documentText = ''; documentRows = []; documentRegions = new Map();
    code.replaceChildren();
    sourceElements.forEach(element => { element.hidden = false; });
    button.textContent = 'Dump view';
    button.setAttribute('aria-pressed', 'false');
    changed();
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
  return {
    reset, refresh,
    getText() { return documentText; },
    getSelection() {
      const selection = window.getSelection();
      if (!selection?.rangeCount || !code.contains(selection.anchorNode) || !code.contains(selection.focusNode)) return '';
      const fragment = selection.getRangeAt(0).cloneContents();
      for (const gutter of fragment.querySelectorAll('.dump-gutter')) gutter.remove();
      const lines = [...fragment.querySelectorAll('.dump-line')];
      for (const line of lines.slice(0, -1)) line.append(document.createTextNode('\n'));
      return fragment.textContent;
    },
    clearMatch,
    showMatch(start, end) {
      clearMatch();
      const affected = documentRows.map((row, index) => ({ ...row, index }))
        .filter(row => row.start < end && row.end > start);
      for (const [line, region] of documentRegions) {
        if (region.folded && affected.some(row => row.index > line && row.index <= region.end)) {
          region.folded = false; region.update();
        }
      }
      for (const row of affected) {
        const walker = document.createTreeWalker(row.content, NodeFilter.SHOW_TEXT);
        const nodes = [];
        while (walker.nextNode()) nodes.push(walker.currentNode);
        let offset = row.start;
        for (const node of nodes) {
          const length = node.textContent.length;
          const from = Math.max(0, start - offset), to = Math.min(length, end - offset);
          if (to > from) {
            const selected = node.splitText(from);
            selected.splitText(to - from);
            const mark = document.createElement('mark');
            mark.className = 'search-hit';
            selected.replaceWith(mark); mark.append(selected); marks.push(mark);
          }
          offset += length;
        }
      }
      marks[0]?.scrollIntoView({ block: 'center', inline: 'center' });
    },
  };
}
