# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by
[SignPath Foundation](https://signpath.org).

Signing is being set up. Releases up to and including 0.2.3 are not signed. This page says which
release is the first signed one once it is out.

## What is signed

Only files built from this repository by its GitHub Actions workflow
([release.yml](.github/workflows/release.yml)), started by a version tag. Nothing built on a personal
computer is signed.

On Windows these files are signed:

- `Slayumind.Aiko-win-Setup.exe`, the installer;
- `Update.exe`, the Velopack updater that the installer puts next to the app;
- `Aiko.App.exe`, the app;
- `Aiko.Bridge.exe`, the helper that Claude Code runs for the status line and hooks;
- `claude.exe`, the small launcher that picks the right Claude Code folder for a project.

Third-party files that come with the app, such as the .NET runtime, keep the signature of their
own authors. The macOS build is not signed.

## Team

| Role | Members |
|---|---|
| Committers and reviewers | [Aleksandr Fomichenko](https://github.com/Slayumind) |
| Approvers | [Aleksandr Fomichenko](https://github.com/Slayumind) |

Every signing request is approved by hand. The approver checks that the request comes from a tag on
`main` and that the release notes match the changes. Everyone on the team uses multi-factor
authentication for GitHub and for SignPath.

## Privacy

Aiko sends nothing over the network unless you turn it on. Direct mode, update checks and anonymous
counting each have their own switch, and all three are off by default.
[PRIVACY.md](PRIVACY.md) lists what Aiko reads, what it sends, where it keeps things and how to turn
each part off.

## Changes to your system

Aiko changes things outside its own folders only after you agree, in the setup checklist or in
settings:

- one line and Aiko's plugins in a Claude Code `settings.json`, with a copy of the original;
- its command folder at the start of your user `PATH`;
- account-switching functions in your PowerShell profile, turned off because they hide Aiko's
  commands, with a copy of the profile;
- a value under `HKCU\...\Run`, if you want Aiko to start with Windows.

Removing Aiko puts all of this back.

## Checking a download

[SECURITY.md](SECURITY.md) shows how to check the checksums and the build attestation of a release.
