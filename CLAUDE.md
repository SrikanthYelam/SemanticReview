# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Working Principles

**1. Think before coding.** Don't assume, don't hide confusion, surface tradeoffs. State
assumptions explicitly; if uncertain, ask. If multiple interpretations exist, present them rather
than silently picking one. If a simpler approach exists, say so — push back when warranted. If
something is unclear, stop, name what's confusing, and ask.

**2. Simplicity first.** Minimum code that solves the problem, nothing speculative: no features
beyond what was asked, no abstractions for single-use code, no unrequested "flexibility" or
configurability, no error handling for impossible scenarios. If 200 lines could be 50, rewrite it.
Ask: "Would a senior engineer call this overcomplicated?" — if yes, simplify.

**3. Surgical changes.** Touch only what you must; clean up only your own mess. Don't "improve"
adjacent code, comments, or formatting; don't refactor things that aren't broken; match existing
style even if you'd do it differently. If you notice unrelated dead code, mention it rather than
deleting it. Remove imports/variables/functions that *your* changes made unused, but don't remove
pre-existing dead code unless asked. Every changed line should trace directly to the request.

**4. Goal-driven execution.** Turn tasks into verifiable goals and loop until verified: "add
validation" → write tests for invalid inputs, then make them pass; "fix the bug" → write a test
that reproduces it, then make it pass; "refactor X" → ensure tests pass before and after. For
multi-step tasks, state a brief plan as `[step] → verify: [check]` per step — strong success
criteria let the work proceed independently; weak ones ("make it work") force constant
clarification.

## Commands

```bash
dotnet build                                   # build the whole solution
dotnet test                                    # run all tests (tests/AI.CodeReview.Tests)
dotnet test --filter "FullyQualifiedName~RoslynStaticAnalyzerTests"   # run one test class
dotnet test --filter "FullyQualifiedName~Analyze_ResultUsage"         # run one test method
dotnet run --project src/AI.CodeReview.Api     # run the API (Swagger UI at /swagger in Development)
```

Target framework is `net8.0` (the spec called for .NET 10, but only .NET 6/8 SDKs are installed
on the dev machine — see "Notable deviations from the spec" below).

Configure `AI:ApiKey` to get AI findings in addition to Roslyn's:
`dotnet user-secrets set "AI:ApiKey" "sk-..."` from `src/AI.CodeReview.Api`, or set `AI__ApiKey`.
Without a real key, `POST /api/reviews` still returns `200` with Roslyn's deterministic findings
— the AI call fails per file but is caught and logged, not propagated (see "Error handling"
below). This is expected, not a bug.

## Architecture

Clean architecture, dependency direction is strictly inward:

```
Domain          → no dependencies on anything else in the solution
Application     → depends only on Domain; defines IDiffParser, IStaticCodeAnalyzer,
                   IAiCodeReviewer, IGitDiffFetcher, IGitHubReviewPublisher as abstractions;
                   CodeReviewOrchestrator implements ICodeReviewService and is the only place
                   that composes the review pipeline
Infrastructure  → implements the Application abstractions: UnifiedDiffParser (Diffing/),
                   RoslynStaticAnalyzer (Roslyn/), SemanticKernelCodeReviewer + CodeReviewPlugin +
                   SecurityScanPlugin + prompt templates (SemanticKernel/, Plugins/, Prompts/),
                   GitHubDiffFetcher + GitHubReviewPublisher + GitHubUrlParser (Git/)
Api             → thin ReviewsController (POST /api/reviews) + Program.cs DI wiring
```

When adding a new capability, put the interface in Application and the implementation in
Infrastructure — Application must never reference Semantic Kernel, Roslyn (`Microsoft.CodeAnalysis`),
or ASP.NET Core types directly.

### Review pipeline (`CodeReviewOrchestrator`, in Application)

`ICodeReviewService` has two entry points that both funnel into the same private
`ReviewDiffAsync(string diff, ct)` core: `ReviewAsync(diff)` validates and reviews a diff
directly; `ReviewFromUrlAsync(gitUrl)` validates the URL, calls `IGitDiffFetcher.FetchDiffAsync`
to resolve it to a diff, then calls the same core. Don't duplicate the parse/analyze/merge logic
across the two — add new pipeline behavior to `ReviewDiffAsync` once.

