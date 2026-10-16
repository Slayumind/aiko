import AikoKit
import AppKit
import SwiftUI

/// What the place of the conversation draws: the feed, whether Claude Code works now, and what
/// the line under the field says. The twin of TalkView in Terminal/TalkPanel.cs.
struct TalkScreen: Equatable {
    let session: String
    let items: [FeedItem]
    let busy: Bool
    let hasOpenCard: Bool
    let mode: PermissionMode
    let model: String
    let environment: String
}

/// The column of the conversation: at most 860 points wide, in the middle. The feed itself takes
/// the whole width, so its scroll bar stands at the right edge of the window (AIKO-244).
private struct TalkColumn: ViewModifier {
    func body(content: Content) -> some View {
        content
            .padding(.horizontal, 24)
            .frame(maxWidth: 860)
            .frame(maxWidth: .infinity)
    }
}

/// The conversation with Claude Code: the feed above, the field below (D-294, D-302).
///
/// The feed is a lazy list: a row is built only when it comes into view. The twin of
/// Terminal/TalkPanel.cs.
struct TalkPanelView: View {
    let talk: TalkScreen
    @ObservedObject var state: TerminalPanelState

    /// Whether the open card is on screen. A lazy row says so when it comes and goes.
    @State private var cardInView = true

    /// Whether the last row is on screen. Only then a new row pulls the feed down: someone who
    /// scrolled up to read stays where they are, as on Windows.
    @State private var atEnd = true

    /// The height of the field and the line above it, which lie over the bottom of the feed.
    @State private var bottomHeight: CGFloat = 0

    private static let endId = "end"

    /// The feed takes the whole panel, so its scroll bar runs along the right edge from the top to
    /// the bottom. The field lies over the bottom of the feed, and an empty row of its height ends
    /// the feed, so the last row stops above the field (AIKO-244).
    var body: some View {
        ScrollViewReader { scroll in
            ZStack(alignment: .bottom) {
                ScrollView(.vertical) {
                    LazyVStack(alignment: .leading, spacing: 0) {
                        // The past from the file and the present of the process both count their items
                        // from one, so a row is known by its place in the feed.
                        ForEach(Array(talk.items.enumerated()), id: \.offset) { at, item in
                            FeedRowView(item: item, state: state)
                                .id(at)
                                .onAppear { if isOpenCard(item) { cardInView = true } }
                                .onDisappear { if isOpenCard(item) { cardInView = false } }
                        }
                        if talk.busy && !talk.hasOpenCard {
                            WorkingRow()
                        }
                        Color.clear.frame(height: bottomHeight)
                        // The end is after the room of the field: it is on screen only when the
                        // feed is scrolled all the way down.
                        Color.clear.frame(height: 1).id(Self.endId)
                            .onAppear { atEnd = true }
                            .onDisappear { atEnd = false }
                    }
                    // Room for the scroll bar, which lies over the right edge of the feed.
                    .padding(.trailing, 12)
                    .modifier(TalkColumn())
                }
                .scrollIndicators(.automatic)
                .overlay {
                    if talk.items.isEmpty {
                        Text(Strings.terminalEmptyTalk)
                            .font(Theme.sans(Theme.textName))
                            .foregroundStyle(Theme.muted)
                    }
                }
                .onChange(of: talk.items.count) { _, _ in
                    guard atEnd else { return }
                    withAnimation(nil) { scroll.scrollTo(Self.endId, anchor: .bottom) }
                }
                .onChange(of: talk.items.last) { _, _ in
                    guard atEnd else { return }
                    scroll.scrollTo(Self.endId, anchor: .bottom)
                }
                .onAppear { scroll.scrollTo(Self.endId, anchor: .bottom) }

                VStack(spacing: 0) {
                    if let at = openCard, !cardInView {
                        // In the middle: at the bottom the card would stand under the field.
                        reminder(talk.items[at]) {
                            scroll.scrollTo(at, anchor: .center)
                        }
                    }
                    TalkInputView(talk: talk, input: state.input)
                }
                // Hides the rows that go under the field. It stays inside the column, so the scroll
                // bar at the edge of the window is not covered.
                .background(Theme.background)
                .modifier(TalkColumn())
                .onGeometryChange(for: CGFloat.self) { $0.size.height } action: { bottomHeight = $0 }
            }
        }
    }

