// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/** Match multiline braces and brackets, excluding Swift-highlighted strings and comments. */
export function foldingRanges(source, spans) {
  const ignored = spans.filter(([, , kind]) => kind === 'string' || kind === 'comment');
  const stack = [], ranges = new Map();
  let token = 0, line = 0, ch = 0;
  for (let offset = 0; offset < source.length; offset++) {
    while (token < ignored.length && ignored[token][0] + ignored[token][1] <= offset) token++;
    const skip = token < ignored.length && ignored[token][0] <= offset;
    const value = source[offset];
    if (!skip) {
      if (value === '{' || value === '[') stack.push({ value, line, ch });
      else if (value === '}' || value === ']') {
        const opening = stack.at(-1);
        if (opening && opening.value === (value === '}' ? '{' : '[')) {
          stack.pop();
          if (line > opening.line) {
            const previous = ranges.get(opening.line);
            if (!previous || previous.from.ch > opening.ch) {
              ranges.set(opening.line, { from: { line: opening.line, ch: opening.ch + 1 }, to: { line, ch } });
            }
          }
        }
      }
    }
    if (value === '\n') { line++; ch = 0; } else ch++;
  }
  return ranges;
}
