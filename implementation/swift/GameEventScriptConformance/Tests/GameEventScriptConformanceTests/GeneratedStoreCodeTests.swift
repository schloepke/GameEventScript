// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/stores.gess: typed stores that read their entries into
/// models, look them up by id and module, take the stores they depend on and resolve their handles like GesStore.
final class GeneratedStoreCodeTests: XCTestCase {
    private struct Files: GesStoreFiles {
        let files: [(root: String, path: String, text: String)]

        init(_ files: (String, String, String)...) { self.files = files.map { (root: $0.0, path: $0.1, text: $0.2) } }

        func read(root: String, path: String) -> String? { files.first { $0.root == root && $0.path == path }?.text }
    }

    private func hulls() throws -> StoreModel.HullStore {
        try StoreModel.HullStore.read(
            roots: [GesStoreRoot(name: "content", path: "hulls.gesd")],
            files: Files(("content", "hulls.gesd", ":Store(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy', armor: 80]\n"))
        )
    }

    private let units = Files(
        (
            "content", "units.gesd",
            ":Store(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escorts: [], speed: 120m/s]\nmodule drone {\n"
                + "  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escorts: [@scout], speed: 40m/s]\n"
                + "  brakk: [name: 'Brakk', hull: @Hull:light~, escorts: [@scout, @gnabblo], speed: 60m/s]\n}\n"
        )
    )

    func testReadsEntriesIntoModelsAndLooksThemUp() throws {
        let hulls = try hulls()
        let store = try StoreModel.UnitStore.read(roots: [GesStoreRoot(name: "content", path: "units.gesd")], files: units, hull: hulls)

        XCTAssertEqual(store.entries.map(\.id), ["drone.brakk", "drone.gnabblo", "scout"])
        XCTAssertEqual(store.entries.map(\.handle), store.index)
        XCTAssertEqual(store.module("drone").map(\.id), ["drone.brakk", "drone.gnabblo"])
        XCTAssertTrue(store.module("dron").isEmpty)
        XCTAssertNil(store["drone"])
        XCTAssertTrue(store.sources.isEmpty)
        let brakk = try XCTUnwrap(store["drone.brakk"])
        XCTAssertEqual(brakk.handle, StoreModel.UnitHandle(uncheckedId: "drone.brakk"))
        XCTAssertEqual(brakk.value.name, "Brakk")
        XCTAssertEqual(brakk.value.speed, 60)
        XCTAssertEqual(brakk.value.hull.withoutVersion(), try XCTUnwrap(hulls["light"]).handle)
        XCTAssertNotNil(brakk.value.hull.version)
        XCTAssertEqual(brakk.value.escorts, [StoreModel.UnitHandle(uncheckedId: "scout"), StoreModel.UnitHandle(uncheckedId: "drone.gnabblo")])
        XCTAssertEqual(brakk.origin.line, 5)
        XCTAssertEqual(hulls["heavy"]?.value.armor, 80)
        XCTAssertEqual(store.store.type.name, "Unit")
    }

    func testNarrowsEntriesToProjectionsAndIndexesTheirVersions() throws {
        let files = Files(
            (
                "content", "units.gesd",
                ":Store(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escorts: [], speed: 120m/s, look: [icon: 'eye']]\n"
                    + "module drone {\n  brakk: [name: 'Brakk', hull: @Hull:light~, escorts: [@scout], speed: 60m/s]\n}\n"
            )
        )
        let store = try StoreModel.UnitStore.read(roots: [GesStoreRoot(name: "content", path: "units.gesd")], files: files, hull: try hulls())

        var scout = try XCTUnwrap(store["scout"]).value
        let display = StoreModel.UnitDisplay(scout)
        XCTAssertEqual(display, StoreModel.UnitDisplay(name: "Scout", hull: scout.hull, icon: "eye"))
        scout.look?.icon = "ear"
        XCTAssertEqual(display.icon, "eye")
        // An absent optional Record on the path leaves the taken field absent.
        XCTAssertNil(StoreModel.UnitDisplay(try XCTUnwrap(store["drone.brakk"]).value).icon)
        XCTAssertEqual(StoreModel.UnitLabel(display).name, "Scout")
        XCTAssertEqual(StoreModel.UnitLabel(display), StoreModel.UnitLabel(scout))

        let displays = store.index(StoreModel.UnitDisplay.self)
        let labels = store.index(StoreModel.UnitLabel.self)
        XCTAssertEqual(displays.map { $0.withoutVersion() }, store.index)
        XCTAssertTrue(displays.allSatisfy { $0.version != nil })
        // Each projection has its own content hash.
        XCTAssertNotEqual(displays[1].version, labels[1].version)
        XCTAssertEqual(store.module("drone", StoreModel.UnitLabel.self), [labels[0]])
        XCTAssertTrue(store.module("dron", StoreModel.UnitDisplay.self).isEmpty)
    }

    func testRejectsPinsWithoutTheStoreItDependsOn() {
        XCTAssertThrowsError(try StoreModel.UnitStore.read(roots: [GesStoreRoot(name: "content", path: "units.gesd")], files: units)) { error in
            guard let error = error as? GesStoreError else { return XCTFail("\(error)") }
            XCTAssertFalse(error.diagnostics.isEmpty)
            XCTAssertTrue(error.diagnostics.allSatisfy { $0.code == GesStoreError.invalidPin })
        }
    }
}
