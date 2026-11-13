// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import SwiftSyntaxMacros
import SwiftSyntaxMacrosTestSupport
import XCTest

@testable import GameEventScriptSwiftBridgeMacros

final class MacroDiagnosticsTests: XCTestCase {
    func testNonfinalClassIsRejected() {
        assertMacroExpansion(
            """
            @GesType
            class Bot {}
            """,
            expandedSource: "class Bot {}",
            diagnostics: [DiagnosticSpec(message: "@GesType requires a nongeneric struct or final class.", line: 1, column: 1)],
            macros: ["GesType": GesTypeMacro.self]
        )
    }

    func testInvalidBindingsProduceActionableDiagnostics() {
        let cases: [(String, String)] = [
            ("struct Bot<T> {}", "@GesType requires a nongeneric struct or final class."),
            ("struct Bot { @GesField var value: Unknown }", "Cannot infer the GES field type; specify @GesField(typeName: ...)."),
            ("struct Bot { @GesField static var value: Int = 1 }", "@GesField requires one instance property with an explicit Swift type."),
            ("struct Bot { @GesField var x: Int, y: Int }", "@GesField requires one instance property with an explicit Swift type."),
            ("struct Bot { @GesField var value = 1 }", "@GesField requires one instance property with an explicit Swift type."),
            ("struct Bot { @GesField(unit: .meter) var value: Bool }", "@GesField unit requires a native numeric property."),
            ("struct Bot { @GesField var value: Int { get throws { 1 } } }", "@GesField requires a synchronous, nonthrowing getter; use an explicit getter binding otherwise."),
            ("struct Bot { @GesConstruct init?() {} }", "@GesConstruct requires a synchronous, nonfailable, nongeneric initializer."),
            ("struct Bot { @GesConstruct init(value: Int) {} }", "@GesConstruct parameter value must refer to an annotated field."),
            ("struct Bot { @GesConstruct func make() -> Self { self } }", "@GesConstruct factories must be synchronous, nongeneric static functions returning Self or the enclosing type."),
            ("struct Bot { func createGesType() {} }", "@GesType reserves the member name createGesType."),
            (#"struct Bot { @GesField("same") var x: Int; @GesField("same") var y: Int }"#, "Duplicate @GesField name: same"),
            (#"struct Bot { @GesField(typeName: "Number", unit: .meter) var value: Double }"#, "Use either typeName or unit on @GesField, not both."),
        ]
        for (declaration, message) in cases {
            assertMacroExpansion(
                "@GesType\n" + declaration,
                expandedSource: declaration == "struct Bot<T> {}" ? declaration : String(declaration.dropLast()) + "\n}",
                diagnostics: [DiagnosticSpec(message: message, line: 1, column: 1)],
                macros: ["GesType": GesTypeMacro.self]
            )
        }
    }

    func testConditionalMembersCannotSilentlyDisappear() {
        let declaration = """
            struct Bot {
            #if DEBUG
                @GesField var value: Int
            #endif
            }
            """
        assertMacroExpansion(
            "@GesType\n" + declaration,
            expandedSource: declaration,
            diagnostics: [DiagnosticSpec(message: "Conditional @GesField/@GesConstruct declarations require manual bindings.", line: 1, column: 1)],
            macros: ["GesType": GesTypeMacro.self]
        )
    }

    func testOrphanFieldCannotSilentlyDisappear() {
        assertMacroExpansion(
            """
            struct Bot {
                @GesField var value: Int
            }
            """,
            expandedSource: """
                struct Bot {
                    var value: Int
                }
                """,
            diagnostics: [DiagnosticSpec(message: "This annotation requires an enclosing @GesType declaration.", line: 2, column: 5)],
            macros: ["GesField": GesFieldMacro.self]
        )
    }

    func testExtensionDeclarationsAreValidated() {
        let macros: [String: Macro.Type] = [
            "GesExtension": GesExtensionMacro.self, "GesFunction": GesFunctionMacro.self, "GesPredicate": GesFunctionMacro.self,
        ]
        let cases: [(String, String, String)] = [
            (#"@GesExtension("nav") class Nav {}"#, #"class Nav {}"#, "@GesExtension requires an enum, struct or final class."),
            (#"@GesExtension("nav") enum Nav { @GesFunction static func go() {} }"#, #"enum Nav { static func go() {} }"#, "@GesFunction requires a result."),
            (
                #"@GesExtension("nav") enum Nav { @GesPredicate static func near(_ a: Double, _ b: Double) -> Bool { true } }"#,
                #"enum Nav { static func near(_ a: Double, _ b: Double) -> Bool { true } }"#,
                "@GesPredicate requires one parameter and a Bool or Bool? result."
            ),
            (#"@GesExtension("nav") enum Nav { @GesFunction(units: ["x": .meter]) static func go(_ y: Double) -> Double { y } }"#, #"enum Nav { static func go(_ y: Double) -> Double { y } }"#, "units names x, which is no parameter."),
            (
                #"@GesExtension("nav") enum Nav { @GesFunction(units: ["v": .meter]) static func go(_ v: GesValue) -> Double { 0 } }"#,
                #"enum Nav { static func go(_ v: GesValue) -> Double { 0 } }"#,
                "A GesValue parameter receives the raw argument and takes no unit."
            ),
            (
                #"@GesExtension("nav") enum Nav { @GesFunction static func go(_ v: Double = 1) -> Double { v } }"#,
                #"enum Nav { static func go(_ v: Double = 1) -> Double { v } }"#,
                "@GesFunction does not support variadic, defaulted, inout or ownership-qualified parameters."
            ),
            (
                #"@GesExtension("nav") enum Nav { @GesFunction("a") static func x() -> Int { 1 }; @GesFunction("a") static func y() -> Int { 2 } }"#,
                #"enum Nav { static func x() -> Int { 1 }; static func y() -> Int { 2 } }"#,
                "@GesExtension declares :nav.a() twice."
            ),
            (
                #"@GesExtension("nav") enum Nav { @GesFunction static func go(_ a: GesRandomGenerator, _ b: GameEventScriptRuntime.GesRandomGenerator) -> Int { 1 } }"#,
                #"enum Nav { static func go(_ a: GesRandomGenerator, _ b: GameEventScriptRuntime.GesRandomGenerator) -> Int { 1 } }"#,
                "@GesFunction declares more than one GesRandomGenerator parameter."
            ),
            (
                #"@GesExtension("nav") enum Nav { @GesFunction(units: ["random": .meter]) static func go(random: GesRandomGenerator) -> Int { 1 } }"#,
                #"enum Nav { static func go(random: GesRandomGenerator) -> Int { 1 } }"#,
                "A GesRandomGenerator parameter receives the call's random generator and takes no unit."
            ),
            (
                #"@GesExtension("nav") enum Nav { @GesPredicate static func lucky(_ random: GesRandomGenerator) -> Bool { true } }"#,
                #"enum Nav { static func lucky(_ random: GesRandomGenerator) -> Bool { true } }"#,
                "@GesPredicate requires one parameter and a Bool or Bool? result."
            ),
            (#"@GesExtension("nav") enum Nav { func gesExtensions() {} }"#, #"enum Nav { func gesExtensions() {} }"#, "@GesExtension reserves the member name gesExtensions."),
        ]
        // An expansion with members puts the closing brace of the declaration on its own line, as for @GesType.
        for (declaration, expanded, message) in cases {
            let source = expanded.hasSuffix(" {}") ? expanded : String(expanded.dropLast()) + "\n}"
            assertMacroExpansion(declaration, expandedSource: source, diagnostics: [DiagnosticSpec(message: message, line: 1, column: 1)], macros: macros)
        }
        assertMacroExpansion(
            "enum Nav { @GesFunction static func go() -> Int { 1 } }",
            expandedSource: "enum Nav { static func go() -> Int { 1 } \n}",
            diagnostics: [DiagnosticSpec(message: "This annotation requires an enclosing @GesExtension declaration.", line: 1, column: 12)],
            macros: macros
        )
    }

    func testRandomGeneratorParameterReceivesTheCallGenerator() {
        assertMacroExpansion(
            """
            @GesExtension("arena")
            enum Arena {
                @GesFunction
                static func spawnSpots(count: Int, random: GesRandomGenerator, spacing: Double) -> Int { count }
            }
            """,
            expandedSource: """
                enum Arena {
                    static func spawnSpots(count: Int, random: GesRandomGenerator, spacing: Double) -> Int { count }

                    /// The extensions of the static `@GesFunction` and `@GesPredicate` methods, for a `GesBridgeExtensionRegistry`.
                    static func gesExtensions() throws -> [GesBridgeExtension] {
                        [try GesBridgeExtension(namespace: "arena", name: "spawnSpots", parameters: ["count", "spacing"]) { call in
                        guard call.arguments.count == 2, case .success(let a0) = Result(catching: { try GesBridgeValueConversion.decode(call.arguments[0], as: (Int).self, unit: .none)
                                    }), case .success(let a1) = Result(catching: { try GesBridgeValueConversion.decode(call.arguments[1], as: (Double).self, unit: .none)
                                    }) else {
                            call.setNothing()
                            return
                        }
                        let result = Self.`spawnSpots`(count: a0, random: call.random, spacing: a1)
                        call.setValue((try? GesBridgeValueConversion.encode(result, unit: .none)) ?? .nothing)
                            }]
                    }
                }
                """,
            macros: ["GesExtension": GesExtensionMacro.self, "GesFunction": GesFunctionMacro.self]
        )
    }
}
