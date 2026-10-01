// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "CardGamePrototype",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "CardGameEnvironment", targets: ["CardGameEnvironment"]),
        .executable(name: "card-game-wasm", targets: ["CardGameWasm"]),
    ],
    dependencies: [
        .package(path: "../../../implementation/swift/GameEventScriptRuntime"),
        .package(path: "../../../implementation/swift/GameEventScriptCompiler"),
        .package(path: "../../../implementation/swift/GameEventScriptSyntaxHighlighter"),
    ],
    targets: [
        .target(
            name: "CardGameEnvironment",
            dependencies: [
                .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"),
                .product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler"),
            ]
        ),
        .executableTarget(
            name: "CardGameWasm",
            dependencies: ["CardGameEnvironment", .product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler"), .product(name: "GameEventScriptSyntaxHighlighter", package: "GameEventScriptSyntaxHighlighter")]
        ),
        .testTarget(name: "CardGameEnvironmentTests", dependencies: ["CardGameEnvironment"]),
    ]
)
