// swift-tools-version: 6.0
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

let package = Package(
    name: "GameEventScriptNetworkSockets",
    platforms: [.macOS(.v10_15)],
    products: [.library(name: "GameEventScriptNetworkSockets", targets: ["GameEventScriptNetworkSockets"])],
    dependencies: [
        .package(path: "../GameEventScriptRuntime"),
        .package(path: "../GameEventScriptNetwork"),
        .package(url: "https://github.com/apple/swift-nio.git", from: "2.81.0"),
        .package(url: "https://github.com/apple/swift-nio-ssl.git", from: "2.29.0"),
    ],
    targets: [
        .target(
            name: "GameEventScriptNetworkSockets",
            dependencies: [
                .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"),
                .product(name: "GameEventScriptNetwork", package: "GameEventScriptNetwork"),
                .product(name: "NIOCore", package: "swift-nio"),
                .product(name: "NIOPosix", package: "swift-nio"),
                .product(name: "NIOWebSocket", package: "swift-nio"),
                .product(name: "NIOConcurrencyHelpers", package: "swift-nio"),
                .product(name: "NIOSSL", package: "swift-nio-ssl"),
            ]
        ),
        .testTarget(
            name: "GameEventScriptNetworkSocketsTests",
            dependencies: [
                "GameEventScriptNetworkSockets",
                .product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime"),
                .product(name: "GameEventScriptNetwork", package: "GameEventScriptNetwork"),
                .product(name: "NIOCore", package: "swift-nio"),
                .product(name: "NIOPosix", package: "swift-nio"),
                .product(name: "NIOWebSocket", package: "swift-nio"),
            ]
        ),
    ]
)
