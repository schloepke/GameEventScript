// swift-tools-version: 6.1
// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import PackageDescription

// SwiftNIO SSL does not build for WASI, so the socket transports need these platforms. Their sources check
// `canImport(NIOSSL)`, so the module stays empty, not broken, on any platform outside this list.
let socketPlatforms: [Platform] = [.macOS, .macCatalyst, .iOS, .tvOS, .watchOS, .visionOS, .linux, .windows, .android, .openbsd]

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
                .product(name: "NIOCore", package: "swift-nio", condition: .when(platforms: socketPlatforms)),
                .product(name: "NIOPosix", package: "swift-nio", condition: .when(platforms: socketPlatforms)),
                .product(name: "NIOWebSocket", package: "swift-nio", condition: .when(platforms: socketPlatforms)),
                .product(name: "NIOConcurrencyHelpers", package: "swift-nio", condition: .when(platforms: socketPlatforms)),
                .product(name: "NIOSSL", package: "swift-nio-ssl", condition: .when(platforms: socketPlatforms)),
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
