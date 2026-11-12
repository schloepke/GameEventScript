<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Changelog

User-visible changes and migration guidance are collected here before each
release. Entries under **Unreleased** are available on the development branch;
they are not yet included in a published package. Published release notes are
available on [GitHub Releases](https://github.com/schloepke/GameEventScript/releases).

## Unreleased

### Breaking
- `Box` and `Poly` are built-in schema types, so a schema, message or message set of that name is now
  `schema.duplicateName`; rename it.
- A required computed schema field that reads an optional field without `has value` or `default` is the new
  `schema.computedMayBeAbsent` instead of giving NaN or false where the field is absent; mark it `optional`, guard the
  read or give a fallback.
- Swift `[UInt8]` converts to and from GES Bytes instead of a List of numbers.
- Text such as `"0x10"` is now a hexadecimal number for `parse`, `as :Number`, `as :Percentage`,
  documents and Message JSON instead of Text or `nothing`.
- C# `GesValue.AsMap()` returns only stored Map and Record data, as in Swift; an external
  value gives `null`. `MaterializedMap()` reads its fields.
- `:Vector(value)` and `:Point(value)` with one positional argument are now the cast,
  so `:Vector([1, 2])` is 1, 2, 0 instead of `nothing` and `:Vector(:Dice[6, 3])` is
  6, 3, 0 instead of 9, 0, 0.
- `:Message` rejects the labels `value` and `tags` in source; use `with`.
- `MaxByteCount` of a schema is the larger of its largest full record and its largest delta,
  which can raise it by a few bytes.
- Encoding a schema value reports `nothing` in a required field, argument or list element
  as the new `wire.nothingValue`; `wire.missingField` stays for absent keys and arguments
  and for fields a typed writer leaves unwritten.
- The literal `nothing` and a constant with that value are no tags: this is the new
  `validate.invalidTag`, and a tag value that is `nothing` at runtime makes the tag
  clause invalid instead of adding `#nothing`.
- A send with an invalid tag clause sends nothing and continues; a result-bearing send
  is false. A statement send used to end the handler, in C# always and in Swift for a
  Message value.
- Message names start uppercase: `initialization` and `undeliverable` are entry points
  only the host uses. `parse` and documents no longer recognize
  `:Message(initialization())` and `:Handler(undeliverable())`, and in source a lowercase
  name is a variable or a call of a function or predicate. `GameEventScriptMessage.Create`
  and `FromJson` reject those names, `CreateMessage` returns nothing for them, and
  loading a Program with such an outbound binding or message shape fails.
- A labeled `:Range` needs `from` and `to`, so `:Range(from: 1)` is an error.
- `value as :Record` is `validate.invalidTypeConstructor` instead of a compiler failure
  with `decode.invalidOperand`.
- Handlers print a space after each comma: `:Handler(Done(value, _))`.
- Labeled Vector and Point components must be a run without gaps:
  `:Vector(x: 1, z: 2)` is `validate.invalidTypeConstructor` in source, stays Text
  for `parse` and is `document.invalidLiteral` in documents. The literals `true`
  and `false` are no components either, though a Boolean variable counts as 0 or 1.
  `ConstructData` no longer accepts `vector` and `point`. `CreateVector` and
  `CreatePoint` use `ImmediateX` 3 for labeled `x`, which never lifts; values above
  3 fail validation. Recompile `.gesb` files to get the `:Vector(x: v, y: 3)` fix.
- A bare name without a visible binding of its own source is now the
  validation diagnostic `validate.unresolvedVariable` at the name, reported for
  every such name, instead of the first `compile.unresolvedSymbol` of lowering.
  The Syntax module reports it too, so editors see it while typing.
  `$constants`, calls, extension calls and inline-assembly operands are not
  affected; an unknown `$constant` remains `compile.unresolvedSymbol`.
- Validation reports the rules of each source on its own first, source by source
  and in source order of their positions: spelling of names and labels,
  repeated parameters, arguments, fields and bindings, shadowing, system
  endpoint forms and removed type names. The program-wide rules follow. A
  program with errors of both kinds therefore lists its diagnostics in a new
  order; their codes and positions are unchanged.

- The optional highlighter packages are renamed to the Syntax module, which
  will hold the shared lexer, parser and syntax tree: NuGet
  `GameEventScript.SyntaxHighlighter` becomes `GameEventScript.Syntax` (namespace
  `GameEventScript.Syntax`), and the SwiftPM product and module
  `GameEventScriptSyntaxHighlighter` become `GameEventScriptSyntax`. Type names
  such as `GesSyntaxHighlighter` are unchanged. Migrate by replacing the package
  reference and the `using`/`import`.
- Highlighting is derived from the lossless syntax tree instead of the bundled
  TextMate grammars, in both ports. Spans carry a category and the diagnostics
  overlapping them, and `GesHighlightResult.Diagnostics`/`diagnostics` lists the
  document's syntax errors or, without those, its violated single-source rules
  as new `GesHighlightDiagnostic` values with UTF-16 ranges. The line API
  (`HighlightLine`/`highlightLine`), `GesHighlightState`, `NextState`/`nextState`,
  `CreateState`/`makeState` and the TextMate `Scopes`/`scopes` are removed;
  highlight the whole document after an edit and theme by `Kind`/`kind`. Swift
  `GesHighlightingError` loses `expectedSingleLine` and `incompatibleState`. Some
  categories become more precise: function and predicate names are Function,
  contextual keywords such as `between`, `from` or `ascending` are Keyword,
  brackets and braces are Symbol, extension names are Builtin, and GESA label
  definitions are Label. Input with statements outside a handler, as console
  input has, keeps its statement categories. The Swift module no longer imports
  Foundation, and `scripts/sync-highlighter-grammars.py` is removed; the TextMate
  grammars remain for the editor bundles.
- Runtime now depends on the Syntax module, which owns the quantity unit table
  and the text and name rules. `GesQuantityUnit` and `GesQuantityUnits` move to
  the Syntax assembly in C# with the unchanged namespace `GameEventScript.Api`;
  Swift Runtime re-exports `GesQuantityUnit`, so source code is unchanged. The
  NuGet Runtime, Compiler and CSharpBridge packages declare the Syntax
  dependency, and the `runtime`, `compiler` and `unity` DLL sets now include
  `GameEventScript.Syntax.dll`, which manual DLL installations must copy too.
- `parse` materializes at most `MaxRangeItems` Range items into Lists per
  invocation in total, so many `:List(:Range(...))` forms in one Text cannot
  multiply the per-Range limit. Exceeding the total stops the handler with the
  `MaxRangeItems` runtime limit. This includes a single Range longer than the
  limit, such as `parse ":List(:Range(1, 10001))"` with the default limit, which
  previously produced an empty List and let the handler continue. Migrate by
  raising `MaxRangeItems` or keeping such Ranges lazy, for example
  `:Range(1, 10001)` without `:List(...)`.
- `strict` directly after `parse` is now the strict mode word. To parse a
  binding named `strict`, write `parse (strict)`; `parse strict` alone is a
  syntax error.
- A number directly followed by a unit spelling is now one quantity literal, so
  `10m/s`, `6m/s²` or `120°/s` are a speed, an acceleration and an angular speed
  rather than divisions by a binding `s`, `2/s` is the rate `2Hz`, and `2m²` or
  `2m^2` is an area rather than the square of `2m`. Write `10m / s` or `2 / s`
  with whitespace to divide and `(2m)²` to square. Text such as `'250ms'` now
  parses as `0.25s` instead of failing.
- `floor`, `ceil`, `truncate` and the three `round` forms keep the unit of a
  quantity, at runtime and in constant folding: `floor 2.5m` is now `2m` instead
  of the unitless `2`, and `round half even 2.5m/s` is `2m/s`. Percentages still
  round to unitless numbers. A rounded time such as `floor 1.5s` is therefore a
  valid send delay. Remove the unit explicitly with `as :Quantity(none)` where a
  plain number is needed.
- Swift `GesQuantityUnit` has new cases for the added physical units, so an
  exhaustive `switch` over it must handle `kilogram`, `newton`, `joule`, `watt`,
  `volt`, `ampere`, `hertz`, `squareMeter`, `cubicMeter`, `wattPerSquareMeter`,
  `kelvin`, `degreePerSecond`, `decibel`, `wattPerKelvin` and `joulePerKelvin`
  (or add a `default`).
  C# `GesQuantityUnit` gains the same members; existing values keep their numbers.
- Runtime observer callbacks report the host-local send ID of the message they
  describe: C# `MessageEmitted(message, id, accepted)`,
  `MessagePublished(message, id, result)`, `DispatchStarted(message, id,
  dispatchSignatureId)` and `DispatchCompleted(message, id, dispatchSignatureId)`;
  Swift `messageEmitted(_:id:accepted:)`, `messagePublished(_:id:result:)`,
  `dispatchStarted(_:id:signatureID:)` and `dispatchCompleted(_:id:signatureID:)`.
  Every queued emit or received message, processed publish, delayed send and
  initialization gets the next ID of its host, so a trace can match each dispatch
  to its emit, publish or delayed send; 0 marks an emit that was not queued or a
  publish the host did not accept. Messages themselves carry no ID.
  Migrate by adding the `id` parameter: every callback now has an empty default
  implementation in both ports, so an observer that keeps an old signature still
  compiles but no longer receives that callback.
- Public API types are renamed so that only the three edge types keep the
  `GameEventScript` prefix: `GameEventScriptHost`, `GameEventScriptProgram` and
  `GameEventScriptMessage`. All other public types use `Ges`, identically in C#
  and Swift, for example `GesContext`, `GesHostBuilder`, `GesMessageArgument`,
  `GesRuntimeLimits`, `IGesPublishSink` (Swift: `GesPublishSink`) and
  `GesSyntaxHighlighter`. Deviating names: `GameEventScriptBuilder` becomes
  `GesProgramBuilder`, `GameEventScriptInstance` becomes `GesProgramInstance`,
  the C# `GameEventScriptBytecodeInstructionUnit` and Swift `GesUnit` become
  `GesQuantityUnit`, the concrete `GameEventScriptExternalTypeCatalog` becomes
  `GesDeclaredExternalTypeCatalog` (the C# interface is `IGesExternalTypeCatalog`,
  the Swift protocol `GesExternalTypeCatalog`), Swift `GameEventScriptExtensionFault`
  becomes `GesExtensionFaultError` and Swift `GameEventScriptAPIError` becomes
  `GesApiError`. Behavior is unchanged; migrate by renaming references.
- Builders are created directly or through their target type, following each
  language's idiom. C#: `new GesProgramBuilder()` (the static `Create()` is
  removed) or, with C# 14, `GameEventScriptProgram.CreateBuilder()`; hosts via
  `new GesHostBuilder()` or `GameEventScriptHost.CreateBuilder()`. Swift:
  `GesProgramBuilder()` or `GameEventScriptProgram.makeBuilder()` (`create()` is
  removed); hosts via `GesHostBuilder()` or `GameEventScriptHost.makeBuilder()`
  (formerly `createBuilder()`). The Swift `GameEventScriptHost` initializer is no
  longer public; configure hosts through the builder. Swift factory methods use
  `make`: `GesMessageSignature.makeMessage(_:)` and `makeSignatureId(name:parameterNames:)`.
