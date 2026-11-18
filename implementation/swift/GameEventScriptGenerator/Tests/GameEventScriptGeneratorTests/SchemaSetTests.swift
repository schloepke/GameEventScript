// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptGenerator
import GameEventScriptRuntime
import XCTest

/// Checks the options and the shape of generated schema sets.
final class SchemaSetTests: XCTestCase {
    private static let source = "parameter size: :Quantity(m)\nwire schema :S as { x: :Quantity(m) between 0m and size step 1m }\n"

    private func generate(_ options: GesSchemaGenerateOptions, size: String = "64m", source: String = source) throws -> String {
        try GesSchemaGenerator.generate(try GesSchemaSet(parsing: source), roots: ["S"], parameters: try GesSchemaParameters().with("size", literal: size), options: options)
    }

    private func failure(_ options: GesSchemaGenerateOptions) -> String {
        do {
            _ = try generate(options)
            return ""
        } catch let error as GesGeneratorError {
            return error.code
        } catch {
            return "\(error)"
        }
    }

    private func hash(_ code: String) -> String {
        for marker in ["Hash => ", "hash: UInt64 = "] {
            if let range = code.range(of: marker) { return String(code[range.upperBound...].prefix(18)) }
        }
        return ""
    }

    func testOptionsMustNameTypesAndModules() throws {
        for language in [GesSchemaLanguage.csharp, .swift] {
            XCTAssertEqual(failure(GesSchemaGenerateOptions(language: language, schemaSet: "Set", useSchemaSet: "Game.Set")), "generate.usage")
            XCTAssertEqual(failure(GesSchemaGenerateOptions(language: language, schemaSet: "1Set")), "generate.usage")
            XCTAssertEqual(failure(GesSchemaGenerateOptions(language: language, schemaSet: "Game.Set")), "generate.usage")
            XCTAssertEqual(failure(GesSchemaGenerateOptions(language: language, schemaSet: "class")), "generate.usage")
            XCTAssertEqual(failure(GesSchemaGenerateOptions(language: language, useSchemaSet: "Game..Set")), "generate.usage")
            XCTAssertEqual(failure(GesSchemaGenerateOptions(language: language, schemaSet: "S")), "generate.nameCollision")
        }
        // Only Swift files import modules.
        XCTAssertEqual(failure(GesSchemaGenerateOptions(language: .csharp, imports: ["Shared"])), "generate.usage")
        XCTAssertEqual(failure(GesSchemaGenerateOptions(language: .swift, imports: ["Shared.Net"])), "generate.usage")
        XCTAssertTrue(try generate(GesSchemaGenerateOptions(language: .swift, imports: ["Shared"])).contains("\nimport Shared\n"))
    }

    func testTheSetHashCoversSourcesAndParameters() throws {
        let csharp = try generate(GesSchemaGenerateOptions(language: .csharp, schemaSet: "GameSchemas"))
        let swift = try generate(GesSchemaGenerateOptions(language: .swift, schemaSet: "GameSchemas"))
        XCTAssertTrue(csharp.contains("public static class GameSchemas"))
        XCTAssertTrue(csharp.contains("public static GesBoundSchema Bind(string root, ulong bindingHash)"))
        XCTAssertTrue(swift.contains("public enum GameSchemas"))
        XCTAssertNotEqual(hash(csharp), "")
        XCTAssertEqual(hash(csharp), hash(swift))
        XCTAssertNotEqual(hash(csharp), hash(try generate(GesSchemaGenerateOptions(language: .csharp, schemaSet: "GameSchemas"), size: "65m")))
        XCTAssertNotEqual(hash(csharp), hash(try generate(GesSchemaGenerateOptions(language: .csharp, schemaSet: "GameSchemas"), source: Self.source + "\n")))
        XCTAssertEqual(hash(csharp), hash(try generate(GesSchemaGenerateOptions(language: .csharp, schemaSet: "GameSchemas"))))
    }

    func testUsingASetDeclaresNone() throws {
        for language in [GesSchemaLanguage.csharp, .swift] {
            XCTAssertFalse(try generate(GesSchemaGenerateOptions(language: language, useSchemaSet: "Shared.GameSchemas")).contains("GameSchemas\n"))
        }
    }
}
