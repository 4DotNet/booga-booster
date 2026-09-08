## Context

`.github/` today holds only instructions, prompts and skills — there is no `workflows/` directory, so this change introduces CI to the repository. That makes the workflow greenfield, but also means the review job must fetch and install everything it needs itself; there is no existing build job to hang it off.

Three properties of this repo shape the design:

1. **The standards are already checked in and machine-readable.** `CLAUDE.md`, the nine `.claude/skills/csharp-*/SKILL.md` rule sets, `.claude/skills/dto-organization`, `openspec/specs/` (20 published behavioural contracts) and `docs/` (the physics specification) are all in the tree. A CLI running in a checkout has them for free.
2. **Copilot CLI is already a first-class consumer of those files.** Per the repo's own tooling table, Copilot CLI reads `CLAUDE.md` and scans `.claude/skills/` and `.github/skills/` natively. The reviewer therefore inherits the same instructions the developers work under, with no duplicated rule copy to drift.
3. **The repo's `.mcp.json` is hostile to CI.** `4dotnet-csharp-style-guide` is a local executable expected on `PATH` and will not exist on a runner; `primeng` is an `npx` download; `microsoft-learn` is remote HTTP. None of them are wanted in a review job.

The chosen shape is fixed by the request: **GitHub Copilot CLI**, run headless, posting inline PR review comments, failing the check on blocking findings.

### What changes because the engine is Copilot CLI rather than Claude Code

This is the same workflow skeleton as a Claude-Code-driven review, but five differences are load-bearing and drive most of the decisions below.

**All rows below were verified against Copilot CLI 1.0.83** — the version this change pins — by reading `copilot --help` and the `permissions`, `environment` and `config` help topics. Earlier drafts of this document asserted three things about the CLI that turned out to be false; they are corrected here and called out in D4, D5 and D6 so the record of what was assumed versus verified stays visible.

| Concern | Consequence for this design |
| --- | --- |
| **Credential** | Copilot CLI authenticates as a *user with a Copilot seat*, via a PAT it reads from `COPILOT_GITHUB_TOKEN` — which the CLI documents as taking precedence over both `GH_TOKEN` and `GITHUB_TOKEN`. That precedence matters: Actions puts `GITHUB_TOKEN` in the environment routinely, and the reviewer must not silently fall back to it. The Actions-issued `GITHUB_TOKEN` is an installation token with no Copilot entitlement and **cannot** drive the CLI. Two distinct credentials are therefore mandatory, not merely tidy. → D2, D3 |
| **Permissions are two independent layers** | `--available-tools` / `--excluded-tools` decide what the model can *see*; `--allow-tool` / `--deny-tool` / `--allow-all-tools` decide what prompts for approval. **Denial always beats allow, including `--allow-all-tools`.** Crucially, `--allow-all-tools` is *required* for non-interactive mode, so the restriction has to come from deny rules rather than from withholding blanket approval. → D5 |
| **Built-in GitHub MCP server** | Copilot CLI ships with `github-mcp-server` enabled, including write-capable tools, running under a *user* PAT. `--disable-builtin-mcps` switches it off. → D5 |
| **Structured output and usage reporting both exist** | `--output-format json` emits JSONL, and `--usage-output-file` writes usage statistics as JSON. `--max-ai-credits` caps spend for the session outright. → D4, D8, D10 |
| **Path access is already confined** | File access defaults to the working directory and its subdirectories plus the system temp directory; `--disallow-temp-dir` removes the latter. The checkout is therefore the blast radius by default, before any rule we add. → D5, D9 |

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

### D4 — Structured findings via a written file, alongside the CLI's own JSON output

**Correction.** An earlier draft justified this decision by claiming Copilot CLI has no structured output mode. That is false: 1.0.83 supports `--output-format json` (JSONL, one object per line) and `--usage-output-file`. The decision stands, but on different and narrower grounds.

`--output-format json` gives a *transcript* — a stream of session events — not a result document. Recovering a finding's file path and line number from it means scraping whichever assistant message happened to contain the conclusions, which is the same brittleness as parsing prose with extra steps. A gate should not depend on the shape of a model's closing message.

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

The file is the review's only authoritative source of findings. The CLI's own outputs are still captured, for different jobs: `--output-format json` goes to the job log as a debuggable transcript, and `--usage-output-file` produces the usage figures the job summary reports (D8). If the findings file is missing or fails validation, the `publish` job fails loudly rather than reporting a clean review — **a review that did not happen must never look like a review that found nothing.**

### D5 — Restrict by denying permission *kinds*, not by allowlisting tool names

