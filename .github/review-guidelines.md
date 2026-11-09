# Review Guidelines

Project-specific rules for the AI reviewer, in addition to its built-in checks. Sent as
`guidelines` on the self-review request `review.yml` makes.

- **CI/CD secret references are not hardcoded secrets.** Syntax like GitHub Actions'
  `${{ secrets.X }}` or `${{ env.X }}` pulls a value from an encrypted secret store at workflow
  run time — it is a reference, not a literal value, and should never be flagged as "hardcoded."
  A real hardcoded secret is a literal string value (an API key, token, or password) assigned
  directly in source.
- **Respect documented rationale in `CLAUDE.md` and doc comments.** This codebase explains several
  deliberate, non-obvious decisions in its own `CLAUDE.md` and in XML doc comments right above the
  code in question — e.g. why `IGitHubReviewPublisher`/`ReviewRequest.Guidelines` take their
  values per-request instead of from server-side config, why `SecurityScanPlugin` reuses the
  general reviewer's DTO, why Roslyn line numbers get remapped. If a finding would contradict an
  explanation already given, read it first before flagging the pattern as a problem.
- **No agentic function-calling is intentional, not a missed feature.** This project deliberately
  uses Semantic Kernel for structured, single-shot prompt orchestration, not an agent loop — don't
  suggest adding tool-use/function-calling autonomy as an improvement.
- **Prioritize real bugs, security issues, and correctness problems over style.** Don't spend
  findings on things a formatter/analyzer would already catch, and don't inflate a minor style
  preference to "High" or "Critical" severity.
- **Config and workflow files deserve lighter scrutiny than application code.** Treat issues in
  `.github/workflows/*.yml`, `appsettings*.json`, and similar infrastructure as worth flagging
  only when they're genuine correctness/security problems, not stylistic opinions about how CI or
  config is structured.
- **One clear finding beats several vague ones.** If the same underlying problem would otherwise
  surface as multiple findings, report it once with the most useful suggested fix, not once per
  symptom.
