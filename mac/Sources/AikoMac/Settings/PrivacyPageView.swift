import AikoKit
import AppKit
import SwiftUI

/// What leaves the computer, and the two switches that decide it. The list of fields is shown
/// whether or not the count is on: somebody has to see what they are agreeing to before they
/// agree, not after.
///
/// The twin of Settings/PrivacyPage.xaml(.cs).
struct PrivacyPageView: View {
    @ObservedObject var state: SettingsState

    @State private var idWasReset = false

    private static let privacyDoc = "https://github.com/Slayumind/aiko/blob/main/PRIVACY.md"

    /// Every field of the request, in the order it is sent. Shared with the checklist, so the
    /// consent and the page after it list exactly the same things.
    static var ledger: [(key: String, what: String)] {
        [
        ("v", Strings.sentVersion),
        ("os", Strings.sentMacOS),
        ("day", Strings.sentDayId),
        ("w", Strings.sentWeekFlag),
        ("m", Strings.sentMonthFlag),
         ("p", Strings.sentPersona)]
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.pageTitle(Strings.navPrivacy)

            Control.sectionLabel(Strings.sectionConnection).padding(.top, 18).padding(.bottom, 8)

            switchRow(Strings.checkUpdatesToggle, isOn: state.app.checkUpdates) {
                var next = state.app
                next.checkUpdates = $0
                state.saveApp(next)
            }
            Control.rowHint(Strings.checkUpdatesWhat).padding(.top, 6).padding(.trailing, 56)

            switchRow(Strings.sendStatsToggle, isOn: state.app.sendStats, set: onStats)
                .padding(.top, 14)
            Control.rowHint(Strings.sendStatsWhat).padding(.top, 6).padding(.trailing, 56)

            Control.sectionLabel(Strings.sectionWhatIsSent).padding(.top, 22).padding(.bottom, 8)
            PrivacyLedger(muted: !state.app.sendStats)

            if !state.app.sendStats {
                Control.rowHint(Strings.nothingSentYet).padding(.top, 8)
            }

            Control.sectionLabel(Strings.sectionNeverSent).padding(.top, 22).padding(.bottom, 8)
            Control.rowHint(Strings.neverSentWhat).padding(.trailing, 56)
            Control.rowHint(Strings.statsKeptFor).padding(.top, 8)

            HStack(spacing: 8) {
                FlatButton(title: Strings.resetInstallId, enabled: state.app.sendStats) {
                    InstallId.forget()
                    idWasReset = true
                }
                FlatButton(title: Strings.openPrivacyDoc) { open(Self.privacyDoc) }
                if idWasReset {
                    Control.rowHint(Strings.resetInstallIdDone).padding(.leading, 2)
                }
            }
            .padding(.top, 22)

            Control.rowHint(Strings.resetInstallIdWhat).padding(.top, 8).padding(.trailing, 56)
        }
    }

    private func switchRow(
        _ title: String, isOn: Bool, set: @escaping (Bool) -> Void
    ) -> some View {
        HStack(spacing: 12) {
            Control.rowName(title)
            Spacer(minLength: 12)
            AikoSwitch(isOn: isOn, set: set)
        }
    }

    /// Answering here counts as answering the checklist's question: somebody who found the switch
    /// themselves should not be asked about it again.
    private func onStats(_ on: Bool) {
        idWasReset = false
        var next = state.app
        next.sendStats = on
        next.privacyAsked = true
        state.saveApp(next)
    }

    private func open(_ address: String) {
        guard let url = URL(string: address) else { return }
        NSWorkspace.shared.open(url)
    }
}

/// The list of fields, always there. Off, it is dimmed: a person deciding needs to see the list
/// before they decide, and a list that only appears after the answer is a list nobody read.
struct PrivacyLedger: View {
    let muted: Bool
    var small = false

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            ForEach(PrivacyPageView.ledger, id: \.key) { field in
                HStack(alignment: .top, spacing: 0) {
                    Text(field.key)
                        .font(Theme.mono(Theme.textTiny))
                        .foregroundStyle(Theme.positive)
                        .frame(width: 42, alignment: .leading)

                    if small {
                        Control.rowNote(field.what)
                            .fixedSize(horizontal: false, vertical: true)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } else {
                        Control.rowHint(field.what)
                    }
                }
            }
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.surface)
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
        .opacity(muted ? 0.45 : 1)
    }
}
