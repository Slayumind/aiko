import AikoKit
import SwiftUI

/// The page of the window itself: its zoom, the sound of the notices and voice input. The twin of
/// Settings/TerminalPage.xaml(.cs).
struct TerminalPageView: View {
    @ObservedObject var state: SettingsState

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
    }
}