1. Validate diff is non-empty/whitespace (`ArgumentException` → controller returns `400`).
2. `IDiffParser.Parse` → per-file lists of added lines with their **real** new-file line numbers.
3. Filter to `.cs` files with at least one added line.
4. Per file: reconstruct the added-line text, run `IStaticCodeAnalyzer.Analyze` (Roslyn — returns
   findings with line numbers **local to the reconstructed fragment**, 1-based), then remap those
   local line numbers back to real diff line numbers via `ParsedFileDiff.AddedLines[localLine-1]`.
5. Per file: run `IAiCodeReviewer.ReviewAsync`, passing each added line prefixed with its real
   line number so the model can report real line numbers directly (no remapping needed for AI
   findings).
6. Merge Roslyn + AI findings, dedupe by `(File, Line, Category)` keeping the first occurrence
   (Roslyn findings are added to the list before AI findings, so Roslyn wins a collision),
   sort by `Severity` (enum declaration order *is* the priority order: Critical→Info), then
   `File`, then `Line`.

If `IAiCodeReviewer.ReviewAsync` throws `AiReviewException` for a file (step 5), the orchestrator
catches it, logs a warning, and continues with that file's Roslyn findings only — the AI review
is a best-effort enhancement, not a hard dependency of a successful response. See "Error
handling" below.

`CodeReviewResult.Findings` (in `CodeReviewOrchestrator`'s return value) is still one deduped,
sorted list — every `ReviewFinding` carries a `Source` (`FindingSource.StaticAnalysis` or
`FindingSource.Ai`), set at construction time by whichever of `RoslynStaticAnalyzer` or
`SemanticKernelCodeReviewer` created it. The *API layer* is what splits by source:
`ReviewsController` partitions `review.Findings` on `Source` into the response's
`staticAnalysisFindings`/`aiFindings` arrays (`ReviewResponse` in `Api/Models/`). If you add a
new finding source in the future, extend `FindingSource` and the controller's partition, not the
orchestrator's merge/dedupe/sort logic.

### Fetching a diff from a GitHub URL (Infrastructure/Git)

`GitHubUrlParser.TryParse` is a pure, synchronous, HTTP-free parser (regex over `Uri.AbsolutePath`
after checking the host is `github.com`) that resolves a commit or PR URL to the `api.github.com`
path to call — this is what's unit-tested directly in `GitHubUrlParserTests`, no HTTP mocking
needed. `GitHubDiffFetcher` (the actual `IGitDiffFetcher`) calls that parser first — an
unrecognized URL throws `GitDiffFetchException` before any network call happens — then issues a
`GET` against the resolved path with `Accept: application/vnd.github.v3.diff`. GitHub returns the
unified diff directly as the response body for that media type on **both** commit and PR
endpoints, so there's no separate "fetch metadata, then build a diff" step. Registered via
`GitServiceCollectionExtensions.AddGitDiffFetching` (`services.AddHttpClient<IGitDiffFetcher, GitHubDiffFetcher>`)
— note the required `User-Agent` default header, GitHub's API rejects requests without one.
Public repositories only (no token configured): a private repo or a nonexistent commit/PR both
surface as GitHub's `404`, mapped to `GitDiffFetchException`; GitHub's unauthenticated rate limit
(60 requests/hour/IP) surfaces as `403`, also mapped. `GitHubDiffFetcherTests` covers all of this
via a hand-written `FakeHttpMessageHandler` (same "no mocking library" convention as the rest of
the test suite) rather than a real network call.

### Posting findings back to a GitHub PR (Infrastructure/Git, Api)

Opt-in write-back: a request with `gitUrl` (a **pull request** URL) plus `postToGitHub: true` and
`gitHubToken` has its findings posted as one GitHub review. This lives in the controller, not the
orchestrator — `ReviewsController` runs the review, then calls `IGitHubReviewPublisher.PublishReviewAsync`
(Application interface, `GitHubReviewPublisher` in Infrastructure) with the full merged
`review.Findings`. The publisher `POST`s `repos/{owner}/{repo}/pulls/{number}/reviews` with
`event: COMMENT`: findings with a `Line` become inline comments, findings without one are listed in
the review's summary body rather than dropped. `GitHubUrlParser.TryParsePullRequest` (returns
owner/repo/number as `GitHubPullRequestRef`) is the second parse method alongside `TryParse`,
sharing its host/scheme validation and PR regex; it only matches PR URLs, so a commit URL is
rejected. Registered separately from the fetcher via `AddGitHubReviewPublishing` (same
`api.github.com` base address and required `User-Agent`).

