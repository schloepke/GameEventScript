// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptRuntime",
    products: [.library(name: "GameEventScriptRuntime", targets: ["GameEventScriptRuntime"])],
    dependencies: [.package(path: "../GameEventScriptSyntax")],
    targets: [.target(name: "GameEventScriptRuntime", dependencies: [.product(name: "GameEventScriptSyntax", package: "GameEventScriptSyntax")])]
)
