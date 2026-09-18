import Foundation

/// The little file the bridge writes and the tray reads: numbers and times, nothing else.
///
/// Claude Code hands the bridge much more than limits — the working folder, the transcript path,
/// the session id. None of that is kept: what is not written cannot leak.
public enum SnapshotFile {
    public static func toJson(_ snapshot: LimitSnapshot) -> String {
        var root = JsonObject()
        root["environment"] = .string(snapshot.environment)
        root["source"] = .string(name(snapshot.source))
        root["receivedAt"] = .string(dateText(snapshot.receivedAt))
        root["windows"] = .array(snapshot.windows.map { window in
            .object(JsonObject([
                ("kind", .string(name(window.kind))),
                ("percent", .number(String(window.percent))),
                ("resetsAt", .string(dateText(window.resetsAt))),
            ]))
        })

        if let model = snapshot.model {
            root["model"] = .object(JsonObject([
                ("name", .string(model.modelName)),
                ("percent", .number(String(model.percent))),
                ("resetsAt", .string(dateText(model.resetsAt))),
            ]))
        }

        return JsonNode.object(root).toJsonString(indented: true) + "\n"
    }

    /// The tray may read the file while the bridge is writing the next one, so anything
    /// unreadable simply means "nothing new yet".
    public static func fromJson(_ json: String?, fallbackEnvironment: String = "") -> LimitSnapshot {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let root = JsonNode.parse(json)?.objectValue else {
            return .noData(fallbackEnvironment)
        }

        let environment = root["environment"]?.stringValue ?? fallbackEnvironment
        let windows = (root["windows"]?.arrayValue ?? []).compactMap { node -> LimitWindow? in
            guard let object = node.objectValue else { return nil }
            return LimitWindow(
                kind: limitKind(object["kind"]?.stringValue) ?? .fiveHour,
                percent: Int(object["percent"]?.int64Value ?? 0),
                resetsAt: date(object["resetsAt"]?.stringValue))
        }

        guard !windows.isEmpty else {
            return .noData(environment)
        }

        var model: ModelLimit?
        if let object = root["model"]?.objectValue {
            model = ModelLimit(
                modelName: object["name"]?.stringValue ?? "model",
                percent: Int(object["percent"]?.int64Value ?? 0),
                resetsAt: date(object["resetsAt"]?.stringValue))
        }

        return LimitSnapshot(
            environment: environment,
            source: limitSource(root["source"]?.stringValue) ?? .statusLine,
            receivedAt: date(root["receivedAt"]?.stringValue),
            windows: windows,
            model: model)
    }

    // MARK: - words, not numbers: a file people may open

    private static func name(_ source: LimitSource) -> String {
        switch source {
        case .statusLine: return "StatusLine"
        case .directMode: return "DirectMode"
        }
    }

    private static func name(_ kind: LimitKind) -> String {
        switch kind {
        case .fiveHour: return "FiveHour"
        case .sevenDay: return "SevenDay"
        case .modelWeek: return "ModelWeek"
        }
    }

    private static func limitSource(_ text: String?) -> LimitSource? {
        guard let text else { return nil }
        if text.caseInsensitiveCompare("StatusLine") == .orderedSame { return .statusLine }
        if text.caseInsensitiveCompare("DirectMode") == .orderedSame { return .directMode }
        return nil
    }

    private static func limitKind(_ text: String?) -> LimitKind? {
        guard let text else { return nil }
        if text.caseInsensitiveCompare("FiveHour") == .orderedSame { return .fiveHour }
        if text.caseInsensitiveCompare("SevenDay") == .orderedSame { return .sevenDay }
        if text.caseInsensitiveCompare("ModelWeek") == .orderedSame { return .modelWeek }
        return nil
    }

    // MARK: - times

    private static func date(_ text: String?) -> Date {
        Iso8601.parse(text)?.date ?? .distantPast
    }

    /// The way System.Text.Json writes a DateTimeOffset: seconds always, the fraction only when
    /// there is one, and the offset spelled out even when it is zero.
    private static func dateText(_ date: Date) -> String {
        let text = Iso8601.roundTrip(date, offsetSeconds: 0)
        guard let dot = text.firstIndex(of: ".") else { return text }

        let fractionEnd = text.index(dot, offsetBy: 8)
        var fraction = String(text[text.index(after: dot)..<fractionEnd])
        while fraction.last == "0" { fraction.removeLast() }

        let head = String(text[text.startIndex..<dot])
        let tail = String(text[fractionEnd...])
        return fraction.isEmpty ? head + tail : head + "." + fraction + tail
    }
}
