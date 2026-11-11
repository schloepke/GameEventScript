// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

private typealias Code = GesSchemaDiagnostic

/// The parameter values of one binding.
public final class GesSchemaParameters {
    @_spi(Generator) public struct Value {
        @_spi(Generator) public let number: GesSchemaDecimal
        let unit: GesQuantityUnit
        let isPercent: Bool
        // A literal says whether it is a percentage; a plain number is the ratio of either kind.
        let isLiteral: Bool
    }

    private var values: [String: Value] = [:]

    /// Creates empty parameter values.
    public init() {}

    /// Sets a parameter from a literal such as `5km` or `10%`, exactly as written.
    /// - Returns: This instance.
    /// - Throws: `GesSchemaError` with `schema.syntax` when the text is no exact finite literal.
    @discardableResult public func with(_ name: String, literal: String) throws -> GesSchemaParameters {
        guard let read = GesSchemaLiterals.read(literal) else { throw GesSchemaError(code: GesSchemaDiagnostic.syntax, sourceName: "", line: 0, column: 0, message: "Expected an exact finite literal.") }
        values[name] = Value(number: read.value, unit: read.unit, isPercent: read.isPercent, isLiteral: true)
        return self
    }

    /// Sets a parameter to an integer in base units.
    /// - Returns: This instance.
    @discardableResult public func with(_ name: String, _ value: Int64, unit: GesQuantityUnit = .none) -> GesSchemaParameters {
        values[name] = Value(number: GesSchemaDecimal(value, 0), unit: unit, isPercent: false, isLiteral: false)
        return self
    }

    /// Sets a parameter to a Binary64 in base units, which converts through its shortest roundtrip text.
    /// - Returns: This instance.
    /// - Throws: `GesSchemaError` with `schema.invalidBound` when the value is not finite.
    @discardableResult public func with(_ name: String, _ value: Double, unit: GesQuantityUnit = .none) throws -> GesSchemaParameters {
        guard value.isFinite, let read = GesSchemaLiterals.read(value.description) else { throw GesSchemaError(code: GesSchemaDiagnostic.invalidBound, sourceName: "", line: 0, column: 0, message: "Expected a finite value.") }
        values[name] = Value(number: read.value, unit: unit, isPercent: false, isLiteral: false)
        return self
    }

    @_spi(Generator) public func value(_ name: String) -> Value? { values[name] }
}

@_spi(Generator) public enum GesSchemaNodeKind: UInt8, Sendable {
    case boolean
    case integer
    case quantized
    case float
    case tag
    case text
    case list
    case group
    case union
    case nothing
    // One bit per member of a :Flags set, in declared order, as one mask of `width` bits.
    case flags
}

// The numbers of one field of a bound layout, free of references so that the hot path never counts them. A subtree occupies
// consecutive nodes, and `next` is the first node after it. `after` is where writing goes on: the same, except that the
// end of a union alternative continues behind the union. `isTagText` marks a Text on the wire that holds a tag name, for a
// Tag with a max instead of a set.
@_spi(Generator) public struct GesSchemaNode: Sendable {
    @_spi(Generator) public var kind = GesSchemaNodeKind.boolean
    @_spi(Generator) public var optional = false
    @_spi(Generator) public var key = false
    @_spi(Generator) public var wrapping = false
    @_spi(Generator) public var hasRange = false
    @_spi(Generator) public var isTagBit = false
    @_spi(Generator) public var isTagText = false
    /// A `:Bytes max n`: a Text on the wire whose bytes need not be UTF-8.
    @_spi(Generator) public var isBytes = false
    @_spi(Generator) public var width = 0
    @_spi(Generator) public var next = 0
    @_spi(Generator) public var after = 0
    // The direct children of a group, which its delta has one change bit each for.
    @_spi(Generator) public var childCount = 0
    // A list whose single element type is a schema with a key has deltas by element, with positions of this width. Its
    // element is the next node and the element's key the one after that.
    @_spi(Generator) public var keyedElements = false
    @_spi(Generator) public var positionWidth = 0
    // Integer: its lower bound. Quantized: lowerN. The largest valid index; for a wrapping field the period is one more.
    @_spi(Generator) public var lower: Int64 = 0
    @_spi(Generator) public var maxIndex: UInt64 = 0
    // The largest difference in indices of a `delta within` and the width of its range form; zero without one.
    @_spi(Generator) public var deltaRange: UInt64 = 0
    @_spi(Generator) public var deltaWidth = 0
    @_spi(Generator) public var p: Int64 = 0
    @_spi(Generator) public var tenPower: Int64 = 1
    @_spi(Generator) public var pBinary = 1.0
    @_spi(Generator) public var sBinary = 1.0
    @_spi(Generator) public var lowerValue = 0.0
    @_spi(Generator) public var upperValue = 0.0
    @_spi(Generator) public var maxCount: Int64 = 0
}

// What a node is called and what its values are, for error paths and the value path.
@_spi(Generator) public struct GesSchemaNodeInfo: Sendable {
    @_spi(Generator) public var name = ""
    @_spi(Generator) public var path = ""
    @_spi(Generator) public var tags: [String] = []
    /// The named tag set of a Tag, or empty.
    @_spi(Generator) public var tagSet = ""
    @_spi(Generator) public var valueKind = GesSchemaTypeKind.nothing
    @_spi(Generator) public var unit = GesQuantityUnit.none
    @_spi(Generator) public var schemaName = ""
    /// The value a value or document that leaves the field out takes, for the field's first node.
    var defaultValue: GesResolvedDefault?
}

