## Context

`.github/` today holds only instructions, prompts and skills — there is no `workflows/` directory, so this change introduces CI to the repository. That makes the workflow greenfield, but also means the review job must fetch and install everything it needs itself; there is no existing build job to hang it off.

Three properties of this repo shape the design:

1. **The standards are already checked in and machine-readable.** `CLAUDE.md`, the nine `.claude/skills/csharp-*/SKILL.md` rule sets, `.claude/skills/dto-organization`, `openspec/specs/` (20 published behavioural contracts) and `docs/` (the physics specification) are all in the tree. A CLI running in a checkout has them for free.
2. **Copilot CLI is already a first-class consumer of those files.** Per the repo's own tooling table, Copilot CLI reads `CLAUDE.md` and scans `.claude/skills/` and `.github/skills/` natively. The reviewer therefore inherits the same instructions the developers work under, with no duplicated rule copy to drift.
3. **The repo's `.mcp.json` is hostile to CI.** `4dotnet-csharp-style-guide` is a local executable expected on `PATH` and will not exist on a runner; `primeng` is an `npx` download; `microsoft-learn` is remote HTTP. None of them are wanted in a review job.

The chosen shape is fixed by the request: **GitHub Copilot CLI**, run headless, posting inline PR review comments, failing the check on blocking findings.

### What changes because the engine is Copilot CLI rather than Claude Code

This is the same workflow skeleton as a Claude-Code-driven review, but four differences are load-bearing and drive most of the decisions below:

| Concern | Consequence for this design |
| --- | --- |
| **Credential** | Copilot CLI authenticates as a *user with a Copilot seat*, via a PAT it reads from `COPILOT_GITHUB_TOKEN`. The Actions-issued `GITHUB_TOKEN` is an installation token with no Copilot entitlement and **cannot** drive the CLI. Two distinct credentials are therefore mandatory, not merely tidy. → D2, D3 |
| **No structured result envelope** | Copilot CLI has no `--output-format json`; it prints prose. There is no machine-readable per-run cost or turn count. → D4, D8 |
| **Built-in GitHub MCP server** | Copilot CLI ships with GitHub MCP enabled, including write-capable tools, running under a *user* PAT. That must be switched off. → D5 |
| **Folder trust** | Copilot CLI only loads workspace configuration for a trusted folder, and a fresh CI checkout is untrusted. This conveniently suppresses `.mcp.json`, but risks a trust prompt stalling a non-interactive run. → D6 |
| **Billing model** | Premium requests against a seat's monthly allowance, not metered tokens. Cost control becomes an account-design question, not just a turn cap. → D2, risks |

## Goals / Non-Goals

**Goals:**

- Every non-draft pull request into `main` gets an automatic review grounded in this repo's own standards, posted as line-anchored PR comments.
- The review is a **read-only** operation with respect to the repository: it cannot modify tracked files, push, or reach a write API.
- A `blocking` finding fails the `code-quality-check` job so the check can be made required on `main`.
- Bounded consumption per PR: a job timeout, per-PR concurrency cancellation, a draft skip and `paths-ignore`.
- The workflow is legible as a teaching artefact — a reader should see exactly how Copilot CLI is wired into CI, without decoding an opaque marketplace action.

**Non-Goals:**

- **No `@claude` / `@copilot` mention handling.** No conversational, comment-triggered runs on PRs or issues. Separate change.
- **No auto-fixing.** The reviewer does not commit, push or open suggestion-patches. Findings are advice.
- **No build, test or coverage gate.** A `dotnet build` / `dotnet test` / Vitest workflow is worth having and this repo lacks one, but it is a different change with different maintenance. This workflow reviews the diff; it does not compile it.
- **No fork-PR review.** See D1.
- **No GitHub Copilot code review (the platform feature).** The built-in "request a review from Copilot" is a different product with a fixed, repo-agnostic prompt. The whole point here is a review driven by *this* repo's checked-in standards, which requires the CLI.
- **No self-hosted runners.**

## Decisions

### D1 — Trigger on `pull_request`, and skip fork PRs outright

