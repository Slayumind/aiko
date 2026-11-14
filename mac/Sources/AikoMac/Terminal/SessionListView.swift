import AikoKit
import SwiftUI

/// The left side of the terminal window: the environments as tabs, then the sessions of the chosen
/// one by folder, each folder with its "+" for Claude Code and ">_" for a command line (D-350).
struct SessionListView: View {
    @ObservedObject var state: TerminalState

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            tabs
            ScrollView {
                VStack(alignment: .leading, spacing: 4) {
                    ForEach(state.groups, id: \.folder) { group in
                        folder(group)
                    }
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 8)
            }
            Spacer(minLength: 0)
            newSession
        }
        .frame(maxHeight: .infinity, alignment: .top)
        .background(Theme.surface)
    }

    private var tabs: some View {
        HStack(spacing: 4) {
            ForEach(state.environments, id: \.self) { name in
                Button { state.onEnvironment?(name) } label: {
                    Text(name)
                        .font(.system(size: Theme.textNumber, weight: name == state.environment ? .medium : .regular))
                        .foregroundStyle(name == state.environment ? Theme.ink : Theme.muted)
                        .padding(.horizontal, 10)
                        .padding(.vertical, 5)
                        .background(name == state.environment ? Theme.chosenLayer : Color.clear)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                }
                .buttonStyle(.plain)
            }
            Spacer(minLength: 0)
        }
        .padding(8)
    }

    private func folder(_ group: RowGroup) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 6) {
                Text(group.name)
                    .font(.system(size: Theme.textTiny, design: .monospaced))
                    .foregroundStyle(Theme.muted)
                Spacer(minLength: 0)
                small("plus") { state.onNew?(group.folder, .claude) }
                small("chevron.right") { state.onNew?(group.folder, .shell) }
            }
            .padding(.horizontal, 8)
            .padding(.top, 8)

            ForEach(group.rows, id: \.id) { row in
                Button { state.onChoose?(row.id) } label: {
                    HStack(spacing: 8) {
                        Image(systemName: row.kind == .shell ? "dollarsign" : "asterisk")
                            .font(.system(size: 11))
                            .foregroundStyle(Theme.muted)
                        Text(row.name)
                            .font(.system(size: Theme.textName))
                            .foregroundStyle(Theme.ink)
                            .lineLimit(1)
                            .truncationMode(.tail)
                        Spacer(minLength: 0)
                    }
                    .padding(.horizontal, 10)
                    .padding(.vertical, 8)
                    .background(row.id == state.chosen ? Theme.chosenLayer : Color.clear)
                    .clipShape(RoundedRectangle(cornerRadius: 10))
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
        }
    }

    private var newSession: some View {
        Button { state.onNewInOtherFolder?() } label: {
            HStack {
                Image(systemName: "plus")
                Text(Strings.terminalNewSession)
                Spacer(minLength: 0)
            }
            .font(.system(size: Theme.textNumber))
            .foregroundStyle(Theme.ink)
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .overlay(RoundedRectangle(cornerRadius: 8).stroke(Theme.inputLine, lineWidth: 1))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(8)
    }

    private func small(_ symbol: String, _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: symbol)
                .font(.system(size: 11))
                .foregroundStyle(Theme.muted)
                .frame(width: 22, height: 22)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}