**Correction.** An earlier draft stated that `--allow-all-tools` "is never used" and built an allowlist of `--allow-tool` entries around that. The premise was wrong twice over: `--allow-all-tools` is *required* for non-interactive mode, so the invocation as drafted would not have run at all; and `--allow-tool` controls approval prompts, not which tools exist.

The verified model has two independent layers, and denial beats everything:

- **Visibility** — `--available-tools` (allowlist) and `--excluded-tools` (denylist) decide what the model can see.
- **Approval** — `--allow-tool`, `--deny-tool` and `--allow-all-tools` decide what prompts. **Deny rules take precedence over allow rules, including over `--allow-all-tools`.**

`--deny-tool` and `--allow-tool` take a pattern of the form `kind(argument)`, where the documented kinds are `shell(command:*?)`, `write(path?)`, `url(domain?)` and `<mcp-server-name>(tool?)`. Those four kinds are the whole vocabulary the design needs — no internal tool identifiers appear anywhere in it.

**Decision:** grant blanket approval (as non-interactive mode requires) and then deny the dangerous kinds outright:

```bash
copilot -p "$(cat .github/code-review/review-prompt.md)" \
  --model <pinned model> \
  --allow-all-tools \
  --deny-tool 'shell' \
  --deny-tool 'url' \
  --disable-builtin-mcps \
  --no-custom-instructions \
  --no-ask-user \
  --disallow-temp-dir \
  --secret-env-vars=COPILOT_GITHUB_TOKEN \
  --no-remote --no-remote-export \
  --no-auto-update \
  --output-format json \
  --usage-output-file .code-review/usage.json \
  --max-ai-credits <cap> \
  --no-color \
  --log-level error
```

Why each restriction is there:

- **`--deny-tool 'shell'`** — denies *every* shell command, so no `git`, `gh`, `dotnet`, `npm` or anything else. This is stronger than the drafted `shell(git diff)` allowlist and, better, it removes a need rather than managing one: the workflow writes `diff.patch` and `changed-files.txt` to disk before the CLI starts (D7), so the reviewer has no reason to run git at all. Denying the whole kind also sidesteps the precedence trap — a `--deny-tool 'shell'` alongside `--allow-tool 'shell(git diff)'` would have denied the git command too, since denial wins.
- **`--deny-tool 'url'`** — the `url` kind gates both the shell and web-fetch tools, so the reviewer cannot reach the network. Note `--allow-all-tools` does *not* imply URL access (`--allow-all` = `--allow-all-tools --allow-all-paths --allow-all-urls`), so this is belt-and-braces rather than strictly required.
- **`--disable-builtin-mcps`** — switches off `github-mcp-server`, which is enabled by default, carries write-capable tools, and would be running under a *user's* PAT (D2). Leaving it on would hand a prompt-injectable reviewer the ability to comment, label, close or push under a human's name. The reviewer needs nothing from it (D7).
- **`--no-custom-instructions`** — stops `AGENTS.md` and related files from silently shaping the system prompt. The prompt reads the standards by explicit path instead (D7, and the `pr-review-prompt` spec), so grounding is auditable and a PR cannot rewrite the reviewer's instructions before the review starts. This also removes a dependency on the repo's claim that Copilot CLI auto-loads `CLAUDE.md` and `.claude/skills/` — `--help` documents `.github/skills` and `.github/agents` for `--add-dir`, and `AGENTS.md` for custom instructions, but never `.claude/skills`, so that claim may be stale. Explicit reads make it moot.
- **`--no-ask-user`** — disables the `ask_user` tool so the agent cannot stall the job waiting on a question it will never get answered.
- **`--disallow-temp-dir`** — path access already defaults to the working directory plus the system temp directory; this drops the temp directory, leaving the checkout as the only writable area.
- **`--secret-env-vars=COPILOT_GITHUB_TOKEN`** — strips the token's value from tool environments and redacts it from output, so the credential cannot be echoed into the findings file, the transcript or the job log.
- **`--no-remote --no-remote-export`** — the CLI can export or remote-control a session via GitHub web and mobile. A CI review of an unmerged diff has no business being exportable, so both are off.
- **`--max-ai-credits`** — a hard per-session spend cap, which is a far better bound than a turn count (D10).
- **`--no-auto-update`** — auto-update is already disabled when `CI` is set, but stating it keeps the pinned version honest (D12).

Belt and braces regardless: after the CLI exits the job asserts the working tree is clean apart from `.code-review/` (D9). That assertion, not the flag list, is what actually *guarantees* the run was read-only — and it is the reason this design survived its own flags being wrong.

