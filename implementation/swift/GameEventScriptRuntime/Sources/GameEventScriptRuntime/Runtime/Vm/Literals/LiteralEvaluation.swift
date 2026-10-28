// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesLiteralNode {
    let type: String
    let labels: [String]
    let arguments: [GesValue]
    var constructor: GesBindingSegment.Entry?
}

final class GesLiteralEvaluation {
    struct Operation {
        let node: GesLiteralNode
        let inputs: [Int]
    }

    // Data-only evaluation has no Host and uses the default host limit for its Range item total.
    private static let dataRangeItems = GesRuntimeLimits().maxRangeItems
    private var operations: [Operation] = [], results: [GesValue] = []
    private var next = 0
    private var materializedRangeItems: Int64 = 0
    // Both are absent for data-only evaluation, which never contains constructor nodes.
    private unowned let state: GesVirtualMachine?
    private let context: GesContext?
    private let destination: Int
    private let nodes: [GesLiteralNode]

    init(_ root: GesValue, nodes: [GesLiteralNode], state: GesVirtualMachine?, context: GesContext?, destination: Int) {
        self.nodes = nodes
        self.state = state
        self.context = context
        self.destination = destination
        _ = add(root)
    }

    // Throws GesLiteralLimitError when a fixed literal limit or the default maxRangeItems total is exceeded.
    static func evaluate(_ root: GesValue, nodes: [GesLiteralNode]) throws -> GesValue {
        let evaluation = GesLiteralEvaluation(root, nodes: nodes, state: nil, context: nil, destination: 0)
        try evaluation.run()
        return evaluation.results.last!
    }

    private func add(_ value: GesValue) -> Int {
        let node: GesLiteralNode
        if value.customTypeName == "@literal", let id = value.asMap?.get("id")?.integerValue, id >= 0, id < nodes.count {
            node = nodes[Int(id)]
        } else if let list = value.listValue {
            node = .init(type: "@list", labels: [], arguments: list)
        } else if value.kind == .map, let map = value.asMap {
            node = .init(type: "@map", labels: map.keyStorage, arguments: map.valueStorage)
        } else {
            node = .init(type: "@value", labels: [], arguments: [value])
        }
        let inputs = node.type == "@value" ? [] : node.arguments.map(add)
        let index = operations.count
        operations.append(.init(node: node, inputs: inputs))
        results.append(.nothing)
        return index
    }

    // One parse materializes at most maxRangeItems Range items into Lists in total, not only per Range. Data-only
    // evaluation throws GesLiteralLimitError; a Host exhausts the handler.
    private func withinRangeItems(_ count: Int64) throws -> Bool {
        let limit = context?.budget.limits.maxRangeItems ?? Self.dataRangeItems
        materializedRangeItems += count
        if limit <= 0 || materializedRangeItems <= Int64(limit) { return true }
        guard let context else { throw GesLiteralLimitError(limitName: "MaxRangeItems", limit: limit) }
        context.budget.exhaust("MaxRangeItems", limit)
        return false
    }

    func resume(_ value: GesValue) throws {
        results[next] = value
        next += 1
        try run()
    }

    func run() throws {
        while next < operations.count && context?.budget.isExhausted != true {
            let operation = operations[next]
            let node = operation.node
            let args = operation.inputs.map { results[$0] }
            if let constructor = node.constructor, let state {
                state.clearStage()
                var unnamed = 0
                for index in constructor.argumentNames {
                    let name = state.text(index)
                    let match = (name == "_" ? unnamed : 0)..<node.labels.count
                    if let position = match.first(where: { node.labels[$0] == name }) {
                        state.stageValue(args[position])
                        if name == "_" { unnamed = position + 1 }
                    } else {
                        state.stageNothing()
                    }
                }
                if state.processing { state.call(Int(constructor.entryAddress), destination: destination, literal: self) }
                return
            }
            let result: GesValue
            switch node.type {
            case "@value": result = node.arguments[0]
            case "@list": result = .list(args)
            case "@map": result = .map(zip(node.labels, args).map { .init(key: $0.0, value: $0.1) })
            default:
                if node.type.hasPrefix("@message:") {
                    result = .message(try .init(name: String(node.type.dropFirst(9)), arguments: try zip(node.labels, args).map { try .init(name: $0.0, value: $0.1) }))
                } else {
                    if node.type == "list", args.count == 1, let count = args[0].integerRangeValue?.count ?? args[0].floatRangeValue?.count, try !withinRangeItems(count) { return }
                    result = try GesDataConstruction.create(node.type, node.labels, args, context)
                }
            }
            results[next] = result
            next += 1
        }
        if next == operations.count { state?.setValue(destination, results.last!) }
    }
}
