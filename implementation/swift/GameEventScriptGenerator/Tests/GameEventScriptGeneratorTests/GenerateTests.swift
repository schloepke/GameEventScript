// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptGenerator
import GameEventScriptRuntime
import XCTest

/// Checks the generation rules that the corpus harness cannot reach: names, namespaces and the shape of types.
final class GenerateTests: XCTestCase {
    private func generate(_ source: String, _ root: String, _ language: GesSchemaLanguage, _ namespace: String = "") throws -> String {
        try GesSchemaGenerator.generate(try GesSchemaSet(parsing: source), roots: [root], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: language, namespace: namespace))
    }

    func testTheValuesOfRecordDefaultsTakeNoTypeName() throws {
        // The values of the record defaults of :S are held by SDefaults, which the schema of that name would also be.
        let set = try GesSchemaSet(parsing: "schema :C as { a: :Number default 1 }\nschema :SDefaults as { b: :Boolean }\nschema :S as {\n  c: :C default [:]\n  d: :SDefaults\n}\n")
        for language in [GesSchemaLanguage.csharp, .swift] {
            XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["S"], options: GesSchemaGenerateOptions(language: language, values: true)) }, "generate.nameCollision")
            XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["S"], options: GesSchemaGenerateOptions(language: language, bindings: true)) }, "generate.nameCollision")
            XCTAssertTrue(try GesSchemaGenerator.generateModels(set, roots: ["S"], options: GesSchemaGenerateOptions(language: language)).contains("SDefaults"))
        }
    }

    func testModelsNeedTheirOwnNamespaceAndWireName() throws {
        let set = try GesSchemaSet(parsing: "wire model packed schema :Cell as { a: :Boolean }\nwire schema :CellWire as { b: :Boolean }\nwire model schema :Grid as { c: :List(:Cell) max 4 }\n")
        let roots = ["Cell", "Grid"]

        func wire(_ language: GesSchemaLanguage, _ namespace: String, _ models: String, _ more: [String] = []) -> String {
            code { try GesSchemaGenerator.generate(set, roots: roots + more, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: language, namespace: namespace, models: models)) }
        }

        for language in [GesSchemaLanguage.csharp, .swift] {
            // Wire records and models of one schema share its name, so they need different namespaces.
            XCTAssertEqual(wire(language, "", ""), "generate.nameCollision")
            XCTAssertEqual(wire(language, "Game.Shared", "Game.Shared"), "generate.nameCollision")
            // The wire members of the shared packed root Cell take the name CellWire.
            XCTAssertEqual(wire(language, "Wire", "", ["CellWire"]), "generate.nameCollision")
            XCTAssertEqual(wire(language, "Wire", ""), "")
        }
        // Swift namespace components are enums: wire code extends those it shares with the models' namespace.
        let extended = try GesSchemaGenerator.generate(set, roots: roots, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Wire", models: "Game.Model"))
        XCTAssertTrue(extended.contains("extension Game {\n    /// The namespace `Wire` of generated schema types.\n    public enum Wire {"))
        XCTAssertEqual(wire(.csharp, "Game.Wire", "Game.Model"), "")
        let panels = try GesSchemaGenerator.generateModels(set, roots: ["Grid"], options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Panels", extendNamespace: "Game"))
        XCTAssertTrue(panels.contains("extension Game {\n    /// The namespace `Panels` of generated model types.\n    public enum Panels {"))
        // A namespace that starts in the global one of the other side must not take the name of a type there.
        XCTAssertEqual(wire(.swift, "Grid.Wire", ""), "generate.nameCollision")
        XCTAssertEqual(wire(.csharp, "", "Grid.Model"), "generate.nameCollision")
        // Nor where one namespace continues the other: models in Game hold Grid, so wire code cannot live in Game.Grid.
        XCTAssertEqual(wire(.swift, "Game.Grid", "Game"), "generate.nameCollision")
        // Wire code in the whole namespace that the models' one begins with extends it with one brace for every one it opens.
        let whole = try GesSchemaGenerator.generate(set, roots: roots, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, namespace: "Game", models: "Game.Model"))
        XCTAssertTrue(whole.contains("\nextension Game {\n"))
        XCTAssertEqual(whole.filter { $0 == "{" }.count, whole.filter { $0 == "}" }.count)
        // C# merges namespaces, so extending them is a usage error there.
        XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["Grid"], options: GesSchemaGenerateOptions(language: .csharp, namespace: "Game.Panels", extendNamespace: "Game")) }, "generate.usage")
        // A one-part namespace can be extended as a whole, and only a leading part of the namespace can be extended.
        let single = try GesSchemaGenerator.generateModels(set, roots: ["Grid"], options: GesSchemaGenerateOptions(language: .swift, namespace: "Game", extendNamespace: "Game"))
        XCTAssertTrue(single.contains("\nextension Game {\n"))
        XCTAssertEqual(single.filter { $0 == "{" }.count, single.filter { $0 == "}" }.count)
        XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["Grid"], options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Panels", extendNamespace: "Panels")) }, "generate.usage")
        // A type of any model group counts, since the models' namespace holds them all: models in Game hold Report, so wire code
        // cannot live in Game.Report, whether Report shares the wire root's group or not.
        for source in ["wire model schema :Grid as { a: :Boolean }\nmodel schema :Report as { b: :Boolean }\n", "wire model schema :Grid as { a: :Boolean }\nmodel(hud) schema :Report as { b: :Boolean }\n"] {
            let withReport = try GesSchemaSet(parsing: source)
            XCTAssertEqual(
                code { try GesSchemaGenerator.generate(withReport, roots: ["Grid"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Report", models: "Game")) },
                "generate.nameCollision"
            )
        }
        // The wire namespace holds the `<Name>Wire` members of packed roots, which a models' namespace continuing it cannot name.
        XCTAssertEqual(wire(.swift, "Game", "Game.CellWire"), "generate.nameCollision")
        // Swift wire code names model types through the models' namespace, which no wire type, nested type or later wire
        // namespace component may hide; C# qualifies with global:: and is not affected.
        let hiding = try GesSchemaSet(parsing: "wire(w) model(m) packed schema :Cell as { level: :Number integer between 0 and 9 }\nwire(w) schema :Model as { cell: :Cell }\nwire(w) schema :Holder as { cell: :Cell\n  model: :Flags(#a, #b) }\n")
        for (space, modelSpace, hidingRoots) in [("Wire", "Model", ["Cell", "Model"]), ("Wire.Model", "Model", ["Cell"]), ("Wire", "ModelFlags", ["Cell", "Holder"])] {
            XCTAssertEqual(
                code { try GesSchemaGenerator.generate(hiding, roots: hidingRoots, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, namespace: space, models: modelSpace)) },
                "generate.nameCollision",
                space + " " + modelSpace
            )
            let csharp = try GesSchemaGenerator.generate(hiding, roots: hidingRoots, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .csharp, namespace: space, models: modelSpace))
            XCTAssertTrue(csharp.contains("global::\(modelSpace).Cell"))
        }
        // Wire generation also extends only a leading part, and the larger of that part and the one shared with the models.
        XCTAssertEqual(code { try GesSchemaGenerator.generate(set, roots: roots, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Wire", extendNamespace: "Wire")) }, "generate.usage")
        let deeper = try GesSchemaGenerator.generate(set, roots: roots, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Wire.Packed", models: "Game.Model", extendNamespace: "Game.Wire"))
        XCTAssertTrue(deeper.contains("\nextension Game.Wire {\n"))
        // Models that read documents extend the namespace too.
        let documents = try GesSchemaGenerator.generateModels(set, roots: ["Grid"], options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Panels", documents: true, extendNamespace: "Game"))
        XCTAssertTrue(documents.contains("extension Game {\n    /// The namespace `Panels` of generated model types.\n    public enum Panels {"))
        // An invalid namespace reports itself first.
        XCTAssertEqual(wire(.swift, ".Wire", ".Wire"), "generate.invalidNamespace")
    }

    func testTheDocumentTypeTakesNoGeneratedName() throws {
        // In a namespace the reading type is Document, which a model root of that name would also be.
        let set = try GesSchemaSet(parsing: "schema :Document as { a: :Boolean }\n")
        XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["Document"], options: GesSchemaGenerateOptions(language: .swift, namespace: "Units", documents: true)) }, "generate.nameCollision")
        XCTAssertTrue(try GesSchemaGenerator.generateModels(set, roots: ["Document"], options: GesSchemaGenerateOptions(language: .swift, documents: true)).contains("enum ModelDocument"))
    }

    private func code<Result>(_ body: () throws -> Result) -> String {
        do {
            _ = try body()
            return ""
        } catch let error as GesGeneratorError {
            return error.code
        } catch {
            return "\(error)"
        }
    }

    func testNamesFollowTheLanguage() throws {
        let source = "schema :S as {\n  default: :Boolean\n  state: :Tag(#idle, #in)\n}\n"
        let csharp = try generate(source, "S", .csharp, "Game.Net")
        XCTAssertTrue(csharp.contains("namespace Game.Net\n{"))
        XCTAssertTrue(csharp.contains("public bool Default;"))
        XCTAssertTrue(csharp.contains("public StateTag State;"))
        XCTAssertTrue(csharp.contains("            In,\n"))
        let swift = try generate(source, "S", .swift, "Game.Net")
        XCTAssertTrue(swift.contains("public enum Game {\n    /// The namespace `Net` of generated schema types.\n    public enum Net {"))
        XCTAssertTrue(swift.contains("public var `default`: Bool = false"))
        XCTAssertTrue(swift.contains("case `in`"))
        XCTAssertTrue(swift.contains("public struct S: Sendable {"))
    }

    func testCollidingNamesAreRejected() throws {
        XCTAssertEqual(code { try generate("schema :S as {\n  write: :Boolean\n}\n", "S", .csharp) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("schema :S as {\n  write: :Boolean\n}\n", "S", .swift) }, "generate.nameCollision")
        XCTAssertTrue(try generate("schema :S as {\n  s: :Boolean\n}\n", "S", .csharp).contains("public bool SValue;"))
        XCTAssertEqual(code { try generate("schema :S as {\n  s: :Boolean\n  sValue: :Boolean\n}\n", "S", .csharp) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("schema :S as {\n  quantize: :Boolean\n}\n", "S", .swift) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("schema :S as {\n  state: :Tag(#a)\n  stateTag: :Boolean\n}\n", "S", .csharp) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("schema :S as {\n  heat: :Boolean optional\n  hasHeat: :Boolean\n}\n", "S", .swift) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("schema :S as {\n  mode: :Flags(#rawValue, #other)\n}\n", "S", .swift) }, "generate.nameCollision")
        let keywords = try GesSchemaSet(parsing: "model schema :S as {\n  default: :Text\n  in: :Boolean\n  var: :Boolean\n}\n")
        let model = try GesSchemaGenerator.generateModels(
            keywords,
            roots: GesSchemaGenerator.modelRoots(keywords, group: "default"),
            options: GesSchemaGenerateOptions(language: .swift, values: true, bindings: true)
        )
        XCTAssertTrue(model.contains("init(default: String, in: Bool, `var`: Bool)"))
        XCTAssertTrue(model.contains("S(default: field0, in: field1, var: field2)"))
        XCTAssertTrue(try generate("schema :S as {\n  mode: :Flags(#rawValue, #other)\n}\n", "S", .csharp).contains("RawValue = 1UL << 0,"))
        XCTAssertTrue(try generate("schema :S as {\n  s: :Boolean\n}\n", "S", .swift).contains("public var sValue: Bool = false"))
    }

    func testInvalidNamespacesAreRejected() {
        XCTAssertEqual(code { try generate("schema :S as {\n  a: :Boolean\n}\n", "S", .csharp, "Game..Net") }, "generate.invalidNamespace")
        XCTAssertEqual(code { try generate("schema :S as {\n  a: :Boolean\n}\n", "S", .csharp, "class") }, "generate.invalidNamespace")
        XCTAssertEqual(code { try generate("schema :S as {\n  a: :Boolean\n}\n", "S", .swift, "1Wire") }, "generate.invalidNamespace")
    }

    func testStorageMakesClassesAndMessagesGetTagsAndSets() throws {
        let source =
            "schema :Cell as {\n  x: :Boolean\n}\nschema :S as {\n  name: :Text max 4\n  cells: :List(:Cell) max 2\n  words: :List(:Text max 2) max 2\n  heat: :Number optional\n}\n"
            + "message Hit(target: :Number integer between 0 and 9) tags (#critical)\nmessage Miss()\nmessages :Events as (Hit, Miss)\n"
        let set = try GesSchemaSet(parsing: source)
        let csharp = try GesSchemaGenerator.generate(set, roots: ["S", "Events"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions())
        XCTAssertTrue(csharp.contains("public sealed partial class S"))
        XCTAssertTrue(csharp.contains("public partial struct Cell"))
        XCTAssertTrue(csharp.contains("public List<Cell> Cells = new List<Cell>();"))
        XCTAssertTrue(csharp.contains("public sealed partial class WordsItem"))
        XCTAssertTrue(csharp.contains("public ReadOnlySpan<byte> NameUtf8"))
        XCTAssertTrue(csharp.contains("public double? HeatOptional"))
        XCTAssertTrue(csharp.contains("public bool Critical;"))
        XCTAssertTrue(csharp.contains("public partial struct Events"))
        XCTAssertTrue(csharp.contains("public Member Selected;"))
        let swift = try GesSchemaGenerator.generate(set, roots: ["S", "Events"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift))
        XCTAssertTrue(swift.contains("public final class S {"))
        XCTAssertTrue(swift.contains("public var cells: [Cell] = []"))
        XCTAssertTrue(swift.contains("public var nameUTF8: [UInt8] = []"))
        XCTAssertTrue(swift.contains("public var heatOptional: Double? {"))
        XCTAssertTrue(swift.contains("public var critical = false"))
        XCTAssertTrue(swift.contains("public var selected: Member = .hit"))
        XCTAssertEqual(code { try generate("schema :S as {\n  name: :Text max 4\n  nameUtf8: :Boolean\n}\n", "S", .csharp) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("message M(critical: :Boolean) tags (#critical)\n", "M", .swift) }, "generate.nameCollision")
    }

    func testSchemasGetDeltasWithKeyedLists() throws {
        let source =
            "schema :Item as {\n  id: :Number integer between 0 and 9 key\n}\nschema :S as {\n  x: :Number integer between 0 and 9 delta within 2\n  name: :Text max 4\n}\n"
            + "schema :K as {\n  items: :List(:Item) max 2\n}\nmessage M(a: :Boolean)\n"
        let set = try GesSchemaSet(parsing: source)
        let csharp = try GesSchemaGenerator.generate(set, roots: ["S", "K", "M"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions())
        XCTAssertTrue(csharp.contains("public int WriteDelta(S baseline, Span<byte> buffer)"))
        XCTAssertTrue(csharp.contains("public void CopyFrom(S other)"))
        XCTAssertTrue(csharp.contains("public bool WireEquals(S other)"))
        XCTAssertTrue(csharp.contains("GesWireRules.WriteDeltaRange(ref writer"))
        XCTAssertTrue(csharp.contains("public int WriteDelta(K baseline, Span<byte> buffer)"))
        XCTAssertTrue(csharp.contains("private bool __WriteKeyed0(K baseline, ref GesBitWriter writer, string[] paths, int at)"))
        XCTAssertTrue(csharp.contains("internal readonly ulong __KeyOrder(string[] paths, int at) => __Wire0(Id, paths, at);"))
        XCTAssertTrue(csharp.contains("var order = global::Item.__ReadKeyOrder(ref reader, paths, at + 2);"))
        XCTAssertFalse(csharp.contains("WriteDelta(in M baseline"))
        let spaced = try GesSchemaGenerator.generate(set, roots: ["K"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(namespace: "Game.Net"))
        XCTAssertTrue(spaced.contains("global::Game.Net.Item.__ReadKeyOrder("))
        let swift = try GesSchemaGenerator.generate(set, roots: ["S", "K", "M"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift))
        XCTAssertTrue(swift.contains("public func writeDelta(against baseline: S, into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {"))
        XCTAssertTrue(swift.contains("public func copy(from other: S) {"))
        XCTAssertTrue(swift.contains("public func quantize() throws(GesWireError) {"))
        XCTAssertTrue(swift.contains("public func writeDelta(against baseline: K, into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {"))
        XCTAssertTrue(swift.contains("let order = try Item.__readKeyOrder(&reader, paths, at + 2)"))
    }

    func testValuesEmbedTheSourcesAndParameters() throws {
        let source = "parameter p: :Percentage\nparameter w: :Quantity(°)\nschema :S as {\n  a: :Percentage between 0% and p step 1%\n  b: :Quantity(°) between 0° and w\n}\nmessage M(a: :Boolean)\n"
        let set = try GesSchemaSet(sources: [GesSchemaSource(name: "dir/s.gess", text: source)])
        let parameters = GesSchemaParameters()
        try parameters.with("p", literal: "50%")
        try parameters.with("w", literal: "90°")
        let csharp = try GesSchemaGenerator.generate(set, roots: ["S", "M"], parameters: parameters, options: GesSchemaGenerateOptions(values: true))
        XCTAssertTrue(csharp.contains("public readonly global::GameEventScript.Runtime.Values.GesValue ToValue()"))
        XCTAssertTrue(csharp.contains("public void ReadMessage(GameEventScriptMessage message)"))
        XCTAssertTrue(csharp.contains("new GesSchemaSource(\"s.gess\", string.Join(\"\\n\", new[]"))
        XCTAssertTrue(csharp.contains("\"parameter w: :Quantity(\\u00B0)\","))
        XCTAssertTrue(csharp.contains(".With(\"p\", \"5e1%\").With(\"w\", \"9e1\\u00B0\")"))
        XCTAssertFalse(try GesSchemaGenerator.generate(set, roots: ["S"], parameters: parameters, options: GesSchemaGenerateOptions()).contains("ToValue"))
        let swift = try GesSchemaGenerator.generate(set, roots: ["S", "M"], parameters: parameters, options: GesSchemaGenerateOptions(language: .swift, values: true))
        XCTAssertTrue(swift.contains("public func toValue() throws -> GesValue {"))
        XCTAssertTrue(swift.contains("public mutating func read(message: GameEventScriptMessage) throws {"))
        XCTAssertTrue(swift.contains("try parameters.with(\"w\", literal: \"9e1\\u{B0}\")"))
        let storage = try GesSchemaSet(parsing: "schema :T as {\n  name: :Text max 4\n  flag: :Boolean\n}\n")
        let unchecked = try GesSchemaGenerator.generate(storage, roots: ["T"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift))
        XCTAssertTrue(unchecked.contains("@exclusivity(unchecked) public var flag: Bool = false"))
        XCTAssertTrue(unchecked.contains("@exclusivity(unchecked) public var nameUTF8: [UInt8] = []"))
        let checked = try GesSchemaGenerator.generate(storage, roots: ["T"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, checkedExclusivity: true))
        XCTAssertFalse(checked.contains("@exclusivity"))
        XCTAssertFalse(try GesSchemaGenerator.generate(set, roots: ["S"], parameters: parameters, options: GesSchemaGenerateOptions(language: .swift)).contains("@exclusivity"))
        let collision = try GesSchemaSet(parsing: "schema :S as {\n  toValue: :Boolean\n}\n")
        XCTAssertEqual(code { try GesSchemaGenerator.generate(collision, roots: ["S"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(values: true)) }, "generate.nameCollision")
        XCTAssertEqual(code { try GesSchemaGenerator.generate(collision, roots: ["S"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: .swift, values: true)) }, "generate.nameCollision")
    }

    func testSharedTypesAreGeneratedOnce() throws {
        let source = "schema :Inner as {\n  a: :Boolean\n}\nschema :A as {\n  i: :Inner\n}\nschema :B as {\n  i: :Inner\n}\n"
        let text = try GesSchemaGenerator.generate(try GesSchemaSet(parsing: source), roots: ["A", "B"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions())
        XCTAssertEqual(text.components(separatedBy: "partial struct Inner").count, 2)
        XCTAssertTrue(text.contains("from the roots A, B."))
        XCTAssertTrue(text.contains("\"i.a\","))
    }

    func testSplitOutputPlacesEachDeclarationInTheFileOfItsDeclaration() throws {
        let set = try GesSchemaSet(sources: [
            GesSchemaSource(name: "maps/world.gess", text: "tags :Side as (#left, #right)\nschema :World as {\n  bot: :Bot\n  side: :Side\n}\n"),
            GesSchemaSource(name: "bots.gess", text: "schema :Bot as {\n  names: :List(:Text max 4) max 2\n}\n"),
        ])
        for language in [GesSchemaLanguage.csharp, .swift] {
            let fileExtension = language == .swift ? ".swift" : ".g.cs"
            var options = GesSchemaGenerateOptions(language: language, namespace: "Game.Wire", values: true, split: .schema)
            var files = try GesSchemaGenerator.generateFiles(set, roots: ["World"], parameters: GesSchemaParameters(), options: options)
            XCTAssertEqual(files.map(\.path), ["Game.Wire.Bot", "Game.Wire.Side", "Game.Wire.World", "_Game.Wire"].map { $0 + fileExtension } + [".ges-generated.json"])
            XCTAssertTrue(files[0].text.contains("NamesItem"))
            XCTAssertFalse(files[2].text.contains("NamesItem"))
            XCTAssertTrue(files[3].text.contains("__GesSchemas"))
            XCTAssertTrue(files[4].text.contains("\"split\": \"schema\""))
            XCTAssertTrue(files[4].text.contains("\"files\": [\n    \"Game.Wire.Bot\(fileExtension)"))
            options.split = .file
            files = try GesSchemaGenerator.generateFiles(set, roots: ["World"], parameters: GesSchemaParameters(), options: options)
            XCTAssertEqual(files.map(\.path), ["Game.Wire.bots" + fileExtension, "Game.Wire.world" + fileExtension, "_Game.Wire" + fileExtension, ".ges-generated.json"])
            XCTAssertTrue(files[1].text.contains("Side"))
        }
        let swift = try GesSchemaGenerator.generateFiles(
            set,
            roots: ["World"],
            parameters: GesSchemaParameters(),
            options: GesSchemaGenerateOptions(language: .swift, namespace: "Game.Wire", split: .schema)
        )
        XCTAssertTrue(swift[0].text.contains("extension Game.Wire {\n"))
        XCTAssertTrue(swift[3].text.contains("public enum Game {\n    /// The namespace `Wire` of generated schema types.\n    public enum Wire {}\n}\n"))
        XCTAssertFalse(swift[0].text.contains("from the root"))
    }

    func testSplitOutputRejectsMismatchedRequestsAndCollidingFileNames() throws {
        let set = try GesSchemaSet(parsing: "schema :S as {\n  on: :Boolean\n}\n")
        let split = GesSchemaGenerateOptions(split: .schema)
        XCTAssertEqual(code { try GesSchemaGenerator.generate(set, roots: ["S"], parameters: GesSchemaParameters(), options: split) }, "generate.usage")
        XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["S"], options: split) }, "generate.usage")
        XCTAssertEqual(code { try GesSchemaGenerator.generateFiles(set, roots: ["S"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions()) }, "generate.usage")
        XCTAssertEqual(code { try GesSchemaGenerator.generateModelFiles(set, roots: ["S"], options: GesSchemaGenerateOptions()) }, "generate.usage")
        let cases = try GesSchemaSet(parsing: "schema :Ab as {\n  b: :AB\n}\nschema :AB as {\n  on: :Boolean\n}\n")
        XCTAssertEqual(code { try GesSchemaGenerator.generateFiles(cases, roots: ["Ab"], parameters: GesSchemaParameters(), options: split) }, "generate.nameCollision")
        let sources = try GesSchemaSet(sources: [
            GesSchemaSource(name: "a/s.gess", text: "schema :A as {\n  b: :B\n}\n"), GesSchemaSource(name: "b/s.gess", text: "schema :B as {\n  on: :Boolean\n}\n"),
        ])
        XCTAssertEqual(code { try GesSchemaGenerator.generateModelFiles(sources, roots: ["A"], options: GesSchemaGenerateOptions(split: .file)) }, "generate.nameCollision")
        XCTAssertEqual(try GesSchemaGenerator.generateModelFiles(sources, roots: ["A"], options: GesSchemaGenerateOptions(split: .schema)).count, 4)
    }

    func testNestedTypesMustNotHideTopLevelTypes() throws {
        let set = try GesSchemaSet(
            parsing:
                "tags :ModeTag as (#p, #q, #r)\nschema :SlotsItem as {\n  a: :Boolean\n}\nschema :Squad as {\n  mode: :Tag(#a, #b)\n  other: :ModeTag\n}\nschema :Crew as {\n  slots: :List(:Text max 4 | :Number integer between 0 and 3) max 3\n  spare: :SlotsItem\n}\n"
        )
        for language in [GesSchemaLanguage.csharp, .swift] {
            let options = GesSchemaGenerateOptions(language: language)
            XCTAssertEqual(code { try GesSchemaGenerator.generate(set, roots: ["Squad"], parameters: GesSchemaParameters(), options: options) }, "generate.nameCollision")
            XCTAssertEqual(code { try GesSchemaGenerator.generate(set, roots: ["Crew"], parameters: GesSchemaParameters(), options: options) }, "generate.nameCollision")
            XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["Squad"], options: options) }, "generate.nameCollision")
        }
    }

    func testTypesMustNotTakeNamesThatGeneratedCodeUses() throws {
        let swift = GesSchemaGenerateOptions(language: .swift)
        let csharp = GesSchemaGenerateOptions()
        for name in ["String", "Int", "Type", "Swift"] {
            let set = try GesSchemaSet(parsing: "schema :\(name) as {\n  on: :Boolean\n}\n")
            XCTAssertEqual(code { try GesSchemaGenerator.generate(set, roots: [name], parameters: GesSchemaParameters(), options: swift) }, "generate.nameCollision")
            XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: [name], options: swift) }, "generate.nameCollision")
            _ = try GesSchemaGenerator.generate(set, roots: [name], parameters: GesSchemaParameters(), options: csharp)
        }
        let runtime = try GesSchemaSet(parsing: "schema :GesValue as {\n  on: :Boolean\n}\n")
        for options in [csharp, swift] {
            XCTAssertEqual(code { try GesSchemaGenerator.generate(runtime, roots: ["GesValue"], parameters: GesSchemaParameters(), options: options) }, "generate.nameCollision")
            XCTAssertEqual(code { try GesSchemaGenerator.generateModels(runtime, roots: ["GesValue"], options: options) }, "generate.nameCollision")
        }
        let member = try GesSchemaSet(parsing: "schema :S as {\n  gesValue: :Boolean\n  gesture: :Boolean\n}\n")
        XCTAssertEqual(code { try GesSchemaGenerator.generate(member, roots: ["S"], parameters: GesSchemaParameters(), options: csharp) }, "generate.nameCollision")
        _ = try GesSchemaGenerator.generate(try GesSchemaSet(parsing: "schema :Gesture as {\n  gesture: :Boolean\n}\n"), roots: ["Gesture"], parameters: GesSchemaParameters(), options: csharp)
    }

    func testSwiftModelsReserveTheHashableMembers() throws {
        for field in ["hashValue", "hash"] {
            let set = try GesSchemaSet(parsing: "schema :Unit as {\n  \(field): :Number integer between 0 and 9\n}\n")
            XCTAssertEqual(code { try GesSchemaGenerator.generateModels(set, roots: ["Unit"], options: GesSchemaGenerateOptions(language: .swift)) }, "generate.nameCollision")
            _ = try GesSchemaGenerator.generateModels(set, roots: ["Unit"], options: GesSchemaGenerateOptions())
        }
    }

    func testManifestNamesTheValueConversionsThatDocumentsInclude() throws {
        let set = try GesSchemaSet(parsing: "schema :S as {\n  on: :Boolean\n}\n")
        var documents = GesSchemaGenerateOptions(split: .schema)
        documents.documents = true
        var both = documents
        both.values = true
        let documentFiles = try GesSchemaGenerator.generateModelFiles(set, roots: ["S"], options: documents)
        XCTAssertEqual(documentFiles, try GesSchemaGenerator.generateModelFiles(set, roots: ["S"], options: both))
        XCTAssertTrue(try XCTUnwrap(documentFiles.last).text.contains("\"values\": true"))
    }
}
