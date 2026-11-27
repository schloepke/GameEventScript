// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Reads the state of the objects of one handle type for a host (specs/HostRuntime.md#handles). The host calls it
/// synchronously from script execution on its serial path, at most once per object and handler invocation.
public protocol GesHandleResolver {
    /// Answers one state read with exactly one of `GesHandleResolveCall.setState(_:version:)`,
    /// `GesHandleResolveCall.setDocument(_:version:)`, `GesHandleResolveCall.setMissing()` and
    /// `GesHandleResolveCall.setNotVisible()`. The call must not be retained after returning.
    ///
    /// - Parameter call: The reusable call holding the handle and receiving the answer.
    /// - Throws: Any error, which ends the read with `runtime.handleResolveFailed`; a `GesFatalRuntimeError` keeps its
    ///   own diagnostic.
    func resolve(_ call: GesHandleResolveCall) throws

    /// Turns the ref a handle read from a document carries into the version it names. The default implementation is
    /// for types without refs.
    ///
    /// - Parameter handle: A handle of this resolver's type that carries a ref.
    /// - Returns: The version: an integer Number, or Bytes of 20 or 32 bytes for a hash; Nothing when no such ref
    ///   exists.
    /// - Throws: Any error, which `GameEventScriptHost.resolveRef(_:)` reports as a `GesHandleError`; a
    ///   `GesFatalRuntimeError` is rethrown unchanged.
    func resolveRef(_ handle: GesHandle) throws -> GesValue

    /// Answers the state reads of several handles of one type and projection from one List of the current message, the
    /// read handle first and the others in List order. Each call follows the rules of `resolve(_:)`. The default
    /// implementation calls `resolve(_:)` for each call in order.
    ///
    /// - Parameter batch: The reusable batch of calls; valid only during this invocation.
    /// - Throws: Any error, which ends the read of the first handle with `runtime.handleResolveFailed`; the others stay
    ///   unresolved. A `GesFatalRuntimeError` keeps its own diagnostic.
    func resolveMany(_ batch: GesHandleResolveBatch) throws
}

extension GesHandleResolver {
    /// The default for types without refs: it always throws.
    ///
    /// - Parameter handle: A handle of this resolver's type that carries a ref.
    /// - Returns: Never returns.
    /// - Throws: `GesApiError.invalidOperation`, which `GameEventScriptHost.resolveRef(_:)` reports as
    ///   `runtime.handleResolveFailed`.
    public func resolveRef(_ handle: GesHandle) throws -> GesValue { throw GesApiError.invalidOperation("@\(handle.typeName) has no refs.") }

    /// The default: calls `resolve(_:)` for each call of the batch in order.
    ///
    /// - Parameter batch: The reusable batch of calls; valid only during this invocation.
    /// - Throws: The first error of `resolve(_:)`, which ends the batch.
    public func resolveMany(_ batch: GesHandleResolveBatch) throws {
        for index in 0..<batch.count { try resolve(batch[index]) }
    }
}

/// The state reads a host asks a `GesHandleResolver` to answer together (`GesHandleResolver.resolveMany(_:)`). The host
/// reuses the instance and its calls; they are valid only during the synchronous invocation.
public final class GesHandleResolveBatch {
    private var calls: [GesHandleResolveCall] = []

    /// The number of calls, at least one during an invocation.
    public private(set) var count = 0

    init() {}

    /// A call; the first is the read that started the batch.
    ///
    /// - Parameter index: The zero-based index, less than `count`; another index is a programming error and traps.
    public subscript(_ index: Int) -> GesHandleResolveCall {
        precondition(index >= 0 && index < count, "The index is outside the batch.")
        return calls[index]
    }

    func add(_ handle: GesHandle, _ handleType: GesHandleType, _ projection: GesSchema?, _ party: Int) -> GesHandleResolveCall {
        if count == calls.count { calls.append(GesHandleResolveCall()) }
        let call = calls[count]
        count += 1
        call.begin(handle, handleType, projection, party)
        return call
    }

    func end() {
        for index in 0..<count { calls[index].end() }
        count = 0
    }
}

/// One state read a host asks a `GesHandleResolver` to answer. The host reuses the instance; it is valid only during
/// the synchronous `GesHandleResolver.resolve(_:)` call.
public final class GesHandleResolveCall {
    enum AnswerKind: UInt8 {
        case none
        case state
        case document
        case missing
        case notVisible
        case several
    }

    private var currentHandle: GesHandle?
    private var currentHandleType: GesHandleType?
    private var currentProjection: GesSchema?
    private var currentParty = 0
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

    /// The projection the script reads through, or nil for the full view. The answer may be the full state or the state
    /// of this projection or of a projection between them; the host narrows it.
    ///
    /// Reading it outside the resolver invocation is a programming error and traps.
    public var projection: GesSchema? {
        guard currentHandle != nil else { preconditionFailure("The resolve call is only valid during its resolver invocation.") }
        return currentProjection
    }

    /// The party the read is for: 0 for the host itself, otherwise the host-local number of a network party.
    ///
    /// Reading it outside the resolver invocation is a programming error and traps.
    public var party: Int {
        guard currentHandle != nil else { preconditionFailure("The resolve call is only valid during its resolver invocation.") }
        return currentParty
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

    /// Answers that the asking party may not see the object: its state reads give `nothing`, as for a missing object,
    /// and the host reports `runtime.handleNotVisible` to its observer.
    ///
    /// - Throws: `GesApiError.invalidOperation` outside the resolver invocation.
    public func setNotVisible() throws(GesApiError) { try set(.notVisible, .nothing, nil, .nothing) }

    func begin(_ handle: GesHandle, _ handleType: GesHandleType, _ projection: GesSchema?, _ party: Int) {
        currentHandle = handle
        currentHandleType = handleType
        currentProjection = projection
        currentParty = party
        answer = .none
        record = .nothing
        text = nil
        version = .nothing
    }

    func end() {
        currentHandle = nil
        currentHandleType = nil
        currentProjection = nil
        currentParty = 0
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
