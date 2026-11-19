import AikoKit
import AppKit

/// Voice input in the editor (DictationField), the twin of TerminalEditor.Voice.cs: the round mic
/// button, the wave and the words of a recording under the field, the line above it when voice
/// cannot start, and the words as they are heard.
///
/// The words are shown in the field itself while they come, read only: the ones that will not
/// change in white, the tail the recogniser may still change in grey (D-319). Windows lays a second
/// text over its field; here the field grows with the words without a second measure.
extension EditorView: DictationField {
    func showVoice(_ line: VoiceLine, _ notice: VoiceNotice) {
        voiceActive = line.word != .none
        mic.isHidden = line.button == .hidden
        placeMic()
        mic.recording = line.button == .recording
        mic.toolTip = mic.recording ? Strings.voiceStopTip : Strings.voiceButtonTip
        if !line.wave {
            wave.flatten()
        }

        wave.isHidden = !line.wave
        hint.isHidden = voiceActive
        voiceLine.isHidden = !voiceActive
        voiceWord.stringValue = switch line.word {
        case .warming: Strings.voiceWarming
        case .listening: Strings.format(Strings.voiceListening, line.time ?? "")
        case .finishing: Strings.voiceFinishing
        case .none: ""
        }
        voiceWord.textColor = line.word == .listening ? Theme.nsDestructive : Theme.nsMuted
        voiceWhere.stringValue = switch line.engine {
        case .local: Strings.voiceOnComputer
        case .openAI: "OpenAI"
        case nil: ""
        }
        voiceCancel.stringValue = line.cancelHint ? Strings.voiceCancelHint : ""

        // The line lives in the editor. In a session of Claude Code the editor is not on screen
        // until Cmd+G, so a new line opens it the way Cmd+G does, and Cmd+G closes it again.
        let newNotice = notice != .none && notice != shownNotice
        shownNotice = notice
        if newNotice && !wanted && session?.kind == .claude {
            compose()
        }
        let words: (String, String) = switch notice {
        case .turnedOff: (Strings.voiceTurnedOff, Strings.voiceOpenSettings)
        case .noMicrophone: (Strings.voiceNoMic, Strings.voiceNoMicLink)
        case .needsModel: (Strings.voiceNeedsModel, Strings.voiceOpenSettings)
        case .needsKey: (Strings.voiceNeedsKey, Strings.voiceOpenSettings)
        case .cloudFailed: (Strings.voiceCloudFailed, Strings.voiceOpenSettings)
        case .cloudNoMoney: (Strings.voiceCloudNoMoney, Strings.voiceOpenSettings)
        case .cloudBusy: (Strings.voiceCloudBusy, "")
        case .none: ("", "")
        }
        noticeBar.show(words.0, link: words.1, zoom: zoom)
        noticeBar.isHidden = notice == .none
    }

    /// The loudness of the last moment, from 0 to 1: the wave moves one bar to the left (D-319).
    func pushLevel(_ level: Double) {
        wave.push(level)
    }

    /// The words go where the caret stands; what is around it stays.
    func beginDictation() {
        choices.close()

        // In a session of Claude Code without the editor, the editor comes up only to show the
        // words; they go into the line of Claude Code at the end (D-370).
        strip = !wanted
        kept = (text.string, text.selectedRange().location)
        if strip {
            (before, after) = ("", "")
        } else {
            let around = DictationText.around(text.string, caret: text.selectedRange().location)
            (before, after) = (around.before, around.after)
        }
        dictating = true
        text.isEditable = false
        showDictation(.empty)
        if strip {
            wantedChanged?()
        }
    }

