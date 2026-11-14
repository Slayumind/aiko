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
    // SwiftTerm draws the real terminal of a session (D-360). Pinned to one version: a newer one
    // comes in as a change of its own.
    dependencies: [
        .package(url: "https://github.com/migueldeicaza/SwiftTerm", exact: "1.20.0"),
    ],
    targets: [
        // The core: the same rules as Aiko.Core on Windows, with no AppKit and no SwiftUI.
        .target(name: "AikoKit"),
        .executableTarget(name: "aiko-bridge", dependencies: ["AikoKit"]),
        .executableTarget(name: "aiko-shim", dependencies: ["AikoKit"]),
        .executableTarget(name: "AikoMac", dependencies: ["AikoKit", .product(name: "SwiftTerm", package: "SwiftTerm")]),
        .testTarget(name: "AikoKitTests", dependencies: ["AikoKit"]),
    ]
)
