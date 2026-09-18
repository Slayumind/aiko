// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "AikoMac",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "AikoKit", targets: ["AikoKit"]),
        // The two small programs Claude Code starts. The app ships them inside its bundle as
        // Aiko.Bridge, and the shim under claude and under the command name of every environment.
        .executable(name: "aiko-bridge", targets: ["aiko-bridge"]),
        .executable(name: "aiko-shim", targets: ["aiko-shim"]),
        // The app itself. Scripts/make-app.sh wraps it in Aiko.app with the other two inside.
        .executable(name: "AikoMac", targets: ["AikoMac"]),
    ],
    targets: [
        // The core: the same rules as Aiko.Core on Windows, with no AppKit and no SwiftUI.
        .target(name: "AikoKit"),
        .executableTarget(name: "aiko-bridge", dependencies: ["AikoKit"]),
        .executableTarget(name: "aiko-shim", dependencies: ["AikoKit"]),
        .executableTarget(name: "AikoMac", dependencies: ["AikoKit"]),
        .testTarget(name: "AikoKitTests", dependencies: ["AikoKit"]),
    ]
)
