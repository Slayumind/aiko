import Foundation

/// A short reminder added to a turn when the person talks about themselves.
///
/// Without it the model starts the task and forgets to save what the message said about the
/// person. The check is a list of first person words and nothing smarter: a false alarm costs one
/// short note the model is told to ignore, and a miss costs a fact.
public enum MemoryNudge {
    public static let text =
        "Aiko memory check: this message may tell something about the user (life, how they like "
        + "to work, a project they work on, taste). If it does and the memory does not have it yet, "
        + "call `remember` before you start the task. A project in this repository is also the "
        + "user's project. If there is nothing like that, ignore this note and do not mention it."

    /// True when the message speaks in the first person: «я хочу», «у меня», "my", "I like".
    ///
    /// Windows asks a regular expression with \w; this walks the message one UTF-16 unit at a
    /// time with the same idea of a word, for the reason MemoryRules.keyMatches gives.
    public static func applies(_ prompt: String?) -> Bool {
        guard let prompt, !prompt.isEmpty else {
            return false
        }

        let message = SearchedText(prompt)
        for start in message.units.indices where start == 0 || !joinsWords(message.units[start - 1]) {
            for word in selfTalk where message.has(word, at: start) {
                let end = start + word.count
                if end == message.units.count || !joinsWords(message.units[end]) {
                    return true
                }
            }
        }

        return false
    }

    /// A hyphen counts as part of a word here, so «по-моему» and "i-node" are not first person.
    private static func joinsWords(_ unit: UInt16) -> Bool {
        unit == 0x2D || MemoryText.isWordUnit(unit)
    }

    private static let selfTalk: [[UInt16]] = [
        "я", "мне", "меня", "мой", "моя", "моё", "мои", "мы", "нам", "нас", "наш", "наша", "наше", "наши",
        "i", "i'm", "i've", "my", "we", "our", "me",
    ].map(MemoryText.folded)
}
