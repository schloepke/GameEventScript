// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import CompilerPluginSupport
import PackageDescription

// One versioned distribution; the local packages remain independent development entry points.
let package = Package(
    name: "GameEventScript",
    platforms: [.macOS(.v10_15)],
    products: [
        .library(name: "GameEventScriptSyntaxHighlighter", targets: ["GameEventScriptSyntaxHighlighter"]),
        .library(name: "GameEventScriptRuntime", targets: ["GameEventScriptRuntime"]),
        .library(name: "GameEventScriptCompiler", targets: ["GameEventScriptCompiler"]),
        .library(name: "GameEventScriptSwiftBridge", targets: ["GameEventScriptSwiftBridge"]),
    ],
    dependencies: [.package(url: "https://github.com/swiftlang/swift-syntax.git", exact: "600.0.1")],
    targets: [
        .macro(
            name: "GameEventScriptSwiftBridgeMacros",
            dependencies: [
                .product(name: "SwiftCompilerPlugin", package: "swift-syntax"),
                .product(name: "SwiftSyntax", package: "swift-syntax"),
                .product(name: "SwiftSyntaxBuilder", package: "swift-syntax"),
                .product(name: "SwiftSyntaxMacros", package: "swift-syntax"),
            ],
            path: "implementation/swift/GameEventScriptSwiftBridge/Sources/GameEventScriptSwiftBridgeMacros"
        ),
        .target(name: "GameEventScriptSyntaxHighlighter", path: "implementation/swift/GameEventScriptSyntaxHighlighter/Sources/GameEventScriptSyntaxHighlighter"),
        .target(name: "GameEventScriptRuntime", path: "implementation/swift/GameEventScriptRuntime/Sources/GameEventScriptRuntime"),
        .target(name: "GameEventScriptCompiler", dependencies: ["GameEventScriptRuntime"], path: "implementation/swift/GameEventScriptCompiler/Sources/GameEventScriptCompiler"),
        .target(name: "GameEventScriptSwiftBridge", dependencies: ["GameEventScriptRuntime", "GameEventScriptSwiftBridgeMacros"], path: "implementation/swift/GameEventScriptSwiftBridge/Sources/GameEventScriptSwiftBridge"),
    ]
)
