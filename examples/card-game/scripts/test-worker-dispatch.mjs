// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';
const replies = [], actions = [];
const worker = { postMessage(value) { replies.push(value); } };
const source = (await fs.readFile(new URL('../web/worker.mjs', import.meta.url), 'utf8')).replace(/^import .*;\n/gm, '');
vm.runInNewContext(source, {
  self: worker,
  loadWasm: async () => ({}),
  createEngine: async () => ({
    dump: (source, name) => ({ dump: `${name}: ${source}` }),
    act: (...args) => { actions.push(args); return { accepted: true }; },
  }),
});
worker.onmessage({data:{id:1,type:'dump',source:'on Test() {}',sourceName:'test.ges'}});
worker.onmessage({data:{id:2,type:'unknown'}});
worker.onmessage({data:{id:3,type:'action',player:0,action:{kind:'draw'},revision:1}});
await new Promise(resolve=>setTimeout(resolve,0));
assert.equal(replies.find(reply=>reply.id===1).dump, 'test.ges: on Test() {}');
assert.match(replies.find(reply=>reply.id===2).error, /Unknown worker request/);
assert.equal(replies.find(reply=>reply.id===3).accepted, true);
assert.equal(actions.length, 1, 'Only explicit action requests may dispatch to game actions');
console.log('Worker dispatch: dump, unknown request rejection and subsequent actions passed.');