### D6 — Non-interactive execution needs hardening, not folder trust

**Correction.** An earlier draft devoted a decision to folder trust, on the theory that Copilot CLI only loads workspace configuration for a trusted folder and that an untrusted CI checkout might stall on a confirmation prompt. Nothing in 1.0.83's documented surface supports that. There is no folder-trust prompt in `-p` mode; `--add-dir` grants access to *additional* directories, and path access is confined to the working directory by default. The gating that actually exists is tool approval, handled in D5.

The MCP half of that worry also dissolves. Copilot CLI reads MCP configuration from `~/.copilot/mcp-config.json` and `--additional-mcp-config`; the repo-root `.mcp.json` is never mentioned in its documentation. Combined with `--disable-builtin-mcps` and passing no MCP config at all, **no MCP server starts**, so the missing `4dotnet-csharp-style-guide` executable is a non-issue. No CI-specific MCP config file is needed — unlike the Claude Code shape of this design, which required one.

**Decision:** there is no trust step. What the run does need is the non-interactive hardening already listed in D5 — `--allow-all-tools` to satisfy the mode, `--no-ask-user` so nothing waits on a human, and the job `timeout-minutes` as the backstop. The probe pull request (task 7.2) verifies the run reaches the findings-file write without stalling, that a shell command is refused, and that no MCP tool is present. Verification stays in the plan; the invented mechanism does not.

### D6b — The reviewer's own configuration comes from the base branch

Found while implementing, not while designing. The `review` job checks out the **pull
request head**, because that is the code under review (D7). But the review prompt and the
publisher live in that same checkout — so as first written, a pull request would have been
reviewed using *its own* copy of the prompt. A PR could then replace the prompt wholesale,
and the self-configuration rule that was supposed to flag exactly that (D10, and the
`pr-review-prompt` spec) would have been deleted along with everything else that would
have caught it. The mitigation would have removed itself.

**Decision:** the review's configuration is always read from the merge base, never from
the head:

- The prompt is extracted with `git show <base_sha>:.github/code-review/review-prompt.md`
  into `$RUNNER_TEMP`, outside the working tree so it cannot trip the D9 assertion, and
  passed to `-p` from there. The shell reads it, not the CLI, so no path permission is
  involved.
- The publisher is obtained by the `publish` job checking out
  `.github/code-review` at `github.event.pull_request.base.sha` — it never runs the PR's
  version of the script that decides whether the check passes.

One documented exception: when the base branch has no prompt yet — the bootstrapping case,
including the pull request that introduces this workflow — the job falls back to the head's
copy and emits a `::warning::` telling the reviewer to read the prompt by hand. Failing
instead would make the workflow unable to land itself; failing *silently* is what the
warning exists to prevent.

A pull request can still change the prompt for *subsequent* pull requests, which is
correct — that is what review is for, and the self-configuration rule ensures a human is
pointed at the diff.

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

Written in Node (already on the runner) using `fetch` against the REST API with the Actions `GITHUB_TOKEN`. No `actions/github-script`, no new dependencies. Note this is deliberately **not** done by asking Copilot to post the review through GitHub MCP, even though `github-mcp-server` ships enabled and could: that would put write capability in the model's hands (D5) and attribute the comments to a human (D3).

The publisher also renders the usage figures from `--usage-output-file` into the job summary, so a run's cost is visible on its own page (D10).

**Idempotency:** each pushed commit produces a new review, which is the natural GitHub model — a review is a point-in-time statement about a commit, and stale inline comments collapse in the UI once their lines change. A sticky-comment scheme was considered for the summary and rejected as an unnecessary second mechanism.

### D9 — The run must leave the working tree clean

After the CLI exits, the job runs `git status --porcelain` and fails unless every reported path is under `.code-review/`. This is the real read-only guarantee: it holds even if a D5 flag is misspelled, silently ignored by the pinned version, renamed by a future release, or — as actually happened during this change's reconnaissance — based on a misreading of how the CLI's permissions work. It also catches a reviewer that decides to "helpfully" fix what it found.

This is the one control in the design that does not depend on any claim about the CLI, which is why it is not optional and why the probe pull request tests it directly (task 7.9).

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

`npm i -g @github/copilot@1.0.83` on `node-version: 22`, with npm caching via `actions/setup-node`. 1.0.83 is `latest` at the time of writing (`1.0.84-1` is on the `prerelease` tag and is not used). Note the package declares no `engines` constraint, so Node 22 is pinned on GitHub's stated requirement rather than on anything npm will enforce — which is a reason to pin it explicitly rather than inherit the runner default.

