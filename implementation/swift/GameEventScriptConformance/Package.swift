// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptConformance",
    platforms: [.macOS("10.15.4")],
    products: [.executable(name: "ges-conformance-probe", targets: ["GameEventScriptConformanceTool"]), .executable(name: "ges-conformance-adapter", targets: ["GameEventScriptConformanceAdapter"])],
    dependencies: [.package(path: "../GameEventScriptRuntime"), .package(path: "../GameEventScriptCompiler"), .package(path: "../GameEventScriptSwiftBridge"), .package(path: "../GameEventScriptSyntax")],
    targets: [
        .target(name: "ConformanceInstrumentation"),
        .executableTarget(
            name: "GameEventScriptConformanceAdapter",
            dependencies: [
                "ConformanceInstrumentation", .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"),
                .product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler"),
            ]
        ),
        .executableTarget(
            name: "GameEventScriptConformanceTool",
            dependencies: [.product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler"), .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")]
        ),
        .testTarget(
            name: "GameEventScriptConformanceTests",
            dependencies: [
                .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"), .product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler"),
                .product(name: "GameEventScriptSwiftBridge", package: "GameEventScriptSwiftBridge"), .product(name: "GameEventScriptSyntax", package: "GameEventScriptSyntax"),
            ]
        ),
    ]
)
