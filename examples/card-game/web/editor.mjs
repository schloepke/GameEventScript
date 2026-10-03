// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { diagnosticRange } from './diagnostics.mjs';
import { showLoading } from './loading.mjs';

/** Paint Swift-provided UTF-16 ranges; keep the native textarea's editing behavior. */
export function attachEditor(input, colors, container, status) {
  let worker,
    debounce,
    timeout,
    revision = 0,
    inFlight = false,
    composing = false;
  const lines = document.getElementById('source-lines');
  let lineCount = 0;
  const loading = document.getElementById('editor-loading');
  const diagnosticsList = document.getElementById('diagnostics');
  const checkButton = document.getElementById('check-source');
  const kinds = new Set([
    'plain',
    'keyword',
    'builtin',
    'identifier',
    'message',
    'type',
    'tag',
    'constant',
    'number',
    'string',
    'comment',
    'symbol',
    'function',
    'module',
    'label',
    'register',
  ]);

  function syncScroll() {
    colors.style.width = `${input.clientWidth}px`;
    colors.style.height = `${input.clientHeight}px`;
    colors.scrollTop = input.scrollTop;
    colors.scrollLeft = input.scrollLeft;
    lines.style.height = `${input.clientHeight}px`;
    lines.scrollTop = input.scrollTop;
  }

  function updateLines() {
    const count = input.value.split('\n').length;
    if (count !== lineCount) {
      lineCount = count;
      lines.textContent = Array.from({ length: count }, (_, index) => index + 1).join('\n');
      container.style.setProperty('--gutter-width', `${Math.max(3, String(count).length) * 9 + 24}px`);
    }
    syncScroll();
  }

  function insertText(text, start = input.selectionStart, end = input.selectionEnd) {
    input.setSelectionRange(start, end);
    // Native insertion preserves the textarea's undo history where supported.
    if (!document.execCommand('insertText', false, text)) {
      input.setRangeText(text, start, end, 'end');
      input.dispatchEvent(new Event('input', { bubbles: true }));
    }
  }

  function editKey(event) {
    if (composing || event.isComposing || event.ctrlKey || event.metaKey || event.altKey) return;
    const start = input.selectionStart;
    const end = input.selectionEnd;
    const lineStart = start === 0 ? 0 : input.value.lastIndexOf('\n', start - 1) + 1;
    if (event.key === 'Backspace' && start === end && lineStart > 0) {
      const indentLength = input.value.slice(lineStart).match(/^[ \t]*/)[0].length;
      if (indentLength > 0 && start <= lineStart + indentLength) {
        event.preventDefault();
        // Join the lines in one undoable edit, including indentation after the caret.
        const newlineStart = input.value[lineStart - 2] === '\r' ? lineStart - 2 : lineStart - 1;
        insertText('', newlineStart, lineStart + indentLength);
      }
    } else if (event.key === 'Tab') {
      event.preventDefault();
      if (!event.shiftKey && !input.value.slice(start, end).includes('\n')) {
        insertText('    ');
      } else {
        // A selection ending at the next line's start does not indent that line.
        const last = end > start && input.value[end - 1] === '\n' ? end - 1 : end;
        const block = input.value.slice(lineStart, last);
        const rows = block.split('\n');
        const removed = rows.map((row) => event.shiftKey ? (row.match(/^(?: {1,4}|\t)/)?.[0].length ?? 0) : 0);
        const replacement = rows.map((row, index) => event.shiftKey ? row.slice(removed[index]) : '    ' + row).join('\n');
        insertText(replacement, lineStart, last);
        const delta = replacement.length - block.length;
        const firstDelta = event.shiftKey ? -Math.min(start - lineStart, removed[0]) : 4;
        input.setSelectionRange(start + firstDelta, Math.max(start + firstDelta, end + delta));
      }
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const prefix = input.value.slice(lineStart, start);
      const indent = prefix.match(/^[ \t]*/)[0];
      const lineEnd = input.value.indexOf('\n', end);
      const suffix = input.value.slice(end, lineEnd < 0 ? input.value.length : lineEnd);
      const code = prefix.trim();
      const opensBlock = /[\[{]$/.test(code);
      // A split inside a statement gets one continuation level; closing delimiters do not.
      const continuesStatement = code.length > 0 && !code.startsWith('//')
        && suffix.trim().length > 0 && !/^[\]})]/.test(suffix.trimStart());
      const extra = opensBlock || continuesStatement ? '    ' : '';
      const leadingSpace = suffix.match(/^[ \t]*/)[0].length;
      insertText('\n' + indent + extra, start, end + leadingSpace);
    }
  }

  function plain() {
    container.classList.remove('highlighted');
    colors.replaceChildren();
  }

  function fail(message) {
    loading.hidden = true;
    clearTimeout(timeout);
    worker?.terminate();
    worker = null;
    inFlight = false;
    plain();
    status.textContent = message;
  }

  function paint(source, spans, diagnostics) {
    const ranges = diagnostics
      .map((d) => ({ ...diagnosticRange(source, d), diagnostic: d }))
      .filter((r) => Number.isInteger(r.start));
    const text = source + ' \n';
    const boundaries = new Set([0, text.length]);
    let previousEnd = 0;
    for (const [start, length, kind] of spans) {
      if (
        !Number.isInteger(start) ||
        !Number.isInteger(length) ||
        start < previousEnd ||
        length < 0 ||
        start + length > source.length ||
        !kinds.has(kind)
      )
        throw new Error('Invalid highlight range');
      boundaries.add(start);
      boundaries.add(start + length);
      previousEnd = start + length;
    }
    for (const range of ranges) {
      boundaries.add(range.start);
      boundaries.add(range.end);
    }
    const points = [...boundaries].sort((a, b) => a - b);
    const fragment = document.createDocumentFragment();
    let token = 0;
    for (let i = 0; i < points.length - 1; i++) {
      const start = points[i],
        end = points[i + 1];
      while (token < spans.length && spans[token][0] + spans[token][1] <= start) token++;
      const kind = token < spans.length && spans[token][0] <= start ? spans[token][2] : 'plain';
      const marked = ranges.some((r) => r.start < end && r.end > start);
      const span = document.createElement('span');
      span.className = `syntax-${kind}${marked ? ' diagnostic-error' : ''}`;
      span.textContent = text.slice(start, end);
      fragment.append(span);
    }
    colors.replaceChildren(fragment);
    container.classList.add('highlighted');
    syncScroll();
    diagnosticsList.replaceChildren();
    for (const diagnostic of diagnostics) {
      const item = document.createElement('li');
      const range = diagnosticRange(source, diagnostic);
      const label = `${diagnostic.line ? `Line ${diagnostic.line}, column ${diagnostic.column}: ` : ''}${diagnostic.code} — ${diagnostic.message}`;
      if (range) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'diagnostic-link';
        button.textContent = label;
        button.onclick = () => {
          input.focus();
          input.setSelectionRange(range.start, Math.min(source.length, range.end));
          input.scrollTop = Math.max(0, (diagnostic.line - 3) * 23);
          // Tabs affect visual columns, while compiler columns count each tab once.
          const prefix = source
            .slice(0, range.start)
            .split(/\r\n|\r|\n/)
            .at(-1);
          let columns = 0;
          for (const scalar of prefix) columns += scalar === '\t' ? 4 - (columns % 4) : 1;
          const canvas = document.createElement('canvas');
          const context = canvas.getContext('2d');
          context.font = getComputedStyle(input).font;
          input.scrollLeft = Math.max(
            0,
            columns * context.measureText('M').width - input.clientWidth / 2,
          );
          syncScroll();
        };
        item.append(button);
      } else item.textContent = label;
      diagnosticsList.append(item);
    }
    input.setAttribute('aria-invalid', String(diagnostics.length > 0));
  }

  function request() {
    if (composing || inFlight) return;
    const source = input.value;
    if (new TextEncoder().encode(source).length > 131072) {
      plain();
      status.textContent = 'Highlighting supports up to 128 KiB; editing remains available.';
      return;
    }
    if (!worker) {
      const active = new Worker('./worker.mjs', { type: 'module' });
      worker = active;
      showLoading(loading, { phase: 'download', loaded: 0 });
      active.onmessage = ({ data }) => {
        if (worker !== active) return;
        if (data.type === 'loading') {
          showLoading(loading, data);
          return;
        }
        loading.hidden = true;
        clearTimeout(timeout);
        inFlight = false;
        if (data.id !== revision) {
          request();
          return;
        }
        if (data.error) {
          fail(`Code check failed: ${data.error}`);
          return;
        }
        try {
          paint(input.value, data.complete ? data.spans : [], data.diagnostics);
          status.textContent = data.diagnostics.length
            ? `${data.diagnostics.length} compiler diagnostics · Click to jump to source.`
            : 'Compilation successful · no compiler diagnostics.';
        } catch {
          fail('Highlighting failed; editing remains available.');
        }
      };
      active.onerror = () => {
        if (worker === active) fail('Could not load highlighting; editing remains available.');
      };
    }
    inFlight = true;
    worker.postMessage({ id: revision, type: 'analyze', source });
    timeout = setTimeout(() => fail('Code check timed out; editing remains available.'), 120000);
  }

  function changed() {
    revision++;
    updateLines();
    plain();
    clearTimeout(debounce);
    diagnosticsList.replaceChildren();
    input.removeAttribute('aria-invalid');
    status.textContent = 'Code check pending …';
    if (!composing) debounce = setTimeout(request, 350);
  }

  checkButton.addEventListener('click', () => {
    changed();
    clearTimeout(debounce);
    request();
  });
  input.addEventListener('input', changed);
  input.addEventListener('keydown', editKey);
  input.addEventListener('scroll', syncScroll);
  input.addEventListener('compositionstart', () => {
    composing = true;
  });
  input.addEventListener('compositionend', () => {
    composing = false;
    changed();
  });
  new ResizeObserver(syncScroll).observe(input);
  window.addEventListener('pagehide', () => {
    worker?.terminate();
    worker = null;
    inFlight = false;
    clearTimeout(timeout);
    clearTimeout(debounce);
  });
  window.addEventListener('pageshow', (event) => {
    if (event.persisted) request();
  });
  updateLines();
  request();
}
