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

    private func code(_ body: () throws -> String) -> String {
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
}
