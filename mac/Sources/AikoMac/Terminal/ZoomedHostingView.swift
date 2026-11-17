import AppKit
import Combine
import SwiftUI

/// A SwiftUI view of the terminal window at the zoom of the window (D-384). The twin of the
/// LayoutTransform the window of Windows takes.
///
/// The zoom is done by SwiftUI itself: the content is laid out in the room divided by the zoom and
/// drawn larger from the top left corner. Done by AppKit with smaller bounds, SwiftUI kept laying
/// the content out at the size of the room in points of the window, and a part of every page fell
/// outside, at the bottom or at the top.
final class ZoomedHostingView<Content: View>: NSHostingView<ZoomedContent<Content>> {
    private let zoomState: ZoomState

    var zoom: CGFloat {
        get { zoomState.value }
        set { if zoomState.value != newValue { zoomState.value = newValue } }
    }

    /// The view that takes the keys: this one.
    var host: NSView { self }

    /// Whether SwiftUI sees the room this view has in the window, for --try-terminal: the class of
    /// fault where a part of a page fell outside.
    var seesItsRoomForCheck: Bool {
        abs(zoomState.room.width - frame.width) < 1 && abs(zoomState.room.height - frame.height) < 1
    }

    init(rootView: Content) {
        let state = ZoomState()
        zoomState = state
        super.init(rootView: ZoomedContent(zoom: state, content: rootView))
        // The title of the system is a safe area for SwiftUI; the views of the window draw over it.
        safeAreaRegions = []
        // The window gives this view its size.
        sizingOptions = []
    }

    @available(*, unavailable)
    required init(rootView: ZoomedContent<Content>) {
        fatalError("made with the content alone")
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("not made from a file")
    }
}

/// The zoom one view follows.
final class ZoomState: ObservableObject {
    @Published var value: CGFloat = 1

    /// The room SwiftUI was given last, for the self test. Not published: it changes nothing on screen.
    var room: CGSize = .zero
}

struct ZoomedContent<Content: View>: View {
    @ObservedObject var zoom: ZoomState
    let content: Content

    var body: some View {
        GeometryReader { room in
            let _ = zoom.room = room.size
            content
                .frame(width: room.size.width / zoom.value, height: room.size.height / zoom.value)
                .scaleEffect(zoom.value, anchor: .topLeading)
        }
    }
}
