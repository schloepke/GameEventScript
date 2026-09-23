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

}
