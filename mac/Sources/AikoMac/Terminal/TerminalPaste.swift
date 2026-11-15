import AikoKit
import AppKit
import ImageIO
import UniformTypeIdentifiers

/// What goes straight into the line of a program when there is no editor in between (D-370): a
/// picture is written as a PNG into the folder of the session and its path pasted on its own, so
/// Claude Code makes it an attachment; another file is its path in quotes; text is pasted as it is.
/// The twin of TerminalPaste.cs.
@MainActor
enum TerminalPaste {
    /// Pictures larger than this are made smaller before they go: Claude Code takes up to about
    /// 5 MB of a picture, and a screenshot of a large screen is often more as PNG.
    static let largestSide = 1800

    private static let pictureFiles: Set<String> = ["png", "jpg", "jpeg", "gif", "webp", "bmp"]

    static func isPicture(_ path: String) -> Bool {
        pictureFiles.contains((path as NSString).pathExtension.lowercased())
    }

    /// The writes for what the pasteboard holds now, or none when it holds nothing to paste.
    ///
    /// Files come first, unlike on Windows: Finder puts the icon of a copied file on the pasteboard
    /// as a picture too, and the file itself is what the person copied.
    static func fromPasteboard(_ pasteboard: NSPasteboard, picturesFolder: String) -> [String] {
        let files = files(on: pasteboard)
        if !files.isEmpty {
            return forFiles(files)
        }
        if let picture = picture(on: pasteboard), let path = save(picture, folder: picturesFolder) {
            return EditorSend.inserts("", pictures: [path])
        }
        if let text = pasteboard.string(forType: .string) {
            return EditorSend.inserts(text, pictures: [])
        }
        return []
    }

    /// Dropped or copied files: each picture on its own, the rest as paths in quotes, one paste.
    static func forFiles(_ paths: [String]) -> [String] {
        let pictures = paths.filter(isPicture)
        let others = paths.filter { !isPicture($0) }.map { $0.contains(" ") ? "\"\($0)\"" : $0 }
        return EditorSend.inserts(others.isEmpty ? "" : others.joined(separator: " ") + " ", pictures: pictures)
    }

    /// The file paths on a pasteboard: copied in Finder or dragged from it.
    static func files(on pasteboard: NSPasteboard) -> [String] {
        let urls = pasteboard.readObjects(forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL]
        return urls?.map(\.path) ?? []
    }

    /// A picture on the pasteboard: a screenshot copied with Cmd+Ctrl+Shift+4, or one copied in a
    /// browser or a viewer.
    static func picture(on pasteboard: NSPasteboard) -> CGImage? {
        for type in [NSPasteboard.PasteboardType.png, .tiff] {
            if let data = pasteboard.data(forType: type),
               let source = CGImageSourceCreateWithData(data as CFData, nil),
               let image = CGImageSourceCreateImageAtIndex(source, 0, nil) {
                return image
            }
        }
        return nil
    }

    /// A picture file, read so it can be written again into the folder of the session.
    static func load(_ path: String) -> CGImage? {
        guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil) else { return nil }
        return CGImageSourceCreateImageAtIndex(source, 0, nil)
    }

    /// The picture as a PNG in the folder, made smaller when it is large. Nil when it cannot be written.
    static func save(_ picture: CGImage, folder: String) -> String? {
        guard let sized = smaller(picture) else { return nil }
        do {
            try FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        } catch {
            return nil
        }

        let name = "picture-\(UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased().prefix(8)).png"
        let path = (folder as NSString).appendingPathComponent(name)
        guard let file = CGImageDestinationCreateWithURL(
            URL(fileURLWithPath: path) as CFURL, UTType.png.identifier as CFString, 1, nil) else { return nil }
        CGImageDestinationAddImage(file, sized, nil)
        return CGImageDestinationFinalize(file) ? path : nil
    }

    private static func smaller(_ picture: CGImage) -> CGImage? {
        let longest = max(picture.width, picture.height)
        guard longest > largestSide else { return picture }

        let scale = Double(largestSide) / Double(longest)
        let width = max(1, Int((Double(picture.width) * scale).rounded()))
        let height = max(1, Int((Double(picture.height) * scale).rounded()))
        guard let space = CGColorSpace(name: CGColorSpace.sRGB),
              let context = CGContext(
                data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0, space: space,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return nil }
        context.interpolationQuality = .high
        context.draw(picture, in: CGRect(x: 0, y: 0, width: width, height: height))
        return context.makeImage()
    }
}
