// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

// One versioned distribution; the local packages remain independent development entry points.
let package = Package(
    name: "GameEventScript",
    products: [
        .library(name: "GameEventScriptRuntime", targets: ["GameEventScriptRuntime"]),
        .library(name: "GameEventScriptCompiler", targets: ["GameEventScriptCompiler"]),
        .library(name: "GameEventScriptSwiftBridge", targets: ["GameEventScriptSwiftBridge"]),
    ],
    targets: [
        .target(name: "GameEventScriptRuntime", path: "implementation/swift/GameEventScriptRuntime/Sources/GameEventScriptRuntime"),
        .target(name: "GameEventScriptCompiler", dependencies: ["GameEventScriptRuntime"], path: "implementation/swift/GameEventScriptCompiler/Sources/GameEventScriptCompiler"),
        .target(name: "GameEventScriptSwiftBridge", dependencies: ["GameEventScriptRuntime"], path: "implementation/swift/GameEventScriptSwiftBridge/Sources/GameEventScriptSwiftBridge"),
    ]
)
