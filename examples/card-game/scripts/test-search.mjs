// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import { findMatches, attachSearch } from '../web/search.mjs';
assert.deepEqual(findMatches('Test test TEST', 'test'), [{start:0,end:4},{start:5,end:9},{start:10,end:14}]);
assert.deepEqual(findMatches('Test test TEST', 'test', true), [{start:5,end:9}]);
assert.deepEqual(findMatches('a.b a*b', 'a.b'), [{start:0,end:3}]);
assert.deepEqual(findMatches('😀 Test', 'Test'), [{start:3,end:7}]);
assert.deepEqual(findMatches('x', ''), []);
class Element extends EventTarget {
  value = ''; hidden = false; checked = false;
  focus() {} select() {}
}
const elements = new Map();
const byId = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
globalThis.document = {getElementById: byId};
let hits = [], source = 'abc abc', dumpText = 'ABC';
const editor = {getText:()=>source, clearMatch(){}, showMatch:(a,b)=>hits.push(['source',a,b])};
const dump = {getText:()=>dumpText, clearMatch(){}, showMatch:(a,b)=>hits.push(['dump',a,b])};
byId('dump-view').hidden = true;
const input = new Element();
attachSearch(new Element(), input, editor, dump);
byId('search-query').value = 'abc';
byId('search-query').dispatchEvent(new Event('input'));
assert.deepEqual(hits.at(-1), ['source',0,3]);
byId('search-next').onclick();
assert.deepEqual(hits.at(-1), ['source',4,7]);
byId('search-next').onclick();
assert.deepEqual(hits.at(-1), ['source',0,3]);
byId('search-previous').onclick();
assert.deepEqual(hits.at(-1), ['source',4,7]);
const hitCount = hits.length;
source = 'abc changed abc';
input.dispatchEvent(new Event('input'));
assert.equal(hits.length, hitCount, 'Editing with an active query must not move the caret or selection');
assert.equal(byId('search-status').textContent, '2 matches');
byId('search-next').onclick();
assert.deepEqual(hits.at(-1), ['source',0,3]);
byId('dump-view').hidden = false;
byId('dump-view').dispatchEvent(new Event('dumpchange'));
assert.deepEqual(hits.at(-1), ['dump',0,3]);
assert.equal(byId('search-status').textContent, '1 / 1');
dumpText = '';
byId('dump-view').dispatchEvent(new Event('dumpchange'));
assert.equal(byId('search-status').textContent, 'No matches');
assert.equal(byId('search-next').disabled, true);
console.log('Search: literal/Unicode offsets, case sensitivity, navigation, wrapping and document changes passed.');
