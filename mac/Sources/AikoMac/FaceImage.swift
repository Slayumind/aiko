import AikoKit
import AppKit
import SwiftUI

/// One face as a picture, for the settings window. The twin of what FaceDrawing.For hands a WPF
/// Image on Windows.
///
/// The island and the menu bar icon draw their faces straight into a view, because they animate
/// them. A face in the window never moves, so a picture is enough.
enum FaceImage {
    static func make(_ style: FaceStyle, _ face: AikoFace, _ ground: FaceGround, size: CGFloat) -> NSImage {
        let picture = FaceArt.draw(style, face, ground, size < 20)

        return NSImage(size: NSSize(width: size, height: size), flipped: false) { box in
            FacePaint.drawUpwards(picture, in: box)
            return true
        }
    }
}

/// The picture inside SwiftUI, where the rest of the page lives.
struct FaceView: View {
    let style: FaceStyle
    let face: AikoFace
    let size: CGFloat

    var body: some View {
        Image(nsImage: FaceImage.make(style, face, .dark, size: size))
            .resizable()
            .frame(width: size, height: size)
    }
}
