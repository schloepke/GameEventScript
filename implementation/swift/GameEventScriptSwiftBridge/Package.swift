// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import CompilerPluginSupport
import PackageDescription

let package = Package(
    name: "GameEventScriptSwiftBridge",
    products: [.library(name: "GameEventScriptSwiftBridge", targets: ["GameEventScriptSwiftBridge"])],
    dependencies: [
        .package(path: "../GameEventScriptRuntime"),
        .package(url: "https://github.com/swiftlang/swift-syntax.git", exact: "600.0.1"),
    ],
    targets: [
        .macro(
            name: "GameEventScriptSwiftBridgeMacros",
            dependencies: [
                .product(name: "SwiftCompilerPlugin", package: "swift-syntax"),
                .product(name: "SwiftSyntax", package: "swift-syntax"),
                .product(name: "SwiftSyntaxBuilder", package: "swift-syntax"),
                .product(name: "SwiftSyntaxMacros", package: "swift-syntax"),
            ]
        ),
        .testTarget(
            name: "GameEventScriptSwiftBridgeMacrosTests",
            dependencies: [
                "GameEventScriptSwiftBridgeMacros",
                .product(name: "SwiftSyntaxMacros", package: "swift-syntax"),
                .product(name: "SwiftSyntaxMacrosTestSupport", package: "swift-syntax"),
            ]
        ),
        .target(name: "GameEventScriptSwiftBridge", dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"), "GameEventScriptSwiftBridgeMacros"]),
        .testTarget(name: "GameEventScriptSwiftBridgeTests", dependencies: ["GameEventScriptSwiftBridge", .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")]),
    ]
)
