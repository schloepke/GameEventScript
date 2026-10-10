// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/** Download progress measures decoded bytes; compressed Content-Length is not comparable. */
export async function loadWasm(report) {
  report({ phase: 'download', loaded: 0, total: null });
  const response = await fetch('./card-game.wasm?v=0465e905d1726f5ab2da16cb14125758904c91acff322d1be2e1567750ffb5f1', { cache: 'no-cache' });
  if (!response.ok) throw new Error(`Wasm download failed: ${response.status}`);

  const length = Number(response.headers.get('Content-Length'));
  const encoding = response.headers.get('Content-Encoding');
  const total = (!encoding || encoding === 'identity') && length > 0 ? length : null;
  const reader = response.body?.getReader();
  if (!reader) return response.arrayBuffer();

  const chunks = [];
  let loaded = 0;
  let lastReport = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    chunks.push(value);
    loaded += value.byteLength;
    if (performance.now() - lastReport >= 100) {
      report({ phase: 'download', loaded, total });
      lastReport = performance.now();
    }
  }
  report({ phase: 'download', loaded, total });

  const bytes = new Uint8Array(loaded);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return bytes.buffer;
}

/** An indeterminate bar is used when the download size or initialization duration is unknown. */
export function showLoading(container, progress) {
  container.hidden = false;
  const bar = container.querySelector('progress');
  const label = container.querySelector('span');
  bar.removeAttribute('value');
  if (progress.phase === 'initialize') {
    label.textContent = 'Initializing WebAssembly …';
  } else if (progress.phase === 'ready') {
    label.textContent = 'WebAssembly ready · Running rules …';
  } else {
    const mb = (bytes) => (bytes / 1_000_000).toFixed(1);
    if (progress.total && progress.loaded <= progress.total) {
      bar.value = progress.loaded / progress.total;
      label.textContent = `Loading WebAssembly · ${Math.floor(bar.value * 100)}% · ${mb(progress.loaded)} / ${mb(progress.total)} MB`;
    } else {
      label.textContent = `Loading WebAssembly${progress.loaded ? ` · ${mb(progress.loaded)} MB received` : ' …'}`;
    }
  }
}
