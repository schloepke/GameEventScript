// swift-tools-version: 6.1
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptNetwork",
    products: [.library(name: "GameEventScriptNetwork", targets: ["GameEventScriptNetwork"])],
    dependencies: [.package(path: "../GameEventScriptRuntime")],
    targets: [
        .target(name: "GameEventScriptNetwork", dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")]),
        .testTarget(
            name: "GameEventScriptNetworkTests",
            dependencies: ["GameEventScriptNetwork", .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")]
        ),
    ]
)