An unpinned global install means the gate's behaviour can change between two runs of the same commit, which is unacceptable for a required check; bumping the pin then becomes a normal, reviewable PR. Because this change has already had three of its assumptions about the CLI's flag surface invalidated (D4, D5, D6), a version bump is treated as a change that **re-runs the probe pull request** of task 7.2, not a rubber-stamp.

`--model` is pinned for the same reason rather than left to the CLI default (`auto` lets Copilot choose, which is the opposite of what a gate wants) — with the added wrinkle that model availability depends on Copilot org policy, so the README records which model the pin assumes and the workflow fails with a clear message if it is unavailable.

### D13 — Two models review independently, in a matrix

One reviewer is one opinion. The `review` job is therefore a matrix with one leg per model — `claude-sonnet-5` and `gpt-5.6-terra` — and the legs are **isolated by construction**: each gets the same byte-identical `diff.patch` from the `prepare` job, neither can see the other's findings, and neither knows another review is happening. Agreement between them is then evidence rather than an artefact of one model having read the other's output.

`fail-fast: false`, because both reviews must complete: if one model fails, the job fails, `compare` never runs and nothing is published. A comparison of one review must never be mistaken for a comparison of two.

The guards and the diff computation moved into a separate `prepare` job for a mundane but real reason: **outputs of a matrix job are ambiguous** — GitHub keeps whichever leg finished last — and `skipped` / `empty` gate three downstream jobs. Computing them once also guarantees both reviewers see the same input, which the fairness of the comparison depends on.

The roster lives in exactly one place, the matrix. Artifacts are named `code-review-<model>`, and both the comparison and the publisher derive the reviewer list from those artifact names, so adding a third model is a two-line matrix edit and nothing else.

### D14 — Compare mechanically first, then narrate; the narrative cannot move the gate

The comparison has two halves, deliberately separated:

- **`compare-reviews.mjs` — deterministic and unit-tested.** It pairs findings that describe the same problem (same file, within five lines, and either the same `category` or a title-token overlap of 0.5+), and reports per-reviewer counts, what more than one reviewer found, what only one found, the severity disagreements, and an agreement rate. Every pairing records *how* it matched (`exact`, `same-category`, `similar-title`) so a loose pairing is visible as one. A cluster holds at most one finding per model, because each reviewer already deduplicates its own list.
- **A third Copilot session — judgement.** It reads the deterministic comparison, both raw findings files and the diff, and writes `comparison.md`: which single-reviewer findings are real, plausible or likely false positives, who graded a disagreement correctly, and which review was more useful. This is the half a script cannot do — two findings can describe the same defect in words that share no tokens at all.

**The narrative is commentary.** It is rendered into the review body and nothing else: it cannot change a severity, add or remove a finding, or affect the exit code. That separation is what keeps a third model's opinion from quietly becoming the gate.

Two consequences worth stating. First, the narrating model is one of the two reviewers (`COMPARE_MODEL`), so it reads its own output alongside a rival's; `compare-prompt.md` tells it not to try to identify or favour either, and the published body names who wrote the narrative so a reader can discount it. Second, the `compare` job checks out the **base branch**, not the PR head: the standards a judgement rests on are then the versions this pull request cannot have edited, and the change reaches the model only as `diff.patch`.

The narrative is also the one place this workflow tolerates a failure. If that session fails, a warning is emitted and publication continues with the figures alone — both reviews are already complete at that point, and losing a valid review over a missing prose section would be the wrong trade. It is expressed as `if ! copilot …` in the step rather than `continue-on-error:`, which would swallow the working-tree assertion too.

### D15 — The gate is the union of the reviewers, not their consensus

A merged finding inherits the **worst** severity any reviewer gave it. One model catching a blocking violation the other missed still fails the check.

The alternative — gate only on findings both models flagged `blocking` — was considered and rejected. `blocking` is already narrow and enumerated (D10): an explicit MUST violation, a contradicted published spec, or a demonstrable failure. A rule that a MUST violation only counts when two models independently notice it weakens the gate rather than making it more precise, and `CLAUDE.md` warns against weakening this configuration casually. False positives are addressed where they belong — the `blocking` bar, and the visible attribution that marks a lone finding as one — not by raising the bar to unanimity.

## Risks / Trade-offs

