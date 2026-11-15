import AppKit
import SwiftUI

/// A SwiftUI view of the terminal window at the zoom of the window (D-384): its bounds are its frame
/// divided by the zoom, so AppKit draws the content larger. SwiftUI draws the text again at that
/// size, so it stays sharp; a spike at 130 % showed no blur. The twin of the LayoutTransform the
/// window of Windows takes.
final class ZoomedHostingView<Content: View>: NSHostingView<Content> {
    var zoom: CGFloat = 1 {
        didSet {
            if zoom != oldValue { needsLayout = true }
        }
    }

    required init(rootView: Content) {
        super.init(rootView: rootView)
        // The title of the system is a safe area for SwiftUI; the views of the window draw over it.
        safeAreaRegions = []
        // The window gives this view its size. A size of its own would be in the smaller bounds
        // while the constraints count in the frame, and the two would undo each other for ever.
        sizingOptions = []
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }

    override func layout() {
        let size = NSSize(width: frame.width / zoom, height: frame.height / zoom)
        if abs(bounds.width - size.width) > 0.5 || abs(bounds.height - size.height) > 0.5 {
            setBoundsSize(size)
        }
        super.layout()
    }
}
