// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptSyntaxHighlighter",
    products: [.library(name: "GameEventScriptSyntaxHighlighter", targets: ["GameEventScriptSyntaxHighlighter"])],
    targets: [
        .target(name: "GameEventScriptSyntaxHighlighter"),
        .testTarget(name: "GameEventScriptSyntaxHighlighterTests", dependencies: ["GameEventScriptSyntaxHighlighter"]),
    ]
)