- **Copilot CLI's flag surface is the least stable part of this design — demonstrably so.** Three of this document's original claims about it were false (D4, D5, D6), and one of them would have shipped an invocation that could not run non-interactively at all. A renamed flag or a silently-ignored deny rule degrades the security model with no error. → The D9 working-tree assertion depends on no claim about the CLI and catches the consequence; the design now uses only the four documented permission *kinds* rather than internal tool identifiers, which is the more stable vocabulary; D12 pins the version and makes a bump re-run the probe PR (task 7.2). The general lesson is recorded rather than smoothed over: verify this CLI's surface, do not reason about it from analogy with other agentic CLIs.
- **The reviewer authenticates as a human with a Copilot seat.** A personal PAT carries that person's full repo access into CI, and its rotation or departure silently breaks the gate. → D2 recommends a dedicated machine account with a repo-scoped fine-grained PAT; D3 ensures the PAT-holding job has no write permission and never touches the GitHub API; publishing runs under the Actions token so nothing is attributed to the seat owner; `--secret-env-vars` keeps the token out of transcripts and logs.
- **AI-credit consumption is a shared budget.** A busy PR day draws down the seat's allowance, and if it is a person's seat that is their working capacity. → `--max-ai-credits` imposes a hard per-session cap, which is a real bound rather than a proxy for one; `--usage-output-file` makes each run's usage visible in the job summary, so drift is noticed in the run rather than on a bill; a dedicated seat makes the budget legible (D2); concurrency cancellation, the draft skip, `paths-ignore` and the job timeout bound how often the CLI starts at all. If the allowance is exhausted the CLI fails and the check fails — noisily, not silently.
- **False-positive `blocking` findings block merges and erode trust.** → The `blocking` bar in D10 is narrow and enumerated; the prompt is explicit that a clean review is a good outcome; `event: COMMENT` keeps GitHub itself from blocking the PR; a maintainer can re-run or merge past a not-yet-required check. Treat the first weeks as calibration and tighten the prompt from real output *before* making the check required.
- **Prompt injection from PR content.** A PR can add text to any file the reviewer reads — a source file, `CLAUDE.md`, a skill file — instructing it to pass everything. → `--no-custom-instructions` (D5) stops repo files from shaping the system prompt, so injected text arrives as file content the prompt has framed as data rather than as instructions the CLI itself loaded. The `review` job holds no write permission, no shell, no network and no GitHub tools (D3, D5), so the worst outcome is a useless review, not a compromised repo. The prompt additionally requires any diff touching its own review configuration (`.github/code-review/`, `.github/workflows/`, `CLAUDE.md`, `.claude/`) to be reported at `major` or higher, so such a diff is surfaced even when the reviewer has been talked into silence elsewhere. Residual risk is accepted: a reviewer that has been successfully misled produces a clean review, and only a human reading the diff will notice.
- **Three Copilot sessions per run instead of one.** Two reviews plus the narrative roughly triples the AI-credit draw of a run, and `--max-ai-credits` is a *per-session* cap, so the workflow ceiling is three times what the number suggests. → Concurrency cancellation matters more than it did with one reviewer and is unchanged; the job summary now reports usage per session, so the real cost is visible per run rather than inferred; the second reviewer and the narrative are each one matrix or `env:` edit away from being removed if the budget does not justify them.
- **Pairing findings mechanically can merge two different problems, or split one.** Two unrelated findings on neighbouring lines with the same category will be merged; the same defect described in different words five lines apart will not. → The window is narrow (five lines) and a same-category match is required unless titles genuinely overlap; every pairing records how loose it was, and `compare-prompt.md` explicitly asks the narrating model to call out a pairing that looks wrong. A mis-pairing costs a confusing comment, never a lost finding: both reviewers’ details are carried into the merged finding, and the severity is the worst of the two.
- **Non-determinism: the same diff can yield different findings.** → Accepted, and inherent to the approach. It is why the gate is narrow, why the check is re-runnable, and why this workflow is not a substitute for `dotnet test`. Pinning the CLI and the model (D12) removes the avoidable share of the variance.
- **A missing secret, a revoked seat, or a policy-blocked model fails the check on every PR.** → The first substantive step asserts the secret is present and fails with a message naming `COPILOT_GITHUB_TOKEN` and pointing at the README; entitlement and model-policy failures are surfaced with the CLI's own error rather than swallowed, and the README lists both as the first things to check.
- **The review API's diff-position rules are fiddly and version-sensitive.** → All position computation lives in one small, unit-testable Node module, and unanchorable findings degrade into the review body (D8) instead of 422-ing the entire review away. One bad line number must not suppress the other twelve findings.
- **Reviewing without building means whole classes of defect are invisible.** → Stated as a non-goal and called out in the README, so nobody mistakes a green `code-quality-check` for a green build. The natural follow-up is a real build/test/coverage workflow.
