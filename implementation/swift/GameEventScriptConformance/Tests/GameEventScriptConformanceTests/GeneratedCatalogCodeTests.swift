// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/catalogs.gess: typed catalogs that read their entries into
/// models, look them up by id and module, take the catalogs they depend on and resolve their handles like GesCatalog.
final class GeneratedCatalogCodeTests: XCTestCase {
    private struct Files: GesCatalogFiles {
        let files: [(root: String, path: String, text: String)]

        init(_ files: (String, String, String)...) { self.files = files.map { (root: $0.0, path: $0.1, text: $0.2) } }

        func read(root: String, path: String) -> String? { files.first { $0.root == root && $0.path == path }?.text }
    }

    private func hulls() throws -> CatalogModel.HullCatalog {
        try CatalogModel.HullCatalog.read(
            roots: [GesCatalogRoot(name: "content", path: "hulls.gesd")],
            files: Files(("content", "hulls.gesd", ":Catalog(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy', armor: 80]\n"))
        )
    }

    private let units = Files(
        (
            "content", "units.gesd",
            ":Catalog(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escorts: [], speed: 120m/s]\nmodule drone {\n"
                + "  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escorts: [@scout], speed: 40m/s]\n"
                + "  brakk: [name: 'Brakk', hull: @Hull:light~, escorts: [@scout, @gnabblo], speed: 60m/s]\n}\n"
        )
    )

    func testReadsEntriesIntoModelsAndLooksThemUp() throws {
        let hulls = try hulls()
        let catalog = try CatalogModel.UnitCatalog.read(roots: [GesCatalogRoot(name: "content", path: "units.gesd")], files: units, hull: hulls)

        XCTAssertEqual(catalog.entries.map(\.id), ["drone.brakk", "drone.gnabblo", "scout"])
        XCTAssertEqual(catalog.entries.map(\.handle), catalog.index)
        XCTAssertEqual(catalog.module("drone").map(\.id), ["drone.brakk", "drone.gnabblo"])
        XCTAssertTrue(catalog.module("dron").isEmpty)
        XCTAssertNil(catalog["drone"])
        XCTAssertTrue(catalog.sources.isEmpty)
        let brakk = try XCTUnwrap(catalog["drone.brakk"])
        XCTAssertEqual(brakk.handle, CatalogModel.UnitHandle(uncheckedId: "drone.brakk"))
        XCTAssertEqual(brakk.value.name, "Brakk")
        XCTAssertEqual(brakk.value.speed, 60)
        XCTAssertEqual(brakk.value.hull.withoutVersion(), try XCTUnwrap(hulls["light"]).handle)
        XCTAssertNotNil(brakk.value.hull.version)
        XCTAssertEqual(brakk.value.escorts, [CatalogModel.UnitHandle(uncheckedId: "scout"), CatalogModel.UnitHandle(uncheckedId: "drone.gnabblo")])
        XCTAssertEqual(brakk.origin.line, 5)
        XCTAssertEqual(hulls["heavy"]?.value.armor, 80)
        XCTAssertEqual(catalog.catalog.type.name, "Unit")
    }

    func testNarrowsEntriesToProjectionsAndIndexesTheirVersions() throws {
        let files = Files(
            (
                "content", "units.gesd",
                ":Catalog(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escorts: [], speed: 120m/s, look: [icon: 'eye']]\n"
                    + "module drone {\n  brakk: [name: 'Brakk', hull: @Hull:light~, escorts: [@scout], speed: 60m/s]\n}\n"
            )
        )
        let catalog = try CatalogModel.UnitCatalog.read(roots: [GesCatalogRoot(name: "content", path: "units.gesd")], files: files, hull: try hulls())

        var scout = try XCTUnwrap(catalog["scout"]).value
        let display = CatalogModel.UnitDisplay(scout)
        XCTAssertEqual(display, CatalogModel.UnitDisplay(name: "Scout", hull: scout.hull, icon: "eye"))
        scout.look?.icon = "ear"
        XCTAssertEqual(display.icon, "eye")
        // An absent optional Record on the path leaves the taken field absent.
        XCTAssertNil(CatalogModel.UnitDisplay(try XCTUnwrap(catalog["drone.brakk"]).value).icon)
        XCTAssertEqual(CatalogModel.UnitLabel(display).name, "Scout")
        XCTAssertEqual(CatalogModel.UnitLabel(display), CatalogModel.UnitLabel(scout))

        let displays = catalog.index(CatalogModel.UnitDisplay.self)
        let labels = catalog.index(CatalogModel.UnitLabel.self)
        XCTAssertEqual(displays.map { $0.withoutVersion() }, catalog.index)
        XCTAssertTrue(displays.allSatisfy { $0.version != nil })
        // Each projection has its own content hash.
        XCTAssertNotEqual(displays[1].version, labels[1].version)
        XCTAssertEqual(catalog.module("drone", CatalogModel.UnitLabel.self), [labels[0]])
        XCTAssertTrue(catalog.module("dron", CatalogModel.UnitDisplay.self).isEmpty)
    }

    func testRejectsPinsWithoutTheCatalogItDependsOn() {
        XCTAssertThrowsError(try CatalogModel.UnitCatalog.read(roots: [GesCatalogRoot(name: "content", path: "units.gesd")], files: units)) { error in
            guard let error = error as? GesCatalogError else { return XCTFail("\(error)") }
            XCTAssertFalse(error.diagnostics.isEmpty)
            XCTAssertTrue(error.diagnostics.allSatisfy { $0.code == GesCatalogError.invalidPin })
        }
    }
}