/// A schema, message or message set bound to parameter values. Immutable; it can be shared between threads.
public final class GesBoundSchema: Sendable {
    /// The description this binding comes from.
    public let schema: GesSchema
    /// The declaration's name.
    public let name: String
    /// The size of the largest encoding in bytes, of a full record or of a delta.
    public let maxByteCount: Int
    /// The schema hash, independent of parameter values.
    public let hash: UInt64
    /// The hash over the schema hash and the parameter values.
    public let bindingHash: UInt64
    @_spi(Generator) public let kind: GesSchemaKind
    @_spi(Generator) public let nodes: [GesSchemaNode]
    @_spi(Generator) public let infos: [GesSchemaNodeInfo]
    @_spi(Generator) public let members: [GesBoundSchema]
    @_spi(Generator) public let memberWidth: Int
    // The largest encoding of a schema or message in bits, which a message set adds its index to.
    let maxBitCount: Int64
    // The top-level fields with a change bit in a delta: all but the key.
    let deltaFieldCount: Int
    // The labels of a message's arguments, `_` for an unlabeled one.
    let labels: [String]
    private let fields: [String: Int]

    init(
        schema: GesSchema,
        name: String,
        kind: GesSchemaKind,
        nodes: [GesSchemaNode],
        infos: [GesSchemaNodeInfo],
        members: [GesBoundSchema],
        memberWidth: Int,
        maxByteCount: Int,
        maxBitCount: Int64,
        deltaFieldCount: Int = 0,
        hash: UInt64,
        bindingHash: UInt64,
        labels: [String]
    ) {
        self.schema = schema
        self.name = name
        self.kind = kind
        self.nodes = nodes
        self.infos = infos
        self.members = members
        self.memberWidth = memberWidth
        self.maxByteCount = maxByteCount
        self.maxBitCount = maxBitCount
        self.deltaFieldCount = deltaFieldCount
        self.hash = hash
        self.bindingHash = bindingHash
        self.labels = labels
        var fields: [String: Int] = [:]
        for (index, info) in infos.enumerated() { fields[info.path] = index }
        self.fields = fields
    }

    /// Whether the first field is a key that a reader can take first.
    public var hasKey: Bool { !nodes.isEmpty && nodes[0].key }

    /// The handle of a field; resolve it once and reuse it.
    /// - Parameter path: The path, such as `x`, `position.x`, `units[]`, `units[].name`, `#critical`, or `facing:Tag` for a union alternative.
    /// - Returns: The handle, or nil when the field does not exist.
    public func field(_ path: String) -> GesSchemaField? { fields[path].map { GesSchemaField(owner: self, index: $0) } }

    /// A member of a message set, or nil when the set has no such message.
    public func member(_ name: String) -> GesBoundSchema? { members.first { $0.name == name } }

    /// A member's index, which selects it in a writer, or nil when the set has no such message.
    public func memberIndex(_ name: String) -> Int? { members.firstIndex { $0.name == name } }
}

/// The handle of a field in a bound schema. Resolving it once makes further uses free of lookups.
public struct GesSchemaField: Sendable {
    let owner: GesBoundSchema
    let index: Int

    init(owner: GesBoundSchema, index: Int) {
        self.owner = owner
        self.index = index
    }

    /// The field's path.
    public var path: String { owner.infos[index].path }
}

/// Builds the layout of a declaration for fixed parameter values.
enum GesSchemaBinder {
    static let maximumByteCount: Int64 = 10_485_760
    private static let maximumNodeCount = 1_048_576
    private static let largestSteps: Int64 = 1 << 50
    private static let largestMultiple: Int64 = 1 << 53
    private static let maximumFractionDigits = 15
    private static let maximumListNesting = 4

    // The numbers of a numeric field, from literals alone or with parameter values. Parameters without a value leave the
    // checks that need them to binding.
    private struct Numeric {
        var lower: Int64 = 0
        var upper: Int64 = 0
        var p: Int64 = 1
        var k = 0
        var deltaRange: UInt64 = 0
    }

    static func checkLiterals(_ scope: GesFileScope, _ constraints: GesResolvedConstraints) throws { _ = try compute(scope, constraints, nil) }

