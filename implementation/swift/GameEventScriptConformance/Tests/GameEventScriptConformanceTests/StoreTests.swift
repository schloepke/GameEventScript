// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import XCTest

@testable import GameEventScriptRuntime

/// Markdown Conformance covers store reading (api/stores.md); these tests keep the argument checks, the lookups of the
/// result and the path arithmetic that the corpus reaches only through its file table.
final class StoreTests: XCTestCase {
    private static let schemas = """
        schema :Hull as { armor: :Number integer between 0 and 100 }
        handle @Hull of :Hull as { id: :Text max 32, version: :ContentHash }
        schema :Composition as { name: :Text max 64, hull: @Hull~, escort: @Unit optional }
        handle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }
        schema :Contact as { label: :Text max 16 }
        handle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }
        handle @Slot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }

        """

    private struct Files: GesStoreFiles {
        let files: [(root: String, path: String, text: String)]

        init(_ files: (root: String, path: String, text: String)...) { self.files = files }

        func read(root: String, path: String) -> String? { files.first { $0.root == root && $0.path == path }?.text }
    }

    private func type(_ name: String) throws -> GesHandleType { try GesSchemaSet(sources: [GesSchemaSource(name: "units.gess", text: Self.schemas)]).handleType(name) }

    private func hulls() throws -> GesStore {
        try GesStore.read(
            type("Hull"),
            roots: [GesStoreRoot(name: "content", path: "hulls.gesd")],
            files: Files(("content", "hulls.gesd", ":Store(@Hull)\nlight: [armor: 20]\nheavy: [armor: 80]\n"))
        )
    }

    func testReadsEntriesWithPinnedDependenciesAndOrdersThemById() throws {
        let hulls = try hulls()
        let files = Files(
            ("content", "units.gesd", ":Store(@Unit)\nimport 'drones/all.gesd'\nscout: [name: 'Scout', hull: @Hull:light~]\n"),
            ("content", "drones/all.gesd", ":Store(@Unit)\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escort: @scout]\n  brakk: import 'brakk.gesd'\n}\n"),
            ("content", "drones/brakk.gesd", ":Schema(:Composition)\nname: 'Brakk', hull: @Hull:light~, escort: @gnabblo\n")
        )

        let store = try GesStore.read(type("Unit"), roots: [GesStoreRoot(name: "content", path: "units.gesd")], files: files, dependencies: [hulls])

        XCTAssertEqual(store.entries.map(\.id), ["drone.brakk", "drone.gnabblo", "scout"])
        XCTAssertEqual(store.module("drone").map(\.id), ["drone.brakk", "drone.gnabblo"])
        XCTAssertTrue(store.module("dron").isEmpty)
        XCTAssertNil(store["drone"])
        let brakk = try XCTUnwrap(store["drone.brakk"])
        XCTAssertEqual(brakk.origin.sourceName, "drones/all.gesd")
        XCTAssertEqual(brakk.origin.moduleName, "content")
        XCTAssertEqual(brakk.origin.line, 4)
        XCTAssertEqual(brakk.origin.column, 3)
        XCTAssertTrue(brakk.handle.hasVersion)
        let fields = try XCTUnwrap(brakk.value.asMap)
        XCTAssertEqual(fields.get("hull")?.handleValue, hulls["light"]?.handle)
        XCTAssertEqual(fields.get("escort")?.handleValue?.description, "@Unit:drone.gnabblo")
        XCTAssertNotEqual(store["drone.gnabblo"]?.handle, store["scout"]?.handle)
    }

    func testReportsEveryErrorWithASuggestion() throws {
        let files = Files(("content", "units.gesd", ":Store(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escort: @scuot]\nmodule drone {\n  a: [name: 'A', hull: @Hull:hevy~]\n}\n"))
        let unit = try type("Unit")
        let hulls = try hulls()

        XCTAssertThrowsError(try GesStore.read(unit, roots: [GesStoreRoot(name: "content", path: "units.gesd")], files: files, dependencies: [hulls])) { error in
            guard let error = error as? GesStoreError else { return XCTFail("\(error)") }
            XCTAssertEqual(error.diagnostics.map(\.code), [GesStoreError.unknownReference, GesStoreError.unknownReference])
            XCTAssertEqual(error.diagnostics[0].suggestion, "scout")
            XCTAssertEqual(error.diagnostics[0].location.line, 2)
            XCTAssertEqual(error.diagnostics[1].suggestion, "heavy")
        }
    }

