import AikoKit
import SwiftUI

/// Every bound folder in one table, folder on the left and its environment on the right (D-178).
/// The last row is every other folder, and the environment picked there is the default. A list
/// under a folder used to read as that folder's environment; in a table row it cannot.
///
/// The twin of Settings/FoldersPage.xaml(.cs).
struct FoldersPageView: View {
    @ObservedObject var state: SettingsState

    private var names: [String] { state.environments.environments.map(\.name) }

    private var two: Bool { names.count > 1 }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Control.pageTitle(Strings.itemFolders)
            Control.rowHint(Strings.format(
                Strings.foldersTableLead,
                EnvironmentEdits.forNewBinding(.macOS, state.environments, Store.home)?.command ?? "aiko"))
                .padding(.top, 10)

            if two {
                table.padding(.top, 16)
            } else {
                oneEnvironment.padding(.top, 16)
            }
        }
    }

    private var oneEnvironment: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(Strings.format(Strings.foldersOneEnvironment, names.first ?? ""))
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .fixedSize(horizontal: false, vertical: true)
            FlatButton(title: Strings.addSecondEnvironment) {
                state.openChecklist(at: .secondEnvironment)
            }
        }
    }

    private var table: some View {
        VStack(alignment: .leading, spacing: 0) {
            if !CommandFolder.isSetUp {
                needCommands.padding(.bottom, 12)
            }

            VStack(spacing: 0) {
                HStack(spacing: 0) {
                    Control.rowNote(Strings.folderColumn).frame(maxWidth: .infinity, alignment: .leading)
                    Control.rowNote(Strings.environmentColumn).frame(width: 176, alignment: .leading)
                        .padding(.leading, 10)
                    Color.clear.frame(width: 36)
                }
                .padding(.leading, 12)
                .padding(.trailing, 8)
                .padding(.top, 8)
                .padding(.bottom, 6)

                ForEach(EnvironmentEdits.bindings(state.environments), id: \.folder) { binding in
                    row(binding)
                }

                rest
            }
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .strokeBorder(Theme.hairline, lineWidth: 1))

            FlatButton(title: Strings.addFolder, press: addFolder).padding(.top, 12)
        }
    }

    private var needCommands: some View {
        HStack(spacing: 12) {
            Text(Strings.foldersNeedCommands)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 12)
            FlatButton(title: Strings.setUpEnvironments) { state.openChecklist(at: .commands) }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .background(Theme.caution.opacity(0.05))
        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .strokeBorder(Theme.caution.opacity(0.22), lineWidth: 1))
    }

    private func row(_ binding: FolderBinding) -> some View {
        HStack(spacing: 0) {
            Text(binding.folder)
                .font(Theme.sans(Theme.textSmall))
                .foregroundStyle(Theme.ink)
                .lineLimit(1)
                .truncationMode(.head)
                .help(binding.folder)
                .frame(maxWidth: .infinity, alignment: .leading)

            DropDown(items: names, index: names.firstIndex(of: binding.environment) ?? 0) {
                bind(binding.folder, to: $0)
            }
            .frame(width: 176)
                .padding(.leading, 10)

            MarkButton(help: Strings.removeBinding, press: { unbind(binding.folder) }) {
                Image(systemName: "xmark")
                    .font(.system(size: 9, weight: .medium))
                    .foregroundStyle(Theme.muted)
            }
            .frame(width: 36, alignment: .trailing)
        }
        .padding(.leading, 12)
        .padding(.trailing, 8)
        .padding(.vertical, 6)
        .overlay(alignment: .top) { Control.hairline() }
    }

    private var rest: some View {
        HStack(spacing: 0) {
            Control.rowName(Strings.allOtherFolders).frame(maxWidth: .infinity, alignment: .leading)
            DropDown(
                items: names,
                index: names.firstIndex(
                    of: state.environments.defaultEnvironmentIn(.macOS, Store.home)?.name ?? "") ?? 0,
                pick: setDefault)
                .frame(width: 176)
                .padding(.leading, 10)
            Color.clear.frame(width: 36)
        }
        .padding(.leading, 12)
        .padding(.trailing, 8)
        .padding(.vertical, 8)
        .background(Color.white.opacity(0.024))
        .overlay(alignment: .top) { Control.hairline() }
    }

    // ---- what the page does ----

    private func bind(_ folder: String, to index: Int) {
        guard names.indices.contains(index) else { return }
        state.editor.commit(
            EnvironmentEdits.bind(state.environments, folder, names[index]),
            "moved a folder to another environment")
    }

    private func unbind(_ folder: String) {
        state.editor.commit(EnvironmentEdits.unbind(state.environments, folder), "unbound a folder")
    }

    private func setDefault(_ index: Int) {
        guard names.indices.contains(index) else { return }
        state.editor.commit(
            EnvironmentEdits.setDefault(state.environments, names[index]),
            "changed the default environment")
    }

    private func addFolder() {
        guard let picked = FolderPicker.pick(title: Strings.pickProject),
              let target = EnvironmentEdits.forNewBinding(.macOS, state.environments, Store.home)
        else {
            return
        }

        // A folder that is already in the table keeps its environment: picking it again is not a
        // request to move it.
        guard !EnvironmentEdits.bindings(state.environments)
            .contains(where: { RealClaude.sameFolder($0.folder, picked) })
        else {
            return
        }

        state.editor.commit(
            EnvironmentEdits.bind(state.environments, picked, target.name), "bound a folder")
    }
}
