// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Reads the state of the objects of one handle type for a host (specs/HostRuntime.md#handles). The host calls it
/// synchronously from script execution on its serial path, at most once per object and handler invocation.
public protocol GesHandleResolver {
    /// Answers one state read with exactly one of `GesHandleResolveCall.setState(_:version:)`,
    /// `GesHandleResolveCall.setDocument(_:version:)` and `GesHandleResolveCall.setMissing()`. The call must not be
    /// retained after returning.
    ///
    /// - Parameter call: The reusable call holding the handle and receiving the answer.
    /// - Throws: Any error, which ends the read with `runtime.handleResolveFailed`; a `GesFatalRuntimeError` keeps its
    ///   own diagnostic.
    func resolve(_ call: GesHandleResolveCall) throws

    /// Turns the ref a handle read from a document carries into the version it names.
    ///
    /// - Parameter handle: A handle of this resolver's type that carries a ref.
    /// - Returns: The version: an integer Number, or Bytes of 20 or 32 bytes for a hash.
    /// - Throws: Any error, which `GameEventScriptHost.resolveRef(_:)` reports as a `GesHandleError`.
    func resolveRef(_ handle: GesHandle) throws -> GesValue
}

/// One state read a host asks a `GesHandleResolver` to answer. The host reuses the instance; it is valid only during
/// the synchronous `GesHandleResolver.resolve(_:)` call.
public final class GesHandleResolveCall {
    enum AnswerKind: UInt8 {
        case none
        case state
        case document
        case missing
        case several
    }

    private var currentHandle: GesHandle?
    private var currentHandleType: GesHandleType?
    private(set) var answer = AnswerKind.none
    private(set) var record = GesValue.nothing
    private(set) var text: String?
    private(set) var version = GesValue.nothing

    init() {}

    /// The handle whose state is read, with the version it carries, if any.
    ///
    /// Reading it outside the resolver invocation is a programming error and traps.
    public var handle: GesHandle {
        guard let currentHandle else { preconditionFailure("The resolve call is only valid during its resolver invocation.") }
        return currentHandle
    }

    /// The handle type registered with the resolver, whose state schema the answer must fit.
    ///
    /// Reading it outside the resolver invocation is a programming error and traps.
    public var handleType: GesHandleType {
        guard let currentHandleType else { preconditionFailure("The resolve call is only valid during its resolver invocation.") }
        return currentHandleType
    }

    /// Answers with the state as a Record of the state schema and, optionally, the version of the object it describes.
    ///
    /// - Parameters:
    ///   - record: The state Record.
    ///   - version: The version: an integer Number for a revision, Bytes for a hash, or Nothing for none.
    /// - Throws: `GesApiError.invalidOperation` outside the resolver invocation.
    public func setState(_ record: GesValue, version: GesValue = .nothing) throws(GesApiError) { try set(.state, record, nil, version) }

    /// Answers with the state as `.gesd` document text whose schema header names the state schema and, optionally, the
    /// version of the object it describes.
    ///
    /// - Parameters:
    ///   - text: The document text.
    ///   - version: The version: an integer Number for a revision, Bytes for a hash, or Nothing for none.
    /// - Throws: `GesApiError.invalidOperation` outside the resolver invocation.
    public func setDocument(_ text: String, version: GesValue = .nothing) throws(GesApiError) { try set(.document, .nothing, text, version) }

    /// Answers that the object does not exist: its state reads give `nothing`.
    ///
    /// - Throws: `GesApiError.invalidOperation` outside the resolver invocation.
    public func setMissing() throws(GesApiError) { try set(.missing, .nothing, nil, .nothing) }

    func begin(_ handle: GesHandle, _ handleType: GesHandleType) {
        currentHandle = handle
        currentHandleType = handleType
        answer = .none
        record = .nothing
        text = nil
        version = .nothing
    }

    func end() {
        currentHandle = nil
        currentHandleType = nil
        record = .nothing
        text = nil
        version = .nothing
    }

    private func set(_ answer: AnswerKind, _ record: GesValue, _ text: String?, _ version: GesValue) throws(GesApiError) {
        guard currentHandle != nil else { throw .invalidOperation("The resolve call is only valid during its resolver invocation.") }
        self.answer = self.answer == .none ? answer : .several
        self.record = record
        self.text = text
        self.version = version
    }
}

/// Thrown by `GameEventScriptHost.resolveRef(_:)` when a ref cannot be turned into a version.
public struct GesHandleError: Error, CustomStringConvertible {
    /// The stable code, `runtime.handleResolveFailed`.
    public let code: String
    /// The handle's text form.
    public let symbol: String
    /// An explanation for people; informative text.
    public let message: String
    /// The resolver's error, if any.
    public let underlyingError: (any Error)?

    /// Creates the error.
    ///
    /// - Parameters:
    ///   - code: The stable code, such as `runtime.handleResolveFailed`.
    ///   - symbol: The handle's text form.
    ///   - message: An explanation for people; informative text.
    ///   - underlyingError: The resolver's error, if any.
    public init(code: String, symbol: String, message: String, underlyingError: (any Error)? = nil) {
        self.code = code
        self.symbol = symbol
        self.message = message
        self.underlyingError = underlyingError
    }

    /// The code and message.
    public var description: String { code + ": " + message }
}
