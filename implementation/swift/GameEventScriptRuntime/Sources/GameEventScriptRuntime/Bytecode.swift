// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Explicit V1 identifiers; checked against C# by the repository verification gate.

/// V1 section identifiers used by the portable binary reader and writer.
public enum GameEventScriptSectionType: UInt16, Sendable, CaseIterable {
    /// Required program identity and resource requirements.
    case `programMetadata` = 0x0001
    /// Required UTF-8 string table.
    case `stringConstants` = 0x0002
    /// Required shared lists of UInt16 indexes.
    case `uInt16IndexLists` = 0x0003
    /// Required callable, message and external-import declarations.
    case `bindings` = 0x0004
    /// Required array of fixed-width bytecode instructions.
    case `code` = 0x0010
    /// Optional register names and code lifetimes.
    case `debugSymbols` = 0x0020
    /// Optional instruction-to-source mappings.
    case `sourceMap` = 0x0021
    /// Optional embedded source documents.
    case `sourceArchive` = 0x0022
    /// Optional compiler identity and version.
    case `buildMetadata` = 0x0030
    /// Reserved signature section identifier.
    case `reservedSignature` = 0x0040
    /// Opaque named custom section identifier.
    case `namedCustom` = 0xFFFE
}

/// Role of a declarative binding in a portable Program.
public enum GameEventScriptBinaryBindKind: UInt8, Sendable, CaseIterable {
    /// Script handler matching a name and ordered argument labels.
    case `messageHandler` = 0x10
    /// Script handler matching a message name regardless of argument labels.
    case `messageNameHandler` = 0x11
    /// Script function returning a value.
    case `function` = 0x12
    /// Script predicate returning a Boolean result.
    case `predicate` = 0x13
    /// Import resolved against the host extension registry.
    case `extensionCall` = 0x20
    /// Message signature used by emit or publish instructions.
    case `outboundMessage` = 0x21
    /// Script record constructor.
    case `record` = 0x30
    /// Import resolved against the host external-type registry.
    case `externalType` = 0x31
}

