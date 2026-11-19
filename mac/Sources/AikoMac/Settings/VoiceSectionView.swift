import AikoKit
import SwiftUI

/// Voice input on the page of the window: the switch, the language Whisper hears, who recognises,
/// the model and the key (D-313, D-315, D-321). The voice part of Settings/TerminalPage.xaml(.cs).
/// The size of the model is on the page before anything downloads, and the line about OpenAI
/// stands next to the choice, not in the help.
struct VoiceSectionView: View {
    @ObservedObject var state: SettingsState
    @StateObject private var model = VoicePageModel()

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // The title of the row says what it is; a label over it would say the same again.
            RowsBox {
                HStack(spacing: 12) {
                    VStack(alignment: .leading, spacing: 2) {
                        Control.rowName(Strings.voiceToggle)
                        Control.rowHint(Strings.voiceToggleWhat)
                    }
                    AikoSwitch(isOn: state.app.voiceInput) { on in
                        var next = state.app
                        next.voiceInput = on
                        state.saveApp(next)
                        if on && next.voiceEngine == .local { model.startDownload() }
                    }
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 10)
            }
            .padding(.top, 22)

            // The language Whisper hears, apart from the language of the interface (D-326).
            Control.sectionLabel(Strings.voiceLanguageLabel).padding(.top, 22).padding(.bottom, 4)
            Control.rowHint(Strings.voiceLanguageWhat).padding(.bottom, 8)
            SegmentBar(
                choices: [(AikoLanguage.system, Strings.languageSystem), (.russian, Strings.languageRussian),
                          (.english, Strings.languageEnglish)],
                picked: state.app.voiceLanguage
            ) { language in
                var next = state.app
                next.voiceLanguage = language
                state.saveApp(next)
            }

            Control.sectionLabel(Strings.voiceEngineLabel).padding(.top, 22).padding(.bottom, 8)
            DotChoice(chosen: engine == .local, title: Strings.voiceLocalTitle, hint: Strings.voiceLocalWhat) {
                pick(.local)
            }
            modelBlock.padding(.leading, 24).padding(.top, 10)

            DotChoice(chosen: engine == .openAI, title: Strings.voiceCloudTitle, hint: Strings.voiceCloudWhat) {
                pick(.openAI)
            }
            .padding(.top, 18)
            if engine == .openAI {
                keyBlock.padding(.leading, 24).padding(.top, 12)
            }
        }
        .onAppear { model.follow() }
        .onDisappear { model.leave() }
    }

    /// The engine a snapshot asks for, or the one in the settings.
    private var engine: VoiceEngine { SettingsCheck.voiceEngine ?? state.app.voiceEngine }

    private func pick(_ engine: VoiceEngine) {
        var next = state.app
        next.voiceEngine = engine
        state.saveApp(next)
        if next.voiceInput && engine == .local { model.startDownload() }
        model.readKey()
    }

    // ---- the model ----

    /// One line and one button: what the model is now and the thing to do with it.
    private var modelBlock: some View {
        VStack(alignment: .leading, spacing: 0) {
            if model.running {
                GeometryReader { room in
                    ZStack(alignment: .leading) {
                        Rectangle().fill(Theme.raised)
                        Rectangle().fill(Theme.ink)
                            .frame(width: room.size.width * CGFloat(model.done) / CGFloat(VoiceModel.bytes))
                    }
                }
                .frame(height: 4)
            }
            HStack(spacing: 12) {
                Control.rowHint(model.line)
                FlatButton(title: model.action) { model.act() }
            }
            .padding(.top, 8)
        }
    }

    // ---- the key ----

    private var keyBlock: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 0) {
                Rectangle().fill(Theme.caution).frame(width: 2)
                Text(Strings.voiceHonest)
                    .font(Theme.mono(Theme.textSmall))
                    .foregroundStyle(Theme.ink)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.leading, 10)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .fixedSize(horizontal: false, vertical: true)

            HStack(spacing: 0) {
                Control.rowHint(Strings.voiceKeyLabel).fixedSize().padding(.trailing, 10)
                Group {
                    if let mask = model.keyMask {
                        Text(mask)
                            .font(Theme.mono(Theme.textTiny))
                            .foregroundStyle(Theme.ink)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } else {
                        SecureField("", text: $model.typedKey)
                            .textFieldStyle(.plain)
                            .font(Theme.mono(Theme.textTiny))
                            .foregroundStyle(Theme.ink)
                            .onSubmit { model.keyAction() }
                    }
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 5)
                .background(Theme.raised)
                .overlay(Rectangle().strokeBorder(Theme.hairline, lineWidth: 1))
                FlatButton(title: model.keyMask == nil ? Strings.voiceKeySave : Strings.voiceKeyRemove) {
                    model.keyAction()
                }
                .padding(.leading, 8)
            }
            .padding(.top, 12)

            if let problem = model.keyProblem {
                Text(problem)
                    .font(Theme.mono(Theme.textSmall))
                    .foregroundStyle(Theme.destructive)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 6)
            }
            Control.rowHint(Strings.voiceKeyWhere).padding(.top, 6)
        }
    }
}

