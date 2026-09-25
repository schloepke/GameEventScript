// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesLiteralNode {
    let type: String
    let labels: [String]
    let arguments: [GesValue]
    var constructor: GameEventScriptBinding?
}

final class GesLiteralEvaluation {
    struct Operation {
        let node: GesLiteralNode
        let inputs: [Int]
    }

    private var operations: [Operation] = [], results: [GesValue] = []
    private var next = 0
    private unowned let state: GesVmState
    private let context: GameEventScriptContext
    private let destination: Int
    private let nodes: [GesLiteralNode]

    init(_ root: GesValue, nodes: [GesLiteralNode], state: GesVmState, context: GameEventScriptContext, destination: Int) {
        self.nodes = nodes
        self.state = state
        self.context = context
        self.destination = destination
        _ = add(root)
    }

    private func add(_ value: GesValue) -> Int {
        let node: GesLiteralNode
        if value.customTypeName == "@literal", let id = value.asMap?.get("id")?.integerValue, id >= 0, id < nodes.count {
            node = nodes[Int(id)]
        } else if let list = value.listValue {
            node = .init(type: "@list", labels: [], arguments: list)
        } else if value.kind == .map, let entries = value.mapEntries {
            node = .init(type: "@map", labels: entries.map(\.key), arguments: entries.map(\.value))
        } else {
            node = .init(type: "@value", labels: [], arguments: [value])
        }
        let inputs = node.type == "@value" ? [] : node.arguments.map(add)
        let index = operations.count
        operations.append(.init(node: node, inputs: inputs))
        results.append(.nothing)
        return index
    }

    func resume(_ value: GesValue) throws {
        results[next] = value
        next += 1
        try run()
    }

    func run() throws {
        while next < operations.count && !context.budget.isExhausted {
            let operation = operations[next]
            let node = operation.node
            let args = operation.inputs.map { results[$0] }
            if let constructor = node.constructor {
                state.clearStage()
                var unnamed = 0
                for index in constructor.argumentNames {
                    let name = state.text(index)
                    let match = (name == "_" ? unnamed : 0)..<node.labels.count
                    if let position = match.first(where: { node.labels[$0] == name }) {
                        state.stage(args[position])
                        if name == "_" { unnamed = position + 1 }
                    } else {
                        state.stage(.nothing)
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
                    result = try GesDataConstruction.create(node.type, node.labels, args, context)
                }
            }
            results[next] = result
            next += 1
        }
        if next == operations.count { state.set(destination, results.last!) }
    }
}
