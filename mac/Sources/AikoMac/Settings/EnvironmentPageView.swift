import AikoKit
import AppKit
import SwiftUI

/// One environment (D-177). The account on the left, as Claude Code knows it. What Aiko calls the
/// environment on the right: its name, its command and the folders bound to it. The folders are
/// only shown here; they change on the project folders page, where every folder is in one table.
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

            HStack {
                Text(Strings.directMode)
                    .font(Theme.sans(Theme.textName))
                    .foregroundStyle(Theme.ink)
                Spacer(minLength: 12)
                AikoSwitch(isOn: environment.directMode, set: setDirect)
            }
            .padding(.top, 22)

            // The one control in Aiko that reads an access token. Somebody has to be able to decide
            // before they flip it, not after, so the whole explanation sits right under it.
            Control.rowHint(
                account.plan == .team || account.plan == .enterprise
                    ? Strings.directModeWhat + " " + Strings.directModeAsk
                    : Strings.directModeWhat)
                .padding(.top, 6)
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
            bound(environment)
            if isDefault && two {
                Control.rowHint(Strings.restToo).padding(.top, 6)
            }
            if two {
                LinkButton(title: Strings.editInFolders) { state.show(.folders) }
                    .padding(.top, 10)
            }
        }
    }

    private func bound(_ environment: AikoEnvironment) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            if environment.projectFolders.isEmpty {
                Control.rowHint(Strings.noneBound)
            } else {
                ForEach(environment.projectFolders.sorted { $0.caseInsensitiveCompare($1) == .orderedAscending },
                        id: \.self) { path in
                    Text(path)
                        .font(Theme.mono(Theme.textTiny))
                        .foregroundStyle(Theme.ink)
                        .lineLimit(1)
                        .truncationMode(.head)
                        .help(path)
                        .padding(.horizontal, 10)
                        .padding(.vertical, 6)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(Theme.hoverLayer)
                        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }
            }
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

    private func setDirect(_ on: Bool) {
        guard let environment else { return }
        state.editor.commit(
            EnvironmentEdits.setDirectMode(state.environments, environment.name, on),
            "direct mode \(on ? "on" : "off")")
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