The token is supplied **per request**, never read from server config — a server-side token would
need write access to every repo this API might review, whereas a workflow's own `GITHUB_TOKEN` is
scoped to one repo. `GitHubReviewPublisherTests` covers this with the same hand-written
`FakeHttpMessageHandler` as the fetcher tests.

### Structured AI output (Infrastructure/SemanticKernel, Infrastructure/Plugins)

`CodeReviewPlugin.AnalyzeCodeAsync` (general review) and `SecurityScanPlugin.ScanAsync`
(security-only: secrets, injection, weak crypto — narrower prompt, see `SecurityScanPrompt`) are
each a single `[KernelFunction]` invoking a `KernelFunctionFactory.CreateFromPrompt` function with
`OpenAIPromptExecutionSettings.ResponseFormat = typeof(AiFindingsResponse)`, so OpenAI returns
JSON matching that **same shared DTO** directly instead of free-form prose — `SecurityScanPrompt`
just instructs the model to always emit `"category": "Security"`, rather than introducing a
second response type. Both plugins are invoked directly by `SemanticKernelCodeReviewer.ReviewAsync`
(once each per file, results concatenated) — neither is wired up for agentic/automatic
function-calling by a chat loop; there is no agent in this codebase. A failed security-scan call
is caught locally inside `ReviewAsync` and logged, independent of the general review's own
try/catch, so it can't discard general findings already produced for that file — see "Error
handling" below for how this differs from `GitDiffFetchException`'s propagate-everywhere behavior.

`SemanticKernelCodeReviewer` then validates each plugin's response via a shared private
`InvokeAndParseAsync` helper: `Severity`/`Category` strings are parsed against the Domain enums
with `Enum.TryParse(ignoreCase: true)`; entries that don't parse are dropped and logged rather
than failing the whole review.

**Gotcha**: SK's default prompt template engine HTML-encodes injected `{{$var}}` content (`<` →
`&lt;`, etc.) to guard against prompt-template injection. Left on, this silently corrupts real C#
source (generics, comparisons, `=>`) before the model sees it, and the model will confidently
hallucinate about "incorrectly encoded" operators instead of erroring — so this is easy to miss
just by reading responses that come back `200`. `CodeReviewPlugin`'s constructor disables it via
`PromptTemplateConfig.AllowDangerouslySetContent = true`, but that alone is **not** sufficient:
`PromptTemplateConfig.InputVariables` isn't auto-populated from the template text, so the
per-variable flag has to be set explicitly for each variable (`fileName`, `diffContext`) via
`promptConfig.InputVariables.Add(new InputVariable { Name = ..., AllowDangerouslySetContent = true })`.
If you add a new prompt variable, remember to add it here too.