    func testRejectsTypesRootsAndDependenciesItCannotRead() throws {
        let files = Files()
        let hulls = try hulls()
        let unit = try type("Unit")
        for (type, roots, dependencies) in [
            (try type("Slot"), [GesStoreRoot](), [GesStore]()),
            (try type("Target"), [], []),
            (unit, [GesStoreRoot(name: "a", path: "x"), GesStoreRoot(name: "a", path: "y")], []),
            (unit, [], [hulls, hulls]),
        ] {
            XCTAssertThrowsError(try GesStore.read(type, roots: roots, files: files, dependencies: dependencies)) { error in
                guard case GesApiError.invalidArgument = error else { return XCTFail("\(error)") }
            }
        }
        XCTAssertTrue(try GesStore.read(unit, roots: [], files: files).entries.isEmpty)
    }

    func testIndexesEntriesThroughProjectionsOfItsStateOnly() throws {
        let set = try GesSchemaSet(sources: [
            GesSchemaSource(
                name: "units.gess",
                text: Self.schemas + "projection :Named of :Composition as { name }\nprojection :Short of :Named as { name }\nprojection :Label of :Contact as { label }\n"
            )
        ])
        let store = try GesStore.read(
            set.handleType("Unit"),
            roots: [GesStoreRoot(name: "content", path: "units.gesd")],
            files: Files(("content", "units.gesd", ":Store(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~]\n")),
            dependencies: [try hulls()]
        )

        let named = try store.index(set.schema("Named"))
        let scout = try XCTUnwrap(store["scout"]).handle
        XCTAssertEqual(named.count, 1)
        XCTAssertEqual(named[0].projection, "Named")
        XCTAssertEqual(try named[0].withoutVersion().withProjection(""), scout.withoutVersion())
        XCTAssertTrue(named[0].hasVersion)
        XCTAssertNotEqual(named[0].version, scout.version)
        // A projection of a projection narrows the same state.
        XCTAssertEqual(try store.index(set.schema("Short")).count, 1)
        XCTAssertThrowsError(try store.index(set.schema("Composition")))
        XCTAssertThrowsError(try store.index(set.schema("Label")))
    }

    func testCombinesImportPathsWithinTheRoot() {
        XCTAssertEqual(GesStore.combine("a/b.gesd", "c.gesd"), "a/c.gesd")
        XCTAssertEqual(GesStore.combine("a/b.gesd", "/c.gesd"), "c.gesd")
        XCTAssertEqual(GesStore.combine("a/b.gesd", "../c.gesd"), "c.gesd")
        XCTAssertEqual(GesStore.combine("a/b.gesd", "./d//c.gesd"), "a/d/c.gesd")
        XCTAssertNil(GesStore.combine("a/b.gesd", "../../c.gesd"))
        XCTAssertNil(GesStore.combine("", ".."))
    }

    private static let fileSchemas = """
        schema :Composition as { name: :Text max 16 }
        handle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }
        schema :Source as { text: :Text }
        handle @Repository of :Source as { id: :Text max 16, version: :Number integer between 0 and 1000 }
        handle @UnitFiles of :Store(@Unit) composite @Repository as { part: :Text max 64 }
        handle @Other of :Source composite @Repository as { part: :Text max 64 }

        """

    private struct Resolver: GesHandleResolver {
        let answer: (GesHandleResolveCall) throws -> Void

        func resolve(_ call: GesHandleResolveCall) throws { try answer(call) }
    }

    private struct Offline: Error {}

    private func fileType(_ name: String) throws -> GesHandleType { try GesSchemaSet(sources: [GesSchemaSource(name: "files.gess", text: Self.fileSchemas)]).handleType(name) }

    private func readUnits(_ answer: @escaping (GesHandleResolveCall) throws -> Void) throws -> GesStore {
        try GesStore.read(fileType("Unit"), roots: [GesHandle(parsing: "@UnitFiles:content/units.gesd")], resolver: Resolver(answer: answer))
    }

