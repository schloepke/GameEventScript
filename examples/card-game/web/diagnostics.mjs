// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/** Compiler columns count Unicode scalars; browser selections count UTF-16 units. */
export function diagnosticRange(source, diagnostic) {
  function offset(targetLine, targetColumn) {
    if (
      !Number.isInteger(targetLine) ||
      !Number.isInteger(targetColumn) ||
      targetLine < 1 ||
      targetColumn < 1
    )
      return null;
    let line = 1,
      column = 1,
      index = 0,
      previousCR = false;
    for (const scalar of source) {
      if (line === targetLine && column === targetColumn && !(previousCR && scalar === '\n'))
        return index;
      if (scalar === '\r') {
        line++;
        column = 1;
      } else if (scalar === '\n') {
        if (!previousCR) line++;
        column = 1;
      } else column++;
      previousCR = scalar === '\r';
      index += scalar.length;
    }
    return line === targetLine && column === targetColumn ? index : null;
  }

  const start = offset(diagnostic.line, diagnostic.column);
  if (start === null) return null;
  const end = offset(diagnostic.endLine, diagnostic.endColumn);
  // Zero-width diagnostics (especially EOF) still need a visible marker.
  return { start, end: end > start ? end : start + (source.codePointAt(start) > 0xffff ? 2 : 1) };
}
