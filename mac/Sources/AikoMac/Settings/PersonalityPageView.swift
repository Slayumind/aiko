import AikoKit
import SwiftUI

/// Aiko's personality (D-207). Where she talks is a switch per environment; the face, the
/// temperament and the skills are one choice for all of them. A sample answer shows what the
/// temperament means, so nobody has to guess from a word.
///
/// The twin of Settings/PersonalityPage.xaml(.cs).
struct PersonalityPageView: View {
    @ObservedObject var state: SettingsState

    /// Her own words in the sample, a little apart from the work in the middle.
    private static let voiceInk = Color(.sRGB, red: 0.81, green: 0.91, blue: 0.87)

    /// A commit Aiko would write for the sample fix. It is English in every language, like any
    /// commit.
    private static let sampleCommit = "Round reset countdown to the nearest minute"

    private static var about: [String: String] {
        [
        "copy": Strings.skillAikoCopy,
        "release-gate": Strings.skillAikoReleaseGate,
        "docs-hygiene": Strings.skillAikoDocsHygiene,
        "playtest": Strings.skillAikoPlaytest,
        "polishing": Strings.skillAikoPolishing,
        "blender-to-unity": Strings.skillAikoBlenderToUnity,
        "texturing": Strings.skillAikoTexturing,
        "glb-for-web": Strings.skillAikoGlbForWeb,
        "palette": Strings.skillAikoPalette,
        "gamedesign-research": Strings.skillAikoGamedesignResearch,
        "balance": Strings.skillAikoBalance,
        "calendar": Strings.skillAikoCalendar,
         "drive": Strings.skillAikoDrive]
    }

    private var anyoneTalks: Bool { state.environments.environments.contains(where: \.persona) }

    /// A calm temperament smiles, a loud one beams, as in the mockup.
    private var face: AikoFace {
        state.persona.temperament == .bright || state.persona.temperament == .musou ? .done : .fresh
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 10) {
                FaceView(style: state.persona.face, face: face, size: 24)
                Control.pageTitle(Strings.navPersonality)
            }

            Control.sectionLabel(Strings.sectionWhereAikoTalks).padding(.top, 18).padding(.bottom, 8)
            environments
            Control.rowHint(Strings.personaNewSessions).padding(.top, 8)

            Control.sectionLabel(Strings.sectionFace).padding(.top, 22).padding(.bottom, 8)
            SegmentBar(
                choices: [(FaceStyle.chibi, Strings.faceChibi), (FaceStyle.emoji, Strings.faceEmoji)],
                picked: state.persona.face) { face in
                    var next = state.persona
                    next.face = face
                    state.savePersona(next)
                }
            Control.rowHint(Strings.faceWhereMac).padding(.top, 8)

            Control.sectionLabel(Strings.sectionTemperament).padding(.top, 22).padding(.bottom, 8)
            SegmentBar(
                choices: [
                    (Temperament.quiet, Strings.temperamentQuiet),
                    (Temperament.normal, Strings.temperamentNormal),
                    (Temperament.bright, Strings.temperamentBright),
                    // The same word in every language.
                    (Temperament.musou, "無双"),
                ],
                picked: state.persona.temperament,
                pick: pickTemperament)
            Control.rowHint(temperamentAbout).padding(.top, 8)

            Control.sectionLabel(Strings.sectionSampleReply).padding(.top, 22).padding(.bottom, 8)
            sample
            commitLine.padding(.top, 8)

