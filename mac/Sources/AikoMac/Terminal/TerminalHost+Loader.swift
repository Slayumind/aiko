import AikoKit
import AppKit
import SwiftUI

/// The loader in place of a terminal whose program has not drawn anything yet (D-368), the twin of
/// TerminalLoader.cs. The terminal keeps its place under it, so the size reaches the program, and
/// is shown with the first letter.
extension TerminalHost {
    func showLoader(over terminal: AikoTerminalView?) {
        guard let terminal, !terminal.drawn else {
            terminal?.alphaValue = 1
            loader?.isHidden = true
            return
        }

        terminal.alphaValue = 0
        terminal.drewFirst = { [weak self] in self?.draw() }

        let loader = self.loader ?? makeLoader()
        if loader.superview !== stage || loaderFor !== terminal {
            loader.removeFromSuperview()
            // On top of the stage: over the blocks of a command line too, which lie over the terminal.
            stage.addSubview(loader)
            NSLayoutConstraint.activate([
                loader.leadingAnchor.constraint(equalTo: terminal.leadingAnchor),
                loader.trailingAnchor.constraint(equalTo: terminal.trailingAnchor),
                loader.topAnchor.constraint(equalTo: terminal.topAnchor),
                loader.bottomAnchor.constraint(equalTo: terminal.bottomAnchor),
            ])
            loaderFor = terminal
        }
        loader.isHidden = false
    }

    private func makeLoader() -> NSView {
        let loader = zoomed(LoaderView())
        loader.translatesAutoresizingMaskIntoConstraints = false
        self.loader = loader
        return loader
    }
}

/// Three dots that light up in turn, and a word. The dots move only while the loader is on screen,
/// and stand still when the system asks for less motion.
private struct LoaderView: View {
    /// One beat: each dot lights up for a quarter of it, a fifth of a second after the one before.
    private static let beat = 1.2

    var body: some View {
        VStack(spacing: 12) {
            if Motion.reduceMotion {
                dots(at: nil)
            } else {
                TimelineView(.animation) { time in
                    dots(at: time.date.timeIntervalSinceReferenceDate)
                }
            }
            Text(Strings.terminalStarting)
                .font(Theme.sans(Theme.textNumber))
                .foregroundStyle(Theme.muted)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Theme.surface)
    }

    private func dots(at seconds: Double?) -> some View {
        HStack(spacing: 6) {
            ForEach(0..<3, id: \.self) { index in
                Circle()
                    .fill(Theme.muted)
                    .frame(width: 6, height: 6)
                    .opacity(seconds.map { Self.light($0 - Double(index) * 0.2) } ?? 0.35)
            }
        }
    }

    /// From 0.35 to full in the first quarter of the beat, back by the half, then resting.
    private static func light(_ seconds: Double) -> Double {
        let phase = (seconds.truncatingRemainder(dividingBy: beat) + beat).truncatingRemainder(dividingBy: beat) / beat
        if phase < 0.25 { return 0.35 + 0.65 * phase / 0.25 }
        if phase < 0.5 { return 1 - 0.65 * (phase - 0.25) / 0.25 }
        return 0.35
    }
}
