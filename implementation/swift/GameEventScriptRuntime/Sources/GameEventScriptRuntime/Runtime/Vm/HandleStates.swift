// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

// One handle type a host registered with its resolver.
final class GesHandleBinding {
    let type: GesHandleType
    let resolver: any GesHandleResolver

    init(type: GesHandleType, resolver: any GesHandleResolver) {
        self.type = type
        self.resolver = resolver
    }
}

// A resolved handle state: the normalized state Record, or Nothing for a missing object, and the object's version.
struct GesHandleState {
    let state: GesValue
    let version: GesValue
}

// Resolves handle states for the VM and refs for the host (specs/HostRuntime.md#handles).
enum GesHandleStates {
    // Asks the type's resolver for the state, checks it against the state schema with defaults applied and checks the
    // versions. Failures throw a runtime fault that ends the handler.
    static func resolve(_ binding: GesHandleBinding?, _ call: GesHandleResolveCall, _ handle: GesHandle) throws -> GesHandleState {
        let symbol = handle.description
        guard let binding else { throw fault(GesDiagnosticCodes.runtimeHandleResolveFailed, "No resolver reads the state of @\(handle.typeName).", symbol) }
        let answer: GesHandleResolveCall.AnswerKind
        let record: GesValue
        let text: String?
        let passed: GesValue
        call.begin(handle, binding.type)
        defer { call.end() }
        do {
            try binding.resolver.resolve(call)
            answer = call.answer
            record = call.record
            text = call.text
            passed = call.version
        } catch let fault as any GesFatalRuntimeError {
            throw fault
        } catch {
            throw fault(GesDiagnosticCodes.runtimeHandleResolveFailed, "The resolver of @\(handle.typeName) failed.", symbol, error)
        }

        if answer == .none || answer == .several {
            throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The resolver of @\(handle.typeName) must give exactly one answer.", symbol)
        }
        let type = binding.type
        if !passed.isNothing && !isVersionOf(type.version, passed) {
            throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The resolver of @\(handle.typeName) passed no valid version of its type.", symbol)
        }
        if answer == .missing { return GesHandleState(state: .nothing, version: try resolved(type, handle, passed, symbol)) }
        let state = try checkedState(type, answer == .document ? text! : try print(type, record, symbol), symbol)
        if type.version == .contentHash {
            let computed = GesValue.bytes(try GesSchemaValues.contentHash(type, state: state).bytes)
            if !passed.isNothing && !versionEquals(passed, computed) || handle.hasVersion && !versionEquals(handle.version, computed) {
                throw fault(GesDiagnosticCodes.runtimeHandleVersionMismatch, "The content hash of \(symbol) differs.", symbol)
            }
            return GesHandleState(state: state, version: computed)
        }
        return GesHandleState(state: state, version: try resolved(type, handle, passed, symbol))
    }

    // The ref of a handle turned into its version by the type's resolver.
    static func resolveRef(_ binding: GesHandleBinding?, _ handle: GesHandle) throws(GesHandleError) -> GesHandle {
        if handle.ref.isNothing { return handle }
        let symbol = handle.description
        guard let binding else {
            throw GesHandleError(code: GesDiagnosticCodes.runtimeHandleResolveFailed, symbol: symbol, message: "No resolver turns refs of @\(handle.typeName) into versions.")
        }
        let version: GesValue
        do {
            version = try binding.resolver.resolveRef(handle)
        } catch {
            throw GesHandleError(code: GesDiagnosticCodes.runtimeHandleResolveFailed, symbol: symbol, message: "The resolver of @\(handle.typeName) failed.", underlyingError: error)
        }
        guard isVersionOf(binding.type.version, version), let versioned = try? handle.withVersion(version) else {
            throw GesHandleError(code: GesDiagnosticCodes.runtimeHandleResolveFailed, symbol: symbol, message: "The resolver of @\(handle.typeName) returned no valid version.")
        }
        return versioned
    }

    // Whether the value is a version of the kind; a type without versions has none.
    static func isVersionOf(_ kind: GesHandleVersionKind, _ version: GesValue) -> Bool {
        switch kind {
        case .revision: version.valueKind == .integer && version.unit == .none
        case .sha1: version.valueKind == .bytes && version.asBytes.count == GesHash20.length
        case .sha256, .contentHash: version.valueKind == .bytes && version.asBytes.count == GesHash32.length
        case .none: false
        }
    }