    private static func compute(_ scope: GesFileScope, _ constraints: GesResolvedConstraints, _ parameters: GesSchemaParameters?) throws -> Numeric {
        var numeric = Numeric()

        func resolved(_ value: GesResolvedValue?) -> GesSchemaDecimal? {
            guard let value else { return nil }
            return value.isParameter ? parameters?.value(value.parameter)?.number : value.value
        }

        let lower = resolved(constraints.lower)
        let upper = resolved(constraints.upper)
        if let step = constraints.step {
            if step.value.significand <= 0 { throw scope.error(Code.invalidStep, step.offset, "A step is positive.") }
            let exponent = step.value.exponent
            numeric.k = exponent < 0 ? -exponent : 0
            if numeric.k > maximumFractionDigits { throw scope.error(Code.tooWide, step.offset, "A step has at most 15 fractional digits.") }
            guard let p = scale(step.value.significand, exponent < 0 ? 0 : exponent) else { throw scope.error(Code.tooWide, step.offset, "A step is too large.") }
            numeric.p = p
            if let lower { numeric.lower = try steps(scope, lower, numeric, constraints.lower!) }
            if let upper { numeric.upper = try steps(scope, upper, numeric, constraints.upper!) }
            if lower != nil && upper != nil && numeric.lower >= numeric.upper { throw scope.error(Code.invalidRange, constraints.lower!.offset, "A lower bound is below its upper bound.") }
            if let delta = constraints.delta {
                if delta.value.significand <= 0 { throw scope.error(Code.invalidStep, delta.offset, "A delta range is positive.") }
                numeric.deltaRange = UInt64(try steps(scope, delta.value, numeric, delta))
            }
        } else if constraints.integer {
            if let lower { numeric.lower = try whole(scope, lower, constraints.lower!) }
            if let upper { numeric.upper = try whole(scope, upper, constraints.upper!) }
            if lower != nil && upper != nil && numeric.lower >= numeric.upper { throw scope.error(Code.invalidRange, constraints.lower!.offset, "A lower bound is below its upper bound.") }
            if let delta = constraints.delta {
                if delta.value.significand <= 0 { throw scope.error(Code.invalidStep, delta.offset, "A delta range is positive.") }
                numeric.deltaRange = UInt64(try whole(scope, delta.value, delta))
            }
        } else {
            if let lower { try checkBinary(scope, lower, constraints.lower!) }
            if let upper { try checkBinary(scope, upper, constraints.upper!) }
            if let lower, let upper, compare(lower, upper) >= 0 {
                throw scope.error(Code.invalidRange, constraints.lower!.offset, "A lower bound is below its upper bound.")
            }
        }
        return numeric
    }

    // A fractional field compares in binary64, so its bounds must keep their meaning there.
    // A let is an integer when it is a whole unitless number within Int64, and otherwise a binary64 in base units, which must
    // keep its meaning there as a bound does.
    static func checkLet(_ scope: GesFileScope, _ declared: GesResolvedLet) throws -> Bool {
        if declared.kind == .number && declared.value.exponent >= 0 && scale(declared.value.significand, declared.value.exponent) != nil { return true }
        guard let binary = Double(declared.value.canonical), binary.isFinite, binary != 0 || declared.value.significand == 0 else {
            throw scope.error(Code.tooWide, declared.offset, "A let lies beyond binary64.")
        }
        return false
    }

    /// A numeric default lies in the range, a wrapping one below its upper bound, on the step and, for an integer field, is
    /// whole within Int64; a fractional one keeps its meaning in binary64. Bounds that a parameter without a value decides stay
    /// unchecked.
    static func checkDefault(_ scope: GesFileScope, _ constraints: GesResolvedConstraints, _ value: GesSchemaDecimal, _ offset: Int, _ parameters: GesSchemaParameters?) throws {
        if constraints.integer {
            if value.exponent < 0 || scale(value.significand, value.exponent) == nil {
                throw scope.error(Code.invalidDefault, offset, "The default of an integer field is whole and within Int64.")
            }
        } else {
            guard let binary = Double(value.canonical), binary.isFinite, binary != 0 || value.significand == 0 else {
                throw scope.error(Code.tooWide, offset, "A default lies beyond binary64.")
            }
        }

        func resolved(_ bound: GesResolvedValue?) -> GesSchemaDecimal? {
            guard let bound else { return nil }
            return bound.isParameter ? parameters?.value(bound.parameter)?.number : bound.value
        }

        if let lower = resolved(constraints.lower), compare(value, lower) < 0 {
            throw scope.error(Code.invalidDefault, offset, "A default lies within the field's range.")
        }
        if let upper = resolved(constraints.upper) {
            let order = compare(value, upper)
            if order > 0 || (order == 0 && constraints.wrapping) { throw scope.error(Code.invalidDefault, offset, "A default lies within the field's range.") }
        }
        if let step = constraints.step, step.value.significand > 0 {
            // value / step is whole: value · 10^k is a multiple of p for the step p · 10^-k.
            let exponent = step.value.exponent
            let k = exponent < 0 ? -exponent : 0
            let shift = value.exponent + k
            guard let p = scale(step.value.significand, exponent < 0 ? 0 : exponent), shift >= 0, let scaled = scale(value.significand, shift), scaled % p == 0 else {
                throw scope.error(Code.invalidDefault, offset, "A default lies on the field's step.")
            }
        }
    }

    private static func checkBinary(_ scope: GesFileScope, _ value: GesSchemaDecimal, _ origin: GesResolvedValue) throws {
        guard let binary = Double(value.canonical), binary.isFinite, binary != 0 || value.significand == 0 else {
            throw scope.error(Code.tooWide, origin.offset, "A bound of a fractional field lies beyond binary64.")
        }
    }

    // value / step as an exact integer within the limits.
    private static func steps(_ scope: GesFileScope, _ value: GesSchemaDecimal, _ numeric: Numeric, _ origin: GesResolvedValue) throws -> Int64 {
        let shift = value.exponent + numeric.k
        if shift < 0 { throw scope.error(Code.invalidStep, origin.offset, "A bound or delta range is a multiple of the step.") }
        guard let scaled = scale(value.significand, shift) else { throw scope.error(Code.tooWide, origin.offset, "A value exceeds 2^50 steps.") }
        if scaled % numeric.p != 0 { throw scope.error(Code.invalidStep, origin.offset, "A bound or delta range is a multiple of the step.") }
        let steps = scaled / numeric.p
        if steps > largestSteps || steps < -largestSteps || scaled > largestMultiple || scaled < -largestMultiple { throw scope.error(Code.tooWide, origin.offset, "A value exceeds 2^50 steps.") }
        return steps
    }

