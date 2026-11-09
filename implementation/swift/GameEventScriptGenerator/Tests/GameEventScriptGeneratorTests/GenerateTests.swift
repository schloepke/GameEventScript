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
        XCTAssertEqual(code { try generate("schema :S as {\n  s: :Boolean\n}\n", "S", .csharp) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("schema :S as {\n  state: :Tag(#a)\n  stateTag: :Boolean\n}\n", "S", .csharp) }, "generate.nameCollision")
        XCTAssertEqual(code { try generate("schema :S as {\n  heat: :Boolean optional\n  hasHeat: :Boolean\n}\n", "S", .swift) }, "generate.nameCollision")
        XCTAssertTrue(try generate("schema :S as {\n  s: :Boolean\n}\n", "S", .swift).contains("public var s: Bool = false"))
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

    func testSharedTypesAreGeneratedOnce() throws {
        let source = "schema :Inner as {\n  a: :Boolean\n}\nschema :A as {\n  i: :Inner\n}\nschema :B as {\n  i: :Inner\n}\n"
        let text = try GesSchemaGenerator.generate(try GesSchemaSet(parsing: source), roots: ["A", "B"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions())
        XCTAssertEqual(text.components(separatedBy: "partial struct Inner").count, 2)
        XCTAssertTrue(text.contains("from the roots A, B."))
        XCTAssertTrue(text.contains("\"i.a\","))
    }
}
