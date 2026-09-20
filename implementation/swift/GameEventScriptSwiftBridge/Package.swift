// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptSwiftBridge",
    products: [.library(name: "GameEventScriptSwiftBridge", targets: ["GameEventScriptSwiftBridge"])],
    dependencies: [.package(path: "../GameEventScriptRuntime")],
    targets: [
        .target(
            name: "GameEventScriptSwiftBridge",
            dependencies: [
                .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")
            ]),
        .testTarget(
            name: "GameEventScriptSwiftBridgeTests",
            dependencies: [
                "GameEventScriptSwiftBridge",
                .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"),
            ]),
    ]
)
