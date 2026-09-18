import Foundation

/// Telling a file that holds nothing from a file that holds rubbish.
///
/// Every parser here answers a broken file with "no data", which is right for reading and wrong
/// for writing: the next save would put defaults over choices somebody may still want back. So the
/// store asks this first, and puts an unreadable file aside instead of overwriting it.
public enum JsonText {
    public static func isObject(_ json: String?) -> Bool {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return false
        }
        return JsonNode.parse(json)?.isObject ?? false
    }
}
