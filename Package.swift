// swift-tools-version: 6.1
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import CompilerPluginSupport
import PackageDescription

// One versioned distribution; the local packages remain independent development entry points.
let package = Package(
    name: "GameEventScript",
    platforms: [.macOS("10.15")],
    products: [
        .library(name: "GameEventScriptSyntax", targets: ["GameEventScriptSyntax"]),
        .library(name: "GameEventScriptRuntime", targets: ["GameEventScriptRuntime"]),
        .library(name: "GameEventScriptCompiler", targets: ["GameEventScriptCompiler"]),
        .library(name: "GameEventScriptSwiftBridge", targets: ["GameEventScriptSwiftBridge"]),
        .library(name: "GameEventScriptGenerator", targets: ["GameEventScriptGenerator"]),
        .library(name: "GameEventScriptNetwork", targets: ["GameEventScriptNetwork"]),
        .library(name: "GameEventScriptNetworkSockets", targets: ["GameEventScriptNetworkSockets"]),
    ],
    dependencies: [
        .package(url: "https://github.com/swiftlang/swift-syntax.git", exact: "600.0.1"),
        .package(url: "https://github.com/apple/swift-nio.git", from: "2.81.0"),
        .package(url: "https://github.com/apple/swift-nio-ssl.git", from: "2.29.0"),
    ],
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
        .target(name: "GameEventScriptSyntax", path: "implementation/swift/GameEventScriptSyntax/Sources/GameEventScriptSyntax"),
        .target(name: "GameEventScriptRuntime", dependencies: ["GameEventScriptSyntax"], path: "implementation/swift/GameEventScriptRuntime/Sources/GameEventScriptRuntime"),
        .target(name: "GameEventScriptCompiler", dependencies: ["GameEventScriptRuntime", "GameEventScriptSyntax"], path: "implementation/swift/GameEventScriptCompiler/Sources/GameEventScriptCompiler"),
        .target(name: "GameEventScriptSwiftBridge", dependencies: ["GameEventScriptRuntime", "GameEventScriptSwiftBridgeMacros"], path: "implementation/swift/GameEventScriptSwiftBridge/Sources/GameEventScriptSwiftBridge"),
        .target(name: "GameEventScriptGenerator", dependencies: ["GameEventScriptRuntime", "GameEventScriptSyntax"], path: "implementation/swift/GameEventScriptGenerator/Sources/GameEventScriptGenerator"),
        .target(name: "GameEventScriptNetwork", dependencies: ["GameEventScriptRuntime"], path: "implementation/swift/GameEventScriptNetwork/Sources/GameEventScriptNetwork"),
        .target(
            name: "GameEventScriptNetworkSockets",
            dependencies: [
                "GameEventScriptRuntime",
                "GameEventScriptNetwork",
                .product(name: "NIOCore", package: "swift-nio"),
                .product(name: "NIOPosix", package: "swift-nio"),
                .product(name: "NIOWebSocket", package: "swift-nio"),
                .product(name: "NIOConcurrencyHelpers", package: "swift-nio"),
                .product(name: "NIOSSL", package: "swift-nio-ssl"),
            ],
            path: "implementation/swift/GameEventScriptNetworkSockets/Sources/GameEventScriptNetworkSockets"
        ),
        .testTarget(
            name: "GameEventScriptSwiftBridgeTests",
            dependencies: ["GameEventScriptSwiftBridge", "GameEventScriptRuntime", "GameEventScriptSyntax"],
            path: "implementation/swift/GameEventScriptSwiftBridge/Tests/GameEventScriptSwiftBridgeTests"
        ),
        .testTarget(
            name: "GameEventScriptSwiftBridgeMacrosTests",
            dependencies: [
                "GameEventScriptSwiftBridgeMacros",
                .product(name: "SwiftSyntaxMacros", package: "swift-syntax"),
                .product(name: "SwiftSyntaxMacrosTestSupport", package: "swift-syntax"),
            ],
            path: "implementation/swift/GameEventScriptSwiftBridge/Tests/GameEventScriptSwiftBridgeMacrosTests"
        ),
        .testTarget(
            name: "GameEventScriptSyntaxTests",
            dependencies: ["GameEventScriptSyntax"],
            path: "implementation/swift/GameEventScriptSyntax/Tests/GameEventScriptSyntaxTests"
        ),
    ]
)