`pull_request` from a fork gets a read-only `GITHUB_TOKEN` and **no access to secrets**, so the job can neither read `COPILOT_GITHUB_TOKEN` nor post a review. The alternatives are worse:

- `pull_request_target` runs the *base* branch's workflow with a write token and secrets, but checking out the PR head under it is the classic CI-credential-exfiltration hole. Untrusted code plus a **user PAT** plus `pull-requests: write` is a particularly bad combination — worse than with a scoped API key, because a PAT carries its owner's access to every repo they can reach. Copilot reading an attacker-authored `CLAUDE.md` or skill file compounds it, since prompt-injected instructions would arrive with those credentials attached.
- The `workflow_run` two-workflow pattern is safe but doubles the surface area and the debugging cost.

**Decision:** trigger on `pull_request`; if `github.event.pull_request.head.repo.full_name != github.repository`, the job short-circuits to success with an explanatory job-summary line. BoogaBooster is a same-repo, branch-based project, so this costs nothing in practice. The `workflow_run` variant is documented in a workflow comment for anyone who later needs fork coverage.

### D2 — Authenticate with a Copilot-entitled PAT, preferably a machine account's

Copilot CLI needs a token belonging to an identity with an active Copilot seat. Three options:

- **A maintainer's PAT.** Works immediately, but every PR review spends *that person's* premium-request allowance, the token carries their full repo access into CI, and the workflow silently breaks when they rotate the token, change teams or leave.
- **A dedicated machine account with its own Copilot seat.** One more seat to pay for and an account to administer, but the allowance is the CI budget rather than a share of somebody's working capacity, and its PAT can be scoped to this repository alone.
- **A GitHub App.** Not applicable — Copilot CLI entitlement is per user, not per installation.

**Decision:** the workflow reads a secret named `COPILOT_GITHUB_TOKEN` and is indifferent to which identity backs it; the README **recommends the machine account** and states the consequences of using a personal PAT. A fine-grained PAT scoped to this repository with read-only contents is sufficient — the reviewer never writes to GitHub (D3). The secret is named for the environment variable the CLI reads, `COPILOT_GITHUB_TOKEN`, so it passes straight through with no remapping; it is set in the review step's `env` only, never at job or workflow level.

### D3 — Two jobs: `review` (Copilot PAT, no write scope) and `publish` (Actions token, no model access)

The diff Copilot reads is attacker-influenced content in the general case, and its output is model-generated text. Splitting the work keeps the write scope tiny — and here the credential split is forced anyway, since the PAT cannot post as a bot and the Actions token cannot run Copilot:

- **`review` job** — `permissions: contents: read` only. Installs the CLI, runs `copilot -p`, produces `findings.json`, uploads `.code-review/` as an artifact. Holds `COPILOT_GITHUB_TOKEN`.
- **`publish` job** — `needs: review`, `permissions: contents: read` + `pull-requests: write`. Downloads the artifact, validates it, posts the review, sets the exit code. Holds the Actions `GITHUB_TOKEN` and **not** `COPILOT_GITHUB_TOKEN`.

Two consequences that matter: the job holding a user PAT has no way to write to the PR, and review comments are attributed to `github-actions[bot]` rather than to whichever human owns the seat — which is both honest and avoids a bot's findings appearing to be a colleague's approval. Neither job ever needs `contents: write`.

### D4 — Structured findings via a written file — mandatory here, not merely preferable

With Claude Code one could at least parse a JSON envelope. Copilot CLI offers no structured output mode at all: stdout is prose intended for a human terminal, and `--log-dir` produces session logs, not a result document. Scraping either for file paths and line numbers would be indefensible in a gate.

So the prompt instructs the reviewer to end its run by writing exactly one file, `.code-review/findings.json`:

```json
{
  "summary": "one-paragraph verdict",
  "findings": [
    {
      "path": "src/DigitalTwin/FourDotnet.BoogaBooster.DigitalTwin/Domain/Ride.cs",
      "line": 214,
      "severity": "blocking",
      "category": "adr-0003-domain-model",
      "title": "public setter on aggregate property",
      "detail": "what is wrong, and what the standard requires instead",
      "standard": ".claude/skills/csharp-domain-model/SKILL.md"
    }
  ]
}
```