    /// Where the open card stands in the feed.
    private var openCard: Int? {
        talk.items.lastIndex { isOpenCard($0) }
    }

    private func isOpenCard(_ item: FeedItem) -> Bool {
        if case .card(_, .open) = item.content { return true }
        return false
    }

    /// "Waiting for you: edit" above the field while the open card is out of view (D-309).
    private func reminder(_ card: FeedItem, show: @escaping () -> Void) -> some View {
        var word = ""
        if case .card(let request, _) = card.content {
            word = StepWords.word(request.step.kind, request.step.label)
        }

        return HStack {
            Text(Strings.format(Strings.terminalWaitsForYou, word))
                .font(Theme.sans(Theme.textNumber))
                .foregroundStyle(Theme.caution)
            Spacer()
            FlatButton(title: Strings.terminalShow, look: .ghost, press: show)
        }
        .padding(.leading, 14)
        .padding(.trailing, 4)
        .padding(.vertical, 2)
        .background(Theme.surface)
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .padding(.top, 8)
    }
}

/// The word of a step, the one the list of sessions uses for it too.
enum StepWords {
    static func word(_ kind: StepKind, _ label: String) -> String {
        switch kind {
        case .read: Strings.terminalStepRead
        case .search: Strings.terminalStepSearch
        case .edit: Strings.terminalStepEdit
        case .run: Strings.terminalStepRun
        case .agent: Strings.terminalStepAgent
        case .web: Strings.terminalStepWeb
        case .other: label
        }
    }
}

/// The word "working" and three dots that fade to the right (D-308).
private struct WorkingRow: View {
    var body: some View {
        HStack(spacing: 3) {
            Text(Strings.terminalWorking)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .padding(.trailing, 5)
            ForEach([1.0, 0.6, 0.3], id: \.self) { opacity in
                Circle().fill(Theme.muted).frame(width: 4, height: 4).opacity(opacity)
            }
        }
        .padding(.top, 10)
        .padding(.bottom, 12)
    }
}

// MARK: - A row of the feed

private struct FeedRowView: View {
    let item: FeedItem
    @ObservedObject var state: TerminalPanelState

    var body: some View {
        switch item.content {
        case .ownerMessage(let text, let images, let queued, let attachments):
            MessageRow(text: text, images: images, queued: queued, attachments: attachments)
        case .answer(let text, let streaming):
            VStack(alignment: .leading, spacing: 0) {
                AnswerView(markdown: text)
                // A finished answer copies whole, as the markdown Claude wrote (D-303).
                if !streaming {
                    CopyAnswerButton(markdown: text)
                }
            }
        case .step(let step) where !step.line.hidden:
            StepRow(step: step, state: state)
        case .card(let request, .open):
            RequestCardView(request: request, choose: { state.chosen?(request.requestId, $0) })
        case .notice(let kind, let text):
            NoticeRow(kind: kind, text: text)
        default:
            EmptyView()
        }
    }
}

private struct CopyAnswerButton: View {
    let markdown: String

    @State private var copied = false

    var body: some View {
        FlatButton(title: copied ? Strings.copied : Strings.copy, look: .ghost) {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(markdown, forType: .string)
            copied = true
        }
        .padding(.leading, -10)
        .padding(.top, -8)
        .padding(.bottom, 4)
    }
}

/// The words of the owner: a block on the right, at most 70% of the feed wide (D-299). A queued
/// message is dimmed and says so under the block (D-308).
private struct MessageRow: View {
    let text: String
    let images: Int
    let queued: Bool
    let attachments: [AttachedImage]