    func showDictation(_ dictation: DictationText) {
        guard dictating, let font = text.font else { return }

        let shown = NSMutableAttributedString()
        func add(_ words: String, _ colour: NSColor) {
            shown.append(NSAttributedString(string: words, attributes: [.font: font, .foregroundColor: colour]))
        }
        add(before + dictation.confirmedText, Theme.nsInk)
        if !dictation.tail.isEmpty {
            add((dictation.confirmed.isEmpty ? "" : " ") + dictation.tailText, Theme.nsMuted)
        }
        add(after, Theme.nsInk)
        text.textStorage?.setAttributedString(shown)
        text.needsDisplay = true
        refit()
    }

    /// The final words go into the field, with the caret after them; nil leaves it as it was.
    func endDictation(_ words: String?) {
        guard dictating else { return }
        dictating = false
        text.isEditable = true

        if strip {
            strip = false
            setText(kept.text, caret: kept.caret)
            wantedChanged?()
            if let words, !words.isEmpty {
                inserted?(EditorSend.inserts(words, pictures: []))
            }
            return
        }

        if let words, !words.isEmpty {
            let placed = DictationText.placed(before: before, words: words, after: after)
            setText(placed.text, caret: placed.caret)
        } else {
            setText(kept.text, caret: kept.caret)
        }
        focusText()
    }

    /// Cmd+Shift+Space, taken by the window before any field sees it (D-354).
    func pressVoice() {
        voicePressed?()
    }

    /// The link of the line above the field: the privacy page of the system for the mic, the
    /// settings of Aiko for anything else.
    func noticeLinkClicked() {
        if shownNotice == .noMicrophone {
            if let page = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone") {
                NSWorkspace.shared.open(page)
            }
        } else {
            voiceSettingsAsked?()
        }
    }
}

/// A round button with the mic, tinted red while it records (D-315).
final class MicButton: NSView {
    /// The microphone of the slayumind set, in a 24 unit box.
    private static let icon = "M12,1.5 A4,4 0 0 1 16,5.5 V10.5 A4,4 0 0 1 8,10.5 V5.5 A4,4 0 0 1 12,1.5 Z M4.5,11 A7.5,7.5 0 0 0 19.5,11 M12,18.5 V22.5 M8.5,22.5 H15.5"

    var pressed: (() -> Void)?

    var recording = false {
        didSet { needsDisplay = true }
    }

    private var hovering = false {
        didSet { needsDisplay = true }
    }

    override var isFlipped: Bool { true }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(rect: bounds, options: [.mouseEnteredAndExited, .activeInKeyWindow], owner: self))
    }

    override func mouseEntered(with event: NSEvent) { hovering = true }
    override func mouseExited(with event: NSEvent) { hovering = false }
    override func mouseDown(with event: NSEvent) {}

    override func mouseUp(with event: NSEvent) {
        if bounds.contains(convert(event.locationInWindow, from: nil)) {
            pressed?()
        }
    }

    override func resetCursorRects() {
        addCursorRect(bounds, cursor: .pointingHand)
    }

    override func draw(_ dirtyRect: NSRect) {
        let circle = NSBezierPath(ovalIn: bounds)
        if recording {
            Theme.nsDestructive.withAlphaComponent(0x33 / 255.0).setFill()
            circle.fill()
        }
        if hovering {
            Theme.nsHoverLayer.setFill()
            circle.fill()
        }

        // The 24 unit icon in a box of 15, in the middle of the button.
        let side = bounds.width * 15 / 24
        let scale = side / 24
        guard let context = NSGraphicsContext.current?.cgContext else { return }
        context.saveGState()
        context.translateBy(x: (bounds.width - side) / 2, y: (bounds.height - side) / 2)
        context.scaleBy(x: scale, y: scale)
        context.addPath(BarMarks.path(Self.icon).cgPath)
        context.setStrokeColor((recording ? Theme.nsDestructive : Theme.nsMuted).cgColor)
        context.setLineWidth(1.95)
        context.setLineCap(.round)
        context.setLineJoin(.round)
        context.strokePath()
        context.restoreGState()
    }
}