    private static func whole(_ scope: GesFileScope, _ value: GesSchemaDecimal, _ origin: GesResolvedValue) throws -> Int64 {
        guard value.exponent >= 0, let whole = scale(value.significand, value.exponent) else { throw scope.error(Code.invalidBound, origin.offset, "An integer bound is integral and within Int64.") }
        return whole
    }

    private static func scale(_ value: Int64, _ power: Int) -> Int64? {
        var result = value
        var index = 0
        while index < power && result != 0 {
            let (next, overflow) = result.multipliedReportingOverflow(by: 10)
            if overflow { return nil }
            result = next
            index += 1
        }
        return result
    }

    static func compare(_ left: GesSchemaDecimal, _ right: GesSchemaDecimal) -> Int {
        let a = left.significand
        let b = right.significand
        if a == 0 || b == 0 || (a < 0) != (b < 0) { return a < b ? -1 : a > b ? 1 : 0 }
        let magnitude = digits(a) + left.exponent
        let other = digits(b) + right.exponent
        if magnitude != other { return (a > 0) == (magnitude > other) ? 1 : -1 }
        let common = min(left.exponent, right.exponent)
        let first = scaledMagnitude(a, left.exponent - common)
        let second = scaledMagnitude(b, right.exponent - common)
        if first == second { return 0 }
        return (first < second) == (a > 0) ? -1 : 1
    }

    // Equal magnitudes have at most 19 digits when scaled, which a UInt64 holds also where an Int64 does not.
    private static func scaledMagnitude(_ value: Int64, _ power: Int) -> UInt64 {
        var result = value.magnitude
        var index = 0
        while index < power {
            result *= 10
            index += 1
        }
        return result
    }

    private static func digits(_ value: Int64) -> Int {
        var value = value
        var count = 1
        while value >= 10 || value <= -10 {
            value /= 10
            count += 1
        }
        return count
    }

    // The width of n values given n - 1: the bit length.
    static func bitLength(_ value: UInt64) -> Int { UInt64.bitWidth - value.leadingZeroBitCount }

    private struct Build {
        var nodes: [GesSchemaNode] = []
        var infos: [GesSchemaNodeInfo] = []
        let parameters: GesSchemaParameters
    }

    static func bind(_ root: GesResolvedDeclaration, _ parameters: GesSchemaParameters) throws -> GesBoundSchema {
        if root.isAbstract { throw root.scope.error(Code.invalidAbstract, root.offset, "'\(root.name)' is abstract; it is never encoded.") }
        var declarations: [GesResolvedDeclaration] = []
        var reached: [String: GesSchemaTypeSyntax] = [:]
        try GesSchemaCanonical.reach(root, &declarations, &reached)
        let hash = GesSchemaCanonical.hash(try GesSchemaCanonical.form(root))
        var lines = GesSchemaCanonical.hex(hash) + "\n"
        for name in reached.keys.sorted(by: { Array($0.utf8).lexicographicallyPrecedes(Array($1.utf8)) }) {
            guard let value = parameters.value(name) else { throw root.scope.error(Code.unknownName, root.offset, "Parameter '\(name)' has no value.") }
            try checkKind(root, name, reached[name]!, value)
            lines += "\(name) \(value.number.canonical)\n"
        }
        let bindingHash = GesSchemaCanonical.hash(lines)
        if root.kind == .messages {
            let members = try root.members.map { try bindNodes($0, parameters, hash, bindingHash) }
            let largest = members.map(\.maxBitCount).max() ?? 0
            let width = bitLength(UInt64(members.count) - 1)
            let total = largest + Int64(width)
            if total > maximumByteCount * 8 { throw root.scope.error(Code.tooLarge, root.offset, "The largest encoding of '\(root.name)' exceeds \(maximumByteCount) bytes.") }
            return GesBoundSchema(
                schema: root.schema!,
                name: root.name,
                kind: .messages,
                nodes: [],
                infos: [],
                members: members,
                memberWidth: width,
                maxByteCount: Int((total + 7) / 8),
                maxBitCount: total,
                hash: hash,
                bindingHash: bindingHash,
                labels: []
            )
        }
        return try bindNodes(root, parameters, hash, bindingHash)
    }

    // The values a document check uses must have their declared kind and unit, as at binding; others stay unchecked.
    static func checkParameters(_ root: GesResolvedDeclaration, _ parameters: GesSchemaParameters) throws {
        var declarations: [GesResolvedDeclaration] = []
        var reached: [String: GesSchemaTypeSyntax] = [:]
        try GesSchemaCanonical.reach(root, &declarations, &reached)
        for name in reached.keys.sorted(by: { Array($0.utf8).lexicographicallyPrecedes(Array($1.utf8)) }) {
            if let value = parameters.value(name) { try checkKind(root, name, reached[name]!, value) }
        }
    }

    private static func checkKind(_ root: GesResolvedDeclaration, _ name: String, _ declared: GesSchemaTypeSyntax, _ value: GesSchemaParameters.Value) throws {
        if value.unit != declared.unit || (value.isLiteral && value.isPercent != (declared.kind == .percentage)) {
            throw root.scope.error(Code.unitMismatch, root.offset, "Parameter '\(name)' has another kind or unit.")
        }
    }

