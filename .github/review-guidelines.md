# Review Guidelines

Project-specific rules for the AI reviewer, in addition to its built-in checks. Sent as
`guidelines` on every request `review.yml` makes (see README's "Supplying project-specific
guidelines").

- **CI/CD secret references are not hardcoded secrets.** Syntax like GitHub Actions'
  `${{ secrets.X }}` or `${{ env.X }}` pulls a value from an encrypted secret store at workflow
  run time — it is a reference, not a literal value, and should never be flagged as "hardcoded."
  The same applies to clear equivalents in other systems (`os.environ['X']`, `process.env.X`,
  Azure Pipelines' `$(X)`) when the value is obviously a reference, not a quoted literal.
- **Respect documented rationale.** This codebase explains non-obvious decisions in doc comments
  and in `CLAUDE.md`/`README.md` (why a particular retry policy, why a type was renamed, why a
  check works a certain way). If a finding would contradict an explanation already given in a
  comment right above the code in question, read that comment first — don't flag something the
  author already explained.
- **Prioritize real bugs, security issues, and correctness problems over style.** Don't spend
  findings on things a formatter/linter would already catch, and don't inflate a minor style
  preference to "High" or "Critical" severity.
- **Config, workflow, and generated files deserve lighter scrutiny than application code.** Treat
  issues in `.github/workflows/*.yml`, build scripts, and similar infrastructure as worth flagging
  only when they're genuine correctness/security problems, not stylistic opinions about how CI is
  structured.
- **One clear finding beats several vague ones.** If the same underlying problem would otherwise
  surface as multiple findings, report it once with the most useful suggested fix, not once per
  symptom.
