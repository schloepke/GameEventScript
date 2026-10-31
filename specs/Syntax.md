<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Syntax module

> **Since: Unreleased**

This document owns the structure of the Syntax module that both ports share:
its dependencies, the lossless token stream, the syntax tree with error nodes,
name roles, fragment parsing, the split between single-source and
program-wide validation, and the split of document reading between Syntax and
Runtime. The grammar itself is owned by [Language](Language.md); diagnostic
codes, recovery points and diagnostic positions by
[Diagnostics](Diagnostics.md); documents by [Documents](Documents.md);
highlighting by [Syntax highlighting](SyntaxHighlighting.md).

The lexer, parser, tree, validator and document reader are internal to the
module. Their public results are the Compiler's diagnostics, document errors
and the highlighter's spans; the rules
below make those results equal across ports.

## Dependencies

Syntax depends on no other GES module and no third-party package. Runtime
builds on it for the Quantity unit table, the text, number-text and name
rules, the explicit form parameters and the document reader; the Compiler builds on it for lexing, parsing and single-source
validation. In Swift, Syntax, Runtime and Compiler import only each other and
the platform's C library, never Foundation.

## Tokens

The lexer turns source text into tokens that end with one end-of-file token.
Each token carries its kind, its spelling and three offsets: `FullStart`,
`Start` and `End`. `[FullStart, Start)` is its leading trivia, `[Start, End)`
its own spelling. Trivia holds inline whitespace, `//` comments and a leading
BOM; line breaks are tokens, because they separate statements. The tokens of a
source therefore cover it without gaps and reproduce it exactly, for valid and
malformed source alike: text the lexer cannot read becomes an illegal token.

Offsets are indices of the port's string storage; lines and columns are
one-based and columns count Unicode scalars. Numeric token values are read by
the Compiler, not the lexer.

## Syntax tree

The parser builds one tree per source. Each node has a kind and covers a
contiguous token range; its children are ordered and lie inside it, and the
root covers every token. A node's own tokens are those no child covers. Node
kinds follow the grammar's declarations, statements and expressions; an
expression node keeps only its nesting depth, its token range, its diagnostic
range and, for literals, its shape. The Compiler binds the semantic tree by
reading each node's tokens in parser order.

A tree exists for every source. A declaration or statement that fails to parse
becomes one error node from its start through the recovery point that
[Diagnostics](Diagnostics.md#compiler-diagnostic-collection) defines; the
parser then continues with the next declaration or statement. Exceeding a
source nesting limit turns the innermost open statement and the rest of the
source into one error node and ends parsing. The tree carries the source's
syntax diagnostics in source order.

## Roles

The parser records a role for each token whose meaning depends on its
position: bindings by their binding construct (let, condition, loop, selector,
weight, fold accumulator, generated collection), parameters and fields with or
without label, called and declared callables, predicate tests, argument
labels, members, map keys, message names, inline-assembly declarations,
opcodes and labels, and identifiers that the grammar reads as contextual
keywords such as `between` or `first`. Other tokens have no role. Validation
and highlighting read roles instead of reinterpreting the tokens.

An expression node additionally records the token range its diagnostics point
at, which may differ from its own range: a parenthesized expression points at
its content, a member access at its target.

## Fragments

A fragment parse accepts, besides declarations, statements at top level, as
editor and console input contains them. A top-level token that starts no
declaration starts a statement, which recovers like a statement inside a
block. Fragment parsing serves highlighting; compilation parses whole sources.

## Validation

`GesSyntaxValidator` checks the rules a single source decides on its own tree:
name spelling, repeated parameters, argument labels, record fields and
bindings, the forms of the system endpoints, removed type names and
shadowing. It runs only on a tree without syntax errors. The Compiler keeps
the rules that need other sources, the external catalog or the host, and
reports both groups in the order
[Diagnostics](Diagnostics.md#compiler-diagnostic-collection) defines.

## Documents

The Syntax module also reads [GES documents](Documents.md). Its reader
recognizes the schema header, entries, separators, comments and every data
literal, reports the first reading error with its code and position, and
builds a value tree of scalars, containers and explicit forms. Runtime builds
the document value from that tree and constructs the explicit forms, which
reports construction errors and the Range materialization total. The
recognition of number text and the parameters of the explicit forms live in
Syntax and are shared with Runtime's `parse`.

## Verification

`scripts/test-syntax-roundtrip.py` requires that each port's tokens reproduce
every corpus source and that both ports produce equal token streams. With
`--trees` it also requires equal trees, error nodes, roles and diagnostic
ranges, equal fragment trees and equal highlighting for every source in both
ports, including sources at the nesting limits, and equal document
highlighting and diagnostics for the texts of the document suite.
`scripts/verify-swift-portable-imports.py` rejects other imports and remote
package dependencies in those three Swift packages; its controls are
`scripts/test-swift-portable-imports.py`.