- Reading, encoding, validating and dumping Programs are members of
  `GameEventScriptProgram`; the separate reader, writer, validator and dumper
  types are no longer public. C#: `GameEventScriptProgram.Read(bytes, options)`,
  `program.Encode()` (formerly `ToArray`), `program.GetEncodedSize()`,
  `program.Write(destination)`, `program.Validate()` and `program.Dump(...)`.
  Swift: `GameEventScriptProgram.read(_:options:)`, `program.encode()` (formerly
  `bytes`), `program.encodedSize()`, `program.write(into:)`, `program.validate()`
  and `program.dump(includeInstructionAddresses:)`.
- The Swift Program exposes the same segments as C#: `metadata`,
  `stringConstants`, `uint16IndexLists`, `bindings`, `code`, `debugSymbols`,
  `sourceMap`, `sourceArchive` and `buildMetadata` are segment types
  (`GesStringConstantSegment`, `GesBindingSegment`, `GesCodeSegment`, …) that are
  also random-access collections over their entries. The binding entry type is
  `GesBindingSegment.Entry` in both languages (formerly C#
  `GameEventScriptBinaryBindEntry`, Swift `GameEventScriptBinding`); Swift
  `GameEventScriptSourceMap` and `GameEventScriptBuildMetadata` become
  `GesSourceMapSegment` and `GesBuildMetadataSegment`. Swift
  `GesDebugInfoOptions` moves from the Compiler to the Runtime module, as in C#.
- Message and value JSON are members of the encoded types; `GameEventScriptMessageJson`
  is removed. C#: `message.ToJson()`, `GameEventScriptMessage.FromJson(json)`,
  `value.ToJson()` and `GesValue.FromJson(json)`. Swift: `message.json()`,
  `GameEventScriptMessage(json:)`, `value.json()` and `GesValue(json:)`.
- `GesSymbolKind` is renamed to `GesDiagnosticSymbolKind` in both languages to
  separate it from `GesDebugSymbolKind`.
- Swift gains `GesDiagnosticCodes` with the same stable codes as C#, and
  `GesOpaqueSection.flags` is a `GesSectionFlags` option set instead of a raw
  `UInt16`, as in C#. The C# `GesInstructionFlag` enum is no longer public; both
  languages expose the raw `UnitAndFlags`/`unitAndFlags` byte.
- Bridge types use the `GesBridge` prefix and identical names for shared roles:
  `GesBridgeHostRunner`, `GesBridgeExtensionRegistry`,
  `GesBridgeExternalTypeRegistry`, `GesBridgeMessageHandler`,
  `GesBridgePublishSink` and `GesBridgeValueConversion` (formerly
  `GameEventScriptCSharp…`/`GameEventScriptSwift…`). C# registries are created with
  `GesBridgeExtensionRegistry.Create(types)`/`CreateBuilder()` and
  `GesBridgeExternalTypeRegistry.Create(types)`; the static `GameEventScriptCSharpExtensions`
  and `GameEventScriptCSharpExternalTypes` entry classes are removed. C# gains the
  public `GesBridgeMessageHandler` and `GesBridgePublishSink` delegate adapters.
  Swift descriptors become `GesBridgeFieldBinding<Root>`,
  `GesBridgeConstructorBinding<Root>`, `GesBridgeTypeBinding<Root>` and
  `GesBridgeAnyTypeBinding`; `GesBridgeValueConvertible`,
  `GesBridgeConversionError` and `GesBridgeHostRunnerRegistration` replace their
  `GameEventScriptSwift…` predecessors. Attributes and macros keep their names.
- Swift bridge members no longer name the language: `setSwiftValue(_:)`,
  `swiftValue(at:as:)`, `withSwiftArguments(_:)` and the `swiftArguments:` labels of
  message, emit and publish conveniences become `setNativeValue(_:)`,
  `nativeValue(at:as:)`, `withNativeArguments(_:)` and `nativeArguments:`.
- Swift no longer exposes runtime helper types that C# keeps internal:
  `GesNumber`, `GesText` and `GesSpatialValue` (use `GesValue.x`/`y`/`z`) are
  internal, and `GesSeriesValue` with `GesValue.series(_:)`/`seriesValue` is
  available only to the Compiler.
- Swift reports the public value kind as `GesValue.valueKind: GesBytecodeTypeKind`,
  the same single type C# uses for `ValueKind`; `GesValueKind` and `kind` are no
  longer public. Integer and binary64 ranges are both `.range` (use
  `integerRangeValue`/`floatRangeValue`), and records and external values are
  `.custom`. `GesMessageArguments.kindAt(_:)` returns `GesBytecodeTypeKind`.
- Swift maps follow the C# model of sorted keys with parallel values:
  `GesValue.map(keys:values:)` and `GesValue.record(typeName:keys:values:)` create
  them, and `GesValueMap.key(at:)`/`value(at:)` read them next to `length`,
  `get(_:)` and `containsKey(_:)`. `GesMapEntry`, `GesValueMap.entries` and
  `GesValue.mapEntries` are no longer public.
- C# `GesValueMap.StorageLength` is removed; it always equaled `Length`. Use
  `Length` instead.
- Attributed C# extension parameters check units: a `[GesParam]` with a unit requires exactly
  that unit, and one without a unit a unitless argument; another unit gives `nothing`.
