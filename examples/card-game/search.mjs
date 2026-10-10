// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/** Literal matches preserve original UTF-16 offsets, including case-insensitive Unicode. */
export function findMatches(text, query, matchCase = false) {
  if (!query) return [];
  const pattern = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return [...text.matchAll(new RegExp(pattern, matchCase ? 'gu' : 'giu'))]
    .map(match => ({ start: match.index, end: match.index + match[0].length }));
}

/** Search the complete active document, independently of viewport and folding. */
export function attachSearch(dialog, input, editor, dump) {
  const byId = id => document.getElementById(id);
  const query = byId('search-query'), matchCase = byId('search-case'), status = byId('search-status');
  let matches = [], index = -1, target, text = '';
  const current = () => byId('dump-view').hidden ? editor : dump;

  function update(step = 0, reveal = true) {
    const nextTarget = current(), nextText = nextTarget.getText();
    const changed = target !== nextTarget || text !== nextText;
    target?.clearMatch();
    target = nextTarget; text = nextText;
    matches = findMatches(text, query.value, matchCase.checked);
    if (!matches.length) {
      index = -1;
      status.textContent = query.value ? 'No matches' : 'Search source or dump';
    } else if (!reveal) {
      index = -1;
      status.textContent = `${matches.length} matches`;
    } else {
      index = changed || index < 0 ? (step < 0 ? matches.length - 1 : 0) : (index + step + matches.length) % matches.length;
      if (index >= matches.length) index = 0;
      target.showMatch(matches[index].start, matches[index].end);
      status.textContent = `${index + 1} / ${matches.length}`;
    }
    byId('search-previous').disabled = byId('search-next').disabled = !matches.length;
  }

  const reset = () => { index = -1; update(); };
  query.addEventListener('input', reset);
  matchCase.addEventListener('change', reset);
  byId('search-previous').onclick = () => update(-1);
  byId('search-next').onclick = () => update(1);
  // Editing must never move the caret back to a search result.
  input.addEventListener('input', () => update(0, false));
  byId('dump-view').addEventListener('dumpchange', reset);
  dialog.addEventListener('keydown', event => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'f') {
      event.preventDefault(); event.stopPropagation();
      // Read before focus changes; keep the query when there is no document selection.
      if (event.target !== query) {
        const selected = current().getSelection();
        if (selected) { query.value = selected; reset(); }
      }
      query.focus(); query.select();
    } else if (event.key === 'F3' || (event.target === query && event.key === 'Enter')) {
      event.preventDefault(); event.stopPropagation(); update(event.shiftKey ? -1 : 1);
    } else if (event.target === query && event.key === 'Escape') {
      event.preventDefault(); event.stopPropagation(); query.value = ''; reset();
      if (current() === editor) editor.focus(); else byId('dump-code').focus();
    }
  }, true);
  dialog.addEventListener('close', () => { query.value = ''; reset(); });
  update();
}