**Observing the actual request/response**: `AiCallLoggingFilter` (`IPromptRenderFilter` +
`IFunctionInvocationFilter`, registered on the `Kernel` in
`SemanticKernelServiceCollectionExtensions`) logs the fully rendered prompt and raw model
response at `Debug` level — off by default (see README's "Debugging AI Requests/Responses").
Two things worth knowing if you touch this: (1) the `Kernel` builds its **own** internal
`IServiceProvider` from `builder.Services`, separate from the app's DI container, so filters and
their dependencies (here, `ILogger<AiCallLoggingFilter>`) must be registered on `builder.Services`
directly — registering only `ILoggerFactory` there is not enough for generic `ILogger<T>` to
resolve; the open-generic `typeof(ILogger<>) -> typeof(Logger<>)` mapping has to be registered
too, or the Kernel throws at first use (`Kernel.AddFilters()` resolves filters eagerly in the
constructor). (2) `PromptRenderContext.RenderedPrompt` is only populated *after* calling
`await next(context)` in `OnPromptRenderAsync` — it's null beforehand, since that's the whole
point of the filter (before/after around the actual rendering step).

### Error handling

Any AI provider failure (auth, rate limit, timeout, malformed JSON) is caught inside
`SemanticKernelCodeReviewer` and rethrown as `AiReviewException` (Application-level, defined in
`Application/Ai/`). `CodeReviewOrchestrator` catches `AiReviewException` per file, logs a
warning, and continues — a missing/invalid API key or a down AI provider still returns `200`
with whatever Roslyn found, it doesn't fail the request. Anything else unhandled (a bug, not a
known AI-provider failure mode) still propagates to the API's global exception handler in
`Program.cs` (`app.UseExceptionHandler`), which returns a generic `500` `ProblemDetails` and logs
the real exception server-side only, never in the response.

`GitDiffFetchException` (Application-level, `Application/Git/`) is handled differently on
purpose: `CodeReviewOrchestrator.ReviewFromUrlAsync` does **not** catch it — it propagates
straight through to `ReviewsController`, which maps it to `400` (same catch-block shape as the
existing `ArgumentException` handler). This is deliberately unlike the AI-failure handling above:
an AI failure still leaves Roslyn findings to return, but a failed diff fetch means there's no
diff at all, so there's nothing to degrade to — it has to fail the whole request.

`GitHubPublishException` (Application-level, `Application/Git/`) goes the other way, back to the
AI-failure pattern: by the time publishing runs the findings already exist, so `ReviewsController`
catches it and reports `gitHubPublish: { posted: false, error }` in the `200` response instead of
failing the request. The publisher maps GitHub `401`/`403`/`404`/`422` and network failures to
readable messages; a `403` is also what a fork PR's read-only `GITHUB_TOKEN` produces. Request-shape
errors are different — `postToGitHub` without `gitUrl`, or without `gitHubToken`, is a `400` up front
before any work happens.

### Configuration

Everything AI-related is bound once from the `"AI"` config section into `AiOptions`
(`Infrastructure/Options/AiOptions.cs`) via the Options pattern — no other code reads
`IConfiguration` directly. `Provider` is a string switch (`"OpenAI"` vs `"AzureOpenAI"`) in
`SemanticKernelServiceCollectionExtensions.AddSemanticKernelServices`; only OpenAI is actually
exercised/tested, the AzureOpenAI branch exists to show the swap point.

`ApiKey` specifically: the `OPENAI_API_KEY` environment variable (read via
`Environment.GetEnvironmentVariable`, not through the config system) always wins over whatever
`AI:ApiKey` bound from appsettings/user-secrets/`AI__ApiKey`, applied via a
`services.PostConfigure<AiOptions>(...)` step registered right after `services.Configure<AiOptions>(...)`
in `AddSemanticKernelServices`. This is why `AiOptions`'s properties are `{ get; set; }` rather
than `{ get; init; }` — `PostConfigure` mutates an already-constructed instance, which an
init-only property can't allow.

**API key auth** is a separate, opt-in concern from the AI config above: `ApiAuthOptions`
(`Api/Options/`, bound from the `"Api"` section, so `Api:ApiKey` / `Api__ApiKey`) is registered in
`Program.cs`. If `ApiKey` is empty or unset (the default) the API is open, as in local dev. If
it's set at startup, a small inline middleware requires a matching `X-Api-Key` header on every
request whose path starts with `/api` and returns `401` `ProblemDetails` otherwise; `/swagger` is
deliberately outside that prefix and stays reachable. It's a single shared-key comparison, not a
real auth scheme — the goal is to stop an anonymous caller from burning the AI provider budget, not
multi-user authorization. The check is decided once at startup, so changing the key needs a restart.
Note it is unrelated to `AI:ApiKey`/`OPENAI_API_KEY`, which is the outbound OpenAI credential.