    private static func bindNodes(_ declaration: GesResolvedDeclaration, _ parameters: GesSchemaParameters, _ hash: UInt64, _ bindingHash: UInt64) throws -> GesBoundSchema {
        var build = Build(parameters: parameters)
        for field in declaration.fields { try addField(&build, field.scope, field, "", 0, top: true) }
        for tag in declaration.tags {
            var node = GesSchemaNode()
            node.kind = .boolean
            node.isTagBit = true
            node.width = 1
            node.next = build.nodes.count + 1
            build.nodes.append(node)
            var info = GesSchemaNodeInfo()
            info.name = tag
            info.path = "#" + tag
            build.infos.append(info)
        }
        seal(&build.nodes)
        var bits = cost(build.nodes, 0, build.nodes.count)
        if declaration.kind == .schema { bits = max(bits, deltaCost(build.nodes)) }
        if bits > maximumByteCount * 8 { throw declaration.scope.error(Code.tooLarge, declaration.offset, "The largest encoding of '\(declaration.name)' exceeds \(maximumByteCount) bytes.") }
        return GesBoundSchema(
            schema: declaration.schema!,
            name: declaration.name,
            kind: declaration.kind,
            nodes: build.nodes,
            infos: build.infos,
            members: [],
            memberWidth: 0,
            maxByteCount: Int((bits + 7) / 8),
            maxBitCount: bits,
            deltaFieldCount: countFields(build.nodes, build.nodes.first?.key == true ? build.nodes[0].next : 0, build.nodes.count),
            hash: hash,
            bindingHash: bindingHash,
            labels: declaration.fields.map { $0.unlabeled ? "_" : $0.name }
        )
    }

    private static func countFields(_ nodes: [GesSchemaNode], _ start: Int, _ end: Int) -> Int {
        var count = 0
        var index = start
        while index < end {
            count += 1
            index = nodes[index].next
        }
        return count
    }

    private static func seal(_ nodes: inout [GesSchemaNode]) {
        for index in nodes.indices {
            nodes[index].after = nodes[index].next
            if nodes[index].kind == .group { nodes[index].childCount = countFields(nodes, index + 1, nodes[index].next) }
        }
        for union in nodes.indices where nodes[union].kind == .union {
            var alternative = union + 1
            while alternative < nodes[union].next {
                let end = nodes[alternative].next
                for index in alternative..<end where nodes[index].after == end { nodes[index].after = nodes[union].after }
                alternative = end
            }
        }
    }

    // The largest delta in bits: the key, then a change bit and the largest payload of each field.
    private static func deltaCost(_ nodes: [GesSchemaNode]) -> Int64 {
        let start = nodes.first?.key == true ? nodes[0].next : 0
        return min(maximumByteCount * 8 + 1, (start == 0 ? 0 : Int64(nodes[0].width)) + deltaCost(nodes, start, nodes.count))
    }

    private static func deltaCost(_ nodes: [GesSchemaNode], _ start: Int, _ end: Int) -> Int64 {
        let limit = maximumByteCount * 8 + 1
        var total: Int64 = 0
        var index = start
        while index < end {
            var payload = payloadCost(nodes, index)
            if nodes[index].optional { payload = 1 + max(payload, fullCost(nodes, index)) }
            total = min(limit, total + 1 + payload)
            index = nodes[index].next
        }
        return total
    }

    private static func payloadCost(_ nodes: [GesSchemaNode], _ index: Int) -> Int64 {
        let node = nodes[index]
        switch node.kind {
        case .boolean, .nothing:
            return 0
        case .group:
            return deltaCost(nodes, index + 1, node.next)
        case .list where node.keyedElements:
            return keyedListCost(nodes, index)
        case .union:
            var largest: Int64 = 0
            var alternative = index + 1
            while alternative < node.next {
                largest = max(largest, max(fullCost(nodes, alternative), payloadCost(nodes, alternative)))
                alternative = nodes[alternative].next
            }
            return Int64(node.width) + largest
        default:
            return node.deltaRange > 0 ? 1 + Int64(max(node.deltaWidth, node.width)) : fullCost(nodes, index)
        }
    }

    // The form bit and the larger form: three counts, and a position and the larger of an element and its delta for each.
    private static func keyedListCost(_ nodes: [GesSchemaNode], _ index: Int) -> Int64 {
        let limit = maximumByteCount * 8 + 1
        let node = nodes[index]
        let delta = deltaCost(nodes, nodes[index + 2].next, nodes[index + 1].next)
        let each = Int64(node.positionWidth) + max(cost(nodes, index + 1, node.next), delta)
        let byElement = 3 * Int64(node.width) + min(node.maxCount, limit / max(each, 1) + 1) * each
        return min(limit, 1 + max(fullCost(nodes, index), byElement))
    }

    // The full encoding of a value, without its presence bit.
    private static func fullCost(_ nodes: [GesSchemaNode], _ index: Int) -> Int64 { cost(nodes, index, nodes[index].next) - (nodes[index].optional ? 1 : 0) }

