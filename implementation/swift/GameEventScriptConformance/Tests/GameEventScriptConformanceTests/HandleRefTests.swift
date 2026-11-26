// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

/// Refs never enter the VM (specs/HostRuntime.md#handles). Conformance Markdown cannot construct a handle with a ref, so
/// the boundary checks and `GameEventScriptHost.resolveRef(_:)` are covered natively.
final class HandleRefTests: XCTestCase {
    private static let schemas =
        "schema :DroneState as { altitude: :Number integer between 0 and 1000 }\n"
        + "handle @Drone of :DroneState as { id: :Number integer between 0 and 255, version: :Number integer between 0 and 1000 }\n"

    private static func droneType() throws -> GesHandleType { try GesSchemaSet(sources: [GesSchemaSource(name: "drones.gess", text: schemas)]).handleTypes[0] }

    private static func withRef(_ name: String) throws -> GameEventScriptMessage {
        try GameEventScriptMessage(name: name, arguments: [.init(name: "drone", value: .handle(try GesHandle(parsing: "@Drone:7~#live")))])
    }

    func testReceiveRejectsAHandleWithARef() throws {
        let host = try GameEventScriptHost.makeBuilder().build().startForTest()

        XCTAssertThrowsError(try host.receive(Self.withRef("Seen"))) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.unresolvedRef)
            XCTAssertEqual((error as? GesWireError)?.path, "Seen.drone")
        }
        var delivered = 0
        _ = try host.subscribe(.init(name: "Seen", parameters: ["drone"])) { _, _ in delivered += 1 }
        XCTAssertThrowsError(try host.receive(Self.withRef("Seen")))
        XCTAssertTrue(try host.receive(.init(name: "Seen", arguments: [.init(name: "drone", value: .handle(try GesHandle(parsing: "@Drone:7~3")))])))
        XCTAssertEqual(try host.runToCompletion().state, .completed)
        XCTAssertEqual(delivered, 1, "Only the message without a ref is enqueued.")
    }

    func testContextEmitAndPublishRejectAHandleWithARef() throws {
        let host = try GameEventScriptHost.makeBuilder().build().startForTest()
        var codes: [String] = []
        let message = try Self.withRef("Out")
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

    func testResolveRefAsksTheResolverOfTheType() throws {
        let resolver = RefResolver(.integer(12))
        let host = try GameEventScriptHost.makeBuilder().withHandleResolver(resolver, for: Self.droneType()).build().startForTest()

        let resolved = try host.resolveRef(GesHandle(parsing: "@Drone:7~#live"))

        XCTAssertEqual(resolved.description, "@Drone:7~12")
        XCTAssertTrue(resolved.ref.isNothing)
        XCTAssertEqual(resolver.asked, ["@Drone:7~#live"])
    }

    func testResolveRefReturnsAHandleWithoutARefUnchanged() throws {
        let resolver = RefResolver(.integer(12))
        let host = try GameEventScriptHost.makeBuilder().withHandleResolver(resolver, for: Self.droneType()).build().startForTest()
        let handle = try GesHandle(parsing: "@Drone:7~3")

        XCTAssertTrue(try host.resolveRef(handle) === handle)
        XCTAssertTrue(resolver.asked.isEmpty)
    }

    func testResolveRefFailsWithoutAResolverOrAValidVersion() throws {
        let live = try GesHandle(parsing: "@Drone:7~#live")
        let withoutResolver = try GameEventScriptHost.makeBuilder().build().startForTest()
        XCTAssertThrowsError(try withoutResolver.resolveRef(live)) { error in
            XCTAssertEqual((error as? GesHandleError)?.code, GesDiagnosticCodes.runtimeHandleResolveFailed)
            XCTAssertEqual((error as? GesHandleError)?.symbol, "@Drone:7~#live")
        }

        let invalid = try GameEventScriptHost.makeBuilder().withHandleResolver(RefResolver(.text("12")), for: Self.droneType()).build().startForTest()
        XCTAssertThrowsError(try invalid.resolveRef(live)) { error in
            XCTAssertEqual((error as? GesHandleError)?.code, GesDiagnosticCodes.runtimeHandleResolveFailed)
        }

        let failing = try GameEventScriptHost.makeBuilder().withHandleResolver(RefResolver(.nothing, fail: true), for: Self.droneType()).build().startForTest()
        XCTAssertThrowsError(try failing.resolveRef(live)) { error in
            XCTAssertEqual((error as? GesHandleError)?.code, GesDiagnosticCodes.runtimeHandleResolveFailed)
            XCTAssertTrue((error as? GesHandleError)?.underlyingError is UnknownRefError)
        }
    }
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
