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
        // whisper.cpp for voice input, the same release b5454 (1.9.5) Windows ships (D-427). A
        // universal framework with Metal inside; Scripts/make-app.sh puts it in Contents/Frameworks.
        .binaryTarget(
            name: "whisper",
            url: "https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-b5454-xcframework.zip",
            checksum: "e57f8c48933000acabc13bb913fbe82805d483a2deff692cc6738836bd92393b"),
        .executableTarget(
            name: "AikoMac",
            dependencies: ["AikoKit", "whisper", .product(name: "SwiftTerm", package: "SwiftTerm")],
            // The framework sits in Contents/Frameworks of the bundle, as in every Mac app.
            linkerSettings: [.unsafeFlags(["-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks"])]),
        .testTarget(name: "AikoKitTests", dependencies: ["AikoKit"]),
    ]
)
