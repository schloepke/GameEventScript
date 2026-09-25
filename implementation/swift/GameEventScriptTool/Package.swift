// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptTool",
    platforms: [.macOS("10.15.4")],
    products: [.executable(name: "ges", targets: ["GameEventScriptTool"])],
    dependencies: [.package(path: "../GameEventScriptRuntime"), .package(path: "../GameEventScriptCompiler"), .package(path: "../GameEventScriptSyntaxHighlighter")],
    targets: [
        .target(name: "TerminalSupport"),
        .executableTarget(
            name: "GameEventScriptTool",
            dependencies: [
                .product(name: "GameEventScriptSyntaxHighlighter", package: "GameEventScriptSyntaxHighlighter"), .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"),
                .product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler"), "TerminalSupport",
            ]
        ),
        .testTarget(
            name: "GameEventScriptToolTests",
            dependencies: ["GameEventScriptTool", "TerminalSupport", .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"), .product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler")]
        ),
    ]
)
