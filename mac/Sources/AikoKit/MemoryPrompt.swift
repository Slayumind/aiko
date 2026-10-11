import Foundation

/// What the model reads about the memory at the start of a session: the rules and the facts.
///
/// They are two texts, not one. Claude Code cuts each hook output at 10,000 characters, and the
/// rules with a full memory are longer than that together. Each fits on its own: the limits in
/// MemoryRules are sized for it, and the tests hold both under the cut.
///
/// The text is English whatever the interface language, like the persona.
public enum MemoryPrompt {
    public static let factsHeading = "# Aiko memory: facts"

    /// The day goes to the model as text, because the rules talk about "a month from now".
    public static func rules(_ today: DateOnly) -> String {
        rulesText + "\n\nToday: " + MemoryFile.written(today) + "."
    }

    /// No dates here: the model does not need them, and a line that changes every day would make
    /// the block different in every session for nothing.
    public static func facts(_ memory: MemoryFile) -> String {
        var text = factsHeading
        for category in MemoryRules.categories {
            let facts = memory.inCategory(category)
            text += "\n\n## \(MemoryRules.title(category)) (\(facts.count)/\(MemoryRules.quota(category)))"
            for line in facts.isEmpty ? ["- (empty)"] : facts.map(lineFor) {
                text += "\n" + line
            }
        }

        return text
    }

    private static func lineFor(_ fact: MemoryFact) -> String {
        let meaning = fact.meaning.isEmpty ? "" : " — \(fact.meaning)"
        let pinned = fact.pinned ? " · pinned" : ""
        let keys = fact.keys.isEmpty ? "" : " · keys: \(fact.keys.joined(separator: ", "))"

        return "- [\(fact.id)] \(fact.text)\(meaning)\(pinned)\(keys)"
    }

    /// The same text as RulesText in MemoryPrompt.cs, word for word. Change both or neither.
    private static let rulesText = """
        # Aiko memory

        You keep a small memory about the user. It is shared by all their environments and sessions.
        The current memory comes in its own block, "Aiko memory: facts"; every fact has an id.

        When to save (tool `remember`):
        - Only what will change your answers in other projects or a month from now: who the user is,
          how they like to work, their projects (one line each), their interests and taste.
        - A project is something the user works on, not something they asked about. A question
          ("how would I build X?") is not a project. One line per project, no sub-projects: refresh
          the project line instead.
        - Do not write the state or progress of work into a fact ("migration is half done"). Save
          only what stays true.
        - Write the fact and its meaning in English, whatever language the user speaks. Quote the
          user's own words as they said them when they are the evidence ("я дописала").
        - A fact with its meaning and keys is at most 200 characters. One thought per fact. If the
          tool says a fact is too long, shorten it and call again.
        - Give facts about the user, projects and interests 1-3 keys: names or terms the user would
          say when it comes up again, in their own language and spelling ("Swift", "Ember", "C#").
          Not common words like "question" or "short". A key in the user's message refreshes the
          fact by itself, so you do not need to. Pinned facts and work habits take no keys.
        - Not code details, file names, bugs, one-off tasks or anything about this repository only.
          Claude Code's own memory and the project's CLAUDE.md hold those.
        - Before adding, read the list. If the same thing is already there in other words, call
          `remember` with its id (reword it if it changed). Never add a duplicate.
        - When the user shows an existing fact again in other words (none of its keys were said),
          refresh it by id. Once per fact per session is enough.
        - Work habits have no keys, so only you keep them fresh. When the user acts on a saved work
          habit again, refresh it by id, once per session.
        - When "How to work with me" is full, the tool saves nothing and asks you to merge two close
          facts into one or to forget the one that matters least. Do that and call `remember` again.
          Do not skip the save.
        - Save silently, do not mention it in your reply. When the user asks directly
          ("remember ...", "forget ..."), do it and confirm in a few words.
        - Save only what the user said or did in this conversation. Do not copy facts from CLAUDE.md,
          docs or code: they already reach every session of that project.
        - Keep each fact in its own category. What the user knows or learns goes to `you`, not into
          the meaning of a project.
        - Pin only what the user asks to keep, and their name, how to address them, their grammatical
          gender and language. Take the grammatical gender only from the user's own words about
          themselves ("я забросил"), never from a name. Write it as how to address the user
          ("Address the user with masculine forms in Russian"): it is about the user only, your own
          forms stay feminine.

        How to use it:
        - You may use the memory freely in replies, like a colleague who remembers past talks, except
          in the places where you stay silent (files, commits, error reports and the rest).
        - If the memory has no name, form of address or grammatical gender for the user, and the user
          starts a casual talk (not a task), get to know them with 2-3 short questions. Never in the
          middle of a task.
        """
}
