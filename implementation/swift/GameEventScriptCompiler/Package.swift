// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptCompiler",
    products: [.library(name: "GameEventScriptCompiler", targets: ["GameEventScriptCompiler"])],
    dependencies: [.package(path: "../GameEventScriptRuntime")],
    targets: [.target(name: "GameEventScriptCompiler", dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")])]
)
