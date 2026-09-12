# Security

## Supported versions

The latest release is the supported one. Fixes go into the next release rather than into older
versions.

## Reporting a problem

Please report security problems privately, not as a public issue.

Use **Report a vulnerability** on the Security tab of
[github.com/Slayumind/aiko](https://github.com/Slayumind/aiko/security). That opens a private
report only the maintainer can see.

You can expect a first answer within seven days. If a fix is needed, the release that carries it
says so in the changelog and credits you, unless you ask not to be named.

## What matters most

Aiko touches one thing that has to be handled carefully: the Claude Code access token, used in
direct mode. Reports about the token, about the Claude Code settings file, or about any address
Aiko connects to are the ones to send first. [PRIVACY.md](PRIVACY.md) describes how the token is
handled today.

## Checking what you downloaded

Every release carries `SHA256SUMS.txt` and a build provenance attestation.

```
sha256sum -c SHA256SUMS.txt
gh attestation verify Slayumind.Aiko-win-Setup.exe --repo Slayumind/aiko
```

The attestation says the file was built by the GitHub Actions workflow in this repository, from
the commit named in the release.