### CI workflow (`.github/workflows/review.yml`)

Triggers on `pull_request` (opened/synchronize/reopened) and reviews this repo's own PRs with this
repo's own API. It's **self-contained, not pointed at a hosted instance**: one `ubuntu-latest` job
builds the solution, starts the API in the background on `ASPNETCORE_URLS=http://localhost:5099`,
polls it until it responds, then `curl`s it with the PR's `html_url`, `postToGitHub: true`, and the
job's own `secrets.GITHUB_TOKEN` (the write-back token is per-request, see above). Steps share one
runner, so the backgrounded process survives across steps; the runner and API are destroyed when
the job ends. Other things to know when editing it:

- `permissions: pull-requests: write` is what lets `GITHUB_TOKEN` post; `concurrency` cancels an
  in-progress review when the same PR gets a new push.
- The AI key comes from the `OPENAI_API_KEY` repo secret (which wins over `AI:ApiKey`, see
  Configuration). If it's missing, the job still succeeds with Roslyn-only findings.
- A failed post to GitHub doesn't fail the job: the last step only emits a `::warning::` when
  `gitHubPublish.posted` isn't `true`. Fork PRs always hit this (GitHub gives forks a read-only
  `GITHUB_TOKEN`, so the post is a `403`), but findings are still printed in the job log.
- `dotnet run` applies `launchSettings.json`'s `applicationUrl` over `ASPNETCORE_URLS` unless
  `--no-launch-profile` is passed, so the "start the API" step passes it — without it the API binds
  to the launch profile's port instead of `5099` and the readiness loop times out. Keep the flag.
- The diff fetch is unauthenticated (60 requests/hour/IP) while the publish uses `GITHUB_TOKEN`
  (1,000/hour/repo), so the fetch is the tighter limit; the publish is one call regardless of
  finding count.
- To review a *different* repo, this workflow can't be reused as-is (it only sees this repo's PRs);
  the README's "Using this to review a different repository" covers the two options (hosted
  instance vs. cross-repo checkout).

### Testing

Tests use hand-written fakes (`tests/AI.CodeReview.Tests/TestDoubles/`) instead of a mocking
library — the Application interfaces are small enough that this is simpler than adding Moq.
`FakeStaticCodeAnalyzer`/`FakeAiCodeReviewer` also record which file names they were called with,
so tests can assert filtering behavior (e.g. non-`.cs` files never reach either analyzer).

When writing orchestrator tests, remember: findings from `FakeStaticCodeAnalyzer` go through the
real local→real line-number remapping in the orchestrator, so a canned `Line` value on a "Roslyn"
finding is interpreted as a **local** index into that file's `AddedLines`, not a real line number.
Findings from `FakeAiCodeReviewer` are used as-is. Route findings through whichever fake actually
matches the line-number semantics you want to test.

## Notable deviations from the original spec

- **`.NET 8`, not `.NET 10`** — .NET 10 SDK wasn't installed on the dev machine; nothing in the
  app depends on .NET 10-only features.
- **Domain type is `CodeReviewResult`, not `CodeReview`** — a type literally named `CodeReview`
  is unreachable by its simple name from anywhere under the `AI.CodeReview.*` namespace tree: C#
  resolves enclosing-namespace members (the implicit `AI.CodeReview` namespace, formed by every
  project's own namespace) before `using`-alias directives, so no alias can work around it. This
  is the one thing most likely to surprise you if you go looking for a `CodeReview` type/file —
  it's `Domain/CodeReviewResult.cs`.
- **GitHub PR integration (the spec's Phase 7) is implemented** — diff fetching from a commit/PR
  URL, opt-in write-back of findings as a PR review, and `.github/workflows/review.yml`. It goes
  beyond the original Phase 6 scope; the README's Future Improvements only lists what's still
  unbuilt (e.g. an async/webhook-driven pipeline).