    var body: some View {
        HStack {
            Spacer(minLength: 0)
            VStack(alignment: .trailing, spacing: 4) {
                VStack(alignment: .leading, spacing: 8) {
                    if !attachments.isEmpty {
                        HStack(spacing: 8) {
                            ForEach(Array(attachments.enumerated()), id: \.offset) { _, picture in
                                Picture(base64: picture.base64, side: 80)
                            }
                        }
                    } else if images > 0 {
                        Text(Strings.format(Strings.terminalPictures, images))
                            .font(Theme.mono(Theme.textTiny))
                            .foregroundStyle(Theme.muted)
                    }
                    if !text.isEmpty {
                        Text(text)
                            .font(Theme.sans(Theme.textName))
                            .foregroundStyle(Theme.ink)
                            .textSelection(.enabled)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(Theme.surface)
                .clipShape(UnevenRoundedRectangle(
                    topLeadingRadius: 14, bottomLeadingRadius: 14, bottomTrailingRadius: 4, topTrailingRadius: 14,
                    style: .continuous))
                .overlay(
                    UnevenRoundedRectangle(
                        topLeadingRadius: 14, bottomLeadingRadius: 14, bottomTrailingRadius: 4, topTrailingRadius: 14,
                        style: .continuous)
                    .strokeBorder(Theme.hairline, lineWidth: 1))
                .opacity(queued ? 0.55 : 1)

                if queued {
                    Text(Strings.terminalQueued)
                        .font(Theme.mono(Theme.textTiny))
                        .foregroundStyle(Theme.muted)
                }
            }
            .containerRelativeFrame(.horizontal, alignment: .trailing) { width, _ in width * 0.7 }
        }
        .padding(.top, 12)
        .padding(.bottom, 8)
    }
}

/// A picture from base64, cropped to a square.
struct Picture: View {
    let base64: String
    let side: CGFloat

    var body: some View {
        Group {
            if let data = Data(base64Encoded: base64), let image = NSImage(data: data) {
                Image(nsImage: image).resizable().scaledToFill()
            } else {
                Theme.raised
            }
        }
        .frame(width: side, height: side)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).strokeBorder(Theme.inputLine, lineWidth: 1))
    }
}

/// A step is one line behind a rule on the left, and opens to its result with a click (D-294,
/// D-299).
private struct StepRow: View {
    let step: FeedStep
    @ObservedObject var state: TerminalPanelState

    var body: some View {
        let failed = step.state == .failed
        let open = state.openSteps.contains(step.toolUseId)
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 0) {
                Text(StepWords.word(step.line.kind, step.line.label))
                    .font(Theme.sans(Theme.textNumber))
                    .foregroundStyle(failed ? Theme.destructive : Theme.muted)
                    .frame(minWidth: 52, alignment: .leading)
                Text(step.line.subject)
                    .font(Theme.mono(12.5))
                    .foregroundStyle(failed ? Theme.destructive : Theme.ink)
                    .lineLimit(1)
                    .truncationMode(.tail)
                    .padding(.leading, 10)
                if step.line.added > 0 || step.line.removed > 0 {
                    Text("+\(step.line.added)").font(Theme.mono(12.5)).foregroundStyle(Theme.positive).padding(.leading, 8)
                    Text("−\(step.line.removed)").font(Theme.mono(12.5)).foregroundStyle(Theme.destructive).padding(.leading, 4)
                }
                if step.state == .running {
                    Circle().strokeBorder(Theme.muted, lineWidth: 1).frame(width: 6, height: 6).padding(.leading, 8)
                }
            }
            .frame(minHeight: 24)
            .contentShape(Rectangle())
            .onTapGesture {
                guard !step.result.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
                if open { state.openSteps.remove(step.toolUseId) } else { state.openSteps.insert(step.toolUseId) }
            }

            if open {
                ScrollView(.vertical) {
                    Text(step.result.trimmingCharacters(in: .whitespacesAndNewlines))
                        .font(Theme.mono(Theme.textSmall))
                        .foregroundStyle(Theme.muted)
                        .textSelection(.enabled)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
                .frame(maxHeight: 280)
                .padding(.bottom, 8)
            }
        }
        .padding(.leading, 14)
        .overlay(alignment: .leading) { Rectangle().fill(Theme.raised).frame(width: 2) }
    }
}

private struct NoticeRow: View {
    let kind: NoticeKind
    let text: String

    var body: some View {
        let words: String = switch kind {
        case .interrupted: Strings.terminalTurnStopped
        case .compacted: Strings.terminalCompacted
        case .failed: text.isEmpty ? Strings.terminalFailed : text
        case .output: text
        case .signedOut: ""
        }
        if !words.isEmpty {
            Text(words)
                .font(kind == .output ? Theme.mono(Theme.textSmall) : Theme.sans(Theme.textName))
                .foregroundStyle(kind == .failed ? Theme.destructive : Theme.muted)
                .textSelection(.enabled)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.vertical, 4)
        }
    }
}

// MARK: - Cards

private struct RequestCardView: View {
    let request: RequestCard
    let choose: (CardChoice) -> Void