    static func versionEquals(_ left: GesValue, _ right: GesValue) -> Bool {
        if left.valueKind != right.valueKind { return false }
        switch left.valueKind {
        case .integer: return left.asInteger == right.asInteger
        case .bytes: return left.asBytes == right.asBytes
        default: return left.isNothing && right.isNothing
        }
    }

    // The version of a revision or hash type: the passed one, which must match the carried one, else the carried one.
    private static func resolved(_ type: GesHandleType, _ handle: GesHandle, _ passed: GesValue, _ symbol: String) throws -> GesValue {
        if type.version == .none { return .nothing }
        if passed.isNothing { return handle.version }
        if handle.hasVersion && !versionEquals(passed, handle.version) {
            throw fault(GesDiagnosticCodes.runtimeHandleVersionMismatch, "The resolver of @\(handle.typeName) delivered another version of \(symbol).", symbol)
        }
        return passed
    }

    // A Record answer as document text, so it is checked exactly like a document answer.
    private static func print(_ type: GesHandleType, _ record: GesValue, _ symbol: String) throws -> String {
        guard record.kind == .record, let name = record.customTypeName, GesText.scalarEqual(name, type.stateName) else {
            throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The state of \(symbol) is no :\(type.stateName) Record.", symbol)
        }
        do {
            return try GesDocument(record).text()
        } catch {
            throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The state of \(symbol) is no valid :\(type.stateName) Record.", symbol, error)
        }
    }

    // The document read and checked against the state schema, as a Record with defaults applied.
    private static func checkedState(_ type: GesHandleType, _ text: String, _ symbol: String) throws -> GesValue {
        let declaration: GesResolvedDeclaration = type.resolved.state
        do {
            let value = try GesDocumentReading.read(text).0
            let syntax = GesDocumentReader.read(text)
            var named: String?
            if case .type(let name) = syntax.schema { named = name }
            guard let named, GesText.scalarEqual(named, type.stateName) else {
                throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The state document of \(symbol) does not name :\(type.stateName).", symbol)
            }
            let (failure, chosen) = try GesSchemaDocumentCheck.choosing(syntax, declaration, nil)
            if let failure { throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The state of \(symbol) does not fit :\(type.stateName).", symbol, failure) }
            return try GesSchemaValues.normalizeState(declaration, GesSchemaValues.documentShapes(declaration, value, chosen: chosen), "", type.declarations)
        } catch let error as GesDocumentError {
            throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The state document of \(symbol) does not read.", symbol, error)
        } catch let error as GesWireError {
            throw fault(GesDiagnosticCodes.runtimeInvalidHandleState, "The state of \(symbol) does not fit :\(type.stateName).", symbol, error)
        }
    }

    private static func fault(_ code: String, _ message: String, _ symbol: String, _ inner: (any Error)? = nil) -> GesRuntimeError {
        GesRuntimeError(diagnostic: .init(phase: .runtime, code: code, message: message, symbol: symbol, technicalDetails: inner.map { String(describing: $0) }))
    }

    // Values that hold a handle with a ref, which never enter the VM.
    static func holdsRef(_ value: GesValue) -> Bool { holdsRef(value, 0) }

    private static func holdsRef(_ value: GesValue, _ depth: Int) -> Bool {
        switch value.kind {
        case .handle:
            return !value.handleValue!.ref.isNothing
        case .list where depth < 256:
            return value.asList.contains { holdsRef($0, depth + 1) }
        case .message where depth < 256:
            return value.messageValue!.arguments.values.contains { holdsRef($0, depth + 1) }
        case .map where depth < 256, .record where depth < 256:
            let map = value.asMap!
            for index in 0..<map.length where map.handleKey(at: index).map({ !$0.ref.isNothing }) == true || holdsRef(map.value(at: index), depth + 1) { return true }
            return false
        default:
            return false
        }
    }

    // The first message argument that holds a ref, or -1.
    static func refArgument(_ message: GameEventScriptMessage) -> Int {
        let arguments = message.arguments
        for index in 0..<arguments.count where holdsRef(arguments.valueAt(index)) { return index }
        return -1
    }

    static func unresolvedRef(_ path: String) -> GesWireError { GesWireError(code: GesWireError.unresolvedRef, path: path) }

    // The runtime fault of a value with a ref that a callback hands to a script.
    static func unresolvedRefFault(_ symbol: String) -> GesRuntimeError {
        GesRuntimeError(diagnostic: .init(phase: .runtime, code: GesWireError.unresolvedRef, message: "\(symbol) returned a handle with an unresolved ref.", symbol: symbol))
    }
}
