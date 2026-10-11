import AikoKit
import SwiftUI

/// The memory on the Personality page (D-278): four categories with their quotas, each opening
/// into its facts, and everything a person may do to a fact by hand.
///
/// What to show is decided in AikoKit.MemoryPage and what a click does in AikoKit.MemoryHand.
/// This view only draws; MemoryBlockModel remembers what is open. The twin of MemoryBlock.cs.
struct MemoryBlockView: View {
    @StateObject private var model = MemoryBlockModel()

    /// Facts and forms stand a step to the right of the category they belong to.
    private static let indent: CGFloat = 14

    var body: some View {
        let page = model.page

        VStack(alignment: .leading, spacing: 0) {
            head(page).padding(.bottom, 8)
            sections(page)

            if page.tooLong > 0 {
                warning(Strings.format(Strings.memoryTooLongNote, MemoryRules.maxLength))
            }
            if page.setAside > 0 {
                warning(Strings.format(Strings.memoryAsideNote, page.setAside))
            }
            if let problem = model.problem {
                warning(problem)
            }

            if model.erasing {
                eraseQuestion(page)
            } else {
                foot(page)
            }
        }
        // The model may have saved a fact since the page was built.
        .onAppear { model.reload() }
    }

    // ---- the head: the count and "Add a fact" ----

    private func head(_ page: MemoryPage) -> some View {
        HStack(spacing: 12) {
            Control.sectionLabel(Strings.format(Strings.sectionMemory, page.count, page.capacity))
            Spacer(minLength: 0)

            // While a new fact is being typed the button would open what is already open.
            if model.adding == nil {
                FlatButton(title: Strings.memoryAdd, symbol: "plus") { model.startAdding() }
            }
        }
    }

    // ---- the list ----

    private func sections(_ page: MemoryPage) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            if let category = model.adding {
                addForm(page, category)
            }