The file is the review's only authoritative output; stdout is captured to the job log for debugging and nothing more. If the file is missing or fails validation, the `publish` job fails loudly rather than reporting a clean review — **a review that did not happen must never look like a review that found nothing.** This failure mode is more likely with Copilot CLI than it would be with a JSON-envelope CLI, because a run that ends early leaves no machine-readable trace of having done so, which is exactly why validation is strict and the default is failure.

### D5 — Deny-by-default tools; the built-in GitHub MCP server is switched off

Copilot CLI requires approval for tool use, and in non-interactive mode an unapproved call cannot be granted. The review is therefore run with an explicit allowlist covering only what it needs — file reads, two read-only git commands, and the single `write` used for the findings file — and with the built-in **GitHub MCP server disabled**.

Disabling GitHub MCP is the important half. It is enabled by default, it includes write-capable tools, and under D2 it would be operating with a *user's* PAT. Leaving it on would hand a prompt-injectable reviewer the ability to comment, label, close or (depending on PAT scope) push — under a human's name. The reviewer does not need it: the diff and changed-file list are computed by the workflow and written to disk before the CLI starts (D7), so there is nothing to fetch.

The intended invocation shape:

```bash
copilot -p "$(cat .github/code-review/review-prompt.md)" \
  --model <pinned model> \
  --allow-tool 'write' \
  --allow-tool 'shell(git diff)' \
  --allow-tool 'shell(git log)' \
  --deny-tool 'shell' \
  --no-color \
  --log-level error
```

- **`--allow-all-tools` is never used.** It is the direct equivalent of `--dangerously-skip-permissions` and would defeat the point of the allowlist.
- No general `shell`, no `gh`, no `dotnet`, no `npm`, no network fetch. The reviewer cannot build, test, install or phone home.
- **The exact tool identifiers and the precise mechanism for disabling built-in MCP must be verified against the pinned CLI version before this ships** (task 1.4). Copilot CLI's flag surface is younger and less stable than the rest of this design, and an allowlist entry that is silently ignored is a security hole, not a typo. Verification is a task with a defined pass condition — a probe PR must show a denied `shell(rm -rf)` and an absent `github` MCP tool — rather than an assumption.
- Belt and braces: after the CLI exits, the job asserts the working tree is clean apart from `.code-review/` (D9). That assertion, not the allowlist, is what actually *guarantees* the run was read-only.

### D6 — Handle folder trust explicitly rather than relying on it

Copilot CLI loads workspace configuration only for a folder the user has trusted, and the repo's own onboarding notes say a first interactive run is needed before `.mcp.json` is picked up. In CI the checkout is always fresh and therefore untrusted.

This cuts both ways. The **benefit** is that the hostile `.mcp.json` (D-context 3) is not loaded, so the missing `4dotnet-csharp-style-guide` binary can never stall a run — no CI-specific MCP config file is needed, unlike a Claude Code setup. The **risk** is that an untrusted folder could prompt for confirmation, and a non-interactive run that blocks on a prompt burns the whole job timeout and fails opaquely.

**Decision:** treat trust as an explicit, tested precondition. A setup step establishes the runner's Copilot configuration state before the review step (writing the CLI's config directory so the workspace is pre-trusted, or passing the relevant flag) and the probe PR in task 7.x must confirm the run reaches the findings-file write without any interactive prompt. If the pinned version turns out to prompt regardless, the fallback is `--allow-all-tools` **with** a hard-denied shell — explicitly *not* the first choice, and recorded here so the trade-off is visible rather than discovered later. Whichever mechanism is used, the workflow asserts that `.mcp.json`'s servers did not start.

### D7 — Diff scope comes from git, computed before Copilot runs

`actions/checkout` with `fetch-depth: 0`, then a shell step computes the merge base against `origin/${{ github.base_ref }}` and writes `.code-review/changed-files.txt` and `.code-review/diff.patch`. The prompt tells the reviewer to read those two files first and to raise findings **only** on lines present in the diff.

