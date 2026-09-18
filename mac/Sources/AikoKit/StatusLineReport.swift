import Foundation

public enum LimitKind: Sendable, Equatable, CaseIterable {
    case fiveHour
    case sevenDay

    /// The weekly limit of the heavy model. Direct mode only: the status line does not carry it.
    case modelWeek
}

/// Percentages arrive as doubles with noise (28.000000000000004) and, behind a spend gateway,
/// above a hundred. The card shows whole numbers and a bar cannot be longer than full.
public enum Percentage {
    public static func fromDouble(_ value: Double) -> Int {
        // Away from zero, like Math.Round with MidpointRounding.AwayFromZero, and a value too big
        // for an integer lands on the nearest end, as a saturating cast does on .NET.
        if value.isNaN { return 0 }
        let rounded = value.rounded(.toNearestOrAwayFromZero)
        if rounded <= 0 { return 0 }
        if rounded >= 100 { return 100 }
        return Int(rounded)
    }
}

public struct LimitWindow: Sendable, Equatable {
    public let kind: LimitKind
    public let percent: Int
    public let resetsAt: Date

    public init(kind: LimitKind, percent: Int, resetsAt: Date) {
        self.kind = kind
        self.percent = percent
        self.resetsAt = resetsAt
    }
}

/// What Claude Code reports to the status line: the five hour and the seven day window.
/// The model limit (Fable) is not here — it only comes from the usage API in direct mode.
public struct StatusLineReport: Sendable, Equatable {
    public let windows: [LimitWindow]

    public init(windows: [LimitWindow]) {
        self.windows = windows
    }

    public static let empty = StatusLineReport(windows: [])

    public var hasData: Bool { !windows.isEmpty }

    /// Never throws: a broken line must not break the tray. Anything unexpected reads as "no data".
    public static func fromJson(_ json: String) -> StatusLineReport {
        if json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return empty
        }

        // A byte order mark in front is not the caller's mistake to punish. Windows PowerShell 5.1
        // puts one there when it pipes text into a program, and that is the shell Claude Code falls
        // back to on a machine without Git. The parser refuses it as an invalid first character,
        // the report comes back empty, and the limits never show. Found in Windows Sandbox.
        var text = json
        while text.hasPrefix("\u{FEFF}") {
            text.removeFirst()
        }

        guard let root = JsonNode.parse(text)?.objectValue,
              let limits = root["rate_limits"]?.objectValue else {
            return empty
        }

        var windows: [LimitWindow] = []
        if !addWindow(&windows, limits, "five_hour", .fiveHour) { return empty }
        if !addWindow(&windows, limits, "seven_day", .sevenDay) { return empty }
        return windows.isEmpty ? empty : StatusLineReport(windows: windows)
    }

    public func find(_ kind: LimitKind) -> LimitWindow? {
        windows.first { $0.kind == kind }
    }

    /// False when the numbers are there but unreadable, for example a reset time with a fraction.
    /// The Windows core throws on that, and a throw out of this parser leaves no report at all.
    private static func addWindow(
        _ windows: inout [LimitWindow], _ limits: JsonObject, _ name: String, _ kind: LimitKind
    ) -> Bool {
        guard let window = limits[name]?.objectValue else { return true }

        // Both fields are needed: a percentage without a reset time cannot be counted down,
        // and a reset time without a percentage has nothing to show.
        guard let percent = window["used_percentage"]?.doubleValue,
              let resetsAt = window["resets_at"] else { return true }

        // JSON has one number type, so the seconds can arrive with a fraction. They are cut down to
        // the second instead of dropping the whole report.
        guard case .number = resetsAt, let raw = resetsAt.doubleValue else { return false }
        let seconds = raw.rounded(.down)

        windows.append(LimitWindow(
            kind: kind,
            percent: Percentage.fromDouble(percent),
            resetsAt: Date(timeIntervalSince1970: seconds)))
        return true
    }
}
