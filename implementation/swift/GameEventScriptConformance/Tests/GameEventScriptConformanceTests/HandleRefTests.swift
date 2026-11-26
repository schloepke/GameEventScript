// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

/// Refs never enter the VM (specs/HostRuntime.md#handles). Markdown Conformance covers `receive` and the answers of
/// `resolveRef` (runtime/handles.md); these tests keep what it cannot reach: native emit and publish, the default
/// `resolveRef`, a host without a resolver, identity and the preserved underlying error.
final class HandleRefTests: XCTestCase {
    private static let schemas =
        "schema :DroneState as { altitude: :Number integer between 0 and 1000 }\n"
        + "handle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\n"

    private static func droneType() throws -> GesHandleType { try GesSchemaSet(sources: [GesSchemaSource(name: "drones.gess", text: schemas)]).handleTypes[0] }

    private static func live() throws -> GesHandle { try GesHandle(parsing: "@Drone:7~#live") }

    private static func host(_ resolver: some GesHandleResolver) throws -> GameEventScriptHost {
        try GameEventScriptHost.makeBuilder().withHandleResolver(resolver, for: droneType()).build().startForTest()
    }

    func testContextEmitAndPublishRejectAHandleWithARef() throws {
        let host = try GameEventScriptHost.makeBuilder().build().startForTest()
        var codes: [String] = []
        let message = try GameEventScriptMessage(name: "Out", arguments: [.init(name: "drone", value: .handle(Self.live()))])
        _ = try host.subscribe(.init(name: "Go")) { _, context in
            do {
                _ = try context.emit(message)
            } catch {
                codes.append((error as? GesWireError)?.code ?? "")
            }
            do {
                _ = try context.publish(message)
            } catch {
                codes.append((error as? GesWireError)?.code ?? "")
            }
        }

        XCTAssertTrue(try host.receive(.init(name: "Go")))
        XCTAssertEqual(try host.runToCompletion().state, .completed)

        XCTAssertEqual(codes, [GesWireError.unresolvedRef, GesWireError.unresolvedRef])
    }

    func testResolveRefAsksOnlyForRefsAndReturnsNilForAnUnknownRef() throws {
        let resolver = RefResolver(.integer(12))
        let host = try Self.host(resolver)
        let plain = try GesHandle(parsing: "@Drone:7~3")

        XCTAssertEqual(try host.resolveRef(Self.live())?.description, "@Drone:7~12")
        XCTAssertTrue(try host.resolveRef(plain) === plain)
        XCTAssertEqual(resolver.asked, ["@Drone:7~#live"])
        XCTAssertNil(try Self.host(RefResolver(.nothing)).resolveRef(Self.live()))
    }

    func testResolveRefFailsWithoutAResolverOrRefSupport() throws {
        let live = try Self.live()
        let withoutResolver = try GameEventScriptHost.makeBuilder().build().startForTest()
        XCTAssertThrowsError(try withoutResolver.resolveRef(live)) { error in
            XCTAssertEqual((error as? GesHandleError)?.code, GesDiagnosticCodes.runtimeHandleResolveFailed)
            XCTAssertEqual((error as? GesHandleError)?.symbol, "@Drone:7~#live")
        }

        // A resolver of a type without refs need not implement resolveRef; the default fails like a throwing resolver.
        XCTAssertThrowsError(try Self.host(StateOnlyResolver()).resolveRef(live)) { error in
            XCTAssertEqual((error as? GesHandleError)?.code, GesDiagnosticCodes.runtimeHandleResolveFailed)
        }

        XCTAssertThrowsError(try Self.host(RefResolver(.nothing, fail: true)).resolveRef(live)) { error in
            XCTAssertEqual((error as? GesHandleError)?.code, GesDiagnosticCodes.runtimeHandleResolveFailed)
            XCTAssertTrue((error as? GesHandleError)?.underlyingError is UnknownRefError)
        }
    }
}

private final class StateOnlyResolver: GesHandleResolver {
    func resolve(_ call: GesHandleResolveCall) throws { try call.setMissing() }
}

private struct UnknownRefError: Error {}

private final class RefResolver: GesHandleResolver {
    private let version: GesValue
    private let fail: Bool
    private(set) var asked: [String] = []

    init(_ version: GesValue, fail: Bool = false) {
        self.version = version
        self.fail = fail
    }

    func resolve(_ call: GesHandleResolveCall) throws { try call.setMissing() }

    func resolveRef(_ handle: GesHandle) throws -> GesValue {
        asked.append(handle.description)
        if fail { throw UnknownRefError() }
        return version
    }
}
