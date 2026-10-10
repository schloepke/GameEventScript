// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/** One reactor shared by the game, compiler and highlighter. */
export function createWorkerClient(onProgress) {
  let worker;
  let sequence = 0;
  const pending = new Map();

  function reset(message = 'Execution stopped.') {
    worker?.terminate();
    worker = undefined;
    for (const request of pending.values()) {
      clearTimeout(request.timer);
      request.reject(new Error(message));
    }
    pending.clear();
    onProgress({ phase: 'ready' });
  }

  function send(payload, timeout = 120000) {
    if (!worker) {
      const active = new Worker('./worker.mjs', { type: 'module' });
      worker = active;
      onProgress({ phase: 'download', loaded: 0 });
      active.onmessage = ({ data }) => {
        if (worker !== active) return;
        if (data.type === 'loading') {
          onProgress(data);
          return;
        }
        const request = pending.get(data.id);
        if (!request) return;
        pending.delete(data.id);
        clearTimeout(request.timer);
        if (data.error) request.reject(new Error(data.error));
        else request.resolve(data);
      };
      active.onerror = event => {
        if (worker === active) reset(`Worker error: ${event.message}`);
      };
    }
    const id = ++sequence;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => reset('Time limit reached. Check the rules or your connection and restart.'), timeout);
      pending.set(id, { resolve, reject, timer });
      worker.postMessage({ ...payload, id });
    });
  }

  return { send, reset };
}
