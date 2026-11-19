import AikoKit
import AppKit
import SwiftUI

/// The environment checklist (D-165) as a page of the settings (D-177, D-374): every item on one
/// page, the next one opens by itself on a fresh machine, and any item can be opened again to
/// change its answer. Which item is ready, blocked or done is decided in AikoKit.WizardChecklist,
/// and where it sits on the page in AikoKit.ChecklistLayout.
///
/// The twin of Settings/ChecklistPage.xaml(.cs).
struct ChecklistPageView: View {
    @ObservedObject var state: SettingsState
    @ObservedObject var model: ChecklistModel

    /// One item at a time, as the first run in the window shows it (D-391): the open item, the bar
    /// of the steps under it, a way back, a way on, and Finish once every item has an answer.
    var steps = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // One line on what Aiko is and what an environment is, before the first word about
            // either (D-421).
            Control.rowName(Strings.wizardWelcome)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.trailing, 96)
                .padding(.bottom, 14)

            // The counter sits left of the "Saved" mark the window keeps in the top right corner.
            HStack(spacing: 12) {
                Control.pageTitle(Strings.checklistTitle)
                Control.rowNote(Strings.format(
                    Strings.checklistCount, model.progress.done, model.progress.total))
            }
            .padding(.trailing, 96)
            .padding(.bottom, 14)

            ForEach(ChecklistLayout.order, id: \.self) { item in
                if !steps, let label = ChecklistLayout.labelAbove(item) {
                    Control.sectionLabel(label).padding(.top, 8).padding(.bottom, 6)
                }

                if !steps || model.opened == item {
                    ChecklistRowView(
                        title: ChecklistLayout.title(item),
                        status: ChecklistLayout.status(item, model.state(of: item), model.words),
                        state: model.state(of: item),
                        isOpen: model.opened == item,
                        step: steps,
                        press: { if !steps { model.toggle(item) } }) {
                            panel(for: item)
                        }
                }
            }

            // What happens next, shown once every item has an answer.
            Reveal(isOpen: model.canFinish) { done }

            if !steps {
                Control.hairline().padding(.top, 16).padding(.bottom, 12)
            }

            HStack(spacing: 8) {
                if steps { stepBar }
                Spacer()
                if steps {
                    if model.step(-1) != nil {
                        FlatButton(title: Strings.stepBack, look: .ghost) { model.stepBack() }
                    }
                    // Every step after the install needs Claude Code, so Next waits for it instead
                    // of leading to the last step and back (D-421).
                    if model.opened != nil && !(model.opened == .install && !model.claudeInstalled) {
                        FlatButton(title: Strings.stepNext) { model.stepNext() }
                    }
                }
                if !steps || model.canFinish {
                    FlatButton(title: Strings.finish, enabled: model.canFinish) { model.finish() }
                }
            }
            .padding(.top, steps ? 16 : 0)
        }
        .onDisappear { model.close() }
    }

    /// Where Aiko will be after Finish, and what Finish is about to change, so nothing happens
    /// behind the person's back (D-421).
    private var done: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.rowName(model.island ? Strings.wizardDoneWindowIsland : Strings.wizardDoneTray)
                .fixedSize(horizontal: false, vertical: true)
            if !model.island {
                Control.rowHint(Strings.wizardDoneMenuBarRoom).padding(.top, 8)
            }
            let summary = model.summary
            if !summary.isEmpty {
                Control.sectionLabel(Strings.wizardSummaryTitle).padding(.top, 14).padding(.bottom, 6)
                ForEach(summary, id: \.self) { line in
                    Control.rowNote(line)
                        .fixedSize(horizontal: false, vertical: true)
                        .padding(.bottom, 2)
                }
            }
        }
        .padding(.top, 14)
    }

    /// How far the steps went, as a bar of the new look (StepDots on Windows): it is filled up to
    /// the open step. Every step keeps its place on the bar, and a click there opens a step that is
    /// not locked.
    private var stepBar: some View {
        let order = ChecklistLayout.order
        let place = order.firstIndex { $0 == model.opened }
        let value = order.isEmpty ? 0 : place.map { Double($0 + 1) / Double(order.count) } ?? 1
        return LimitBar(value: value, trackHeight: 6, height: 14)
            .overlay {
                HStack(spacing: 0) {
                    ForEach(order, id: \.self) { item in
                        let itemState = model.state(of: item)
                        Color.clear
                            .contentShape(Rectangle())
                            .onTapGesture { if itemState != .locked { model.open(item) } }
                            .tip(ChecklistLayout.title(item))
                    }
                }
            }
    }

    @ViewBuilder
    private func panel(for item: ChecklistItem) -> some View {
        switch item {
        case .install: install
        case .firstAccount: firstAccount
        case .secondEnvironment: secondEnvironment
        case .projectFolders: folders
        case .access: access
        case .commands: commands
        case .meetAiko: meetAiko
        case .privacy: privacy
        case .place: place
        }
    }

    // ---- Claude Code itself ----

    private var install: some View {
        VStack(alignment: .leading, spacing: 0) {
            if model.claudeInstalled {
                Control.rowHint(Strings.installFound)
            } else {
                Control.rowHint(Strings.installLeadSessions)
                // The button runs the official command in Terminal; the copy is for whoever wants
                // to run it in a terminal of their own (D-421).
                HStack(spacing: 8) {
                    FlatButton(title: Strings.installRun) { ClaudeLauncher.runInstall() }
                    FlatButton(title: model.copied ? Strings.copied : Strings.installCopy, look: .quiet) {
                        NSPasteboard.general.clearContents()
                        NSPasteboard.general.setString(ChecklistModel.installCommand, forType: .string)
                        model.copied = true
                    }
                }
                .padding(.top, 10)

                Control.rowHint(Strings.format(Strings.installHowButtonMac, ChecklistModel.installCommand))
                    .padding(.top, 10)
                HStack(spacing: 0) {
                    Text(Strings.installFailedLead)
                        .font(Theme.mono(Theme.textSmall))
                        .foregroundStyle(Theme.muted)
                    LinkButton(title: Strings.installFailedLink) {
                        if let url = URL(string: TerminalHost.claudeInstallPage) { NSWorkspace.shared.open(url) }
                    }
                    Text(".")
                        .font(Theme.mono(Theme.textSmall))
                        .foregroundStyle(Theme.muted)
                }
                .padding(.top, 6)
                Control.rowName(Strings.waitInstall).padding(.top, 8)
            }
        }
    }

    // ---- environment 1 ----

    private var firstAccount: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.rowHint(Strings.env1Lead)

            if model.firstSignedIn {
                Control.rowName(Strings.env1Found).padding(.top, 10)
                Control.rowNote(model.firstAccountLine).padding(.top, 4)
                Control.sectionLabel(Strings.nameInAiko).padding(.top, 10).padding(.bottom, 4)
                NameField(text: $model.firstName, onCommit: model.fillCommandFields)
            } else {
                Control.rowName(Strings.env1Empty).padding(.top, 10)
                FlatButton(title: Strings.signIn, look: .quiet) { model.signInFirst() }
                    .padding(.top, 10)
                if let problem = model.firstProblem {
                    Control.rowHint(problem).padding(.top, 10)
                } else if model.firstWaiting {
                    Control.rowHint(Strings.loginHow).padding(.top, 10)
                }
                Control.rowNote(Strings.loginNever)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 8)
            }
        }
    }

    // ---- environment 2 ----

    private var secondEnvironment: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.rowHint(model.candidates.isEmpty ? Strings.env2LeadNew : Strings.env2LeadFound)

            VStack(spacing: 6) {
                ForEach(model.candidates, id: \.fullPath) { folder in
                    PickCard(
                        chosen: model.secondFolder.map { RealClaude.sameFolder($0, folder.fullPath) } ?? false,
                        pick: {
                            model.choose(second: folder.fullPath, named: model.suggestedName(for: folder))
                            model.moveOn()
                        }) {
                            VStack(alignment: .leading, spacing: 2) {
                                Control.rowName(model.suggestedName(for: folder))
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                Control.rowNote(facts(of: folder))
                                    .fixedSize(horizontal: false, vertical: true)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                        }
                }
            }
            .padding(.top, 10)

            PickCard(chosen: model.creating, pick: { model.creating = true }) {
                Control.rowName(Strings.createNew).frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(.top, model.candidates.isEmpty ? 10 : 0)

            Reveal(isOpen: model.creating) {
                VStack(alignment: .leading, spacing: 0) {
                    NameField(text: $model.newName, placeholder: Strings.nameExample)
                    Control.rowNote(model.newFolderNote).padding(.top, 4)
                    FlatButton(title: Strings.createSignIn, look: .quiet) { model.createSecond() }
                        .padding(.top, 10)
                }
                .padding(.top, 10)
            }

            if let problem = model.secondProblem {
                Control.rowHint(problem).padding(.top, 10)
            } else if model.secondWaiting {
                Control.rowHint(Strings.loginHow).padding(.top, 10)
            }

            if model.secondFolder != nil {
                Control.sectionLabel(Strings.nameInAiko).padding(.top, 10).padding(.bottom, 4)
                NameField(text: $model.secondName, onCommit: model.fillCommandFields)
            }

            HStack(spacing: 12) {
                Control.rowNote(Strings.env2Later)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 12)
                FlatButton(title: Strings.later, look: .quiet) { model.skipSecond() }
            }
            .padding(.top, 12)
        }
    }

    private func facts(of folder: ClaudeFolder) -> String {
        var facts = [folder.folderName]
        let plan = Store.account(in: folder.fullPath).planLabel
        if !plan.isEmpty { facts.append(plan) }

        if let used = folder.lastUsed, Date().timeIntervalSince(used) > EnvironmentScan.recentlyUsed {
            let when = DateFormatter()
            when.dateFormat = "d MMMM"
            facts.append(Strings.format(Strings.lastSession, when.string(from: used)))
        }

        return facts.joined(separator: " · ")
    }

    // ---- project folders ----

    private var folders: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.rowHint(Strings.format(Strings.foldersLead, model.secondNameText))

            VStack(spacing: 6) {
                ForEach(model.projectFolders, id: \.self) { path in
                    HStack(spacing: 12) {
                        Control.rowNote(path).lineLimit(1).truncationMode(.head)
                        Spacer(minLength: 12)
                        FlatButton(title: Strings.remove, look: .quiet) {
                            model.projectFolders.removeAll { $0 == path }
                        }
                    }
                }
            }
            .padding(.top, 10)

            if model.projectFolders.isEmpty {
                Control.rowNote(Strings.noBinds).fixedSize(horizontal: false, vertical: true)
            }

            FlatButton(title: Strings.addProject, look: .quiet) { model.addProject() }
                .padding(.top, 10)

            // The same switch as on the page of the environment (D-421): on, every other folder
            // opens in environment 2.
            HStack(spacing: 12) {
                Control.rowName(Strings.defaultForRest)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 12)
                AikoSwitch(isOn: model.defaultIndex == 1) { model.defaultIndex = $0 ? 1 : 0 }
            }
            .padding(.top, 14)

            if !model.explicitWins.isEmpty {
                Control.rowHint(model.explicitWins).padding(.top, 10)
            }
        }
    }

    // ---- access ----

    private var access: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.rowHint(Strings.wizardAccessExplain)
            Control.sectionLabel(Strings.wizardTheLine).padding(.top, 12).padding(.bottom, 6)
            CodeBlock(text: model.accessLine)
            Control.sectionLabel(Strings.wizardTheFiles).padding(.top, 12).padding(.bottom, 6)
            ForEach(model.filesToChange, id: \.self) { path in
                Control.rowNote(path).padding(.bottom, 4)
            }
            Control.rowHint(Strings.wizardBackupNote).padding(.top, 8)

            if let result = model.accessResult {
                Control.rowHint(result).padding(.top, 8)
            }

            HStack(spacing: 8) {
                Spacer()
                FlatButton(title: Strings.notNow, look: .quiet) {
                    model.accessGranted = false
                    model.moveOn()
                }
                FlatButton(title: Strings.accessAdd, look: .quiet) { model.grantAccess() }
            }
            .padding(.top, 12)
        }
    }

    // ---- commands ----

    private var commands: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.rowHint(Strings.cmdLeadMac)

            ForEach(0..<model.commandCount, id: \.self) { index in
                commandField(index)
            }

            // The consent before Aiko touches PATH, the same shape as the settings.json consent
            // above: the exact line, the exact file, and what putting Aiko away would undo.
            Control.sectionLabel(Strings.pathLineMac).padding(.top, 14).padding(.bottom, 6)
            CodeBlock(text: CommandFolder.exportLine)
            Control.sectionLabel(Strings.wizardTheFiles).padding(.top, 12).padding(.bottom, 6)
            Control.rowNote(CommandFolder.profilePath)
            Control.rowHint(Strings.pathNoteMac).padding(.top, 10)

            HStack(spacing: 8) {
                Spacer()
                FlatButton(title: Strings.noCommands, look: .quiet) {
                    model.commandsWanted = false
                    model.moveOn()
                }
                FlatButton(title: Strings.pathAdd, look: .quiet, enabled: model.commandsAreGood) {
                    model.commandsWanted = true
                    model.moveOn()
                }
            }
            .padding(.top, 12)
        }
    }

    private func commandField(_ index: Int) -> some View {
        let names = [model.firstNameText, model.secondNameText]
        let problem = model.problem(index)

        return VStack(alignment: .leading, spacing: 0) {
            Control.sectionLabel(Strings.format(Strings.cmdFor, names[index]))
                .padding(.top, 12).padding(.bottom, 4)

            NameField(text: $model.commandText[index],
                      onCommit: { model.commandTyped(index) })

            HStack(alignment: .top, spacing: 8) {
                Text(words(problem, followsTheName: model.followsTheName(index)))
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(problem == .none ? Theme.muted : Theme.caution)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 8)
                if !model.followsTheName(index) {
                    FlatButton(title: Strings.cmdByName, look: .quiet) { model.useTheName(index) }
                }
            }
            .padding(.top, 4)
        }
    }

    private func words(_ problem: CommandProblem, followsTheName: Bool) -> String {
        switch problem {
        case .none: return followsTheName ? Strings.cmdFollows : Strings.cmdOwn
        case .empty: return Strings.cmdEmpty
        case .tooLong: return Strings.cmdTooLong
        case .badCharacters: return Strings.cmdBadCharacters
        case .reserved: return Strings.cmdReserved
        case .taken: return Strings.cmdTaken
        }
    }

    // ---- meet Aiko ----

    /// Optional: the persona can wait, and the personality page has everything else about it.
    private var meetAiko: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .top, spacing: 12) {
                FaceView(face: .fresh, size: 44)
                VStack(alignment: .leading, spacing: 8) {
                    Control.rowHint(Strings.meetAikoLead)
                    Control.rowNote(Strings.meetAikoSettings)
                        .fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }

            HStack(spacing: 8) {
                Spacer()
                FlatButton(title: Strings.later, look: .quiet) {
                    model.personaWanted = false
                    model.moveOn()
                }
                if model.secondSignedIn {
                    FlatButton(title: Strings.format(Strings.meetAikoTurnOn, model.secondNameText), look: .quiet) {
                        model.personaWanted = true
                        model.personaSecond = true
                        model.moveOn()
                    }
                }
                FlatButton(title: Strings.format(Strings.meetAikoTurnOn, model.firstNameText), look: .quiet) {
                    model.personaWanted = true
                    model.personaSecond = false
                    model.moveOn()
                }
            }
            .padding(.top, 12)
        }
    }

    // ---- statistics ----

    /// Consent, so it needs a real answer rather than a default. The list of fields is shown before
    /// the buttons: agreeing to "statistics" without seeing them is not agreeing.
    private var privacy: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.rowHint(Strings.wizardStatsWhat)
            PrivacyLedger(muted: false, small: true).padding(.top, 10)
            Control.sectionLabel(Strings.sectionNeverSent).padding(.top, 14).padding(.bottom, 6)
            Control.rowHint(Strings.neverSentWhat)
            Control.rowHint(Strings.wizardStatsLater).padding(.top, 6)

            HStack(spacing: 8) {
                Spacer()
                FlatButton(title: Strings.statsDecline, look: .quiet) {
                    model.statsWanted = false
                    model.moveOn()
                }
                FlatButton(title: Strings.statsAccept, look: .quiet) {
                    model.statsWanted = true
                    model.moveOn()
                }
            }
            .padding(.top, 12)
        }
    }

    // ---- where Aiko shows ----

    /// The menu bar item is the default here and the island is the equal second choice (D-250), so
    /// the two cards are the same size and neither is picked for the person in advance.
    private var place: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 10) {
                PickCard(chosen: !model.island, pick: { model.island = false }) {
                    VStack(spacing: 8) {
                        ScreenPicture(show: .menuBar, height: 40)
                        Control.rowName(Strings.placeMenuBar)
                    }
                }
                PickCard(chosen: model.island, pick: { model.island = true }) {
                    VStack(spacing: 8) {
                        ScreenPicture(show: .island, height: 40)
                        Control.rowName(Strings.placeIsland)
                    }
                }
            }

            HStack(spacing: 12) {
                Control.rowName(Strings.startAtLogin)
                Spacer(minLength: 12)
                AikoSwitch(isOn: model.runAtStartup) { model.runAtStartup = $0 }
            }
            .padding(.top, 14)

            HStack(alignment: .top, spacing: 12) {
                VStack(alignment: .leading, spacing: 2) {
                    Control.rowName(Strings.checkUpdatesToggle)
                    Control.rowNote(model.updatesWhat)
                        .fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
                Spacer(minLength: 12)
                AikoSwitch(isOn: model.checkUpdates) { model.checkUpdates = $0 }
            }
            .padding(.top, 10)
        }
    }
}

