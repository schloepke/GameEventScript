// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptGenerator
import GameEventScriptRuntime
import XCTest

/// Checks the generation rules that the corpus harness cannot reach: names, namespaces and unsupported types.
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

    func testInvalidNamespacesAndUnsupportedTypesAreRejected() {
        XCTAssertEqual(code { try generate("schema :S as {\n  a: :Boolean\n}\n", "S", .csharp, "Game..Net") }, "generate.invalidNamespace")
        XCTAssertEqual(code { try generate("schema :S as {\n  a: :Boolean\n}\n", "S", .csharp, "class") }, "generate.invalidNamespace")
        XCTAssertEqual(code { try generate("schema :S as {\n  a: :Boolean\n}\n", "S", .swift, "1Wire") }, "generate.invalidNamespace")
        XCTAssertEqual(code { try generate("schema :S as {\n  a: :Text max 4\n}\n", "S", .csharp) }, "generate.unsupported")
        XCTAssertEqual(code { try generate("message M(a: :Boolean)\n", "M", .swift) }, "generate.unsupported")
    }

    func testSharedTypesAreGeneratedOnce() throws {
        let source = "schema :Inner as {\n  a: :Boolean\n}\nschema :A as {\n  i: :Inner\n}\nschema :B as {\n  i: :Inner\n}\n"
        let text = try GesSchemaGenerator.generate(try GesSchemaSet(parsing: source), roots: ["A", "B"], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions())
        XCTAssertEqual(text.components(separatedBy: "partial struct Inner").count, 2)
        XCTAssertTrue(text.contains("from the roots A, B."))
        XCTAssertTrue(text.contains("\"i.a\","))
    }
}
