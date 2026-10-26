import Foundation

/// Steps of one kind in a row are one line: "read ×3 · TalkInput.cs, TalkPanel.cs, +1" (D-342).
/// The line opens to the steps themselves.
public struct StepRun: Sendable, Equatable {
    /// How many names the line shows before "+N".
    public static let shownNames = 2

    /// The items of the run, each a step.
    public let items: [FeedItem]

    public var id: Int { items[0].id }

    public var steps: [FeedStep] {
        items.compactMap { item in
            if case .step(let step) = item.content { return step }
            return nil
        }
    }

    public var kind: StepKind { steps[0].line.kind }

    /// A failed step marks the whole line; then a step at work; else the run is done.
    public var state: StepState {
        if steps.contains(where: { $0.state == .failed }) { return .failed }
        if steps.contains(where: { $0.state == .running }) { return .running }
        return .done
    }

    /// The first names of the run, each once, and how many other names there are.
    public func subjects() -> (names: [String], more: Int) {
        var names: [String] = []
        for step in steps where !step.line.subject.isEmpty && !names.contains(step.line.subject) {
            names.append(step.line.subject)
        }
        return (Array(names.prefix(Self.shownNames)), max(0, names.count - Self.shownNames))
    }
}

/// One row of the feed as the window draws it: one item, or a run of steps of one kind.
public enum FeedEntry: Sendable, Equatable {
    case one(FeedItem)
    case run(StepRun)

    /// A thought of Claude. It stands open while Claude still works after it and nothing else
    /// came yet; after that it folds into one line that opens with a click.
    case thought(FeedItem, open: Bool)

    public var id: Int {
        switch self {
        case .one(let item): return item.id
        case .run(let run): return run.id
        case .thought(let item, _): return item.id
        }
    }
}

public enum FeedEntries {
    /// The feed as rows. Reading and searching come in long series, and each step of a series
    /// says little alone, so two or more of one kind in a row become one line. An edit or a
    /// command stays its own line: each one matters.
    /// busy: Claude is at work on this conversation now.
    public static func of(_ items: [FeedItem], busy: Bool) -> [FeedEntry] {
        // Messages that wait for Claude stand under the turn and do not count as what came next.
        let last = items.last { item in
            if case .ownerMessage(_, true, _) = item.content { return false }
            return true
        }
        var rows: [FeedEntry] = []
        var run: [FeedItem] = []

        func flush() {
            if run.count == 1 {
                rows.append(.one(run[0]))
            } else if run.count > 1 {
                rows.append(.run(StepRun(items: run)))
            }
            run.removeAll()
        }

        for item in items {
            if case .step(let step) = item.content, joins(step) {
                if let first = run.first, case .step(let head) = first.content, head.line.kind != step.line.kind {
                    flush()
                }
                run.append(item)
                continue
            }

            flush()
            if case .thought = item.content {
                rows.append(.thought(item, open: busy && item.id == last?.id))
            } else {
                rows.append(.one(item))
            }
        }

        flush()
        return rows
    }

    private static func joins(_ step: FeedStep) -> Bool {
        (step.line.kind == .read || step.line.kind == .search)
            && !step.line.hidden && step.answers.isEmpty && step.images.isEmpty
    }
}

/// A long output in the feed shows its first lines and a button for the rest (D-342, D-343).
public enum LongOutput {
    /// The lines shown of a long output.
    public static let shown = 10

    /// An output this long is shown whole: hiding one line behind a button saves nothing.
    public static let mostWhole = shown + 1

    /// The lines to show and how many are hidden behind the button.
    public static func cut(_ text: String) -> (text: String, hidden: Int) {
        let trimmed = trimEnd(text)
        let lines = StepLine.lines(trimmed)
        if lines.count > mostWhole {
            return (lines.prefix(shown).joined(separator: "\n"), lines.count - shown)
        }
        return (trimmed, 0)
    }

    private static func trimEnd(_ text: String) -> String {
        var end = text.endIndex
        while end > text.startIndex, text[text.index(before: end)].isWhitespace {
            end = text.index(before: end)
        }
        return String(text[..<end])
    }
}
