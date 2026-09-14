# Security

## Supported versions

Only the latest release is supported. Fixes go into the next release, not into older versions.

## Reporting a problem

Report security problems privately. Please don't open a public issue.

Use **Report a vulnerability** on the Security tab of
[github.com/Slayumind/aiko](https://github.com/Slayumind/aiko/security). It opens a private
report that only the maintainer can see.

You'll get a first answer within seven days. If a fix is needed, the changelog of the release with
the fix mentions it and credits you, unless you ask not to be named.

## What matters most

The most sensitive thing Aiko touches is the Claude Code access token, used in direct mode. Reports
about the token, the Claude Code settings file or any address Aiko connects to matter most.
[PRIVACY.md](PRIVACY.md) describes how Aiko handles the token today.

## Checking what you downloaded

Every release comes with `SHA256SUMS.txt` and a build provenance attestation.

```
sha256sum -c SHA256SUMS.txt
gh attestation verify Slayumind.Aiko-win-Setup.exe --repo Slayumind/aiko
```

The attestation shows that the GitHub Actions workflow in this repository built the file from the
commit named in the release.