            ForEach(Array(page.sections.enumerated()), id: \.offset) { at, section in
                quotaRow(section)
                    .overlay(alignment: .top) {
                        if at > 0 || model.adding != nil { Control.hairline() }
                    }

                if model.open.contains(section.category) {
                    ForEach(section.facts, id: \.fact.id) { shown in
                        indented {
                            if shown.fact.id == model.editing {
                                editForm(shown.fact)
                            } else {
                                factRow(shown)
                            }
                        }
                    }

                    if section.category == .you {
                        indented {
                            Control.rowHint(Strings.memoryYouNote)
                                .padding(.vertical, 10)
                                .padding(.trailing, 12)
                        }
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.background)
        .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 12, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
    }

    /// A row under a category: the row and the line above it both start a step to the right.
    private func indented<Row: View>(@ViewBuilder _ row: () -> Row) -> some View {
        row()
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(alignment: .top) { Control.hairline() }
            .padding(.leading, Self.indent)
    }

    /// The name, a thin bar and the count. The whole row opens and closes its facts.
    @ViewBuilder
    private func quotaRow(_ section: MemoryPageSection) -> some View {
        let title = Self.title(section.category)
        let count = "\(section.count)/\(section.quota)"
        let row = HStack(spacing: 0) {
            Text(title)
                .font(Theme.sans(Theme.textName))
                .foregroundStyle(Theme.ink)
                .frame(maxWidth: .infinity, alignment: .leading)

            quotaBar(section).frame(width: 56, height: 4)

            Text(count)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.muted)
                .frame(width: 48, alignment: .trailing)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .contentShape(Rectangle())

        // An empty category has nothing to open. "You" opens anyway: its note lives inside.
        if section.count > 0 || section.category == .you {
            Button(action: { model.toggle(section.category) }) { row }
                .buttonStyle(.plain)
                .accessibilityLabel("\(title), \(count)")
        } else {
            row
        }
    }

    private func quotaBar(_ section: MemoryPageSection) -> some View {
        let filled = CGFloat(min(section.count, section.quota)) / CGFloat(max(section.quota, 1))

        return GeometryReader { box in
            ZStack(alignment: .leading) {
                Capsule().fill(Theme.raised)
                Capsule().fill(Theme.muted).frame(width: box.size.width * filled)
            }
        }
    }

    private func factRow(_ shown: MemoryPageFact) -> some View {
        let fact = shown.fact

        return HStack(alignment: .top, spacing: 0) {
            VStack(alignment: .leading, spacing: 0) {
                Text(fact.text)
                    .font(Theme.sans(Theme.textName))
                    .foregroundStyle(Theme.ink)
                    .fixedSize(horizontal: false, vertical: true)

                if !fact.meaning.isEmpty {
                    Control.rowHint(fact.meaning).padding(.top, 2)
                }

                if !fact.keys.isEmpty {
                    HStack(spacing: 4) {
                        ForEach(Array(fact.keys.enumerated()), id: \.offset) { _, key in
                            Chip(text: key).lineLimit(1)
                        }
                    }
                    .padding(.top, 5)
                }

                // A mark, a word and a colour: never the colour alone (DESIGN.md).
                if shown.tooLong {
                    Text("● " + Strings.format(Strings.memoryTooLong, shown.length, MemoryRules.maxLength))
                        .font(Theme.mono(Theme.textSmall))
                        .foregroundStyle(Theme.caution)
                        .padding(.top, 4)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 10)
            .padding(.trailing, 8)

            HStack(spacing: 0) {
                // A closed lock is a pinned fact, an open one is the rest (D-278).
                MemoryIconButton(
                    symbol: fact.pinned ? "lock" : "lock.open",
                    name: fact.pinned ? Strings.memoryUnpin : Strings.memoryPin,
                    colour: fact.pinned ? Theme.ink : Theme.muted) { model.pin(fact) }
                MemoryIconButton(symbol: "pencil", name: Strings.memoryEdit, colour: Theme.muted) {
                    model.startEditing(fact)
                }
                MemoryIconButton(symbol: "trash", name: Strings.remove, colour: Theme.muted) {
                    model.remove(fact)
                }
            }
            .padding(.top, 7)
            .padding(.trailing, 8)
        }
    }

    // ---- the two forms ----

    private func editForm(_ fact: MemoryFact) -> some View {
        MemoryForm(
            editing: fact,
            save: { text, meaning in model.edit(fact, text, meaning) },
            cancel: { model.stopEditing() })
            .padding(.vertical, 10)
            .padding(.trailing, 12)
    }

    private func addForm(_ page: MemoryPage, _ category: MemoryCategory) -> some View {
        let full = page.sections.first { $0.category == category }?.full ?? false

        return VStack(alignment: .leading, spacing: 0) {
            SegmentBar(
                choices: MemoryRules.categories.map { (value: $0, title: Self.tab($0)) },
                picked: category,
                pick: { model.pick($0) })
                .padding(.bottom, 8)

            MemoryForm(
                editing: nil,
                canSave: !full,
                save: { text, meaning in model.add(text, meaning) },
                cancel: { model.stopAdding() })

            if full {
                Control.rowHint(Strings.memoryFull).padding(.top, 6)
            }
        }
        .padding(.leading, 14)
        .padding(.trailing, 12)
        .padding(.vertical, 10)
    }

    // ---- the foot ----

    private func foot(_ page: MemoryPage) -> some View {
        let note = page.empty ? Strings.memoryEmpty
            : model.adding != nil ? Strings.memoryAnyLanguageNote
            : Strings.memoryEnglishNote

        return HStack(alignment: .top, spacing: 0) {
            Control.rowHint(note).padding(.trailing, 16)

            HStack(spacing: 6) {
                FlatButton(title: Strings.memoryOpenFile) { model.openFile() }

                // Nothing to erase in an empty memory, so no button that would do nothing.
                if !page.empty {
                    FlatButton(title: Strings.memoryErase, look: .ghostDanger) { model.askToErase() }
                }
            }
            .fixedSize()
        }
        .padding(.top, 10)
    }

    /// Asked in place of the buttons, not in a window of its own. It says what goes and that it
    /// does not come back, with the number, before the person presses anything red.
    private func eraseQuestion(_ page: MemoryPage) -> some View {
        HStack(spacing: 0) {
            Text(Strings.format(Strings.memoryEraseAsk, page.count))
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.trailing, 16)

            HStack(spacing: 4) {
                FlatButton(title: Strings.memoryErase, look: .danger) { model.eraseAll() }
                FlatButton(title: Strings.cancel, look: .ghost) { model.keepMemory() }
            }
            .fixedSize()
        }
        .padding(.leading, 14)
        .padding(.trailing, 12)
        .padding(.vertical, 12)
        .overlay(
            RoundedRectangle(cornerRadius: 12, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
        .padding(.top, 10)
    }

    // ---- small parts ----

    /// A line that asks for attention: a bar on the left, and the words in full colour.
    private func warning(_ text: String) -> some View {
        Text(text)
            .font(Theme.sans(Theme.textSmall))
            .foregroundStyle(Theme.ink)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.leading, 12)
            .overlay(alignment: .leading) { Rectangle().fill(Theme.caution).frame(width: 2) }
            .padding(.top, 10)
    }

    private static func title(_ category: MemoryCategory) -> String {
        switch category {
        case .you: return Strings.memoryCategoryYou
        case .work: return Strings.memoryCategoryWork
        case .projects: return Strings.memoryCategoryProjects
        case .interests: return Strings.memoryCategoryInterests
        }
    }

    private static func tab(_ category: MemoryCategory) -> String {
        switch category {
        case .you: return Strings.memoryTabYou
        case .work: return Strings.memoryTabWork
        case .projects: return Strings.memoryTabProjects
        case .interests: return Strings.memoryTabInterests
        }
    }
}

/// Two fields, Save, Cancel and the count. Save is off while there is no text or the count is
/// over the limit, so the person never types into a refusal.
private struct MemoryForm: View {
    private enum Field {
        case text
        case meaning
    }

    /// The fact being edited, or nil for a new one. Its keys count against the limit too.
    let editing: MemoryFact?
    var canSave = true
    let save: (String, String) -> Void
    let cancel: () -> Void

    @State private var text: String
    @State private var meaning: String
    @FocusState private var focus: Field?

    init(
        editing: MemoryFact?,
        canSave: Bool = true,
        save: @escaping (String, String) -> Void,
        cancel: @escaping () -> Void
    ) {
        self.editing = editing
        self.canSave = canSave
        self.save = save
        self.cancel = cancel
        _text = State(initialValue: editing?.text ?? "")
        _meaning = State(initialValue: editing?.meaning ?? "")
    }

    private var length: Int { MemoryPage.lengthOf(text, meaning, editing) }

    private var fits: Bool { MemoryRules.fits(length) }

    private var hasText: Bool { !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            field($text, Strings.memoryFactPlaceholder, .text)
            field($meaning, Strings.memoryMeaningPlaceholder, .meaning).padding(.top, 6)

            HStack(spacing: 4) {
                FlatButton(title: Strings.memorySave, look: .primary, enabled: canSave && fits && hasText) {
                    save(text, meaning)
                }
                FlatButton(title: Strings.cancel, look: .ghost, press: cancel)
                Spacer(minLength: 12)
                Text(Strings.format(Strings.memoryCount, length, MemoryRules.maxLength))
                    .font(Theme.mono(Theme.textSmall))
                    .foregroundStyle(fits ? Theme.muted : Theme.caution)
            }
            .padding(.top, 8)
        }
        .onAppear { focus = .text }
    }

    /// A field that grows with its text, with a grey line that says what goes into it while it
    /// is empty.
    private func field(_ value: Binding<String>, _ placeholder: String, _ which: Field) -> some View {
        TextField("", text: value, prompt: Text(placeholder).foregroundStyle(Theme.muted), axis: .vertical)
            .textFieldStyle(.plain)
            .font(Theme.sans(Theme.textName))
            .foregroundStyle(Theme.ink)
            .focused($focus, equals: which)
            .accessibilityLabel(placeholder)
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(Theme.raised)
            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .strokeBorder(focus == which ? Theme.inputLine : Theme.hairline, lineWidth: 1))
    }
}

/// Pin, edit, remove: a small picture with its name for a pointer that waits and for a screen
/// reader.
private struct MemoryIconButton: View {
    let symbol: String
    let name: String
    let colour: Color
    let press: () -> Void

    @State private var hovering = false

    var body: some View {
        Button(action: press) {
            Image(systemName: symbol)
                .font(.system(size: 12))
                .foregroundStyle(colour)
                .frame(width: 26, height: 26)
                .background(hovering ? Theme.raised : .clear)
                .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
        .help(name)
        .accessibilityLabel(name)
    }
}
