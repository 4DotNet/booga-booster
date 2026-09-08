## Why

This repository has no CI at all — `.github/workflows/` does not exist — so every pull request is merged on trust. At the same time the repo carries an unusually dense, machine-readable set of standards (`CLAUDE.md`, nine `.claude/skills/csharp-*` rule sets, the ADR-derived style guide, `openspec/specs/` behavioural contracts) that a human reviewer cannot realistically hold in their head. Running an agentic CLI headless on every PR turns those checked-in standards into an automatic first-pass review, and — because BoogaBooster is also a teaching demo for AI-assisted development — makes the workflow itself part of the deliverable.

**GitHub Copilot CLI** is the review engine. It is the natural fit for a GitHub-hosted workflow: the repo already treats Copilot CLI as a first-class consumer of `.claude/` and `CLAUDE.md`, so the reviewer inherits the same standards the developers work under, and review cost lands on an existing Copilot subscription rather than a separate metered API account.

## What Changes

- Add a new GitHub Actions workflow, `.github/workflows/code-quality-check.yml`, triggered on `pull_request` (opened, reopened, synchronize, ready_for_review) against `main`.
- The workflow installs the **GitHub Copilot CLI** (`@github/copilot`, pinned to 1.0.83) on Node 22 and runs it **non-interactively** (`copilot -p …`) with the `shell` and `url` permission kinds **denied outright** — so no git, `gh`, `dotnet`, `npm`, build, install or network access — and with file access confined to the checkout. The reviewer needs none of it: the diff is written to disk before the CLI starts.
- Authentication uses a **Copilot-entitled PAT** held in a repository secret (`COPILOT_GITHUB_TOKEN`). The Actions-provided `GITHUB_TOKEN` **cannot** drive Copilot CLI — it is an installation token with no Copilot entitlement — so the reviewer's credential and the publisher's credential are necessarily different, which the two-job design already wanted.
- The CLI's **built-in GitHub MCP server is disabled** (`--disable-builtin-mcps`) and no MCP configuration is passed, so no server starts — the repository's `.mcp.json`, whose `4dotnet-csharp-style-guide` executable does not exist on a runner, is a non-issue.
- **Repo instruction auto-loading is switched off** (`--no-custom-instructions`); the prompt reads the standards by explicit path instead, so grounding is auditable and a pull request cannot reshape the reviewer's system prompt before the review begins.
- Add a checked-in review prompt at `.github/code-review/review-prompt.md` that scopes the review to the PR diff and grounds it in the repo's own standards (`CLAUDE.md`, `.claude/skills/`, the relevant `openspec/specs/` capability, `docs/` for physics changes) by explicit path rather than relying on auto-discovery.
- Copilot emits a **structured findings file** (`.code-review/findings.json`) with one entry per finding: path, line, severity (`blocking` / `major` / `minor` / `nit`), category, rationale and the standard it cites.
- A publishing step turns those findings into **inline PR review comments** anchored to the changed lines, plus one summary comment carrying any finding that could not be anchored inside the diff. Publishing uses the Actions `GITHUB_TOKEN`, so comments are attributed to the bot rather than to the human who owns the PAT.
- The check **fails the PR** when at least one `blocking` finding survives; `major` and below are advisory.
- Fork pull requests are **explicitly skipped** with a neutral, non-blocking outcome, because `pull_request` grants them no secrets.
- Document the workflow, the required secret, the Copilot org-policy prerequisite and the premium-request budget in `README.md`, `CLAUDE.md` and `.github/copilot-instructions.md`.

## Capabilities

### New Capabilities
- `pr-code-review-workflow`: the GitHub Actions workflow itself — triggers, permissions, concurrency, fork/draft guards, Copilot CLI installation and non-interactive invocation, credential handling, tool and MCP restriction, turn and timeout budget, and the exit-code gating contract.
- `pr-review-prompt`: the checked-in review instruction — what Copilot reviews, which repo standards it must ground findings in, the severity taxonomy, the tools it may use, and the shape of the findings file it must write.
- `pr-review-publication`: turning the findings file into GitHub review output — inline comments anchored to diff lines, the summary comment, handling of unanchorable findings, and idempotency across repeated pushes to the same PR.

### Modified Capabilities
<!-- None. The 20 published capabilities in openspec/specs/ all describe ride behaviour; none of their requirements change. -->

## Impact

- **New files**
  - `.github/workflows/code-quality-check.yml` — the workflow (first workflow in the repo, so `.github/workflows/` is created).
  - `.github/code-review/review-prompt.md` — the review prompt.
  - `.github/code-review/publish-review.mjs` — findings-to-GitHub publisher (Node, no new npm dependencies).
- **Modified files**
  - `README.md` — a CI section describing the review workflow, the required secret, the Copilot policy prerequisite and the premium-request cost model.
  - `CLAUDE.md` and `.github/copilot-instructions.md` — the "AI tooling" tables gain the CI workflow row (both files must stay in sync per the repo rule).
  - `.gitignore` — ignore the `.code-review/` scratch directory.
- **Repository and account configuration (manual, outside the repo)**
  - A `COPILOT_GITHUB_TOKEN` secret containing a PAT for an identity with an active **Copilot seat**. A dedicated machine account with its own seat is recommended over a maintainer's personal PAT; see `design.md` D2.
  - For Copilot Business/Enterprise, the **"Copilot in the CLI"** organisation policy must be enabled, and the pinned model must be permitted by policy.
  - Optionally, `code-quality-check` added to `main`'s required status checks once the team trusts the gate.
- **Dependencies** — `@github/copilot` is installed inside the job only; nothing is added to `package.json` or any `.csproj`. No .NET or Angular source code changes.
- **Cost** — each review draws **AI credits** from the seat's allowance rather than metered API tokens. `--max-ai-credits` caps each session outright, `--usage-output-file` reports what a run actually used into the job summary, and per-PR concurrency cancellation, a draft skip, `paths-ignore` and a job timeout bound how often the CLI starts. A shared personal allowance is the main reason to prefer a dedicated machine account.