    // The largest encoding in bits. Sums stop growing beyond the limit, so nothing overflows.
    private static func cost(_ nodes: [GesSchemaNode], _ start: Int, _ end: Int) -> Int64 {
        let limit = maximumByteCount * 8 + 1
        var full: Int64 = 0
        var index = start
        while index < end {
            let node = nodes[index]
            var field: Int64
            switch node.kind {
            case .boolean:
                field = 1
            case .text:
                field = Int64(node.width) + min(node.maxCount, limit) * 8
            case .list:
                let element = cost(nodes, index + 1, node.next)
                field = Int64(node.width) + (element == 0 ? 0 : min(node.maxCount, limit / max(element, 1) + 1) * element)
            case .group:
                field = cost(nodes, index + 1, node.next)
            case .union:
                var largest: Int64 = 0
                var alternative = index + 1
                while alternative < node.next {
                    largest = max(largest, cost(nodes, alternative, nodes[alternative].next))
                    alternative = nodes[alternative].next
                }
                field = Int64(node.width) + largest
            default:
                field = Int64(node.width)
            }
            if node.optional { field += 1 }
            full = min(full + field, limit)
            index = node.next
        }
        return full
    }

    private static func addField(_ build: inout Build, _ scope: GesFileScope, _ field: GesResolvedField, _ prefix: String, _ lists: Int, top: Bool) throws {
        let path = prefix.isEmpty ? field.name : prefix + "." + field.name
        let index = build.infos.count
        try addUnion(&build, scope, field.alternatives, field.name, path, optional: field.marker == .optional, key: top && field.marker == .key, lists)
        guard let fallback = field.defaultValue else { return }
        if fallback.kind == .number { try checkDefault(scope, field.alternatives[0].constraints, fallback.value, fallback.offset, build.parameters) }
        build.infos[index].defaultValue = fallback
    }

    private static func addUnion(_ build: inout Build, _ scope: GesFileScope, _ alternatives: [GesResolvedType], _ name: String, _ path: String, optional: Bool, key: Bool, _ lists: Int) throws {
        if alternatives.count == 1 { return try addType(&build, scope, alternatives[0], name, path, optional: optional, key: key, lists) }
        try checkNodeCount(build, scope, alternatives[0].offset)
        let index = build.nodes.count
        var node = GesSchemaNode()
        node.kind = .union
        node.optional = optional
        node.maxIndex = UInt64(alternatives.count) - 1
        node.width = bitLength(node.maxIndex)
        build.nodes.append(node)
        var info = GesSchemaNodeInfo()
        info.name = name
        info.path = path
        build.infos.append(info)
        for alternative in alternatives {
            let alternativePath = path + ":" + (alternative.kind == .reference ? alternative.referenceName : alternative.kind.rawValue)
            if alternative.kind == .nothing {
                try checkNodeCount(build, scope, alternative.offset)
                var nothing = GesSchemaNode()
                nothing.kind = .nothing
                nothing.next = build.nodes.count + 1
                build.nodes.append(nothing)
                var nothingInfo = GesSchemaNodeInfo()
                nothingInfo.name = name
                nothingInfo.path = alternativePath
                nothingInfo.valueKind = .nothing
                build.infos.append(nothingInfo)
            } else {
                try addType(&build, scope, alternative, name, alternativePath, optional: false, key: false, lists)
            }
        }
        build.nodes[index].next = build.nodes.count
    }

