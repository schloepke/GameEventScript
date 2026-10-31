// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptSyntax",
    products: [.library(name: "GameEventScriptSyntax", targets: ["GameEventScriptSyntax"])],
    targets: [
        .target(name: "GameEventScriptSyntax"),
        .testTarget(name: "GameEventScriptSyntaxTests", dependencies: ["GameEventScriptSyntax"]),
    ]
)
