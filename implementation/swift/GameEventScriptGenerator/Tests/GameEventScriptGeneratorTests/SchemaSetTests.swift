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
        // A C# member cannot share its class's name; a Swift member can.
        for name in ["Set", "Hash", "Parameters", "Bind", "BindNetwork", "ReadDocument"] {
            XCTAssertEqual(failure(GesSchemaGenerateOptions(language: .csharp, schemaSet: name)), "generate.nameCollision")
        }
        XCTAssertTrue(try generate(GesSchemaGenerateOptions(language: .swift, schemaSet: "Set")).contains("enum Set"))
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

    func testAMessageSuffixNamesOnlyMessageTypes() throws {
        let set = try GesSchemaSet(
            parsing: "wire schema :Shield as { strength: :Number integer between 0 and 100 }\nwire schema :ShieldMessage as { x: :Boolean }\n"
                + "wire messages :Actions as (Shield, Move)\nwire message Shield(level: :Number integer between 0 and 10)\nwire message Move(x: :Number integer between 0 and 255)\n"
        )

        func messages(_ roots: [String], _ suffix: String, _ language: GesSchemaLanguage = .csharp) throws -> String {
            try GesSchemaGenerator.generate(set, roots: roots, parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: language, messageSuffix: suffix))
        }

        func code(_ action: () throws -> Void) -> String {
            do {
                try action()
                return ""
            } catch let error as GesGeneratorError {
                return error.code
            } catch {
                return "\(error)"
            }
        }

        for language in [GesSchemaLanguage.csharp, .swift] {
            // Without a suffix the message and the schema both claim Shield; with one, ShieldMessage meets the schema of that name.
            XCTAssertEqual(code { _ = try messages([":Shield", "Shield()"], "", language) }, "generate.nameCollision")
            XCTAssertEqual(code { _ = try messages([":ShieldMessage", "Shield()"], "Message", language) }, "generate.nameCollision")
            XCTAssertTrue(try messages([":Shield", "Shield()"], "Message", language).contains("ShieldMessage"))
            for suffix in ["a-b", "Ä", " "] {
                XCTAssertEqual(code { _ = try messages(["Shield()"], suffix, language) }, "generate.usage")
            }
            XCTAssertTrue(try messages(["Move"], "1x", language).contains("Move1x"))
            XCTAssertEqual(code { _ = try GesSchemaGenerator.generateModels(set, roots: [":Shield"], options: GesSchemaGenerateOptions(language: language, messageSuffix: "Message")) }, "generate.usage")
        }
        // Set members keep the declared names; only the message types they hold take the suffix.
        let actions = try messages(["Actions"], "Message")
        XCTAssertTrue(actions.contains("public MoveMessage Move"))
        XCTAssertTrue(actions.contains("case Member.Move:"))
        XCTAssertFalse(actions.contains("Member.MoveMessage"))
        // The suffix exists only in code: hashes and paths keep the declared name.
        let plain = try messages(["Shield()"], "")
        let suffixed = try messages(["Shield()"], "Message")
        XCTAssertTrue(plain.contains("SchemaHash = 0x03181e32445ba5acUL"))
        XCTAssertEqual(plain, suffixed.replacing("ShieldMessage", with: "Shield"))
    }

    func testANetworkAndAMessageOfOneNameNeedAQualifiedRoot() throws {
        let set = try GesSchemaSet(parsing: "wire message Lobby(level: :Number integer between 0 and 10)\nnetwork :Lobby as {\n  client sends Lobby reliable\n}\n")

        func schemaCode(_ action: () throws -> Void) -> String {
            do {
                try action()
                return ""
            } catch let error as GesSchemaError {
                return error.code
            } catch {
                return "\(error)"
            }
        }

        for language in [GesSchemaLanguage.csharp, .swift] {
            func lobby(_ root: String) throws -> String {
                try GesSchemaGenerator.generate(set, roots: [root], parameters: GesSchemaParameters(), options: GesSchemaGenerateOptions(language: language, messageSuffix: "Message"))
            }

            XCTAssertEqual(schemaCode { _ = try lobby("Lobby") }, "schema.ambiguousName")
            XCTAssertTrue(try lobby(":Lobby").contains("LobbyMessage"))
            XCTAssertNotEqual(try lobby(":Lobby"), try lobby("Lobby()"))
        }
        XCTAssertEqual(schemaCode { _ = try set.schema("Lobby") }, "schema.ambiguousName")
        XCTAssertEqual(try set.schema("Lobby()").kind, .message)
    }
}
