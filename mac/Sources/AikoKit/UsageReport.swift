import Foundation

public struct ModelLimit: Sendable, Equatable {
    public let modelName: String
    public let percent: Int
    public let resetsAt: Date

    public init(modelName: String, percent: Int, resetsAt: Date) {
        self.modelName = modelName
        self.percent = percent
        self.resetsAt = resetsAt
    }
}

/// The answer of the usage API, used only in direct mode. The shape changes on their side:
/// new keys with code names appear regularly, so anything unknown is ignored on purpose.
public struct UsageReport: Sendable, Equatable {
    public let windows: [LimitWindow]
    public let model: ModelLimit?

    public init(windows: [LimitWindow], model: ModelLimit?) {
        self.windows = windows
        self.model = model
    }

    public static let empty = UsageReport(windows: [], model: nil)

    public var hasData: Bool { !windows.isEmpty || model != nil }

    public static func fromJson(_ json: String) -> UsageReport {
        if json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return empty
        }

        guard let root = JsonNode.parse(json)?.objectValue else {
            return empty
        }

        var windows: [LimitWindow] = []
        addWindow(&windows, root, "five_hour", .fiveHour)
        addWindow(&windows, root, "seven_day", .sevenDay)

        let model = readModelLimit(root)
        return windows.isEmpty && model == nil ? empty : UsageReport(windows: windows, model: model)
    }

    private static func addWindow(
        _ windows: inout [LimitWindow], _ root: JsonObject, _ name: String, _ kind: LimitKind
    ) {
        guard let window = root[name]?.objectValue,
              let percent = window["utilization"]?.doubleValue,
              let resetsAt = resetTime(window) else {
            return
        }

        windows.append(LimitWindow(kind: kind, percent: Percentage.fromDouble(percent), resetsAt: resetsAt))
    }

    /// The weekly limit of the heavy model. notchi looks for it the same way: an entry of
    /// limits[] with kind "weekly_scoped" and a model in its scope.
    private static func readModelLimit(_ root: JsonObject) -> ModelLimit? {
        guard let limits = root["limits"]?.arrayValue else { return nil }

        for limit in limits {
            guard let entry = limit.objectValue,
                  entry["kind"]?.stringValue == "weekly_scoped",
                  let percent = entry["percent"]?.doubleValue,
                  let resetsAt = resetTime(entry) else {
                continue
            }

            let name = entry["scope"]?.objectValue?["model"]?.objectValue?["display_name"]?.stringValue ?? "model"
            return ModelLimit(modelName: name, percent: Percentage.fromDouble(percent), resetsAt: resetsAt)
        }

        return nil
    }

    /// resets_at is ISO 8601 here, with microseconds and an offset: 2026-09-11T15:00:00.123456+00:00.
    /// The weekly_scoped entry comes without the fractional part, so both forms must parse.
    private static func resetTime(_ element: JsonObject) -> Date? {
        guard let text = element["resets_at"]?.stringValue else { return nil }
        return Iso8601.parse(text)?.date
    }
}