/// One item of the checklist: a header that says what it is and how far along, and a body that
/// unfolds under it. The twin of ChecklistRow.cs.
struct ChecklistRowView<Content: View>: View {
    let title: String
    let status: String
    let state: ItemState
    let isOpen: Bool

    /// One item at a time: the header has no chevron and no click, the body is always open.
    var step = false
    let press: () -> Void
    @ViewBuilder let content: () -> Content

    @State private var hovering = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Reveal(isOpen: isOpen) {
                content().padding(.leading, 26).padding(.top, 10).padding(.bottom, 2)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .background(hovering && state != .locked && !step ? Theme.hoverLayer : .clear)
        .overlay(
            Rectangle()
                .strokeBorder(Theme.hairline, lineWidth: 1))
        .opacity(state == .locked ? 0.55 : 1)
        .padding(.bottom, 6)
    }

    private var header: some View {
        HStack(spacing: 0) {
            mark.frame(width: 16, height: 16)

            Text(title)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .lineLimit(1)
                .truncationMode(.tail)
                .padding(.leading, 10)
                .padding(.trailing, 8)

            Spacer(minLength: 8)

            Text(status)
                .font(Theme.mono(Theme.textTiny))
                .foregroundStyle(Theme.muted)
                .lineLimit(1)

            if !step {
                PathMark(data: PathMark.chevron, box: CGSize(width: 8, height: 5))
                    .rotationEffect(.degrees(isOpen ? 180 : 0))
                    .animation(Motion.curve(Motion.standard, Motion.expand), value: isOpen)
                    .padding(.leading, 10)
            }
        }
        .frame(minHeight: 22)
        .contentShape(Rectangle())
        .onHover { hovering = $0 }
        .onTapGesture { if state != .locked { press() } }
    }

    @ViewBuilder
    private var mark: some View {
        switch state {
        case .done:
            ZStack {
                Circle().fill(Theme.positive)
                PathMark(data: "M4.5,8.2 L7,10.6 L11.6,5.6", box: CGSize(width: 16, height: 16), lineWidth: 1.8, tone: Theme.background)
            }
        case .skipped:
            ZStack {
                Circle().strokeBorder(Theme.hairline, lineWidth: 1.4)
                Rectangle().fill(Theme.muted).frame(width: 6, height: 1.4)
            }
        case .locked:
            PathMark(data: "M5,7.5 L5,5.8 A3,3 0 0 1 11,5.8 L11,7.5 M3.8,7.5 L12.2,7.5 L12.2,13 L3.8,13 Z",
                     box: CGSize(width: 16, height: 16), lineWidth: 1.2)
        case .open:
            Circle().strokeBorder(Theme.muted, lineWidth: 1.4)
        }
    }
}
