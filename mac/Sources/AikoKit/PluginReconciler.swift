import Foundation

public struct CliResult: Sendable, Equatable {
    public let exitCode: Int
    public let timedOut: Bool

    public init(exitCode: Int, timedOut: Bool) {
        self.exitCode = exitCode
        self.timedOut = timedOut
    }

    public var succeeded: Bool { !timedOut && exitCode == 0 }
}

/// Runs the real claude command for one account folder. The seam that keeps the core testable: the
/// app starts a process, tests answer from a script.
public protocol ClaudeCli {
    func run(configDirectory: String, arguments: [String], timeout: TimeInterval) -> CliResult
}

public enum PluginReconciler {
    /// A command source runs the bridge while installing; a minute is far more than it needs.
    public static let stepTimeout: TimeInterval = 60

    /// Runs the steps in order. Without the marketplace no install can work, so a failed
    /// marketplace step ends the run; one plugin that fails does not stop the others.
    ///
    /// With a deadline, for the uninstaller: the installer stops waiting after 30 seconds (D-205).
    /// A step never gets more time than is left, and no step starts after the deadline. The clock
    /// is a function, so a test can move time itself.
    public static func apply(
        cli: ClaudeCli,
        configDirectory: String,
        steps: [PluginStep],
        now: (() -> Date)? = nil,
        deadline: Date? = nil
    ) -> [(step: PluginStep, result: CliResult)] {
        var results: [(step: PluginStep, result: CliResult)] = []
        for step in steps {
            var left = stepTimeout
            if let deadline, let now {
                left = deadline.timeIntervalSince(now())
            }
            if left <= 0 {
                break
            }

            let result = cli.run(
                configDirectory: configDirectory,
                arguments: step.arguments,
                timeout: min(left, stepTimeout))
            results.append((step, result))

            if !result.succeeded && (step.kind == .addMarketplace || step.kind == .removeMarketplace) {
                break
            }
        }

        return results
    }
}
