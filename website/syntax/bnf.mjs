// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The Grammar-Kit-style EBNF notation used by the language specification.
export default {
  name: 'bnf',
  scopeName: 'source.bnf',
  patterns: [
    { name: 'comment.line.double-slash.bnf', match: '//.*$' },
    { name: 'string.quoted.single.bnf', begin: "'", end: "'", patterns: [
      { name: 'constant.character.escape.bnf', match: "\\\\." },
    ] },
    { name: 'entity.name.function.bnf', match: '\\b[a-z][a-z0-9_]*(?=\\s*::=)' },
    { name: 'keyword.operator.definition.bnf', match: '::=' },
    { name: 'constant.language.bnf', match: '\\b[A-Z][A-Z0-9_]*\\b' },
    { name: 'variable.other.bnf', match: '\\b[a-z][a-z0-9_]*\\b' },
    { name: 'keyword.operator.bnf', match: '[|*+?]' },
    { name: 'punctuation.brackets.bnf', match: '[()\\[\\]{}]' },
  ],
};
