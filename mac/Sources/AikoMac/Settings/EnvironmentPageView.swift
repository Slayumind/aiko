import AikoKit
import AppKit
import SwiftUI

/// One environment (D-177). The account on the left, as Claude Code knows it. What Aiko calls the
/// environment on the right: its name, its command and the folders bound to it, which are bound and
/// unbound here (D-374, D-398).
///
/// The twin of Settings/EnvironmentPage.xaml(.cs).
struct EnvironmentPageView: View {
    @ObservedObject var state: SettingsState
    let folder: String

    @State private var opened = false
    @State private var asking = false
    @State private var toTrash = false
    @State private var waiter: Waiter?
    @State private var signedIn = false

    /// A folder that just came from another environment, and the name of that environment.
    @State private var moved: (folder: String, from: String)?

    private var environment: AikoEnvironment? {
        state.environments.environments.first { $0.holds(folder) }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            if let environment {
                title(environment)
                note
                columns(environment)
                removal(environment)
            }
        }
        .onAppear(perform: fill)
        .onDisappear {
            // The typed name and command are applied by the state, which outlives this view: Esc
            // has to keep them, and by then the view is already gone.
            waiter?.stop()
        }
    }

    // ---- the top ----

    private func title(_ environment: AikoEnvironment) -> some View {
        let plan = state.plan(of: folder)
        let isDefault = state.environments.defaultEnvironmentIn(.macOS, Store.home)?.name == environment.name
        let two = state.environments.environments.count > 1

        return HStack(spacing: 10) {
            Control.pageTitle(environment.name)
            if !plan.isEmpty { Chip(text: plan) }
            if isDefault && two { Control.rowNote(Strings.byDefault) }
        }
        .padding(.trailing, 96)
    }

    private var note: some View {
        Reveal(isOpen: !state.note.isEmpty) {
            HStack(alignment: .top, spacing: 8) {
                Image(systemName: "checkmark")
                    .font(.system(size: 9, weight: .bold))
                    .foregroundStyle(Theme.positive)
                    .padding(.top, 4)
                Text(state.note)
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.ink)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(.top, 10)
        }
    }

    private func columns(_ environment: AikoEnvironment) -> some View {
        HStack(alignment: .top, spacing: 20) {
            account(environment).frame(maxWidth: .infinity, alignment: .leading)
            names(environment).frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.top, 18)
    }

    // ---- the account ----

    private func account(_ environment: AikoEnvironment) -> some View {
        let account = Store.account(in: folder)

        return VStack(alignment: .leading, spacing: 0) {
            Control.sectionLabel(Strings.sectionAccount).padding(.bottom, 6)

            VStack(alignment: .leading, spacing: 8) {
                HStack(alignment: .firstTextBaseline, spacing: 12) {
                    Control.rowNote(Strings.emailLabel)
                    Text(account.email ?? "—")
                        .font(Theme.sans(Theme.textSmall))
                        .foregroundStyle(Theme.ink)
                        .lineLimit(1)
                        .truncationMode(.tail)
                }

                HStack(spacing: 8) {
                    Control.rowNote(Strings.planLabel)
                    Chip(text: account.planLabel.isEmpty ? "—" : account.planLabel)
                    Circle()
                        .strokeBorder(signedIn ? .clear : Theme.muted, lineWidth: 1.5)
                        .background(Circle().fill(signedIn ? Theme.positive : .clear))
                        .frame(width: 6, height: 6)
                    Control.rowHint(signedIn ? Strings.stateConnected : Strings.stateSignInNeeded)
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 10)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Color.white.opacity(0.03))
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .strokeBorder(Theme.hairline, lineWidth: 1))

            Control.rowNote(shortPath(folder)).padding(.leading, 2).padding(.top, 6)

            FlatButton(title: signedIn ? Strings.signInAgain : Strings.signIn, press: signIn)
                .padding(.top, 10)

            Reveal(isOpen: opened) {
                Control.rowHint(Strings.claudeOpened).padding(.top, 6)
            }

            // Direct mode reads the access token, and on macOS that token lives in the Keychain,
            // which Aiko will not read (AikoKit.SignedIn). A switch that cannot keep its promise is
            // worse than no switch, so here the page says what it means instead of offering it.
            Text(Strings.directMode)
                .font(Theme.sans(Theme.textName))
                .foregroundStyle(Theme.ink)
                .padding(.top, 22)

            Control.rowHint(Strings.directModeMac).padding(.top, 6)
        }
    }

    // ---- the name, the command and the bound folders ----

    private func names(_ environment: AikoEnvironment) -> some View {
        let two = state.environments.environments.count > 1
        let isDefault = state.environments.defaultEnvironmentIn(.macOS, Store.home)?.name == environment.name

        return VStack(alignment: .leading, spacing: 0) {
            Control.sectionLabel(Strings.nameInAiko).padding(.bottom, 6)
            NameField(text: $state.typedName, size: Theme.textName, onCommit: state.commitName)
            Reveal(isOpen: state.nameProblem != .none) {
                Text(words(state.nameProblem))
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.caution)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 5)
            }

            Control.sectionLabel(Strings.sectionCommand).padding(.top, 16).padding(.bottom, 6)
            NameField(text: $state.typedCommand, mono: true, size: Theme.textNumber,
                      onCommit: state.commitCommand)

            if state.suggestion == nil {
                Text(words(state.commandProblem))
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(state.commandProblem == .none ? Theme.muted : Theme.caution)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 5)
            }

            Reveal(isOpen: state.suggestion != nil) {
                VStack(alignment: .leading, spacing: 8) {
                    Control.rowHint(Strings.cmdKept)
                    if let suggestion = state.suggestion {
                        FlatButton(title: Strings.format(Strings.renameCommandTo, suggestion)) {
                            state.typedCommand = suggestion
                            state.commitCommand()
                        }
                    }
                }
                .padding(.top, 5)
            }

            Control.sectionLabel(Strings.sectionBoundFolders).padding(.top, 16).padding(.bottom, 6)
            folders(environment, two: two, isDefault: isDefault)
        }
    }

    /// The folders of this environment (D-394, D-398): a folder of another environment added here
    /// moves here and says so, and one switch says which environment takes every other folder.
    @ViewBuilder
    private func folders(_ environment: AikoEnvironment, two: Bool, isDefault: Bool) -> some View {
        if two {
            Reveal(isOpen: !CommandFolder.isSetUp) {
                needCommands.padding(.bottom, 8)
            }

            VStack(alignment: .leading, spacing: 4) {
                ForEach(environment.projectFolders.sorted { $0.caseInsensitiveCompare($1) == .orderedAscending },
                        id: \.self) { path in
                    folderRow(path)
                }
                if environment.projectFolders.isEmpty {
                    Control.rowHint(Strings.noneBound).padding(.bottom, 2)
                }
            }

            FlatButton(title: Strings.addFolder, symbol: "plus") { addFolder(to: environment) }
                .padding(.top, 4)

            HStack(spacing: 12) {
                Control.rowName(Strings.defaultForRest)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 12)
                AikoSwitch(isOn: isDefault) { on in setDefault(environment, on: on) }
            }
            .padding(.top, 14)
        } else {
            Control.rowHint(Strings.format(Strings.foldersOneEnvironment, environment.name))
        }
    }

    /// One bound folder: its path and a cross that unbinds it; under a folder that just came from
    /// another environment, a line that says from which.
    private func folderRow(_ path: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 8) {
                Text(path)
                    .font(Theme.mono(Theme.textTiny))
                    .foregroundStyle(Theme.ink)
                    .lineLimit(1)
                    .truncationMode(.tail)
                    .help(path)
                Spacer(minLength: 0)
                MarkButton(help: Strings.removeBinding, press: {
                    state.editor.commit(EnvironmentEdits.unbind(state.environments, path), "unbound a folder")
                }) {
                    Image(systemName: "xmark")
                        .font(.system(size: 9, weight: .medium))
                        .foregroundStyle(Theme.muted)
                }
            }
            if let moved, RealClaude.sameFolder(moved.folder, path) {
                Control.rowHint(Strings.format(Strings.folderMovedFrom, moved.from))
            }
        }
        .padding(.leading, 10)
        .padding(.trailing, 4)
        .padding(.vertical, 4)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.hoverLayer)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    private var needCommands: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(Strings.foldersNeedCommands)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .fixedSize(horizontal: false, vertical: true)
            FlatButton(title: Strings.setUpEnvironments) { state.openChecklist(at: .commands) }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.caution.opacity(0.05))
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .strokeBorder(Theme.caution.opacity(0.22), lineWidth: 1))
    }

    /// Binds the folder here. A folder of another environment moves here, and its row says from where.
    private func addFolder(to environment: AikoEnvironment) {
        guard let picked = FolderPicker.pick(title: Strings.pickProject),
              !environment.projectFolders.contains(where: { RealClaude.sameFolder($0, picked) }) else { return }

        let from = EnvironmentEdits.bindings(state.environments)
            .first { RealClaude.sameFolder($0.folder, picked) }?.environment
        moved = from.map { (folder: picked, from: $0) }
        state.editor.commit(
            EnvironmentEdits.bind(state.environments, picked, environment.name),
            from == nil ? "bound a folder" : "moved a folder to another environment")
    }

    /// On: every folder that is not bound runs here. Off: it runs in the other environment.
    private func setDefault(_ environment: AikoEnvironment, on: Bool) {
        let target = on ? environment.name : state.environments.environments.first { $0.name != environment.name }?.name
        if let target {
            state.editor.commit(EnvironmentEdits.setDefault(state.environments, target), "changed the default environment")
        }
    }

    // ---- removing: a second click, and the Trash only when ticked ----

    private func removal(_ environment: AikoEnvironment) -> some View {
        let removable = EnvironmentEdits.canRemove(.macOS, state.environments, environment.name, Store.home)

        return VStack(alignment: .leading, spacing: 0) {
            Control.hairline().padding(.top, 22).padding(.bottom, 14)

            if !removable {
                Control.rowHint(Strings.keepClaude)
            } else if !asking {
                FlatButton(title: Strings.removeEnvironment, look: .ghostDanger) { asking = true }
                    .padding(.leading, -10)
            }

            Reveal(isOpen: asking) {
                VStack(alignment: .leading, spacing: 0) {
                    Text(Strings.format(Strings.removeEnvironmentLine, environment.command, environment.name))
                        .font(Theme.sans(Theme.textSmall))
                        .foregroundStyle(Theme.ink)
                        .fixedSize(horizontal: false, vertical: true)

                    Tick(isOn: $toTrash, title: Strings.moveToTrash).padding(.top, 12)
                    Reveal(isOpen: toTrash) {
                        Control.rowHint(Strings.trashWhy).padding(.leading, 24).padding(.top, 6)
                    }

                    HStack(spacing: 8) {
                        Spacer()
                        FlatButton(title: Strings.cancel, look: .ghost) {
                            asking = false
                            toTrash = false
                        }
                        FlatButton(title: Strings.removeConfirm, look: .danger) {
                            remove(environment.name)
                        }
                    }
                    .padding(.top, 12)
                }
                .padding(12)
                .background(Theme.destructive.opacity(0.06))
                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                .overlay(
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .strokeBorder(Theme.destructive.opacity(0.22), lineWidth: 1))
            }
        }
    }

    // ---- what the page does ----

    private func fill() {
        state.startEditing(folder)
        signedIn = Store.isSignedIn(folder)
    }

    private func signIn() {
        guard ClaudeLauncher.open(configFolder: folder, workingDirectory: Store.home) else { return }
        opened = true

        // The dot turns green by itself once the sign-in is done in the browser.
        if !signedIn {
            waiter?.stop()
            waiter = Waiter.forSignIn(folder) { fill() }
        }
    }



    private func remove(_ name: String) {
        waiter?.stop()
        state.stopEditing()
        let trashed = state.editor.remove(name, toTrash: toTrash)
        state.showNoteOnFirstEnvironment(
            Strings.format(trashed ? Strings.environmentRemovedToTrash : Strings.environmentRemoved, name))
    }

    /// The folder the way people read it: ~/.claude-work rather than the whole path.
    private func shortPath(_ path: String) -> String {
        path.hasPrefix(Store.home) ? "~" + path.dropFirst(Store.home.count) : path
    }

    private func words(_ problem: NameProblem) -> String {
        switch problem {
        case .empty: return Strings.nameEmpty
        case .tooLong: return Strings.nameTooLong
        case .taken: return Strings.nameTaken
        case .none: return ""
        }
    }

    private func words(_ problem: CommandProblem) -> String {
        switch problem {
        case .none: return Strings.cmdShellsMac
        case .empty: return Strings.cmdEmpty
        case .tooLong: return Strings.cmdTooLong
        case .badCharacters: return Strings.cmdBadCharacters
        case .reserved: return Strings.cmdReserved
        case .taken: return Strings.cmdTaken
        }
    }
}
