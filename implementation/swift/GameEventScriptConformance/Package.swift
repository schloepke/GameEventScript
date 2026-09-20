// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptConformance",
    products: [
        .library(name: "GameEventScriptConformance", targets: ["GameEventScriptConformance"]),
        .executable(name: "ges-conformance", targets: ["GameEventScriptConformanceTool"]),
    ],
    dependencies: [
        .package(path: "../GameEventScriptRuntime"), .package(path: "../GameEventScriptCompiler"),
        .package(path: "../GameEventScriptSwiftBridge"),
    ],
    targets: [
        .target(
            name: "GameEventScriptConformance",
            dependencies: [
                .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"),
                .product(name: "GameEventScriptCompiler", package: "GameEventScriptCompiler"),
            ]),
        .target(name: "ConformanceInstrumentation"),
        .executableTarget(
            name: "GameEventScriptConformanceTool",
            dependencies: ["GameEventScriptConformance", "ConformanceInstrumentation"]),
        .testTarget(
            name: "GameEventScriptConformanceTests",
            dependencies: [
                "GameEventScriptConformance",
                .product(name: "GameEventScriptSwiftBridge", package: "GameEventScriptSwiftBridge"),
            ]),
    ]
)