    private static func addType(_ build: inout Build, _ scope: GesFileScope, _ type: GesResolvedType, _ name: String, _ path: String, optional: Bool, key: Bool, _ lists: Int) throws {
        try checkNodeCount(build, scope, type.offset)
        let index = build.nodes.count
        var node = GesSchemaNode()
        node.optional = optional
        node.key = key
        var info = GesSchemaNodeInfo()
        info.name = name
        info.path = path
        info.valueKind = type.kind
        info.unit = type.unit
        // The node keeps its place before its children.
        build.nodes.append(node)
        build.infos.append(info)
        switch type.kind {
        case .boolean:
            node.kind = .boolean
            node.width = 1
        case .number, .percentage, .quantity:
            try number(build.parameters, scope, type.constraints, type.offset, &node)
        case .tag:
            guard let tags = type.tags else {
                guard let max = type.max else { throw scope.error(Code.notEncodable, type.offset, "A Tag without a set or max has no hard limit.") }
                node.kind = .text
                node.isTagText = true
                node.maxCount = max
                node.width = bitLength(UInt64(max))
                break
            }
            node.kind = .tag
            info.tags = tags
            info.tagSet = type.tagSet
            node.maxIndex = UInt64(tags.count) - 1
            node.width = bitLength(node.maxIndex)
        case .flags:
            let members = type.tags ?? []
            node.kind = .flags
            info.tags = members
            node.width = members.count
            node.maxIndex = members.count == 64 ? .max : (1 << UInt64(members.count)) - 1
        case .text, .bytes:
            guard let max = type.max else { throw scope.error(Code.notEncodable, type.offset, "A \(type.kind.rawValue) without max has no hard limit.") }
            node.kind = .text
            node.isBytes = type.kind == .bytes
            node.maxCount = max
            node.width = bitLength(UInt64(max))
        case .list:
            guard let element = type.element, let max = type.max else { throw scope.error(Code.notEncodable, type.offset, "A List without element type and max has no hard limit.") }
            if lists >= maximumListNesting { throw scope.error(Code.notEncodable, type.offset, "Lists nest at most \(maximumListNesting) levels for encoding.") }
            node.kind = .list
            node.maxCount = max
            node.width = bitLength(UInt64(max))
            node.keyedElements = element.count == 1 && element[0].kind == .reference && element[0].reference?.fields.first?.marker == .key
            node.positionWidth = bitLength(UInt64(max - 1))
            try addUnion(&build, scope, element, "[]", path + "[]", optional: false, key: false, lists + 1)
            if !build.nodes[(index + 1)...].contains(where: { $0.width > 0 || $0.optional }) { throw scope.error(Code.notEncodable, type.offset, "A List element without bits has a count that no data limits.") }
        case .dice:
            guard let max = type.max else { throw scope.error(Code.notEncodable, type.offset, "A Dice without max has no hard limit.") }
            if lists >= maximumListNesting { throw scope.error(Code.notEncodable, type.offset, "Lists nest at most \(maximumListNesting) levels for encoding.") }
            try checkDice(scope, type.constraints, type.offset, build.parameters)
            node.kind = .list
            node.maxCount = max
            node.width = bitLength(UInt64(max))
            try checkNodeCount(build, scope, type.offset)
            var roll = GesSchemaNode()
            roll.next = build.nodes.count + 1
            try number(build.parameters, scope, rollConstraints(type.constraints), type.offset, &roll)
            build.nodes.append(roll)
            var rollInfo = GesSchemaNodeInfo()
            rollInfo.name = "[]"
            rollInfo.path = path + "[]"
            rollInfo.valueKind = .number
            build.infos.append(rollInfo)
        case .point, .vector:
            node.kind = .group
            if let axes = type.axes {
                for axis in axes { try addAxis(&build, scope, axis.axis, axis.constraints, path, type.offset) }
            } else {
                for axis in ["x", "y", "z"] { try addAxis(&build, scope, axis, GesResolvedConstraints(), path, type.offset) }
            }
        case .reference:
            node.kind = .group
            info.schemaName = type.referenceName
            let reference = type.reference!
            for field in reference.fields { try addField(&build, field.scope, field, path, lists, top: false) }
        default:
            throw scope.error(Code.notEncodable, type.offset, "A \(type.kind.rawValue) has no width and is only for documents.")
        }
        node.next = build.nodes.count
        build.nodes[index] = node
        build.infos[index] = info
    }

    // References expand into nodes, so a small source can ask for more than any encoding holds.
    // The encodability of a wire root as far as it is known without parameter values: the same conditions as binding, in
    // the same depth-first order, so that `check` reports what binding would. Bounds that a parameter decides stay binding
    // errors.
    static func checkEncodable(_ root: GesResolvedDeclaration) throws {
        var known: Set<EncodableVisit> = []
        if root.kind == .messages {
            for member in root.members { try checkEncodable(member, 0, &known) }
            return
        }
        try checkEncodable(root, 0, &known)
    }

    private struct EncodableVisit: Hashable {
        let declaration: ObjectIdentifier
        let lists: Int
    }

    private static func checkEncodable(_ declaration: GesResolvedDeclaration, _ lists: Int, _ known: inout Set<EncodableVisit>) throws {
        guard known.insert(EncodableVisit(declaration: ObjectIdentifier(declaration), lists: lists)).inserted else { return }
        for field in declaration.fields {
            if field.alternatives.count == 1 {
                try checkEncodable(field.scope, field.alternatives[0], lists, &known)
            } else {
                for alternative in field.alternatives where alternative.kind != .nothing { try checkEncodable(field.scope, alternative, lists, &known) }
            }
        }
    }

    private static func checkEncodable(_ scope: GesFileScope, _ type: GesResolvedType, _ lists: Int, _ known: inout Set<EncodableVisit>) throws {
        switch type.kind {
        case .boolean, .point, .vector:
            for axis in type.axes ?? [] { try checkWrapping(scope, axis.constraints, type.offset) }
        case .number, .percentage, .quantity:
            try checkWrapping(scope, type.constraints, type.offset)
        case .tag:
            if type.tags == nil && type.max == nil { throw scope.error(Code.notEncodable, type.offset, "A Tag without a set or max has no hard limit.") }
        case .flags:
            break
        case .text, .bytes:
            if type.max == nil { throw scope.error(Code.notEncodable, type.offset, "A \(type.kind.rawValue) without max has no hard limit.") }
        case .list:
            guard let element = type.element, type.max != nil else { throw scope.error(Code.notEncodable, type.offset, "A List without element type and max has no hard limit.") }
            if lists >= maximumListNesting { throw scope.error(Code.notEncodable, type.offset, "Lists nest at most \(maximumListNesting) levels for encoding.") }
            if element.count == 1 {
                try checkEncodable(scope, element[0], lists + 1, &known)
            } else {
                for alternative in element where alternative.kind != .nothing { try checkEncodable(scope, alternative, lists + 1, &known) }
            }
            if !hasBits(element) { throw scope.error(Code.notEncodable, type.offset, "A List element without bits has a count that no data limits.") }
        case .dice:
            if type.max == nil { throw scope.error(Code.notEncodable, type.offset, "A Dice without max has no hard limit.") }
            if lists >= maximumListNesting { throw scope.error(Code.notEncodable, type.offset, "Lists nest at most \(maximumListNesting) levels for encoding.") }
        case .reference:
            try checkEncodable(type.reference!, lists, &known)
        default:
            throw scope.error(Code.notEncodable, type.offset, "A \(type.kind.rawValue) has no width and is only for documents.")
        }
    }