/// A round choice with a title and a line under it, the twin of the Dot style in Controls.xaml.
struct DotChoice: View {
    let chosen: Bool
    let title: String
    let hint: String
    let pick: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            // A square with a smaller square inside when chosen, the Dot of Controls.xaml.
            ZStack {
                Rectangle().strokeBorder(Theme.hairline, lineWidth: 1)
                Rectangle().fill(Theme.ink).padding(4).opacity(chosen ? 1 : 0)
            }
            .frame(width: 16, height: 16)
            VStack(alignment: .leading, spacing: 4) {
                Text(title).font(Theme.mono(Theme.textSmall, weight: .medium)).foregroundStyle(Theme.ink)
                Control.rowHint(hint)
            }
        }
        .contentShape(Rectangle())
        .onTapGesture(perform: pick)
    }
}

/// What the voice part of the page shows of the download and the key. The download outlives the
/// page; while the page is shown it follows it.
@MainActor
final class VoicePageModel: ObservableObject {
    private static let totalMegabytes = VoiceModel.megabytes(VoiceModel.bytes)

    @Published private(set) var running = false
    @Published private(set) var done: Int64 = 0
    @Published private(set) var line = ""
    @Published private(set) var action = ""
    @Published private(set) var keyMask: String?
    @Published private(set) var keyProblem: String?
    @Published var typedKey = ""

    func follow() {
        ModelDownload.changed = { [weak self] in self?.showModel() }
        showModel()
        readKey()
    }

    func leave() {
        ModelDownload.changed = nil
    }

    func startDownload() {
        ModelDownload.start()
        showModel()
    }

    func act() {
        if ModelDownload.running {
            ModelDownload.stop()
        } else if ModelDownload.modelBytes == VoiceModel.bytes {
            ModelDownload.remove()
        } else {
            ModelDownload.start()
        }
        showModel()
    }

    private func showModel() {
        let ready = ModelDownload.modelBytes == VoiceModel.bytes
        running = ModelDownload.running
        done = ModelDownload.done
        let megabytes = VoiceModel.megabytes(done)
        let failed = ModelDownload.problem.map { $0 != "stopped" } ?? false
        line = ready ? Strings.format(Strings.voiceModelReady, Self.totalMegabytes)
            : running ? Strings.format(Strings.voiceModelDownloaded, megabytes, Self.totalMegabytes)
            : failed ? Strings.voiceModelFailed
            : done > 0 ? Strings.format(Strings.voiceModelStopped, megabytes, Self.totalMegabytes)
            : Strings.format(Strings.voiceModelWillDownload, Self.totalMegabytes)
        action = running ? Strings.voiceDownloadStop : ready ? Strings.voiceModelRemove : Strings.voiceDownloadGo
    }

    func readKey() {
        keyMask = VoiceKey.load().map(VoiceModel.maskKey)
    }

    func keyAction() {
        keyProblem = nil
        if VoiceKey.stored {
            VoiceKey.remove()
        } else if let key = VoiceModel.cleanKey(typedKey) {
            if !VoiceKey.save(key) { keyProblem = Strings.voiceKeyNotSaved }
        } else {
            keyProblem = Strings.voiceKeyBad
        }
        typedKey = ""
        readKey()
    }
}