/// Explicit V1 instruction identifiers. Operand layouts and execution semantics are defined in the portable Bytecode
/// specification.
public enum GameEventScriptBytecodeOpCode: UInt8, Sendable, CaseIterable {
    /// No operation.
    case `nop` = 0x00
    /// Adds `Count > 0` active local registers or releases `-Count` registers when `Count < 0`. Entry prologs reserve
    /// only locals beyond preloaded arguments.
    case `registerLocals` = 0x01
    /// Unconditional branch.
    case `jump` = 0x02
    /// Branches when `X.IsTrue()`.
    case `jumpIfTrue` = 0x03
    /// Branches when `X.IsFalse()`.
    case `jumpIfFalse` = 0x04
    /// Branches when `!X.IsTrue()`, including `nothing`.
    case `jumpIfNotTrue` = 0x05
    /// Branches when `X.Kind` is `Nothing`.
    case `jumpIfNothing` = 0x06
    /// Enters a VM-owned local call frame at a known code address. Arguments are the contiguous staged sequence
    /// immediately before the call. With `NormalizeResultAsPredicate`, the returned value is normalized to boolean or
    /// `nothing`.
    case `call` = 0x07
    /// Creates a built-in mathematical series. Supported kinds are `Fibonacci` and `Factorial`.
    case `createSeries` = 0x08
    /// Calls a dynamically bound host extension. With `NormalizeResultAsPredicate`, the result is normalized to boolean
    /// or `nothing`.
    case `callExternal` = 0x09
    /// Returns no value from the current frame; normal calls map this to DSL `nothing`.
    case `returnVoid` = 0x0A
    /// Returns the value in `X` from the current frame.
    case `returnValue` = 0x0B
    /// Emits a statically shaped message without tags.
    case `emitMessage` = 0x0C
    /// Emits a statically shaped message with tags.
    case `emitMessageWithTags` = 0x0D
    /// Emits a dynamic message value without tags.
    case `emitMessageValue` = 0x0E
    /// Emits a dynamic message value with tags.
    case `emitMessageValueWithTags` = 0x0F
    /// Publishes a statically shaped message without tags.
    case `publishMessage` = 0x10
    /// Publishes a statically shaped message with tags.
    case `publishMessageWithTags` = 0x11
    /// Publishes a dynamic message value without tags.
    case `publishMessageValue` = 0x12
    /// Publishes a dynamic message value with tags.
    case `publishMessageValueWithTags` = 0x13
    /// Converts `X` to the declared built-in type. Custom/record types use `CastCustom`. `Cast :Tag` validates tag
    /// syntax; invalid tag text writes `nothing`. `Cast :Vector`/`:Point` structurally convert between vectors and
    /// points by copying components and unit.
    case `cast` = 0x14
    /// Converts `X` to a custom/record type identified by `Y`.
    case `castCustom` = 0x15
    /// Converts `X` to the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds.
    case `castUnit` = 0x16
    /// Coerces `X` through the source-level `:Number` type, including explicit numeric text parsing; invalid text
    /// writes `nothing`. Integral values stay integer; otherwise the result is float.
    case `castNumeric` = 0x17
    /// Writes whether `X` has the declared built-in type. Custom/record types use `CheckCustomType`.
    case `checkType` = 0x18
    /// Writes whether `X` has the custom/record type identified by `Y`.
    case `checkCustomType` = 0x19
    /// Writes whether `X` has the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type
    /// kinds.
    case `checkUnit` = 0x1A
    /// Writes whether `X` has a runtime numeric view. Numbers, percentages, booleans (`false` = `0`, `true` = `1`), and
    /// dice sums are numeric. Text and tags are not parsed here.
    case `checkNumeric` = 0x1B
    /// Writes whether `X` has a finite integral numeric view. Booleans and dice are integer. Text is not parsed here.
    case `checkInteger` = 0x1C
    /// Writes whether `X` has a finite non-integral numeric view. Text is not parsed here.
    case `checkFractional` = 0x1D
    /// Copies a register value/reference; the source register remains unchanged.
    case `move` = 0x1E
    /// Reads a named member.
    case `memberAccess` = 0x1F
    /// Reads a statically known positional element.
    case `indexAccess` = 0x20
    /// Reads a dynamic property: integer selectors use index semantics; text/tag selectors use member semantics.
    case `propertyAccess` = 0x21
    /// Binds ordered argument values to a handler signature. Argument names come from the signature.
    case `bindHandler` = 0x22
    /// Loads `nothing`.
    case `loadNothing` = 0x23
    /// Loads boolean `true`.
    case `loadTrue` = 0x24
    /// Loads boolean `false`.
    case `loadFalse` = 0x25
    /// Loads an inline signed `Int64`.
    case `loadInteger` = 0x26
    /// Loads an inline IEEE-754 `Float64`.
    case `loadFloat` = 0x27
    /// Loads an inline percentage ratio as the dedicated percentage value kind.
    case `loadPercentage` = 0x28
    /// Loads a text literal.
    case `loadText` = 0x29
    /// Loads a tag literal.
    case `loadTag` = 0x2A
    /// Loads a handler literal. The shape list is `[messageNameStringIndex, argumentNameStringIndex...]`.
    case `loadHandler` = 0x2B
    /// Loads a statically shaped message value. Shape is `[messageNameStringIndex, argumentNameStringIndex...]`.
    case `loadMessage` = 0x2C
    /// Stages a register value for the next stage-consuming instruction.
    case `stageRegister` = 0x2D
    /// Stages DSL `nothing` for the next stage-consuming instruction.
    case `stageNothing` = 0x2E
    /// Stages `true` for the next stage-consuming instruction.
    case `stageTrue` = 0x2F
    /// Stages `false` for the next stage-consuming instruction.
    case `stageFalse` = 0x30
    /// Stages an inline integer value.
    case `stageInteger` = 0x31
    /// Stages an inline float value.
    case `stageFloat` = 0x32
    /// Stages a text literal from `StringPool`.
    case `stageText` = 0x33
    /// Stages a tag literal from `StringPool`.
    case `stageTag` = 0x34
    /// Stages an inline percentage ratio value.
    case `stagePercentage` = 0x35
    /// Creates a dice value; `X` and `Y` are not registers.
    case `createDice` = 0x36
    /// Creates a vector from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing
    /// components become `0`.
    case `createVector` = 0x37
    /// Creates a point from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing
    /// components become `0`.
    case `createPoint` = 0x38
    /// Creates a list from the contiguous staged value sequence immediately before the opcode.
    case `createList` = 0x39
    /// Creates a map from key names and the contiguous staged value sequence immediately before the opcode.
    case `createMap` = 0x3A
    /// Creates a range value with implicit step `1`.
    case `createRange` = 0x3B
    /// Creates a range value with explicit step.
    case `createRangeWithStep` = 0x3C
    /// Creates a VM-internal range iterator with default step `+1`.
    case `createRangeIterator` = 0x3D
    /// Creates a VM-internal range iterator with an explicit step.
    case `createRangeIteratorWithStep` = 0x3E
    /// Creates a compact literal range iterator.
    case `createRangeIteratorShort` = 0x3F
    /// Calls the record constructor bind with staged constructor-parameter values; computed fields are derived inside
    /// the constructor.
    case `createRecord` = 0x40
    /// Creates the concrete custom record value from the constructor-produced field map.
    case `createRecordValue` = 0x41
    /// Constructs a host-bound external type value from named staged argument values.
    case `createExternalType` = 0x42
    /// Semantic value check; exact complement of `IsEmpty`.
    case `hasValue` = 0x43
    /// Semantic emptiness check; true for `nothing`, `NaN`, and empty text/collections.
    case `isEmpty` = 0x44
    /// Presence/default operator.
    case `default` = 0x45
    /// Tri-state boolean combine.
    case `or` = 0x50
    /// Tri-state boolean combine.
    case `and` = 0x51
    /// Tri-state boolean combine.
    case `xor` = 0x52
    /// Binary implication combine.
    case `implies` = 0x53
    /// Logical negation.
    case `not` = 0x54
    /// Binary comparison.
    case `equal` = 0x55
    /// Binary comparison.
    case `notEqual` = 0x56
    /// Binary comparison.
    case `less` = 0x57
    /// Binary comparison.
    case `greater` = 0x58
    /// Binary comparison.
    case `lessOrEqual` = 0x59
    /// Binary comparison.
    case `greaterOrEqual` = 0x5A
    /// Binary numeric operation.
    case `add` = 0x5B
    /// Binary numeric operation.
    case `subtract` = 0x5C
    /// Binary numeric operation.
    case `multiply` = 0x5D
    /// Binary numeric operation.
    case `divide` = 0x5E
    /// Binary numeric operation.
    case `power` = 0x5F
    /// Floor-like integer division operation.
    case `integerDivide` = 0x60
    /// Numeric modulo operation.
    case `modulo` = 0x61
    /// Numeric remainder operation.
    case `remainder` = 0x62
    /// Binary extrema reduce step.
    case `min` = 0x63
    /// Binary extrema reduce step.
    case `max` = 0x64
    /// Numeric negation.
    case `negate` = 0x65
    /// Absolute value.
    case `abs` = 0x66
    /// Natural logarithm.
    case `logN` = 0x67
    /// Chance evaluation.
    case `chance` = 0x68
    /// The only opcode with three direct source registers.
    case `clamp` = 0x69
    /// Takes an integer random value from integer bounds using the current random scope.
    case `randomTake` = 0x6A
    /// Takes a float random value from numeric bounds using the current random scope.
    case `randomTakeFloat` = 0x6B
    /// Saves the active random state and resets it from a valid dynamic unitless integer seed; an invalid seed retains
    /// the copied state.
    case `randomPush` = 0x6C
    /// Saves the active random state and resets it from an inline signed `Int64`.
    case `randomPushConstant` = 0x6D
    /// Restores the previous random scope.
    case `randomPop` = 0x6E
    /// Reads a zero-based mathematical series term; non-series sources yield `nothing`.
    case `term` = 0x6F
    /// Natural exponential. Unitless numeric input only; overflow to `+Infinity` is valid.
    case `exp` = 0x70
    /// Floors a unitless numeric value and writes an integer.
    case `floor` = 0x71
    /// Ceils a unitless numeric value and writes an integer.
    case `ceil` = 0x72
    /// Truncates a unitless numeric value toward zero and writes an integer.
    case `truncate` = 0x73
    /// Rounds a unitless numeric value using midpoint-to-even and writes an integer.
    case `roundHalfEven` = 0x74
    /// Rounds midpoint values away from zero and writes an integer.
    case `roundHalfUp` = 0x75
    /// Rounds midpoint values toward zero and writes an integer.
    case `roundHalfDown` = 0x76
    /// Converts degrees to unitless radians.
    case `degreeToRadians` = 0x77
    /// Converts unitless radians to a degree quantity.
    case `degreeFromRadians` = 0x78
    /// Normalizes a degree or unitless numeric value into `[0, 360)` degrees.
    case `wrapDegree` = 0x79
    /// Sine. Unitless input is radians; degree input is converted to radians; result is unitless.
    case `sin` = 0x7A
    /// Cosine. Unitless input is radians; degree input is converted to radians; result is unitless.
    case `cos` = 0x7B
    /// Tangent. Unitless input is radians; degree input is converted to radians; result is unitless.
    case `tan` = 0x7C
    /// Inverse sine for unitless numeric input in `[-1, 1]`; result is radians.
    case `asin` = 0x7D
    /// Inverse cosine for unitless numeric input in `[-1, 1]`; result is radians.
    case `acos` = 0x7E
    /// Inverse tangent for unitless numeric input; result is radians.
    case `atan` = 0x7F
    /// `atan2(y, x)` with matching units; result is radians. `(0, 0)` yields `0`.
    case `atan2` = 0x80
    /// 2D hypotenuse; matching units are retained in the result.
    case `hypot2D` = 0x81
    /// 3D hypotenuse; matching units are retained in the result.
    case `hypot3D` = 0x82
    /// Scalar distance or 3D vector/point distance; matching units are retained.
    case `distance` = 0x83
    /// 2D coordinate distance; matching units are retained.
    case `distance2D` = 0x84
    /// 3D coordinate distance; matching units are retained.
    case `distance3D` = 0x85
    /// Scalar or 3D vector/point squared distance; result is unitless.
    case `distanceSquared` = 0x86
    /// 2D squared coordinate distance; result is unitless.
    case `distanceSquared2D` = 0x87
    /// 3D squared coordinate distance; result is unitless.
    case `distanceSquared3D` = 0x88
    /// Scalar square or vector/point squared length; result is unitless.
    case `lengthSquared` = 0x89
    /// 2D squared length; result is unitless.
    case `lengthSquared2D` = 0x8A
    /// 3D squared length; result is unitless.
    case `lengthSquared3D` = 0x8B
    /// Normalizes a 3D vector/point and returns a unitless vector; zero length yields `nothing`.
    case `normalize` = 0x8C
    /// Normalizes 2D coordinates and returns a unitless vector with `z=0`; zero length yields `nothing`.
    case `normalize2D` = 0x8D
    /// Normalizes 3D coordinates and returns a unitless vector; zero length yields `nothing`.
    case `normalize3D` = 0x8E
    /// 3D vector/point dot product with matching units; result is unitless.
    case `dot` = 0x8F
    /// 2D coordinate dot product; result is unitless.
    case `dot2D` = 0x90
    /// 3D coordinate dot product; result is unitless.
    case `dot3D` = 0x91
    /// 3D vector/point cross product with matching units; result is a unitless vector.
    case `cross` = 0x92
    /// 2D coordinate cross product; result is the scalar z component, unitless.
    case `cross2D` = 0x93
    /// 3D coordinate cross product; result is a unitless vector.
    case `cross3D` = 0x94
    /// 3D vector/point angle in radians; zero length yields `nothing`.
    case `angleBetween` = 0x95
    /// 2D coordinate angle in radians; zero length yields `nothing`.
    case `angleBetween2D` = 0x96
    /// 3D coordinate angle in radians; zero length yields `nothing`.
    case `angleBetween3D` = 0x97
    /// Takes the first `Y` values from a series, list, dice, range, or iterator source.
    case `takeFirst` = 0xA0
    /// Drops the first `Y` values from a series, list, dice, range, or iterator source.
    case `dropFirst` = 0xA1
    /// Takes the last `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`.
    case `takeLast` = 0xA2
    /// Drops the last `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`.
    case `dropLast` = 0xA3
    /// Takes the highest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`.
    case `takeHighest` = 0xA4
    /// Takes the lowest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`.
    case `takeLowest` = 0xA5
    /// Drops the highest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`.
    case `dropHighest` = 0xA6
    /// Drops the lowest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`.
    case `dropLowest` = 0xA7
    /// Chooses one random element from a finite list, dice, range, or iterator source; empty/invalid/series sources
    /// yield `nothing`.
    case `oneRandom` = 0xA8
    /// Chooses up to `Y` random elements without replacement. Lists stay lists, dice stay dice, ranges and iterators
    /// materialize as lists.
    case `takeRandom` = 0xA9
    /// Selects one item using positive finite weights. Empty/no-positive-weight lists -> `nothing`.
    case `oneWeighted` = 0xAA
    /// Selects up to `Y` items without replacement using positive finite weights. Result is a list.
    case `takeWeighted` = 0xAB
    /// Counts collection/string/range/map items or consumes an iterator to count.
    case `count` = 0xAC
    /// Text/tag raw-text prefix check or list/dice/range sequence prefix check.
    case `startsWith` = 0xAD
    /// Text/tag raw-text suffix check or list/dice/range sequence suffix check.
    case `endsWith` = 0xAE
    /// Text/tag substring, map key, list/dice/range membership, or vector/point component membership.
    case `contains` = 0xAF
    /// Tests whether the container contains any value from the needle sequence. Iterators are consumed until the first
    /// match or exhaustion.
    case `containsAny` = 0xB0
    /// Tests whether the container contains every value from the needle sequence. Iterators are consumed until all
    /// needles match or exhaustion.
    case `containsAll` = 0xB1
    /// Truthiness `any` over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -> `false`;
    /// `nothing` -> `nothing`.
    case `hasAny` = 0xB2
    /// Truthiness `all` over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -> `true`;
    /// `nothing` -> `nothing`.
    case `hasAll` = 0xB3
    /// Value membership for map-like containers, vectors, and points.
    case `containsValue` = 0xB4
    /// Collection union/merge for list, dice, and map shapes; invalid shapes -> `nothing`.
    case `union` = 0xB5
    /// Map key intersection or list/dice multiset intersection; invalid shapes -> `nothing`.
    case `intersect` = 0xB6
    /// List zip into `{ left, right }` maps up to the shorter length; invalid shapes -> `nothing`.
    case `zip` = 0xB7
    /// Map/custom-type keys projection; `nothing` and non-map operands produce `nothing`.
    case `keysOfMap` = 0xB8
    /// Map/custom-type values projection; `nothing` and non-map operands produce `nothing`.
    case `valuesOfMap` = 0xB9
    /// Map/custom-type entries projection; `nothing` and non-map operands produce `nothing`.
    case `entriesOfMap` = 0xBA
    /// Returns the first element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty
    /// sources -> `nothing`.
    case `first` = 0xBB
    /// Returns the last element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty sources
    /// -> `nothing`.
    case `last` = 0xBC
    /// Returns the only element from list, dice, range, map/custom values, text/tag, or iterator;
    /// invalid/empty/multiple-element sources -> `nothing`.
    case `single` = 0xBD
    /// Creates a VM-internal iterator over a collection or range value. Non-iterable sources write `nothing`.
    case `iteratorCreate` = 0xBE
    /// Creates a VM-internal iterator, or writes `nothing` and jumps to `Y` when no iterator can be created.
    case `iteratorCreateOrJump` = 0xBF
    /// Writes the next item and continues, or jumps to `Y` when exhausted.
    case `iteratorNext` = 0xC0
    /// Disposes/closes a VM-internal iterator.
    case `iteratorClose` = 0xC1
    /// Materializes distinct source items in source order. Supports direct collection fast paths and iterators.
    case `distinct` = 0xC2
    /// Sorts source items ascending. Supports direct list, dice, range, and iterator sources.
    case `sortAscending` = 0xC3
    /// Sorts source items descending. Supports direct list, dice, range, and iterator sources.
    case `sortDescending` = 0xC4
    /// Reverses list, dice, range, or iterator sources. Dice and iterators materialize lists; ranges stay ranges.
    case `reverse` = 0xC5
    /// Shuffles list, dice, range, or iterator sources. Result is a list.
    case `shuffle` = 0xC6
    /// Creates a VM-internal list builder for generated collections.
    case `listBuilderCreate` = 0xC7
    /// Checks `MaxGeneratedCollectionItems` before adding an item.
    case `listBuilderAdd` = 0xC8
    /// Materializes the list builder as a list.
    case `listBuilderFinish` = 0xC9
    /// Creates a VM-internal map builder for generated map projections.
    case `mapBuilderCreate` = 0xCA
    /// Adds or overwrites a map entry; empty/nothing keys are skipped.
    case `mapBuilderAdd` = 0xCB
    /// Materializes the map builder as a map.
    case `mapBuilderFinish` = 0xCC
    /// Creates a VM-internal builder for `distinct by` loop lowering.
    case `distinctBuilderCreate` = 0xCD
    /// Adds the value only when the key was not seen before.
    case `distinctBuilderAdd` = 0xCE
    /// Materializes the distinct-by builder as a list.
    case `distinctBuilderFinish` = 0xCF
    /// Creates a VM-internal builder for `group by` loop lowering.
    case `groupBuilderCreate` = 0xD0
    /// Adds the value to the group identified by the key text.
    case `groupBuilderAdd` = 0xD1
    /// Materializes the group builder as a map from keys to grouped lists.
    case `groupBuilderFinish` = 0xD2
    /// Creates a VM-internal builder for `order by` loop lowering.
    case `orderBuilderCreate` = 0xD3
    /// Adds a key/value pair preserving source order for stable ordering.
    case `orderBuilderAdd` = 0xD4
    /// Sorts by stored keys ascending and materializes the ordered values as a list.
    case `orderBuilderFinishAscending` = 0xD5
    /// Sorts by stored keys descending and materializes the ordered values as a list.
    case `orderBuilderFinishDescending` = 0xD6
    /// Tests a dice/card pattern and returns boolean.
    case `hasPattern` = 0xD7
    /// Takes items matching a dice/card pattern. Dice sources produce dice; list sources produce lists.
    case `takePattern` = 0xD8
    /// Recognizes one complete data literal, preserving original Text on recognition failure.
    case `parseLiteral` = 0xD9
    /// Emits without delay and stores host acceptance in the destination register.
    case emitInstant = 0xDA
    /// Emits after a time quantity and stores host acceptance in the destination register.
    case emitAfter = 0xDB
    /// Publishes without delay and stores host acceptance in the destination register.
    case publishInstant = 0xDC
    /// Publishes after a time quantity and stores host acceptance in the destination register.
    case publishAfter = 0xDD
}

