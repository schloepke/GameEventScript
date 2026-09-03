# Game Event Script C# code style

This is the development formatting contract for the handwritten C# sources in
`StepH-GameEventScript` and `StepH-GameEventScript-Tests`. It is not part of the
portable Game Event Script product specification.

## Automated foundation

The scoped `../.editorconfig` defines the formatting options understood by
Roslyn, Rider, and compatible editors. Its sections match only the production
and test projects; sibling Beamable services are unaffected.

Source files use UTF-8 without BOM, `LF`, a terminal newline, four spaces per
indentation level, standard C# brace placement, and standard spacing. System
`using` directives sort before other directives without mandatory blank groups.

## Line length and compactness

Handwritten C# source lines have a maximum length of 250 characters. This is a
hard repository review rule. `max_line_length = 250` communicates the limit to
editors, but Roslyn's formatter does not itself wrap every overlong construct;
the limit therefore also needs a mechanical line-length check.

Declarations, calls, constructors, conditions, collection expressions, and
initializers stay on one line when the complete construct fits within 250
characters and remains readable. Argument count alone never causes wrapping. A
call with three short arguments normally remains on one line.

Existing multiline code may be joined when it satisfies those conditions. Do
not retain vertical expansion merely because a construct was previously split.

## Necessary wrapping

When a declaration or call does not fit, place each parameter or argument on a
separate continuation line, indented once from its containing statement. Place
the closing delimiter at the containing statement's indentation. Apply the same
shape consistently to constructors, attributes, collection expressions, and
initializers.

Wrapped fluent chains place each continuation member access on its own line.
Wrapped binary and boolean expressions place the operator at the beginning of
the continuation line, matching the EditorConfig setting.

Do not combine unrelated statements on one line to stay compact. Expression-
bodied members and genuinely short single-line blocks remain valid when they
are clear and fit within the limit.

Long string literals and comments must also remain within 250 characters.
Split them without changing their runtime text or documented meaning. Generated
artifacts, API snapshots, Markdown/GESA snapshots, and binary fixtures are not
C# source and are outside this formatting contract.

## Change discipline

A repository-wide formatting pass is a mechanical change. It must not rename
symbols, change APIs, reorder behavior, alter string values, or include runtime
refactoring. Run the API snapshot, all non-performance tests, Markdown
conformance, and performance/allocation gates after the pass.
