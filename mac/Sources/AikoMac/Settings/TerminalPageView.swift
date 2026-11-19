import AikoKit
import SwiftUI
import UserNotifications

/// The page of the window itself: its zoom, the sound of the notices and voice input. The twin of
/// Settings/TerminalPage.xaml(.cs).
struct TerminalPageView: View {
    @ObservedObject var state: SettingsState

    /// The person refused the badges of Aiko (D-426): the count in the Dock cannot show, so the
    /// page says so and leads to the switch.
    @State private var badgesRefused = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.pageTitle(Strings.navTerminal)

            // The keys are in the hint: they are faster than the page (D-384).
            Control.sectionLabel(Strings.zoomLabel).padding(.top, 18).padding(.bottom, 4)
            Control.rowHint(Strings.zoomWhat).padding(.bottom, 8)
            SegmentBar(
                choices: WindowZoom.steps.map { ($0, Strings.format(Strings.zoomStep, $0)) },
                picked: WindowZoom.read(state.app.windowZoom)
            ) { step in
                var next = state.app
                next.windowZoom = step
                state.saveApp(next)
            }

            // The notices are counts and need no switch (D-409); only the sound of a question has
            // one (D-375).
            Control.sectionLabel(Strings.notifyLabel).padding(.top, 22).padding(.bottom, 4)
            Control.rowHint(Strings.notifyWhat).padding(.bottom, 8)
            if badgesRefused {
                HStack(spacing: 8) {
                    Text(Strings.dockBadgesOffMac)
                        .font(Theme.mono(Theme.textSmall))
                        .foregroundStyle(Theme.caution)
                    LinkButton(title: Strings.dockBadgesSettingsMac) {
                        if let page = URL(string: "x-apple.systempreferences:com.apple.Notifications-Settings.extension") {
                            NSWorkspace.shared.open(page)
                        }
                    }
                }
                .padding(.bottom, 8)
            }
            RowsBox {
                HStack(spacing: 10) {
                    LineMark(data: BarMarks.sound, tone: Theme.ink)
                        .frame(width: 14, height: 14)
                    Control.rowName(Strings.notifySound)
                    Spacer(minLength: 12)
                    AikoSwitch(isOn: state.app.notifySound) {
                        var next = state.app
                        next.notifySound = $0
                        state.saveApp(next)
                    }
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 10)
            }

            VoiceSectionView(state: state)
        }
        .task {
            badgesRefused = await Self.badgesRefusedNow()
        }
    }

    /// Reads the settings and answers in the same nonisolated function, so only a Bool crosses back.
    /// UNNotificationSettings is not Sendable, and Swift 6.1 (the CI toolchain) refuses to send it
    /// out of a nonisolated call; newer Swift allows it, so the Mac build alone does not catch this.
    nonisolated private static func badgesRefusedNow() async -> Bool {
        let settings = await UNUserNotificationCenter.current().notificationSettings()
        // Before the first question macOS answers "not determined": nothing is refused yet.
        return settings.authorizationStatus == .denied
            || (settings.authorizationStatus != .notDetermined && settings.badgeSetting == .disabled)
    }
}