            Control.sectionLabel(Strings.format(Strings.sectionSkills, SkillCatalog.all.count))
                .padding(.top, 22).padding(.bottom, 8)
            skills
            Control.rowHint(anyoneTalks ? Strings.skillsWork : Strings.skillsNeedPersona)
                .padding(.top, 8)
        }
    }

    // ---- where Aiko talks ----

    private var environments: some View {
        RowsBox {
            ForEach(Array(SettingsNav.ordered(state.environments, .macOS, Store.home).enumerated()),
                    id: \.offset) { at, environment in
                environmentRow(environment, first: at == 0)
            }
        }
    }

    private func environmentRow(_ environment: AikoEnvironment, first: Bool) -> some View {
        let folder = environment.configDirectories.first ?? ""
        let plan = state.plan(of: folder)
        let ownStyle = environment.persona ? ClaudeSettingsFile.userOutputStyle(folder) : nil

        return VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 8) {
                Text(environment.name)
                    .font(Theme.sans(Theme.textName))
                    .foregroundStyle(Theme.ink)
                if !plan.isEmpty { Chip(text: plan) }
                Spacer(minLength: 12)
                AikoSwitch(isOn: environment.persona) { setPersona(environment.name, $0) }
            }

            Reveal(isOpen: ownStyle != nil) {
                Text(Strings.format(Strings.personaOwnStyle, ownStyle ?? ""))
                    .font(Theme.sans(Theme.textSmall))
                    .foregroundStyle(Theme.caution)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 4)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .overlay(alignment: .top) { if !first { Control.hairline() } }
    }

    private func setPersona(_ environment: String, _ on: Bool) {
        state.editor.commit(
            EnvironmentEdits.setPersona(state.environments, environment, on),
            on ? "turned the persona on" : "turned the persona off")
    }

    // ---- temperament ----

    private func pickTemperament(_ temperament: Temperament) {
        var next = state.persona
        next.temperament = temperament
        state.savePersona(next)

        // The persona text changed, so installed plugins take it for the next session.
        PluginSync.personaChanged()
    }

    private var temperamentAbout: String {
        switch state.persona.temperament {
        case .quiet: return Strings.temperamentQuietAbout
        case .bright: return Strings.temperamentBrightAbout
        case .musou: return Strings.temperamentMusouAbout
        case .normal: return Strings.temperamentNormalAbout
        }
    }

    private var reply: (open: String, body: String, close: String) {
        switch state.persona.temperament {
        case .quiet: return ("", Strings.sampleBody, Strings.sampleCloseQuiet)
        case .bright: return (Strings.sampleOpenBright, Strings.sampleBodyBright, Strings.sampleCloseBright)
        case .musou:
            return (Strings.sampleOpenMusou,
                    Strings.sampleBody + Strings.sampleBodyMusouTail,
                    Strings.sampleCloseMusou)
        case .normal: return (Strings.sampleOpenNormal, Strings.sampleBody, Strings.sampleCloseNormal)
        }
    }

    private var sample: some View {
        let answer = reply

        return VStack(alignment: .leading, spacing: 10) {
            HStack {
                Spacer(minLength: 40)
                chat(Strings.sampleQuestion, Theme.ink)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 7)
                    .background(Theme.raised)
                    .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                    .frame(maxWidth: 380, alignment: .trailing)
            }

            HStack(alignment: .top, spacing: 8) {
                FaceView(style: state.persona.face, face: face, size: 20)
                VStack(alignment: .leading, spacing: 10) {
                    if !answer.open.isEmpty {
                        chat(answer.open, Self.voiceInk)
                    }
                    chat(answer.body, Theme.ink)
                    chat(answer.close, Self.voiceInk)
                }
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.background)
        .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 12, style: .continuous)
                .strokeBorder(Theme.hairline, lineWidth: 1))
    }

    /// The colour comes in rather than being set here and overridden from outside: a Text that
    /// already has a foreground style keeps it, and her own words would come out the same grey as
    /// the work in the middle.
    private func chat(_ text: String, _ colour: Color) -> some View {
        Text(text)
            .font(Theme.sans(Theme.textNumber))
            .foregroundStyle(colour)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var commitLine: some View {
        VStack(alignment: .leading, spacing: 2) {
            Control.rowHint(Strings.sampleCommit)
            Text(Self.sampleCommit)
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.ink)
        }
    }

    // ---- skills ----

    /// One switch for all skills, then the skills themselves: they are one plugin in Claude Code
    /// and come and go together (D-237).
    private var skills: some View {
        VStack(alignment: .leading, spacing: 0) {
            RowsBox {
                HStack(spacing: 12) {
                    Control.rowName(Strings.skillsSwitch)
                    Spacer(minLength: 12)
                    AikoSwitch(isOn: state.persona.skillsOn, enabled: anyoneTalks, set: setSkills)
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 10)
            }

            // One box per domain, so the groups read apart.
            ForEach(Array(SkillCatalog.groups.enumerated()), id: \.offset) { _, group in
                Control.sectionLabel(
                    "\(group.domain == .projects ? Strings.skillDomainProjects : Strings.skillDomainGames)"
                        + " · \(group.skills.count)")
                    .padding(.top, 18)
                    .padding(.bottom, 8)

                RowsBox {
                    ForEach(Array(group.skills.enumerated()), id: \.offset) { at, skill in
                        skillRow(skill, topLine: at > 0)
                    }
                }
            }
            .opacity(state.persona.skillsOn ? 1 : 0.45)
        }
        .opacity(anyoneTalks ? 1 : 0.45)
    }

    private func skillRow(_ skill: String, topLine: Bool) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(SkillCatalog.call(skill))
                .font(Theme.mono(Theme.textSmall))
                .foregroundStyle(Theme.ink)
            Control.rowHint(Self.about[skill] ?? "")
        }
        .padding(.leading, 12)
        .padding(.trailing, 28)
        .padding(.vertical, 9)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .top) { if topLine { Control.hairline() } }
    }

    private func setSkills(_ on: Bool) {
        var next = state.persona
        next.skillsOn = on
        state.savePersona(next)
        PluginSync.request(on ? "skills switched on" : "skills switched off")
    }
}
