# Persona control prompts

Real Claude Code sessions with Aiko's persona plugin, checked for character where the persona must
stay silent: commits, files, error explanations, dangerous actions, bad news and secrets. Where she
may talk, the check allows only the Japanese words on the persona's list, in Japanese script.

The checks are deterministic (`checks.mjs`, tested with `node --test`). A run costs Claude usage.

```
dotnet build src/Aiko.App
node tools/persona-evals/run.mjs --app src/Aiko.App/bin/Debug/net10.0-windows/Aiko.App.exe
node tools/persona-evals/run.mjs --app <exe> --temperaments Musou --cases commit,error
node tools/persona-evals/run.mjs --app <exe> --temperaments Bright --model claude-fable-5-1
```

- It uses the account in `~/.claude` and spends its limits. It stops at `--budget` dollars (default 10,
  as reported by Claude Code) or when Aiko's last snapshot shows a limit at `--stop-at-percent`
  (default 80).
- Aiko's installed plugins are switched off for these sessions, so only the persona under test speaks.
- Each case runs in a fresh git repository in the temp folder. The report with every answer is written
  to `report.json` in `--out`.
- `--model` runs the sessions on another model. Without it Claude Code uses its default.
- The `routine` case fails if the reply names one of Aiko's favourite games. The `design` case may
  name one; read that answer yourself.
- Exit codes: 0 all passed, 1 a case failed, 2 bad arguments, 3 stopped at the budget or the limit.

Run it before a release that changes `PersonaPrompt.cs`, and after any change to the guardrails.
