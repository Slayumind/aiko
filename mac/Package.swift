// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "AikoMac",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "AikoKit", targets: ["AikoKit"]),
    ],
    targets: [
        // The core: the same rules as Aiko.Core on Windows, with no AppKit and no SwiftUI.
        .target(name: "AikoKit"),
        .testTarget(name: "AikoKitTests", dependencies: ["AikoKit"]),
    ]
)