    @State private var picked: [Int: String] = [:]

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            let word = StepWords.word(request.step.kind, request.step.label)
            Text(word.prefix(1).uppercased() + word.dropFirst())
                .font(Theme.sans(Theme.textNumber, weight: .medium))
                .foregroundStyle(Theme.ink)

            switch request.kind {
            case .plan:
                AnswerView(markdown: request.plan)
            case .permission:
                let command = request.step.kind == .run ? (request.source.input["command"]?.stringValue ?? "") : ""
                let subject = command.isEmpty ? request.step.subject : command
                if !subject.isEmpty {
                    Text(subject)
                        .font(Theme.mono(Theme.textSmall))
                        .foregroundStyle(Theme.ink)
                        .textSelection(.enabled)
                        .fixedSize(horizontal: false, vertical: true)
                        .padding(.top, 6)
                }
                if let diff = EditDiff.forTool(request.source.toolName, request.source.input) {
                    DiffView(diff: diff)
                }
            case .question:
                questions
            }

            if request.kind != .question {
                HStack(spacing: 8) {
                    FlatButton(title: Strings.terminalAllow, look: .primary) { choose(.allow) }
                    switch request.second {
                    case .editsInSession:
                        FlatButton(title: Strings.terminalAllowEdits, look: .plain) { choose(.second) }
                    case .alwaysAllow:
                        FlatButton(title: Strings.terminalAllowInProject, look: .plain) { choose(.second) }
                    case .none:
                        EmptyView()
                    }
                    FlatButton(title: Strings.terminalDeny, look: .ghost) { choose(.deny("")) }
                    // The keyboard stays in the field, and the card says which keys answer it from there (D-323).
                    Text(Strings.terminalCardKeys)
                        .font(Theme.mono(Theme.textTiny))
                        .foregroundStyle(Theme.muted)
                        .padding(.leading, 4)
                }
                .padding(.top, 12)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.surface)
        .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).strokeBorder(Theme.inputLine, lineWidth: 1))
        .padding(.vertical, 8)
    }

    /// One button per option. With several questions the card waits until each has its answer.
    private var questions: some View {
        VStack(alignment: .leading, spacing: 6) {
            ForEach(Array(request.questions.enumerated()), id: \.offset) { at, question in
                Text(question.text)
                    .font(Theme.sans(Theme.textNumber))
                    .foregroundStyle(Theme.ink)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 10)
                HStack(spacing: 8) {
                    ForEach(Array(question.options.enumerated()), id: \.offset) { _, option in
                        FlatButton(title: option.label, look: .plain) {
                            picked[at] = option.label
                            if picked.count == request.questions.count {
                                choose(.answers((0..<request.questions.count).map { [picked[$0] ?? ""] }))
                            }
                        }
                        .opacity(picked[at] == nil || picked[at] == option.label ? 1 : 0.5)
                        .help(option.description)
                    }
                }
            }
            FlatButton(title: Strings.terminalDeny, look: .ghost) { choose(.deny("")) }
                .padding(.top, 10)
        }
    }
}

/// The lines an edit takes out and puts in, without numbers (D-303). A long diff scrolls.
private struct DiffView: View {
    let diff: EditDiff

    var body: some View {
        ScrollView(.vertical) {
            VStack(alignment: .leading, spacing: 0) {
                ForEach(Array(diff.lines.enumerated()), id: \.offset) { _, line in
                    let removed = line.side == .removed
                    Text((removed ? "- " : "+ ") + line.text)
                        .font(Theme.mono(Theme.textSmall))
                        .foregroundStyle(removed ? Theme.destructive : Theme.positive)
                        .fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 10)
                        .padding(.vertical, 1)
                        .background((removed ? Theme.destructive : Theme.positive).opacity(0.12))
                }
                if diff.more > 0 {
                    Text(Strings.format(Strings.terminalDiffMore, diff.more))
                        .font(Theme.mono(Theme.textTiny))
                        .foregroundStyle(Theme.muted)
                        .padding(.leading, 10)
                        .padding(.top, 6)
                }
            }
            .padding(.vertical, 6)
        }
        .frame(maxHeight: 320)
        .fixedSize(horizontal: false, vertical: true)
        .background(Theme.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).strokeBorder(Theme.hairline, lineWidth: 1))
        .padding(.top, 10)
    }
}
