// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import assert from 'node:assert/strict';
import { foldingRanges } from '../web/editor-folding.mjs';
const source = "on Test() {\n    // { ignored\n    let text be '{ }'\n    if true {\n        for n in [\n            1, 2\n        ] {\n            emit Done(n)\n        }\n    }\n}";
const comment = source.indexOf('//'), string = source.indexOf("'{ }'");
const ranges = foldingRanges(source, [[comment, '// { ignored'.length, 'comment'], [string, 5, 'string']]);
assert.deepEqual([...ranges.keys()].sort((a, b) => a - b), [0, 3, 4, 6]);
assert.deepEqual(ranges.get(0), { from: { line: 0, ch: 11 }, to: { line: 10, ch: 0 } });
assert.equal(ranges.get(3).to.line, 9);
assert.equal(ranges.get(4).to.line, 6);
assert.equal(ranges.get(6).to.line, 8);
assert.equal(foldingRanges('on Test() {', []).size, 0);
assert.equal(foldingRanges('on Test() {}', []).size, 0);
console.log('Editor folds: handlers, nested blocks, brackets, strings, comments and incomplete input passed.');