    private static func checkWrapping(_ scope: GesFileScope, _ constraints: GesResolvedConstraints, _ offset: Int) throws {
        if constraints.wrapping && constraints.step == nil && !constraints.integer {
            throw scope.error(Code.notEncodable, offset, "A wrapping field without a step or integer has no index and is only for documents.")
        }
    }

    // Whether an element takes bits whatever the parameters: only a Tag of one tag, a Nothing and a schema of such fields have none.
    private static func hasBits(_ alternatives: [GesResolvedType]) -> Bool { alternatives.count > 1 || hasBits(alternatives[0]) }

    private static func hasBits(_ type: GesResolvedType) -> Bool {
        switch type.kind {
        case .tag: return type.tags?.count != 1
        case .nothing: return false
        case .point, .vector: return type.axes.map { !$0.isEmpty } ?? true
        case .reference: return type.reference!.fields.contains { $0.marker == .optional || hasBits($0.alternatives) }
        default: return true
        }
    }

    private static func checkNodeCount(_ build: Build, _ scope: GesFileScope, _ offset: Int) throws {
        if build.nodes.count >= maximumNodeCount { throw scope.error(Code.tooLarge, offset, "A bound layout has at most \(maximumNodeCount) fields.") }
    }

    private static func addAxis(_ build: inout Build, _ scope: GesFileScope, _ axis: String, _ constraints: GesResolvedConstraints, _ path: String, _ offset: Int) throws {
        try checkNodeCount(build, scope, offset)
        var node = GesSchemaNode()
        node.next = build.nodes.count + 1
        try number(build.parameters, scope, constraints, offset, &node)
        build.nodes.append(node)
        var info = GesSchemaNodeInfo()
        info.name = axis
        info.path = path + "." + axis
        info.valueKind = .number
        build.infos.append(info)
    }

    // The rolls of a Dice are positive Int32 values.
    private static func rollConstraints(_ constraints: GesResolvedConstraints) -> GesResolvedConstraints {
        var rolls = GesResolvedConstraints()
        rolls.integer = true
        rolls.lower = constraints.lower ?? GesResolvedValue(value: GesSchemaDecimal(1, 0))
        rolls.upper = constraints.upper ?? GesResolvedValue(value: GesSchemaDecimal(Int64(Int32.max), 0))
        return rolls
    }

    static func checkDice(_ scope: GesFileScope, _ constraints: GesResolvedConstraints, _ offset: Int, _ parameters: GesSchemaParameters?) throws {
        let rolls = rollConstraints(constraints)
        let numeric = try compute(scope, rolls, parameters)
        if (parameters != nil || !rolls.lower!.isParameter) && numeric.lower < 1 { throw scope.error(Code.invalidBound, offset, "A Dice roll is at least 1.") }
        if (parameters != nil || !rolls.upper!.isParameter) && numeric.upper > Int64(Int32.max) { throw scope.error(Code.invalidBound, offset, "A Dice roll fits Int32.") }
    }

    private static func number(_ parameters: GesSchemaParameters, _ scope: GesFileScope, _ constraints: GesResolvedConstraints, _ offset: Int, _ node: inout GesSchemaNode) throws {
        let numeric = try compute(scope, constraints, parameters)
        node.hasRange = constraints.lower != nil
        node.wrapping = constraints.wrapping
        if constraints.step != nil {
            node.kind = .quantized
            node.p = numeric.p
            node.tenPower = scale(1, numeric.k) ?? 1
            node.pBinary = Double(numeric.p)
            node.sBinary = Double(node.tenPower)
            node.lower = numeric.lower
            let span = UInt64(numeric.upper - numeric.lower)
            node.maxIndex = constraints.wrapping ? span - 1 : span
            node.width = bitLength(node.maxIndex)
            node.lowerValue = Double(numeric.lower * numeric.p) / node.sBinary
            node.upperValue = Double(numeric.upper * numeric.p) / node.sBinary
        } else if constraints.integer {
            node.kind = .integer
            node.lower = numeric.lower
            if node.hasRange {
                let span = UInt64(bitPattern: numeric.upper) &- UInt64(bitPattern: numeric.lower)
                node.maxIndex = constraints.wrapping ? span - 1 : span
                node.width = bitLength(node.maxIndex)
            } else {
                node.maxIndex = .max
                node.width = 64
            }
        } else {
            if constraints.wrapping { throw scope.error(Code.notEncodable, offset, "A wrapping field without a step or integer has no index and is only for documents.") }
            node.kind = .float
            node.width = 64
            if node.hasRange {
                node.lowerValue = binary(constraints.lower!, parameters)
                node.upperValue = binary(constraints.upper!, parameters)
            }
        }
        if let delta = constraints.delta {
            node.deltaRange = numeric.deltaRange
            node.deltaWidth = bitLength(numeric.deltaRange * 2)
            if node.deltaWidth >= node.width { throw scope.error(Code.invalidConstraint, delta.offset, "A delta range is narrower than its field.") }
        }
    }

    private static func binary(_ value: GesResolvedValue, _ parameters: GesSchemaParameters) -> Double {
        let number = value.isParameter ? (parameters.value(value.parameter)?.number ?? value.value) : value.value
        return Double(number.canonical) ?? 0
    }
}