    private func assertHandleError(_ code: String, _ answer: @escaping (GesHandleResolveCall) throws -> Void, line: UInt = #line) {
        XCTAssertThrowsError(try readUnits(answer), line: line) { error in
            XCTAssertEqual((error as? GesHandleError)?.code, code, "\(error)", line: line)
        }
    }

    func testReadsThroughHandlesAndAnswersTheStatesOfItsEntries() throws {
        var requested: [String] = []
        let store = try readUnits { call in
            requested.append(call.handle.description)
            if call.handle.part.asText == "units.gesd" {
                try call.setDocument(":Store(@Unit)\nimport 'more.gesd'\nscout: [name: 'Scout']\n", version: .integer(9))
            } else {
                try call.setDocument(":Store(@Unit)\none: [name: 'One']\n")
            }
        }

        XCTAssertEqual(requested, ["@UnitFiles:content/units.gesd", "@UnitFiles:content~9/more.gesd"])
        XCTAssertEqual(store.sources.map(\.description), ["@UnitFiles:content~9/units.gesd"])
        XCTAssertEqual(store["one"]?.origin.moduleName, "@UnitFiles:content/units.gesd")

        let unit = try fileType("Unit")
        let call = GesHandleResolveCall()
        let scout = try XCTUnwrap(store["scout"])
        call.begin(try GesHandle(typeName: "Unit", id: .text("scout")), unit, nil, 0)
        try store.resolve(call)
        XCTAssertEqual(call.answer, .state)
        XCTAssertEqual(call.record, scout.value)
        XCTAssertEqual(call.version, scout.handle.version)
        call.begin(try GesHandle(typeName: "Unit", id: .text("ghost")), unit, nil, 0)
        try store.resolve(call)
        XCTAssertEqual(call.answer, .missing)
        call.end()
    }

    func testReportsAStateAnswerAsTheWrongKindOfFile() {
        XCTAssertThrowsError(try readUnits { try $0.setState(.nothing) }) { error in
            XCTAssertEqual((error as? GesStoreError)?.diagnostics.first?.code, GesStoreError.importKind)
        }
    }

    func testFailsOnMissingSeveralOrInvalidAnswersAndPassesResolverErrorsOn() {
        assertHandleError(GesDiagnosticCodes.runtimeHandleResolveFailed) { _ in }
        assertHandleError(GesDiagnosticCodes.runtimeHandleResolveFailed) { call in
            try call.setMissing()
            try call.setMissing()
        }
        assertHandleError(GesDiagnosticCodes.runtimeHandleResolveFailed) { try $0.setDocument(":Store(@Unit)\n", version: .bytes([1])) }
        XCTAssertThrowsError(try readUnits { _ in throw Offline() }) { error in XCTAssertTrue(error is Offline, "\(error)") }
    }

    func testRejectsRootsThatAreNoStoreFiles() throws {
        let resolver = Resolver { try $0.setMissing() }
        let unit = try fileType("Unit")
        let content = try GesHandle(parsing: "@UnitFiles:content/units.gesd")
        for (type, roots) in [
            (unit, [try GesHandle(parsing: "@Repository:content")]),
            (unit, [try GesHandle(parsing: "@Other:content/units.gesd")]),
            (unit, [content, try GesHandle(parsing: "@UnitFiles:content/units.gesd")]),
            (try fileType("Other"), []),
        ] {
            XCTAssertThrowsError(try GesStore.read(type, roots: roots, resolver: resolver)) { error in
                guard case GesApiError.invalidArgument = error else { return XCTFail("\(error)") }
            }
        }
        XCTAssertTrue(try GesStore.read(unit, roots: [], resolver: resolver).sources.isEmpty)
    }

    func testLeavesStoreFileTypesOutOfScripts() throws {
        let types = try GesSchemaSet(sources: [GesSchemaSource(name: "files.gess", text: Self.fileSchemas)]).handleTypes
        _ = try GesProgramBuilder().withHandleTypes(types).addScript("on Seen(unit as @Unit) { }").compile()
        XCTAssertThrowsError(try GesProgramBuilder().withHandleTypes(types).addScript("on Seen(file as @UnitFiles) { }").compile())
    }
}