/// Fourteen red bars, 2 points wide with 2 between them; silence is a low flat line (D-319).
final class WaveView: NSView {
    private static let low: CGFloat = 2
    private static let high: CGFloat = 14
    private var levels = [Double](repeating: 0, count: 14)
    var zoom: CGFloat = 1 {
        didSet { invalidateIntrinsicContentSize() }
    }

    override var isFlipped: Bool { true }

    override var intrinsicContentSize: NSSize {
        NSSize(width: (14 * 2 + 13 * 2) * zoom, height: Self.high * zoom)
    }

    func push(_ level: Double) {
        levels.removeFirst()
        levels.append(min(max(level, 0), 1))
        needsDisplay = true
    }

    func flatten() {
        levels = [Double](repeating: 0, count: 14)
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        Theme.nsDestructive.setFill()
        for (index, level) in levels.enumerated() {
            let height = (Self.low + (Self.high - Self.low) * CGFloat(level)) * zoom
            let bar = NSRect(x: CGFloat(index) * 4 * zoom, y: (bounds.height - height) / 2, width: 2 * zoom, height: height)
            NSBezierPath(roundedRect: bar, xRadius: zoom, yRadius: zoom).fill()
        }
    }
}

/// The line above the field when voice cannot start: why, and a link to fix it. A thin orange edge
/// on the left, as on Windows.
final class NoticeBar: NSView {
    private let words = NSTextField(wrappingLabelWithString: "")
    private let link = NSTextField(labelWithString: "")
    private let edge = NSView()
    private var insets: [NSLayoutConstraint] = []
    var clicked: (() -> Void)?

    override init(frame: NSRect) {
        super.init(frame: frame)
        edge.wantsLayer = true
        edge.layer?.backgroundColor = Theme.nsCaution.cgColor
        words.textColor = Theme.nsInk
        words.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        link.textColor = Theme.nsInk
        let click = NSClickGestureRecognizer(target: self, action: #selector(linkClicked))
        link.addGestureRecognizer(click)

        for view in [edge, words, link] as [NSView] {
            view.translatesAutoresizingMaskIntoConstraints = false
            addSubview(view)
        }
        let wordsLeft = words.leadingAnchor.constraint(equalTo: edge.trailingAnchor, constant: 10)
        let linkGap = link.leadingAnchor.constraint(equalTo: words.trailingAnchor, constant: 12)
        let top = words.topAnchor.constraint(equalTo: topAnchor, constant: 4)
        let bottom = words.bottomAnchor.constraint(equalTo: bottomAnchor, constant: -4)
        let edgeWidth = edge.widthAnchor.constraint(equalToConstant: 2)
        insets = [wordsLeft, linkGap, top, bottom]
        NSLayoutConstraint.activate([
            edge.leadingAnchor.constraint(equalTo: leadingAnchor),
            edge.topAnchor.constraint(equalTo: topAnchor),
            edge.bottomAnchor.constraint(equalTo: bottomAnchor),
            edgeWidth,
            wordsLeft, linkGap, top, bottom,
            link.firstBaselineAnchor.constraint(equalTo: words.firstBaselineAnchor),
            link.trailingAnchor.constraint(lessThanOrEqualTo: trailingAnchor, constant: -10),
        ])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    func show(_ text: String, link title: String, zoom: CGFloat) {
        let font = Theme.monoFont(Theme.textBody * zoom)
        words.font = font
        words.stringValue = text
        link.attributedStringValue = NSAttributedString(string: title, attributes: [
            .font: font,
            .foregroundColor: Theme.nsInk,
            .underlineStyle: NSUnderlineStyle.single.rawValue,
        ])
        link.isHidden = title.isEmpty
        insets[0].constant = 10 * zoom
        insets[1].constant = 12 * zoom
        insets[2].constant = 4 * zoom
        insets[3].constant = -4 * zoom
    }

    override func resetCursorRects() {
        addCursorRect(link.frame, cursor: .pointingHand)
    }

    @objc private func linkClicked() {
        clicked?()
    }
}
