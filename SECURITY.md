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

## Update signatures

This prepares automatic updates, planned for 0.3. The release workflow signs `SHA256SUMS.txt` with
an ECDSA P-256 key and adds the signature as `SHA256SUMS.txt.sig`. The public key is
`update-public-key.pem` in this repository. Aiko will install an update only when the signature is
good and the file matches its line in `SHA256SUMS.txt`. Releases made before the key existed have
no signature. To check a signed release yourself:

```
openssl dgst -sha256 -verify update-public-key.pem -signature SHA256SUMS.txt.sig SHA256SUMS.txt
sha256sum -c SHA256SUMS.txt
```
