// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptGenerator",
    products: [.library(name: "GameEventScriptGenerator", targets: ["GameEventScriptGenerator"])],
    dependencies: [.package(path: "../GameEventScriptRuntime"), .package(path: "../GameEventScriptSyntax")],
    targets: [
        .target(
            name: "GameEventScriptGenerator",
            dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"), .product(name: "GameEventScriptSyntax", package: "GameEventScriptSyntax")]
        ),
        .testTarget(
            name: "GameEventScriptGeneratorTests",
            dependencies: ["GameEventScriptGenerator", .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")]
        ),
    ]
)
