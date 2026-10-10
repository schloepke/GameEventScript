// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { diagnosticRange } from './diagnostics.mjs';
import { foldingRanges } from './editor-folding.mjs';

/** CodeMirror owns editing and folding; Swift supplies highlighting and diagnostics. */
export function attachEditor(input, colors, container, status, engine) {
  const CodeMirror = window.CodeMirror;
  let active = false, debounce, revision = 0, inFlight = false, syncing = false;
  let ranges = new Map(), spans = [], diagnosticMarks = [], searchMark;
  const diagnosticsList = document.getElementById('diagnostics');
  const checkButton = document.getElementById('check-source');
  const kinds = new Set(['plain', 'keyword', 'builtin', 'identifier', 'message', 'type', 'tag',
    'constant', 'number', 'string', 'comment', 'symbol', 'function', 'module', 'label', 'register']);

  CodeMirror.defineMode('ges-wasm', () => ({
    startState: () => ({ offset: 0, next: 0, token: 0 }),
    blankLine(state) { state.next++; },
    token(stream, state) {
      if (stream.sol()) { state.offset = state.next; state.next += stream.string.length + 1; }
      const offset = state.offset + stream.pos;
      while (state.token < spans.length && spans[state.token][0] + spans[state.token][1] <= offset) state.token++;
      const span = spans[state.token];
      if (!span) { stream.skipToEnd(); return null; }
      if (span[0] > offset) { stream.pos = Math.min(stream.string.length, span[0] - state.offset); return null; }
      stream.pos = Math.min(stream.string.length, span[0] + span[1] - state.offset);
      return `ges-${span[2]}`;
    },
  }));

  const editor = CodeMirror.fromTextArea(input, {
    mode: 'ges-wasm', lineNumbers: true, lineWrapping: false,
    indentUnit: 4, tabSize: 4, indentWithTabs: false,
    gutters: ['CodeMirror-linenumbers', 'CodeMirror-foldgutter'],
    foldGutter: { foldOnChangeTimeSpan: 50 }, foldOptions: { rangeFinder: (_cm, pos) => ranges.get(pos.line) },
    inputStyle: 'textarea', screenReaderLabel: 'GES game rules',
    extraKeys: {
      // Preserve the native dialog Escape action instead of CodeMirror singleSelection.
      Esc: false,
      Tab(cm) { if (cm.somethingSelected()) cm.indentSelection('add'); else cm.replaceSelection('    ', 'end', '+input'); },
      'Shift-Tab': cm => cm.indentSelection('subtract'),
      'Ctrl-Q': cm => cm.foldCode(cm.getCursor()),
      Enter(cm) {
        const from = cm.getCursor('from'), to = cm.getCursor('to');
        const prefix = cm.getLine(from.line).slice(0, from.ch);
        const suffix = cm.getLine(to.line).slice(to.ch);
        const indent = prefix.match(/^[ \t]*/)[0], code = prefix.trim();
        const extra = /[\[{]$/.test(code) || (code && !code.startsWith('//') && suffix.trim() && !/^[\]})]/.test(suffix.trimStart())) ? '    ' : '';
        cm.replaceRange('\n' + indent + extra, from, { line: to.line, ch: to.ch + suffix.match(/^[ \t]*/)[0].length }, '+input');
        cm.setCursor({ line: from.line + 1, ch: indent.length + extra.length });
      },
      Backspace(cm) {
        const pos = cm.getCursor(), indent = cm.getLine(pos.line).match(/^[ \t]*/)[0];
        if (!cm.somethingSelected() && pos.line > 0 && indent.length && pos.ch <= indent.length) {
          cm.replaceRange('', { line: pos.line - 1, ch: cm.getLine(pos.line - 1).length }, { line: pos.line, ch: indent.length }, '+delete');
        } else return CodeMirror.Pass;
      },
    },
  });
  colors.hidden = true;
  document.getElementById('source-lines').hidden = true;
  container.classList.add('folding-editor');

  function clearDiagnostics() {
    for (const mark of diagnosticMarks) mark.clear();
    diagnosticMarks = [];
    diagnosticsList.replaceChildren();
  }

  function paint(source, data) {
    let end = 0;
    for (const [start, length, kind] of data.spans) {
      if (!Number.isInteger(start) || !Number.isInteger(length) || start < end || length < 0 || start + length > source.length || !kinds.has(kind)) {
        throw new Error('Invalid highlight range');
      }
      end = start + length;
    }
    editor.operation(() => {
      spans = data.complete ? data.spans.filter(([, length]) => length > 0) : [];
      ranges = data.complete ? foldingRanges(source, spans) : new Map();
      editor.setOption('mode', 'ges-wasm');
      clearDiagnostics();
      for (const diagnostic of data.diagnostics) {
        const item = document.createElement('li');
        const range = diagnosticRange(source, diagnostic);
        const label = `${diagnostic.line ? `Line ${diagnostic.line}, column ${diagnostic.column}: ` : ''}${diagnostic.code} — ${diagnostic.message}`;
        if (range) {
          const from = editor.posFromIndex(range.start), to = editor.posFromIndex(Math.min(source.length, range.end));
          diagnosticMarks.push(editor.markText(from, to, { className: 'diagnostic-error' }));
          const button = document.createElement('button');
          button.type = 'button'; button.className = 'diagnostic-link'; button.textContent = label;
          button.onclick = () => {
            // Reveal enclosing folds before selecting the real source location.
            for (const mark of editor.getAllMarks()) {
              if (!mark.__isFold) continue;
              const fold = mark.find();
              if (fold && editor.indexFromPos(fold.from) <= range.start && editor.indexFromPos(fold.to) >= range.start) mark.clear();
            }
            editor.setSelection(from, to); editor.scrollIntoView({ from, to }, 46); editor.focus();
          };
          item.append(button);
        } else item.textContent = label;
        diagnosticsList.append(item);
      }
    });
    editor.getInputField().setAttribute('aria-invalid', String(data.diagnostics.length > 0));
  }

  async function request() {
    if (!active || inFlight) return;
    const source = input.value, version = revision;
    if (new TextEncoder().encode(source).length > 131072) {
      status.textContent = 'Highlighting supports up to 128 KiB; editing remains available.';
      return;
    }
    inFlight = true;
    try {
      const data = await engine.send({ type: 'analyze', source });
      if (!active || version !== revision) return;
      paint(source, data);
      status.textContent = data.diagnostics.length
        ? `${data.diagnostics.length} compiler diagnostics · Click to jump to source.`
        : 'Compilation successful · no compiler diagnostics.';
    } catch (error) {
      if (active && version === revision) status.textContent = `Code check failed: ${error.message}`;
    } finally {
      inFlight = false;
      if (active && version !== revision) request();
    }
  }

  function changed() {
    revision++;
    spans = []; ranges = new Map();
    editor.setOption('mode', 'ges-wasm');
    clearDiagnostics();
    editor.getInputField().removeAttribute('aria-invalid');
    clearTimeout(debounce);
    status.textContent = 'Code check pending …';
    debounce = setTimeout(request, 350);
  }

  editor.on('change', () => {
    if (syncing) return;
    input.value = editor.getValue();
    input.dispatchEvent(new Event('input', { bubbles: true }));
  });
  input.addEventListener('input', () => {
    if (editor.getValue() !== input.value) {
      syncing = true;
      editor.setValue(input.value);
      editor.clearHistory();
      syncing = false;
    }
    changed();
  });
  input.addEventListener('focus', () => editor.focus());
  checkButton.addEventListener('click', () => { changed(); clearTimeout(debounce); request(); });
  new ResizeObserver(() => editor.refresh()).observe(container);
  return {
    getText() { return editor.getValue(); },
    getSelection() { return editor.getSelection(); },
    clearMatch() { searchMark?.clear(); searchMark = undefined; },
    showMatch(start, end) {
      searchMark?.clear();
      for (const mark of editor.getAllMarks()) {
        if (!mark.__isFold) continue;
        const fold = mark.find();
        if (fold && editor.indexFromPos(fold.from) < end && editor.indexFromPos(fold.to) > start) mark.clear();
      }
      const from = editor.posFromIndex(start), to = editor.posFromIndex(end);
      searchMark = editor.markText(from, to, { className: 'search-hit' });
      editor.setSelection(from, to);
      editor.scrollIntoView({ from, to }, 46);
    },
    focus() { editor.refresh(); editor.focus(); },
    refresh() { active = true; editor.refresh(); changed(); clearTimeout(debounce); request(); },
    suspend() { active = false; clearTimeout(debounce); },
  };
}
