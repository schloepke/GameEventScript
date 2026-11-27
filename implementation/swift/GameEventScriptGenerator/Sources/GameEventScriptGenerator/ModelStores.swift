// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// A typed store of a handle type whose state is a model root, whose id is Text and that is not composite
// (specs/Schemas.md#model-stores): the type `<Name>Store` and the handle types of the other stores its state reaches,
// which it takes as optional dependencies, ordered by name.
struct GesModelStore {
    var name = ""
    var handle = GesModelHandle()
    var dependencies: [String] = []

    // The projections of the state that the generation declares, by name, each with the name its schema set looks it up by.
    var projections: [(name: String, lookup: String)] = []
}

extension GesModelPlanner {
    // The stores of a layout, in the order of their handle types, which is by name.
    static func stores(_ layout: GesModelLayout, _ topLevel: Set<String>) throws(GesGeneratorError) -> [GesModelStore] {
        var typed: Set<String> = []
        for handle in layout.handles
        where !handle.isComposite && handle.id?.isText == true && layout.roots.contains(handle.state)
            && layout.types.contains(where: { $0.name == handle.state && $0.kind == .schema })
        {
            typed.insert(handle.name)
        }
        var stores: [GesModelStore] = []
        for handle in layout.handles where typed.contains(handle.name) {
            let name = handle.name + "Store"
            if topLevel.contains(name) {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "The store type '\(name)' of @\(handle.name) takes the name of a generated type.")
            }
            // The store declares its entry type Entry, which would hide a state type of that name inside it.
            if handle.state == "Entry" {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "The store type '\(name)' declares the type Entry, which hides the state type :Entry.")
            }
            var visited: Set<String> = []
            var reached: Set<String> = []
            reach(layout, layout.type(handle.state), &visited, &reached)
            reached.remove(handle.name)
            let dependencies = reached.intersection(typed).sorted(by: GesSchemaLayoutBuilder.ordinal)
            for dependency in dependencies {
                let parameter = GesSchemaPlanner.camel(dependency)
                if parameter == "roots" || parameter == "files" || parameter == "resolver" {
                    throw GesGeneratorError(
                        GesGeneratorError.nameCollision,
                        "The store type '\(name)' takes the store of @\(dependency) as the parameter '\(parameter)', which its reading already names."
                    )
                }
            }
            stores.append(GesModelStore(name: name, handle: handle, dependencies: dependencies, projections: handle.projections.map { ($0, layout.type($0).lookup) }))
        }
        return stores
    }

    // The handle types a type reaches through its fields, List elements, union alternatives and Map keys, at any depth.
    private static func reach(_ layout: GesModelLayout, _ type: GesModelType, _ visited: inout Set<String>, _ handles: inout Set<String>) {
        if !visited.insert(type.name).inserted { return }
        for field in type.fields { reach(layout, field.value, &visited, &handles) }
    }

    private static func reach(_ layout: GesModelLayout, _ value: GesModelValue, _ visited: inout Set<String>, _ handles: inout Set<String>) {
        if value.kind == .handle || value.kind == .handleMap { handles.insert(value.handle) }
        if let element = value.element { reach(layout, element.value, &visited, &handles) }
        for alternative in value.alternatives { reach(layout, alternative.value, &visited, &handles) }
        if value.kind == .schema && !value.schema.isEmpty { reach(layout, layout.type(value.schema), &visited, &handles) }
    }
}
