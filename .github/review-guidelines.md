# Review Guidelines

Project-specific rules for the AI reviewer, in addition to its built-in checks. Sent as
`guidelines` on the self-review request `review.yml` makes.

## Do not report these at all

These are known non-issues for this codebase. Do not emit a finding about any of them, at **any**
severity — not Critical, High, Medium, Low, or Info. If your analysis would otherwise produce one
of these, discard it instead of downgrading it to a lower severity.

- **`AllowDangerouslySetContent = true`**, anywhere it appears (`CodeReviewPlugin.cs`,
  `SecurityScanPlugin.cs`). It disables the Semantic Kernel prompt template engine's HTML-encoding
  of injected `{{$var}}` values, which otherwise corrupts real source code (`<`, `=>`, generics)
  before the model sees it — there is no HTML rendering anywhere downstream for that encoding to
  protect against. It is not an XSS/injection vulnerability, and the rationale is already
  documented in a comment directly above every place it's set. Do not suggest adding a comment,
  sanitizing input, or setting it to `false`.
- **A nullable `guidelines`/`customGuidelines` parameter being `null`-defaulted or unvalidated.**
  Every call site that uses it (`CustomGuidelines.BuildSection`, `string.IsNullOrWhiteSpace`
  checks) already handles the null case. Do not flag it as unsafe, confusing, redundant, needing a
  null check, or needing more documentation, in any file.
- **Collection-expression syntax (`[...]` / `= []`), anywhere it appears** — including
  `= new List<T>() ` replacements, inline in method/assertion arguments (e.g.
  `Assert.Equal([...], actual)`), or as an empty-collection initializer. This codebase targets C#
  12 / .NET 10, where this syntax always compiles. Never call it invalid, a compile error, "array
  initializer syntax," or suggest replacing it with `new List<T>()`.
- **CI/CD secret-reference syntax** (`${{ secrets.X }}`, `${{ env.X }}`), wherever it appears —
  including inside test fixtures, sample guideline text, or string literals used to verify this
  exact feature (e.g. in `CodeReviewOrchestratorTests.cs`). It is a reference resolved from an
  encrypted secret store at run time, never a literal secret value, and test data that
  deliberately contains this pattern to test the guidelines feature is not a real secret either.
- **Purely affirming observations** — "this is good practice," "no action needed," "method
  signature is clear and follows conventions," or similar. If you have nothing to recommend
  changing, do not create a finding to say so.
- **The null-forgiving operator (`!`) on `request.GitUrl`/`request.Diff` in `ReviewsController.ReviewAsync`.**
  Both are already guarded: `hasGitUrl`/`hasDiff` are computed from `!string.IsNullOrWhiteSpace(...)`
  earlier in the same method, and the branch using `!` only runs when the matching `has*` flag is
  true. Read the whole method body (including code above the line shown) before flagging a
  null-forgiving operator as unguarded.
- **Speculative "could lead to SQL injection/command execution" findings on a `diff`/`gitUrl`
  string parameter with no evidence in the shown code that it's ever used to build a query or
  shell command.** This project never does that with those parameters. Don't report a
  vulnerability class just because a parameter is a string and theoretically "could" be misused
  somewhere unseen — only report what the shown code actually does.

## Severity

- **`Info` is a disabled severity for this review — never use it**, even though the built-in
  instructions list it as an available option. If an observation would only rise to `Info`, omit
  it entirely. Do not re-report it at `Low` just to still say something.
- Reserve `Low` for things actually worth a human's attention eventually, not for restating
  correct code.

## General

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