/// Portable value and VM-internal storage identifiers used as bytecode operands.
public enum GameEventScriptBytecodeTypeKind: UInt16, Sendable, CaseIterable {
    /// Absence of a value.
    case `nothing` = 0x00
    /// Boolean value.
    case `boolean` = 0x02
    /// Exact signed Int64 number.
    case `integer` = 0x03
    /// IEEE 754 binary64 number.
    case `float` = 0x04
    /// Binary64 ratio carrying Percentage kind.
    case `percentage` = 0x05
    /// Validated tag name.
    case `tag` = 0x06
    /// Unicode text.
    case `text` = 0x07
    /// Three-coordinate vector.
    case `vector` = 0x08
    /// Three-coordinate point.
    case `point` = 0x09
    /// Lazy numeric range.
    case `range` = 0x0a
    /// Message-handler signature.
    case `handler` = 0x10
    /// Message signature, arguments and tags.
    case `message` = 0x11
    /// Ordered immutable value collection.
    case `list` = 0x20
    /// Descending Int32 dice rolls.
    case `dice` = 0x21
    /// Canonical Text-keyed value map.
    case `map` = 0x22
    /// Internal mutable list construction state.
    case `listBuilder` = 0x23
    /// Internal mutable map construction state.
    case `mapBuilder` = 0x24
    /// Internal distinct-value construction state.
    case `distinctBuilder` = 0x25
    /// Internal grouping construction state.
    case `groupBuilder` = 0x26
    /// Internal sorting construction state.
    case `orderBuilder` = 0x27
    /// Internal collection iteration state.
    case `iterator` = 0x30
    /// Lazy built-in numeric series.
    case `series` = 0x31
    /// Record or host-defined external value.
    case `custom` = 0xFF
}

/// Dice-pattern identifiers for pattern instructions.
public enum GameEventScriptBytecodePatternKind: UInt16, Sendable, CaseIterable {
    /// Count a matching group of any face.
    case `countAny` = 0x00
    /// Count occurrences of a specified face.
    case `countFace` = 0x01
    /// Match a full-house pattern.
    case `fullHouse` = 0x02
    /// Match a consecutive-face pattern.
    case `straight` = 0x03
}

/// Built-in lazy numeric series identifiers.
public enum GameEventScriptBytecodeSeriesKind: UInt16, Sendable, CaseIterable {
    /// The Fibonacci series.
    case `fibonacci` = 0x01
    /// The factorial series.
    case `factorial` = 0x02
}