This matters for cost, for signal and for D5: it keeps the review from wandering the whole repo, it means nearly every finding is anchorable to a diff line (D8), and it removes any need for the reviewer to reach GitHub for PR context. The reviewer may still `read` any file in the checkout — understanding a change usually requires reading its surroundings — it simply may not *report* on unchanged lines.

### D8 — Inline comments via one review; unanchorable findings fall back to the summary

`POST /repos/{owner}/{repo}/pulls/{number}/reviews` accepts a `comments[]` array and creates all inline comments as one atomic review. The hard constraint: every comment's `line`/`side` must land on a line the API considers part of the diff, or the whole request 422s and **no** comment is posted.

So `.github/code-review/publish-review.mjs`:

1. Parses `diff.patch` to build the set of valid `(path, line)` positions on the `RIGHT` side.
2. Partitions findings into anchorable and unanchorable.
3. Posts one review with `event: "COMMENT"` carrying the anchorable findings inline, and the summary plus a rendered list of the unanchorable findings in the review body.
4. Never uses `event: "REQUEST_CHANGES"` — that would leave the PR formally blocked by a bot until a human dismisses it. The gate is the check's exit code, which a re-run can clear.

Written in Node (already on the runner) using `fetch` against the REST API with the Actions `GITHUB_TOKEN`. No `actions/github-script`, no new dependencies. Note this is deliberately **not** done by asking Copilot to post the review through GitHub MCP, even though it could: that would put write capability in the model's hands (D5) and attribute the comments to a human (D3).

**Idempotency:** each pushed commit produces a new review, which is the natural GitHub model — a review is a point-in-time statement about a commit, and stale inline comments collapse in the UI once their lines change. A sticky-comment scheme was considered for the summary and rejected as an unnecessary second mechanism.

### D9 — The run must leave the working tree clean

After the CLI exits, the job runs `git status --porcelain` and fails unless every reported path is under `.code-review/`. This is the real read-only guarantee: it holds even if a tool identifier in the D5 allowlist is misspelled, silently ignored by the pinned version, or widened by a future CLI release. It also catches a reviewer that decides to "helpfully" fix what it found.

### D10 — Severity taxonomy, and what `blocking` is allowed to mean

Four levels: `blocking`, `major`, `minor`, `nit`. Only `blocking` fails the check, and the prompt narrows it hard, because an AI gate that fires on taste will be switched off within a week. `blocking` is reserved for:

- a violation of an explicit MUST in `CLAUDE.md` or a `.claude/skills/*` rule — endpoint logic in `Api/Program.cs`, a module referencing another module's non-`.Abstractions` project, FluentAssertions, a public setter on a domain property, a magic number in the physics, a mediator library;
- a change that contradicts a published `openspec/specs/` requirement with no corresponding change proposal;
- a correctness or safety defect the reviewer can state as a concrete failing scenario — not a suspicion.

Everything else is `major` or below. The prompt says explicitly that finding nothing is an acceptable and expected outcome, that speculation is worse than silence, and that the reviewer must not inflate severity to look useful.

`continue-on-error` is deliberately **not** used. The job's exit code is the signal, and the workflow is written so a job that dies for infrastructure reasons — missing secret, revoked Copilot seat, CLI install failure, invalid findings file — also fails. Failing open would make the gate meaningless.

### D11 — Tool-neutral asset paths, kept out of every discovery directory

CI assets live in a new `.github/code-review/` directory. Not `.github/prompts/` — that is Copilot CLI's slash-command discovery path (`*.prompt.md`), and a CI prompt dropped there would surface a bogus `/review-prompt` command. Not `.github/skills/` or `.claude/skills/`, which Copilot CLI scans as skills. Not `.claude/commands/`, which the repo's rules would then force us to mirror into `.github/prompts/`.

The directory name says *what it is for* rather than *which vendor's CLI runs it*, so a future engine swap does not leave a lying path — a real consideration given this change has already swapped engines once. The scratch directory is `.code-review/`, gitignored, for the same reason.

### D12 — Pin the CLI version, the model and Node