- Generated schema code holds `:Point` and `:Vector` fields as the runtime's `GesPoint` and
  `GesVector` instead of a nested struct per field, such as `PositionPoint`. Writing a record
  or a delta whose axis outside the field's axis block is not zero fails with
  `wire.typeMismatch`, as the value path does, and reading sets that axis to zero.

### Added
- `:Bytes`, an immutable byte sequence: literals `:Bytes[0xa0, 183]` and `:Bytes("a0b7")`, `as :Bytes`
  from integer Lists and hexadecimal Text, `as :Text`/`parse`/documents, count, `in`, indexing,
  iteration, equality and order, `:take`/`:drop` `first`/`last` slices, `+` concatenation and UTF-8
  conversion with `:Bytes(utf8: text)` and `:Text(utf8: bytes)`. Message JSON writes Bytes as Base64, schemas add `:Bytes max n` with
  generated `WriteBytes`/`ReadBytes` code, and the Bridges map C# `byte[]` and Swift `[UInt8]`.
- `ges schema generate --model <group> --values` gives every model type `toValue()` and `init(value:)` (C# `ToValue()` and
  `FromValue(value)`) with the plain Record that wire `--values` reads and writes, so models and wire records convert
  through one value instead of hand-written copies. A nested field that does not convert reports its path, such as
  `entries[].level`, as the wire value path does.