`npm i -g @github/copilot@<exact version>` on `node-version: 22` (Copilot CLI requires Node 22+, above the runner default), with npm caching via `actions/setup-node`. An unpinned global install means the gate's behaviour can change between two runs of the same commit, which is unacceptable for a required check; bumping the pin then becomes a normal, reviewable PR. `--model` is pinned for the same reason rather than left to the CLI default — with the added wrinkle that model availability depends on Copilot org policy, so the README records which model the pin assumes and the workflow fails with a clear message if it is unavailable.

## Risks / Trade-offs

- **Copilot CLI's flag and tool surface is the least stable part of this design.** A renamed flag or a silently-ignored `--allow-tool` value degrades the security model without any error. → The D9 working-tree assertion is version-independent and catches the consequence; task 1.4 verifies identifiers against the pinned version with a defined pass condition; D12 pins the version so behaviour cannot shift underneath a passing check. Treat a CLI version bump as a change that re-runs the probe PR, not a rubber-stamp.
- **The reviewer authenticates as a human with a Copilot seat.** A personal PAT carries that person's full repo access into CI, and its rotation or departure silently breaks the gate. → D2 recommends a dedicated machine account with a repo-scoped fine-grained PAT; D3 ensures the PAT-holding job has no write permission and never touches the GitHub API; publishing runs under the Actions token so nothing is attributed to the seat owner.
- **Premium-request consumption is a shared, non-obvious budget, and Copilot CLI reports no per-run cost.** A busy PR day can eat into an individual's monthly allowance with no signal in the run. → A dedicated seat makes the budget legible (D2); concurrency cancellation, the draft skip, `paths-ignore` and the job timeout bound the request count; the README records the observed requests-per-PR from the calibration period, since the workflow cannot report it directly. If the allowance is exhausted the CLI fails and the check fails — noisily, not silently.
- **Folder trust could stall a non-interactive run.** → D6 makes trust an explicit setup step with a probe-PR pass condition, and records the `--allow-all-tools`-plus-denied-shell fallback as a visible trade-off rather than a surprise.
- **False-positive `blocking` findings block merges and erode trust.** → The `blocking` bar in D10 is narrow and enumerated; the prompt is explicit that a clean review is a good outcome; `event: COMMENT` keeps GitHub itself from blocking the PR; a maintainer can re-run or merge past a not-yet-required check. Treat the first weeks as calibration and tighten the prompt from real output *before* making the check required.
- **Prompt injection from PR content.** A PR can add text to a source file, to `CLAUDE.md`, or to a skill file — all of which Copilot CLI auto-loads — instructing the reviewer to pass everything. → The `review` job holds no write permission and no GitHub tools (D3, D5), so the worst outcome is a useless review, not a compromised repo. The prompt additionally requires any diff touching its own review configuration (`.github/code-review/`, `.github/workflows/`, `CLAUDE.md`, `.claude/`) to be reported at `major` or higher, so such a diff is surfaced even when the reviewer has been talked into silence elsewhere.
- **Non-determinism: the same diff can yield different findings.** → Accepted, and inherent to the approach. It is why the gate is narrow, why the check is re-runnable, and why this workflow is not a substitute for `dotnet test`. Pinning the CLI and the model (D12) removes the avoidable share of the variance.
- **A missing secret, a revoked seat, or a policy-blocked model fails the check on every PR.** → The first substantive step asserts the secret is present and fails with a message naming `COPILOT_GITHUB_TOKEN` and pointing at the README; entitlement and model-policy failures are surfaced with the CLI's own error rather than swallowed, and the README lists both as the first things to check.
- **The review API's diff-position rules are fiddly and version-sensitive.** → All position computation lives in one small, unit-testable Node module, and unanchorable findings degrade into the review body (D8) instead of 422-ing the entire review away. One bad line number must not suppress the other twelve findings.
- **Reviewing without building means whole classes of defect are invisible.** → Stated as a non-goal and called out in the README, so nobody mistakes a green `code-quality-check` for a green build. The natural follow-up is a real build/test/coverage workflow.