- Schema union alternatives take labels, as in `:Text max 64 | bind: :Loadout_UnitBind`: generated code names the
  alternative `.bind` (C# `Bind`) instead of after its type; the label changes no bytes and no hash.
- Schema tag sets `tags :Mount as (#front, #rear)`: a field, list element or alternative of type `:Mount` is that
  `:Tag(...)` in place, with the same bytes and hash, and generated code declares one `Mount` enum for every field of it
  instead of one per field.
- Schemas take the fields of another schema with `with :Unit_Common`, optionally `all optional` and
  `without (id)`; an `abstract schema` only lends its fields. The record stays flat and hashes like written fields.
  Generated models adopt a protocol (C# interface) per schema they take unchanged; `GesSchema.IsAbstract` and `Takes`
  (Swift `isAbstract`, `takes`) describe it, and misuse of an abstract schema is `schema.invalidAbstract`.
- Schema fields `computed by` an expression, as in script records: `power: :Quantity(W) computed by 0W when duration <= 0s,
  otherwise energy / duration`. The subset reads fields, lets and constants with arithmetic and comparisons in units,
  `when … otherwise`, `min of`, `max of`, `clamp`, `abs`, `default`, `has value` and list sums and counts; binding checks
  types and units, and the expression belongs to the hash. Computed fields are never encoded, decoding adds them, a
  document that sets one fails, and generated models and wire records give them as read-only properties that compute the
  bits of the virtual machine. `GesSchema.ComputedFields` (Swift `computedFields`) describes them; mistakes are
  `schema.invalidExpression`. Generated code is plain binary64 and Boolean code in the language's own operators, such as
  `duration <= 0.0 ? 0.0 : energy / duration`, with a small `GesComputed` helper type for the rules plain code lacks;
  only a field where whole numbers beyond 2^53 could meet keeps the exact-value helpers.
- `ges schema generate --model <group> --documents` (option `Documents`) lets models read GES document text: each root gets
  C# `FromDocument(text)` (Swift `init(document:)`) and the generation a `<Namespace>Document` type that reads any root by its
  `:Schema(:Name)` header. The document is checked against the embedded schemas, defaults apply, and errors carry their
  `document.*` or `wire.*` code, line, column and field path; a missing or other header is the new
  `document.schemaMismatch`. The Runtime's `GesSchemaDocument` does this for generated code, and `GesDocumentException.Path`
  (Swift `GesDocumentError.path`) names the field.
- Schema types `:Box(unit) { ... }` and `:Poly(unit) max n { ... }` hold the geometry library's `GesBox` and `GesPoly`
  with per-axis constraints like `:Point`. A Box encodes its corners `lower` and `upper` as two Points and its value
  orders them; a Poly encodes its points in the plane z = 0 as a List of x and y. The value path converts the library's
  external values, documents write a Box as the Map of its corners and a Poly as the List of its points, generated wire code
  holds `GesBox` and the reusable Points of a Poly with a `GesPoly` view, and models hold `GesBox` and `GesPoly`.
- Recursive schema Lists: `units: :List(:Loadout_Bind) max 16 depth 3` in `:Loadout_Bind` holds records of that schema
  up to three deep, so a tree needs one schema instead of one per level. The wire encoding is that of one schema per
  level; a record deeper than the depth fails with the new `wire.tooDeep`, whose message names the depth, such as
  `wire.tooDeep at 'units[].units[].units': deeper than depth 3` (`GesWireException.Detail`, Swift
  `GesWireError.detail`), and a document with `document.tooDeep`. Generated code of the recursive schema takes the level
  of each record; every other schema generates the same code as before. `GesSchemaType.Depth` (Swift `depth`) describes it.
  The element may also be a union of scalars and the schema itself, `units: :List(id: :Text max 64 | bind: :Loadout_Bind)
  max 16 depth 3`, so a document writes a unit as its id alone or as a bind; at the depth the List holds neither.
- `ges schema generate -d <dir>` (`--out-dir`) writes one file per declaration, or per `.gess` with `--split file`,
  plus a shared `_<Namespace>` file and the manifest `.ges-generated.json`; a later run removes files the manifest owns and
  no longer needs, warns about foreign files with `cli.unowned`, and `--check` compares the whole directory.
  `GesSchemaGenerator.GenerateFiles` and `GenerateModelFiles` (Swift `generateFiles`, `generateModelFiles`) return the
  files for build integrations, selected by `GesSchemaGenerateOptions.Split`.
- Schema fields take defaults, as in `efficiency: :Percentage default 100%` or `maxPower: :Quantity(W) default power`:
  a literal, a constant, a let or a sibling field of the same type fills in what a value, model value or document leaves
  out, while the wire still carries every field. Generated models take defaults as initializer arguments, describe them
  and add them to the layouts; `GesSchemaFieldInfo.Default` (Swift `defaultValue`) describes them. Invalid defaults are
  `schema.invalidDefault`.
- Schemas declare constants with `let retention be 2s` inside their braces. Its fields can name a let as a bound, step,
  delta range or `max`; generated wire and model types declare it as a static member in base units, and it belongs to
  the hash. `GesSchema.Constants` (Swift `constants`) describes them.
- Generated model types carry their `SchemaHash` (Swift `schemaHash`), as wire records do.
- Generated model reading reports a required field without a key as `wire.missingField` and `nothing` where a value is
  required as `wire.nothingValue`, as the wire value path does, instead of `wire.typeMismatch`.
- Generated wire records fill a list of reused elements with `Set<Field>Count(count)` (Swift `set<Field>Count(_:)`), and
  a list of Text or open Tag elements also reads and sets as strings with `<Field>Values` (Swift `<field>Values`).
- Generated wire records count their exact size with `ByteCount()` and `DeltaByteCount(baseline)` (Swift
  `encodedByteCount()` and `deltaEncodedByteCount(against:)`), and `ToBytes`/`encoded()` and the delta forms allocate only
  that size instead of `MaxByteCount`: a record with a 4 MiB `:Bytes` limit that holds 1 KB allocates 1 KB. They build on
  the new counting `GesBitWriter.Counting()` (Swift `counting()`).
- Schema maximums accept digit separators, such as `:List(:Text max 1_024) max 10_000`.
- Schema `:Flags(#a, #b)` with up to 64 members: one bit per member on the wire, a script flag Map such as
  `[armed:]` in the value path and documents, `WriteFlags`/`ReadFlags` (Swift `writeFlags`/`readFlags`) masks, and
  generated C# `[Flags]` enums and Swift `OptionSet` types.
- Hexadecimal integer literals such as `0xff_88_00` in source, `parse`, numeric text casts and
  documents: exact Int64 values with `_` between digits, without unit or percentage suffix.
- Schema roles: `wire(group)` marks a schema, message or message set as a wire root and
  `model(group)` a schema as a model root, optionally in a group (`default` without one).
  `ges schema layout` and `generate` take `--wire <group>` and `--model <group>`, and the
  Generator adds `WireRoots`, `ModelRoots`, `ModelLayout` and `GenerateModels`. A wire root that
  reaches a document-only type fails reading with `schema.notEncodable`, so `ges schema check`
  reports it; `schema.invalidRole` and `schema.duplicateRole` report misplaced roles. Roles change
  no hash.
- `ges schema generate --model` writes plain model types: Swift `Hashable`, `Sendable`, `Codable`
  structs with a memberwise `init` (`Identifiable` with a key), C# 9 classes with value equality,
  with enums for closed tags and unions, and no ranges, steps or parameters needed.
- Swift extensions declared with macros: `@GesExtension`, `@GesFunction` and `@GesPredicate`
  generate `gesExtensions()` with signatures from the declarations, strict conversions, declared
  units and `nothing` for an argument that does not convert; instance methods yield an instance
  `gesExtensions()`. The C# attributes gain `[GesPredicate]`, nullable, Text, spatial and shape
  parameters and results, `[GesFunction(name, unit)]` and `AddInstance(instance)`.
- `ges schema generate --model --bindings` offers generated models to scripts as external types:
  a generated `GesTypes` catalog and registry, `ToGesValue`/`FromGesValue` (Swift `gesValue()`/
  `init?(gesValue:)`) and a `:Name(fields: map)` constructor, without the Bridge, macros or Reflection.
- `GesBox` and `GesPoly` in both runtimes, and `GesGeometryLibrary`, which offers them to
  scripts as the external types `:Box` and `:Poly` with the functions `:geometry.size`, `center`,
  `union`, `inset`, `nearest`, `bounds`, `area`, `distance`, `centroid`, `contains` and
  `intersects`; an invalid polygon gives `nothing`.
- `GesPoint` and `GesVector` in both runtimes: unitless spatial values with the language's
  affine arithmetic, `length`, `distance`, `normalized`, `dot` and `cross`, and conversions to
  and from `GesValue` with an explicit unit.
- Host conveniences on the spatial values: Swift `+=`, `-=`, `*=` and `/=` on `GesPoint` and `GesVector`
  (C# derives them from the operators), the component bounds `MinX` through `MaxZ` (Swift `minX` through `maxZ`) of
  `GesBox`, and `GesBox.FromSize` (Swift `init(size:)`), the box from the origin to a size. Scripts are unchanged.
- External values have three explicit read paths in both ports: `ExternalValue` returns the
  host object, `ReadField(name)` runs one getter and `MaterializedMap()` runs all of them.
  C# adds these and `GesValue.GesExternal(value)`; Swift adds `readField(_:)` and the protocol
  `GesFatalRuntimeError`, the counterpart of `GesFatalRuntimeException`, so a host can read
  the diagnostic of a failing getter, and a host's own conforming error keeps its diagnostic.
- Hosts build Maps and Records from ordered entries in the Runtime: C#
  `GesValue.GesMap(("a", x), ("b", y))` and `GesValue.GesRecord("Hit", ("a", x))`, Swift
  `.map([("a", x)])` and `.record(typeName: "Hit", [("a", x)])`.
- `value["name"]` reads the field of a Record, the entry of a Map or the coordinate of a
  Point or Vector and can be chained; everything else gives Nothing, without calling
  external field getters.
- `GesValueMap` can be traversed in canonical key order: `foreach` in C#, without
  allocating, and `for (key, value) in map` in Swift.
- `ges schema generate` (and `dotnet ges schema generate`) writes C# 9 or Swift 6 code for schema
  roots: a type per schema that writes and reads its records with the same bytes, error codes and
  paths as `GesSchemaWriter` and `GesSchemaReader`, without allocating, with parameters as constants.
  Schemas with Text, List or Dice become classes whose storage grows on demand and is reused;
  messages and message sets are supported too. Schemas write and read deltas with the bytes of
  `GesSchemaDelta`, lists with keyed elements included, and records compare by wire values
  (`WireEquals`), quantize and copy into existing storage. `--values` adds the conversion to and
  from GES values and messages through the value path. Generated Swift classes skip dynamic
  exclusivity checks, which halves their time; `--checked-exclusivity` keeps them. `--namespace`,
  `--access internal` and `--check` for checked-in output are supported.
- `ges schema layout` (and `dotnet ges schema layout`) prints the language-neutral generation
  layout of one or more roots, or of every declaration with `--all-roots`: every reached type with
  its bound encodings, hashes and the parameters its constants depend on. A type that several roots
  share appears once, and both CLIs print identical JSON.
- New Generator package: the NuGet package `GameEventScript.Generator` and the SwiftPM product
  `GameEventScriptGenerator` expose `GesSchemaGenerator.Layout` and `Generate` (Swift `layout` and
  `generate`) with `GesSchemaGenerateOptions`, so build integrations such as a Unity editor menu,
  an MSBuild target or a SwiftPM plugin generate schema code without the CLI. It depends on Runtime
  and Syntax only and games never ship it; the DLL distribution adds a `generator` set for a Unity
  `Editor` folder.
- Schema bit primitives: `GesBitWriter` and `GesBitReader` write and read values of 0 to 64
  bits in the wire format's bit order, and `GesWireRules` holds its Text, tag, quantization and
  wrapping rules and the delta range over plain operands. `GesWireException` (Swift `GesWireError`) can be created
  from a code and a path, and every `wire.*` code is a public constant. The schema writer and
  reader now build on these primitives, without loss in speed.
- Schema deltas work element by element for a list whose elements have a `key`, when the keys of
  baseline and record ascend strictly; otherwise the list is replaced as a whole. The limit of
  `MaxByteCount` rises from 1 MiB to 10 MiB. `GesSchemaDelta.Encode` uses its buffer as scratch
  space, so size it with `MaxByteCount`: a smaller buffer can fail with `wire.bufferTooSmall`
  even when the delta would fit.
- Schema deltas: `GesSchemaDelta` converts between full records and deltas against a baseline
  record without allocating, `GesSchemaValues` does so for values, and `ges schema dump --baseline`
  makes a delta readable. A decoder accepts only the canonical form (`wire.invalidDelta`), a
  record with another key is `wire.keyMismatch`, and a target buffer that overlaps an input is
  `wire.overlappingBuffers`.
- Type, message and handler names can join segments with single underscores, such
  as `:Unit_Weapon` and `Unit_Fire`. Each segment starts with an uppercase letter,
  so `:Unit_1` and `Unit__Fire` stay invalid; use `:Unit_Type1`. The underscore
  forms no hierarchy: `is :Unit` is false for a `:Unit_Weapon`.

- GES schemas (`.gess`) describe data formats and yield a very compact binary encoding for
  server-driven multiplayer games: ranges, steps and wrapping values with exact quantization,
  closed tag sets, tags, lists, Dice and text with hard limits, optional fields, unions, nested schemas, keys,
  messages and message sets, parameters bound per session, and a canonical FNV-1a 64 hash. In
  Runtime, `GesSchemaSet`, `GesSchema` (a read-only description with its fields), `GesBoundSchema`, `GesSchemaWriter`, `GesSchemaReader` and
  `GesSchemaDump` use no VM, and the writer and reader allocate nothing on success; `GesSchemaValues`
  converts Records and Messages. Schemas also check documents, including union types, through
  `CheckDocument`. The CLIs add `schema check`, `format`, `hash`, `canonical` and `dump`.
- `cm` is a prefixed literal spelling of the meter, like `km` and `mm`: `150cm` is
  `1.5m`. It adds no unit, `:Quantity(cm)` remains invalid, and printing documents
  does not choose it.
- Link failures of an import that the source calls carry one diagnostic per
  call site with its source location, in source order:
  `GesDynamicLinkException.Diagnostics` and Swift
  `GesDynamicLinkError.diagnostics`, whose first entry remains
  `Diagnostic`/`diagnostic`. The message names the import, for example
  `Unknown extension :navigation.nope(x)`. Columns need the SourceArchive;
  without a SourceMap the single diagnostic keeps only the symbol. Both CLIs
  print every call site.
- GES document highlighting: `GesSyntaxLanguage.Gesd` (Swift `.gesd`) categorizes
  `.gesd` text and reports its first document error as a diagnostic with a UTF-16
  range, in both Syntax packages. The CLIs add `document check` and `document
  format` for `.gesd` files and the console command `:show <file>`, which
  highlights a `.ges`, `.gesa` or `.gesd` file and marks its errors. TextMate,
  Sublime Text, bat and VS Code grammars cover `.gesd` files.
- The VS Code extension opens `.gesb` files in a read-only viewer: segments with
  offsets and sizes, inbound and outbound messages, functions, predicates, records,
  extensions, external types, embedded sources and texts. The viewer needs neither
  the CLI nor workspace trust; **GES: Show Assembler Dump** opens the `ges dump` output.
- `parse strict input` recognizes the same literals as `parse`, but produces
  `nothing` instead of the original Text when the input is not a complete data
  literal: `parse strict "standard"` and `parse strict "[1, 2"` are `nothing`,
  while `parse strict "'standard'"` is the Text `standard`. The bytecode encodes
  it as `ParseLiteral` with the Strict flag (`mode=Strict` in GESA, `#strict` in
  inline assembly).
- GES documents (`.gesd`): human-editable data files whose values are written
  like GES literals, such as `maxForce: 21.4kN` or `mount: :Vector(x: 0m, y: -0.8m)`.
  C# `GesDocument.Parse(text)`/`document.Print()` and Swift
  `GesDocument(parsing:)`/`document.text()` read and print them. The root is a
  Map or, with a `:Schema(:TypeName)` header, a Record; `:Schema('file.gess')`
  references a schema file. Entries are separated by commas or line breaks,
  `//` comments are allowed, and reading is strict: unquoted Text, duplicate
  keys and explicit forms or number tokens that construct nothing, such as
  `:Range(1)` or `NaN`, fail with
  a `GesDocumentException`/`GesDocumentError` carrying a stable `document.*`
  code, line and column. Printing writes exact prefixed units such
  as `1kW` or `250ms` and reads back bit-identically in every implementation;
  `GesValue.Format()`/`formatted()` print a single value. Schemas are only
  referenced, not validated.
- Literal parsing without a Host for extensions, configuration and other
  embedding code: C# `GesValue.ParseLiteral(text)` and
  `GesValue.ParseLiteralStrict(text)`, Swift `GesValue(literal:)` and
  `GesValue(strictLiteral:)`. They follow `parse` and `parse strict`, return
  `null`/`nil` for unrecognized strict input, never run constructors, and throw
  `GesLiteralLimitException`/`GesLiteralLimitError` when a parsing limit is
  exceeded, including more than 10,000 items materialized from Ranges in total.
- Physical units in C# and Swift: meter per second (`m/s`), meter per second
  squared (`m/s²`, ASCII `m/s^2`), kilogram (`kg`), newton (`N`), joule (`J`),
  watt (`W`), volt (`V`), ampere (`A`), hertz (`Hz`), square meter (`m²`, ASCII
  `m^2`), cubic meter (`m³`, ASCII `m^3`), watt per square meter (`W/m²`, ASCII
  `W/m^2`), kelvin (`K`), degree per second (`°/s`, ASCII `deg/s`), decibel
  (`dB`), watt per kelvin (`W/K`) and joule per kelvin (`J/K`), next to meters, seconds and degrees (ASCII `deg`). Literals accept the
  prefixed spellings `km`, `mm`, `cm²`, `km²`, `ms`, `t`, `kN`, `kJ`, `MJ`, `mW`,
  `kW`, `MW`, `mW/m²`, `kW/m²`, `mW/K`, `kW/K`, `MW/K`, `kJ/K`, `MJ/K`, `kV`,
  `kHz`, `Wh` and `kWh`, which normalize
  exactly to the base unit, and `/s` after a number for a rate such as `4/s`.
  Products and quotients follow a closed derivation table, for example
  `10m / 2s` is `5m/s`, `2kg * 3m/s²` is `6N`, `2m * 3m` is `6m²`, `10W / 2m²`
  is `5W/m²`, `10W / 2K` is `5W/K` and `1 / 2s` is `0.5Hz` (formerly `nothing`); other combinations of
  two units remain `nothing`. Squares and cubes follow the same table and roots
  invert them, so `(3m)²` is `9m²` and `sqrt 16m²` is `4m` (formerly `NaN`).
  Decibel levels add and scale by numbers but derive with no other unit and
  give `nothing` with a percentage. Every unit works in `:Quantity(...)`, text
  formatting and parsing, message JSON, vectors and points, inline assembly
  (`#meterPerSecond`) and host APIs, including Swift
  `@GesField(unit: .meterPerSecond)`. The UnitIds are 0 to 18; the formerly
  reserved bit, byte and bit-per-second ids 13 to 15 now hold the square meter,
  cubic meter and watt per square meter. Swift `GesQuantityUnit` is now
  `Sendable`.
- Runtime observers can follow delayed messages. C# `MessageScheduled(message, id,
  deadline, publish)` / Swift `messageScheduled(_:id:deadline:publish:)` reports
  each accepted `emit … after` or `publish … after` once, with its send ID and its
  absolute deadline in microseconds on the host clock.
  `MessageDiscarded(id)` / `messageDiscarded(id:)` reports every queued,
  pending-initialization or delayed message removed before it was dispatched,
  such as after a failed initialization, so each send ID ends in a dispatch or a
  discard. Hosts without an observer behave as before. `ges run --verbose`
  traces them as `schedule #<id> … at <local time>` and `discard #<id>` and shows
  the send ID on emit, publish and dispatch lines.
- C# `IGesRuntimeObserver` gives every callback an empty default implementation,
  like Swift, so an observer implements only the callbacks it needs.

### Changed

- Swift Syntax, Runtime and Compiler no longer use Foundation. The Card Lab
  WebAssembly module shrinks from about 71 MB to about 19 MB. Highlighting the
  shared corpus is about 20 times faster in Swift and 3.5 times faster in C#.
- A delayed `emit after …` without recipients now behaves like the same emit
  without `after`: it is not scheduled and is observed as not queued, so it no
  longer keeps the host waiting, counts towards the queue limit or appears in
  `NextMessageDelay`. Its script result stays true. Delayed publishes are still
  scheduled for the outbound sink.
- The root SwiftPM package now exposes only the four libraries. Build `ges` from
  `implementation/swift/GameEventScriptTool`, or install it via Homebrew, the
  install script or standalone downloads. The root macOS minimum is now 10.15;
  the separate CLI retains its 10.15.4 minimum.
- Swift `GameEventScriptProgram` is now an immutable `final class`, so hosts and
  callers share a single Program instance. Program contents, binary encoding and
  copy-on-write isolation of returned arrays are unchanged.
- Script handlers execute faster in both runtimes. Integer arithmetic and
  integer range loops write results in place, constant map literals and data
  constructions no longer redo per-execution work, and sorting numeric keys is
  no longer quadratic. The Swift VM additionally mutates registers in place
  without per-access exclusivity checks or reference counting on integer paths.
  Behavior and results are unchanged.
- Both compilers move loop-invariant constant loads and arithmetic in front of
  their loop, so loops execute fewer instructions. Random draws, messages and
  host calls stay in place and results are unchanged; executed opcode counts
  drop and register requirements can grow slightly.
- The C# and Swift compilers now produce byte-identical Programs for the same
  sources and compilation options, including debug symbols, source maps and
  source archives. Only BuildMetadata differs to identify the producing compiler.
- The Swift compiler now writes the BuildMetadata section with compiler ID
  `steph.ges.compiler.swift` and version `0.1.0`, as the binary format requires
  of every port. Runtime behavior does not depend on it.

### Fixed
- Generated schema code for a List whose elements get an item type, such as `:List(:Number step 0.5 | :Text max 8)` or a
  List of Lists of Points, quantizes its elements in `CopyFrom` and deltas instead of skipping them, and no longer fails to
  generate for a List of Lists.
- Generated Swift models write keyword argument labels such as `default` without backticks, which Swift warned about;
  only a declared `var`, `let` or `inout` label keeps them.
- The C# CLI builds with .NET SDK 10.0.404, so the Windows archives and the AOT tool packages
  contain .NET 8.0.34 instead of 8.0.25, which had known vulnerabilities such as CVE-2026-111497.
  The libraries keep their .NET Standard 2.1 target.
- The CLI overview lists `ges schema layout` and `generate`, and `ges run --help` lists the
  console commands `:edit`, `:save` and `:quit!`.
- External field values are no longer cached, so they can change between handlers. In C#,
  a script's field read runs only that field's getter instead of all of them, so a failing
  getter affects only its own field.
- Swift `GesSchemaValues.encode` accepts an external value of the schema's type name, as C#
  does, instead of reporting `wire.typeMismatch`.
- Swift matches external field names by Unicode scalars, not by canonical equivalence.
- C# `GesValue.GesRecord(typeName, keys, values)` keeps the last value of a duplicate name,
  like `GesMap` and Swift; it used to keep both entries.
- `:Vector(x: 1)` and `:Point(x: 1m)` compile in C# like in Swift, and
  `:Vector(x: v, y: 3)` with a Vector `v` is `nothing` instead of writing 3 into `z`.
- The Swift compiler accepts the source short form `:Message(Done)`, like the C#
  compiler.
- `:Series(kind: fibonacci)` compiles without quotes, and `parse` and documents read
  `:Series(offset: 2, kind: fibonacci)`.
- In Swift, a statement `emit` or `publish` of a Handler value no longer sends a message,
  like in C#.
- A line break may stand before the comma of a further tag inside `:Message(...)`.
- `:Quantity(km)(1)` reports `validate.invalidTypeConstructor` in Swift like in C#, and
  a `:Quantity(m)` without a value reports at its start in C# like in Swift.
- `:Message(...)`, `:Handler(...)` and `:Series(...)` can span lines like other
  type constructors. Depending on the form, a line break made the value `nothing`
  in C# or was a syntax error in one or both compilers.
- Swift link diagnostics for exceeded register and call-stack limits and a full
  initialization queue now carry a message, as in C#, instead of an empty one.
- C# syntax errors that name an unexpected token, such as `Unexpected token '}'`,
  now range over that token, as in Swift, instead of having no length; their
  start positions are unchanged.
- Unbraced `for` and `random with` bodies open a scope of their own during
  validation, as braced bodies do: a binding there no longer escapes into the
  enclosing scope or repeats the loop binding, and shadows it instead.
- `:sum` and `:average` without a binding no longer report that they shadow an
  enclosing binding named `value`.
- A repeated parameter of a `:Handler(Msg(...))` data form is reported at the
  parameter instead of the start of the source.
- Swift no longer accepts `pair of a kind`, which only the counts from `three`
  take, and reads the face after `pair of` as an expression also when it starts
  with a binding named `a`.

- The Swift compiler treats a backslash in a source Text literal as ordinary
  text, as the language specifies and C# does, so `'C:\'` and `"path\"` compile.
  Before, the backslash escaped the closing quote and the literal failed with
  `parse.syntax`.
- The Swift compiler rejects a vertical tab or form feed between tokens with
  `parse.syntax`, like C#; only space, tab and the line breaks LF, CRLF and CR
  are source whitespace.
- C# `parse` and `GesValue.ParseLiteral` no longer accept whitespace between `:`
  and a type name, such as `: Number(5)`, matching Swift and the source type
  syntax; such input now fails recognition like any other malformed literal.

- C# external field reads now follow the field's declared type, as in Swift:
  a field declared `Float<Watt>` that returns the integer `3` reads as `3W`,
  and the same applies to Map conversion, keys, values, entries and iteration.
  Before, C# returned the raw field value.
- External type fields and constructor parameters declared as Tag keep a Tag
  value unchanged; before, C# turned it into `nothing` and Swift into the Tag
  `#nothing`. `nothing` stays `nothing` under every declared type, so a missing
  optional Tag field in Swift no longer reads as `#nothing`, and a Boolean
  constructor parameter no longer turns `nothing` into `false`.
- The Swift compiler no longer exhausts small thread stacks on deeply nested
  sources. Its parser and compiler passes need far less stack per nesting level,
  so sources at the expression and statement nesting limits compile on a 512 KB
  thread, the stack of Swift concurrency tasks, also in Debug builds. Before,
  about 25 nested calls could crash an unoptimized build there.
- The Swift compiler rejects reserved keywords such as `empty`, `record` or `if`
  as argument labels, map literal keys and extension function names with
  `parse.syntax`, like the C# compiler; `to`, `nothing`, the math constants and
  prefix intrinsic names stay valid labels. It also accepts `°` (`degree`)
  wherever an identifier is expected, such as a map key.
- The Swift compiler reports every diagnostic of a failing phase, as C# does,
  instead of only the first: the parser resumes after a syntax error at the next
  declaration or statement, and validation reports all semantic errors in the
  order that `specs/Diagnostics.md` defines. Call, arity and `is`-predicate
  checks now run during validation, so they appear in that list.
- The C# compiler rejects records with a repeated field name or an unknown field
  type, as Swift does. A field type may be a built-in type, a record of the
  program or a configured external type.
- Both compilers reject `:Integer` and `:Float` wherever a type is named; use
  `as :Number` to convert and `is integer` or `is fractional` to check. Casts and
  checks to other custom types bound only at run time remain valid.
- `ln`, `exp`, roots and powers without an exactly representable integer result
  are no longer folded at compile time; they run on the target platform's math
  library, so a constant such as `ln 2` and `ln x` for `x = 2` always agree
  within one execution. Their exact inner terms are still folded.
- Constant folding of integer products with a unit is exact like VM execution:
  `3000000000m * 3000000001` stays `9000000003000000000m` instead of being
  rounded through binary64; only an Int64 overflow falls back to binary64.
- When a source exceeds the nesting limit, both compilers still report the
  syntax errors found before it in that source.
- Both compilers no longer hang on a `module` declaration after another
  declaration or on a repeated one; it is now a `parse.syntax` error at the
  `module` keyword.
- Compiler diagnostics in C# and Swift start at the same, most specific source
  position: the offending token, the repeated or misnamed parameter, argument,
  condition or binding, the failing call or constructor, or the first construct
  that exceeds the nesting limit.

## [0.4.0] — 2026-10-11

### Added

- Inline `asm` in C# and Swift: statement blocks, binding initializers, function
  and predicate bodies, and computed Record fields. Symbolic operands preserve
  compiler-owned signatures and addresses; definite-initialization and scope
  checks keep outer bindings immutable. GESA dumps are not assembler input.
- Compact nested loops, such as `for suit in suits and rank in ranks { ... }`,
  with the same ordering and scope as ordinary nested loops.
- Condition-first guarded choices, `when condition then value otherwise fallback`,
  alongside the existing value-first spelling. Parameterless predicates support
  `is name` and `is not name`.
- Inclusive `value is between lower and upper` and `is not between` comparisons,
  evaluating the value once and preserving short-circuit semantics.
- Parenthesized extension calls support direct member access and collection
  selectors, such as `:board.top(zone: #discard).id`.
- Combined collection pipelines: `cartesian`, `lockstep`/`zip`, `union`,
  `intersect` and `difference`, with source-less or leading-source forms.
  Multiple bindings work in loops and selectors; compact
  `[:entries :filter ... :select ...]` chains avoid repeated brackets.
  List `A * B` materializes a binary Cartesian product. `:foreach` evaluates an
  expression for each item and returns `nothing`.
- Compound iterator modes, component register outputs and the Cartesian opcode
  (`0xE0`) in both runtimes, validators, GESA and inline ASM. Pipeline component
  iteration avoids an intermediate pair allocation per row.
- Strict Numeric mode for arithmetic, Min/Max, Negate/Abs, Clamp and Move.
  Inline ASM uses a final `#numeric` operand; dumps display `mode=Numeric`.
  Accepted values match `is numeric`, including Boolean as 0/1 and Dice sums.
- Optional Host opcode instrumentation and `run --profile report.md` in both
  CLIs. Reports include opcode, source-line and instruction measurements, six
  VM loop phases, terminal loop checks and partial results on runtime failures.
- Browser Card Lab with editable Highest Card, Mau Mau, Skat and Blackjack rules.
  One shared Wasm instance serves the game and fullscreen editor. The editor
  loads examples, Draft and Saved sources, downloads source, checks diagnostics
  and restarts the game on close. Player-count changes also restart immediately.
- Card Lab source folding and a read-only highlighted GESA dump, with line numbers,
  region folding and inline source annotations. Full-document search includes
  folded content; Ctrl/Cmd+F adopts selected text and supports match navigation.
- Configurable Card Lab player layout, colored player/zone/table badges, titled
  notices and action buttons below cards or zones. Blackjack uses bottom seating
  and reports outcomes through badges and table text without a result popup.
- Bounded C#/Swift reader mutation fuzzing, independently generated canonical
  roundtrip properties, bidirectional binary exchange and pinned 0.3.0 consumer
  upgrade checks. Release documentation defines platform evidence and separate,
  revision-bound performance qualification without extra native Swift PR builds.

### Changed

- `sum` and `average` are strictly numeric, including singleton collections.
  Nonnumeric values make the result `nothing`; later projections still execute.
  Numeric strings are not converted implicitly. Use explicit conversions for
  numeric text and `fold` for concatenation.
- Combined collection sources evaluate every source expression once from left
  to right before validation, including invalid combinations. Construction no
  longer creates preliminary validation iterators.
- Qualified extension names must be contiguous (`:namespace.name`).
- Opcode values `0xF8..0xFF` are reserved for local experiments and rejected by
  standard compilers, validators and runtimes; they are not a plugin ABI.

### Fixed

- C# distribution smoke tests use fresh build and restore directories, preventing
  stale reproducible DLLs from a prior version contaminating package validation.
- Program validation rejects internal VM values escaping into script-visible
  data, including through inline assembly control flow.
- Swift compiler lowering now matches C# for constant Text-key access, collection
  guard branches and short-circuit expressions. Adjacent source mappings are
  compacted; inlined constants are attributed to their use sites.
- Combined pipelines preserve ordinary selector semantics: implicit numeric
  aggregates consume complete elements, Dice results retain their kind, Map
  restrictions remain enforced, and Dice difference rejects invalid face values.
- Card Lab browser assets and workers share a build version to prevent mixed
  cached revisions, including stale dump requests. Dump source names follow the
  loaded game instead of always using `rules.ges`.
- Removed Card Lab folding entries accidentally inserted into the historical
  0.2.0 and 0.3.0 notes; that feature belongs to this development release.

### Performance

- Swift uses compact value storage, inline dispatch and owner-local typed register
  writes, reducing register access and value-copy overhead while preserving exact
  Int64 arithmetic, units and runtime access checks. Dice sums use cached numeric
  storage; collection counts stay in payloads. Numeric operations use register
  fast paths. Profiler timestamps are captured at callback boundaries.
- C# single binders no longer allocate binding lists, and binding metadata is
  stored only on relevant AST nodes. Swift normal iterators reuse fixed operand
  descriptions during validation.
- Reviewed profile-specific reference updates distinguish deliberate storage and
  safety-validation costs from avoidable allocations. All zero-dispatch allocation
  budgets remain zero. See the [performance review](docs/development/Release040Performance.md).

### Migrating from 0.3.0

- Upgrade the GES libraries together and recompile scripts with the matching
  Compiler and Runtime. New Numeric modes, compound iterators and Cartesian
  instructions require the updated Runtime. GESB remains V1; this does not imply
  compatibility with every pre-1.0 binary. The tested 0.3.0 upgrade covers selected
  consumers and frozen fixtures, not arbitrary old Programs.
- Perform a clean rebuild of C# consumers after updating packages. Incremental
  CopyLocal can retain an old reproducible DLL with matching timestamp and size;
  the upgrade check uses `dotnet build -t:Rebuild`.
- Replace text/list accumulation through `[:sum]` with an explicit `fold`.
  Convert text deliberately, for example `values[:sum value => value as :Number]`.
  Boolean and Dice remain numeric; incompatible units still produce `nothing`.
  Recompile existing binaries to adopt the corrected aggregate contract.
- Remove spaces or comments inside qualified extension names; whitespace before
  arguments remains valid. The existing value-first `when` syntax, predicate
  calls and nested loops remain supported. Do not mix the two choice spellings
  within one chain.
- Do not depend on short-circuiting between combined source expressions: even
  when an earlier source is invalid, later source expressions execute. Native
  extension side effects are not rolled back.

### Known limitations

- Pre-1.0 compatibility remains subject to the
  [version policy](docs/guide/distribution/Packages.md#version-policy).
  [Platform coverage](docs/guide/distribution/Platforms.md) distinguishes full
  Conformance from package and CLI smoke checks; portable libraries do not imply
  full qualification on every OS, browser or embedding.
- Native effects remain application-owned and nontransactional. Profiling adds
  measurement overhead; profile numbers are not cross-machine speed promises.
  Card Lab is an example embedding, not a multiplayer service or a general WASM
  distribution of all SwiftPM products.
- Final candidate package, symbol, reproducibility, installation and platform
  evidence is required before publishing. See the
  [0.4.0 closing review](docs/development/Release040Review.md) for the distinction
  between completed development work and remaining publication gates.

## [0.3.0] — 2026-09-29

This release adds CLI editing, portable and native AOT NuGet tools, and Host
idle-duration observations in C# and Swift.

### Added

- Exposed the `ges` executable in the root SwiftPM package alongside the libraries.
  Homebrew and standalone downloads remain available. The root package now requires
  macOS 10.15.4 to cover CLI Foundation I/O.

- Host idle-duration observations in C# and Swift, including synchronized Bridge
  runner access, for application-defined inactivity recovery without host timers.

- Added Homebrew installation through `schloepke/gameeventscript/ges`. New standalone
  CLI releases provide C# for Windows and Swift for macOS/Linux; NuGet tools remain
  available across platforms. Previously published downloads remain available.

- Added external-editor REPL drafts with `:edit`, scratch ID `0`, explicit `:save`,
  conflict checks, in-memory reloads and unsaved-change protection on `:quit`.
  Clearing scratch removes its previous handlers. Explicit `:reload` refreshes
  clean file-backed drafts from disk while preserving unsaved edits.
  Editor selection supports GES_EDITOR before VISUAL/EDITOR, with nano (macOS/Linux)
  or notepad.exe (Windows) as the default.
  Terminal editors inherit the foreground terminal in Swift as well as .NET;
  `:help` lists editing, saving and forced exit directly.

- Added NuGet release preparation for the portable `GameEventScript.Tool` and
  native `GameEventScript.Tool.Aot` CLI, with verified Windows/macOS/Linux
  x64 and ARM64 package jobs and explicit manual publication.

- Added `--aot` to the local C# CLI installer for native macOS/Linux builds,
  including updates, switching installation modes and native uninstallation.

- Added generated GES/GESA Sublime Text and bat grammars, TextMate/Sublime CLI
  actions, and shared syntax-only completions and snippets with website downloads.

- Published the release version policy: compatible fixes in 0.x patches, features
  and contract changes in 0.x minor releases with migration notes, and Semantic
  Versioning for the declared compatibility surface from 1.0 onward.

- Defined source and binary compatibility within major release lines from 1.0,
  including message/conversion guarantees, recompilation exceptions for random
  and opcode-budget consumption, and no guaranteed support for pre-1.0 binaries.

- Recorded the current event-driven language as the 1.0 baseline after reviewing
  remaining language work; host-bound reconstruction and a portable
  standard-extension library are not required for that baseline.
- Recorded the C# and Swift embedding API baseline and source-API compatibility
  rules, including intentional native-language differences and API snapshot gates.

### Changed

- Both CLI multiline prompts use plain continuation indentation. Run diagnostics
  appear in red with `--color`, respecting `NO_COLOR`.

- CLI program selectors now use plain numeric IDs (`:source 1`, `:dump 1`,
  `:unload 1`). The previous `@1` spelling remains a compatibility alias.

- Clarified application ownership of state mutation and persistence, including
  native handler and extension effects, external-value stability and failure
  recovery. Existing runtime behavior is unchanged.

### Fixed

- Swift compiler diagnostics now explain validation and compilation failures
  instead of repeating their diagnostic codes, including unknown REPL types.

- Fixed native AOT C# CLI crashes when listing programs, showing source archives
  or tracing text arguments, using generated JSON serialization metadata.

- Fixed C# CLI interactive history and cursor editing without `--color`; color
  selection now affects highlighting only.

### Migrating from 0.2.0

- SwiftPM consumers on macOS must declare macOS 10.15.4 or newer when using the
  root package, which now also exposes the optional `ges` executable.

- Upgrade the GES library packages together to 0.3.0; SwiftPM consumers use the
  `0.3.0` tag. Idle-duration observations require this version or newer.
- NuGet CLI installations can use either `GameEventScript.Tool` or
  `GameEventScript.Tool.Aot`. Uninstall one before switching to the other because
  both provide `dotnet-ges`. Native tool installation requires .NET SDK 10+.
- New standalone C# archives target Windows. On macOS/Linux, use the Swift CLI
  through Homebrew or release downloads, or install a C# NuGet tool.
- REPL edits stay in memory until `:save`. Explicit `:reload` refreshes clean
  sources from disk and preserves unsaved drafts; rebuilding the session reruns
  initialization and clears queued work. Numeric selectors replace `@ID` in
  examples, while the old spelling remains accepted.

## [0.2.0] — 2026-09-26

This development release expands the language, native integration and tooling.
C# and Swift share the same release version. Compatibility may change during 0.x;
see the migration notes below before upgrading from 0.1.0.

### Added

- Website publication triggers the hosting pull webhook after changed output is
  successfully pushed to `site`, with bounded retries and a secret-backed URL.

- Standalone CLI release archives: C# for Windows, Linux and macOS; Swift for
  Linux and macOS; x64 and ARM64 for each. Downloads include runtime dependencies,
  license notices, release identity and checksums, with native relocation tests
  and an explicitly triggered GitHub Release upload.

- Pixel-Duo icons embedded in the NuGet library packages and local .NET CLI package.

- Root SwiftPM test targets reuse native SwiftBridge, macro and highlighter tests,
  making them discoverable by Swift Package Index and runnable with `swift test`.

- Generated C# DocFX and Swift DocC API references in the documentation website,
  with source identity checks, Swift Package Index metadata and compatibility badges.

- Informative feature-version notes in specifications and guides, distinguishing
  the published baseline from unreleased behavior.

- Pixel-Duo website identity with a shared blue/teal palette, repository-owned
  brand sources, and Mermaid diagrams for host startup and dispatch.

- Downloadable TextMate JSON and classic XML editor bundles for GES/GESA in the
  website's Tools section, generated from the canonical repository grammars.

- English language, C# and Swift introductory guides, plus a static documentation
  website with a landing page, local search and the existing normative references.
- Website CI verifies pull requests and publishes successful main builds to a
  separate `site` branch for pull-based deployment by existing webhosting.

- Standalone C# and Swift SyntaxHighlighter libraries, shared with both CLIs,
  using canonical TextMate rules. They expose UTF-16 ranges, semantic categories,
  scope stacks, incremental line states and an optional ANSI renderer.

- SwiftBridge annotations `@GesType`, `@GesField` and `@GesConstruct` generate
  native bindings, with inferred field types and explicit numeric units.

- Explicit portable type data forms, including Range, built-in Series, Handler,
  Message and immutable Record snapshots; `parse` reconstructs them and can run
  known script Record constructors after complete input recognition.
- Text splitting with `[:split on separator]` and `[:split on whitespace]`.
- Explicit Runtime-only V1 product JSON codecs in C# and Swift, preserving
  message argument order and exporting external values as Record snapshots.
- C# and Swift support conditional bindings with `if let`, including multiple
  bindings and conditions separated by semicolons. Checks short-circuit from
  left to right; successful bindings are available to subsequent checks and
  the then body.
- Collection selectors `[:fold acc be seed, value => expression]` and
  `[:reduce acc, value => expression]` accumulate values in iteration order.
  An empty fold returns its seed; an empty reduce returns `nothing`.
- `emit` and `publish` can return a Boolean indicating whether the host accepted
  the send. This does not guarantee delivery or outbound sink acceptance.
- `emit after duration Message(...)` and `publish after duration Message(...)`
  schedule messages using time quantities, including fractional seconds.
  Delayed publication invokes the outbound sink only when due.
- Hosts accept an injectable monotonic clock, expose `NextMessageDelay` /
  `nextMessageDelay`, and return `Waiting` / `.waiting` when only future work
  remains. Native bridge runners schedule wakeups automatically.
- CLI batch runs wait for delayed messages. Interactive sessions process due
  messages while preserving unfinished input and command history.
- Shared Markdown Conformance supports virtual-clock advancement and waiting
  assertions, with coverage for the new language and scheduling behavior.

### Changed

- Website navigation stays visible on desktop landing pages; mobile landing and
  documentation headers scroll with the page to leave more room for content.

- Record, Range, Series, Handler and Message text now uses reconstructible data
  forms. Integral Binary64 range inputs normalize to exact integer ranges.
- GESB V1 adds ConstructData and SplitText; older readers reject these opcodes.
- **Source compatibility:** every `if` header and then body share one local
  scope; else has a separate sibling scope. Bindings from either branch never
  escape the `if`, including unbraced bodies. For a binding needed afterward,
  use `let value be expression when condition otherwise nothing`.
- **Embedding:** `RunToCompletion()` / `runToCompletion()` never sleeps. A
  custom event loop must handle `Waiting` / `.waiting` and pump again when the
  next message is due. Delayed messages count toward the queue limit and keep
  `IsIdle` / `isIdle` false.
- **Bytecode compatibility:** four result-bearing send opcodes extend GESB V1.
  The original statement-send encodings remain unchanged. Programs using the
  new opcodes require an updated Runtime; older validators reject them.

### Fixed

- Website deployment uses the POST method required by Plesk webhooks and reports
  HTTP status or connection failure categories without exposing the webhook URL.

- SwiftPM distribution and SwiftBridge explicitly declare macOS 10.15, matching
  SwiftSyntax's minimum and fixing dependency planning with Swift 6.1.
- Swift highlighting preserves Unicode separators inside comments and GESA
  source-line strings without misclassifying subsequent assembler instructions.
- C# and Swift highlighting retain declaration keyword colors across line breaks
  and while declarations are incomplete.

- Swift CLI recognizes application-mode cursor keys used by Ghostty.
- C# CLI retains command history when switching between the ordinary prompt
  and delayed-message input handling.

### Migrating from 0.1.0

- Upgrade all GES library packages used by an application together to 0.2.0.
  SwiftPM consumers use the `0.2.0` tag. Syntax highlighting is an optional new
  product; add it only to targets that use it.
- Move bindings needed after an `if` outside its branches. Neither braced nor
  unbraced branch bindings escape; conditional expressions with
  `when ... otherwise ...` can initialize an outer binding.
- Custom host pumps must handle `Waiting` / `.waiting` and resume when
  `NextMessageDelay` / `nextMessageDelay` is due. `RunToCompletion()` /
  `runToCompletion()` does not wait for future messages; bridge runners and CLI
  batch execution handle this waiting for their callers.
- Deploy the updated Runtime before using binaries containing the new data,
  splitting or result-bearing send opcodes. The GESB format remains V1, but
  0.1.0 validators reject instructions they do not support.
- Update consumers that depend on the previous Text spelling of Records, Ranges,
  Series, Handlers or Messages. These values now use explicit reconstructible
  data forms; ordinary top-level Text remains unchanged. Integral Range inputs
  normalize to exact integer arithmetic even when supplied as Binary64.
- Product JSON is an explicit transport API. External values arrive as immutable
  Record snapshots, not native objects; decoding does not invoke constructors or
  restore host bindings. Local message delivery continues to use values directly.

## [0.1.0] — 2026-09-21

First public development release of the immutable, event-driven core.

- Published Runtime, Compiler and native Bridge libraries for C# on NuGet and
  Swift through one versioned SwiftPM package.
- Verified C# and Swift against the shared Markdown Conformance corpus, including
  portable GESB V1 program exchange and deterministic execution.
- Included serial Hosts, explicit startup, multiple loaded programs, native
  handlers, extensions and external types, with execution limits.
- Provided source-installed development CLIs for compilation, validation,
  execution, binary inspection and interactive use.
- Documented installation and the pre-1.0 compatibility policy.

[0.4.0]: https://github.com/schloepke/GameEventScript/compare/0.3.0...0.4.0
[0.3.0]: https://github.com/schloepke/GameEventScript/compare/0.2.0...0.3.0
[0.2.0]: https://github.com/schloepke/GameEventScript/compare/0.1.0...0.2.0
[0.1.0]: https://github.com/schloepke/GameEventScript/releases/tag/0.1.0
