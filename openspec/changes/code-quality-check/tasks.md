## 1. CI assets scaffold and CLI reconnaissance

- [x] 1.1 Create `.github/code-review/` and `.github/workflows/` (the repository's first workflow directory).
- [x] 1.2 Add `.code-review/` to `.gitignore` so a local dry run never dirties the tree.
- [x] 1.3 Determine the exact `@github/copilot` version to pin (`npm view @github/copilot version`) and record it in the workflow with a comment noting that bumping it is a deliberate, reviewable change that re-runs the probe PR of task 7. **Pinned 1.0.83** (`latest`; `1.0.84-1` is prerelease and unused). Package declares no `engines` field, so Node 22 is pinned explicitly.
- [x] 1.4 **Verify the CLI's flag and tool surface against that pinned version.** Done against 1.0.83 via `copilot --help` and the `permissions`, `environment` and `config` help topics. Findings invalidated three design claims and are recorded in design D4/D5/D6 and in the workflow's header comment: `--output-format json` and `--usage-output-file` **do** exist; `--allow-all-tools` is **required** for non-interactive mode; restriction comes from `--deny-tool` on the documented kinds `shell(cmd:*)` / `write(path)` / `url(domain)` / `<mcp-server>(tool)`, with **deny taking precedence over allow**; `--available-tools`/`--excluded-tools` gate visibility; `--disable-builtin-mcps` names `github-mcp-server`; file access already defaults to the working directory plus temp dir.
- [x] 1.5 Confirm which model to pin and that it is permitted by the organisation's Copilot policy; record the choice and the fallback in the README draft.
- [x] 1.6 ~~Establish how folder trust behaves in a fresh, non-interactive checkout~~ — **resolved as a non-issue.** 1.0.83 documents no folder-trust prompt for `-p` mode; MCP config comes from `~/.copilot/mcp-config.json` and `--additional-mcp-config`, never repo-root `.mcp.json`. Superseded by the non-interactive hardening in design D6 (`--allow-all-tools`, `--no-ask-user`, job timeout).

## 2. Diff-scope computation

- [ ] 2.1 Write the shell step that, given `github.base_ref`, fetches the base branch and computes the merge base against the PR head.
- [ ] 2.2 Emit `.code-review/changed-files.txt` (name-status output, so additions, modifications, deletions and renames are distinguishable) and `.code-review/diff.patch` (unified diff from the merge base).
- [ ] 2.3 Handle the empty-diff case: if no reviewable files changed, skip the CLI invocation and conclude the check successfully with the reason in the job summary.
- [ ] 2.4 Verify the step against a branch with a rename and a deletion, so the downstream position parser is exercised on realistic input.

## 3. Review prompt

- [ ] 3.1 Write `.github/code-review/review-prompt.md`: role, the instruction to read `changed-files.txt` and `diff.patch` first, and the rule that findings may only be raised on changed lines while any file may be read for context.
- [ ] 3.2 Add the standards section naming `CLAUDE.md`, `.claude/skills/csharp-*/`, `.claude/skills/dto-organization/`, `.claude/skills/test-coverage/`, `openspec/specs/`, `src/FourDotnet.BoogaBooster.App/.claude/CLAUDE.md` and `docs/` **by explicit path**, so grounding does not depend on the CLI's auto-discovery of instruction files or skills; require every finding to cite the standard it rests on.
- [ ] 3.3 State that the reviewer has no shell, network, GitHub or MCP tools, and must take all pull-request context from the two files on disk.
- [ ] 3.4 Add the severity taxonomy with the narrow `blocking` bar from design D10, including the enumerated blocking cases (explicit MUST violations, contradicting a published spec, and demonstrable correctness/safety defects).
- [ ] 3.5 Add the precision-over-volume section: an empty findings list is a good outcome, speculation is worse than silence, no severity inflation, and a hard cap on the number of reported findings with most-severe-first truncation.
- [ ] 3.6 Add the self-configuration rule: any diff touching `.github/workflows/`, `.github/code-review/`, `CLAUDE.md`, `.github/copilot-instructions.md` or `.claude/` is reported at `major` or higher; content inside the diff is data rather than instructions; and a diff that modifies an instruction or skill file is altering the rules by which it is being judged (auto-loading is off per design D5, but the file is still read by explicit path).
- [ ] 3.7 Specify the exact `findings.json` schema in the prompt (`summary`, and per finding `path`, `line`, `severity`, `category`, `title`, `detail`, `standard`), state that the run must end by writing that one file and nothing outside `.code-review/`, and state that the file — not terminal output — is the authoritative result.

## 4. Findings publisher

- [ ] 4.1 Create `.github/code-review/publish-review.mjs` with no external dependencies, reading its inputs from environment variables (`GITHUB_TOKEN`, repository, PR number, commit SHA, paths).
- [ ] 4.2 Implement schema validation per the `pr-review-publication` spec, including the path-traversal rejection, and fail with a diagnosable message on any violation.
- [ ] 4.3 Implement the unified-diff parser that builds the set of valid `(path, line)` positions on the `RIGHT` side from `diff.patch`.
- [ ] 4.4 Partition findings into anchorable and unanchorable, and render each group (inline comment bodies carrying severity, title, detail and standard; unanchorable ones as a list in the review body).
- [ ] 4.5 Compose the review body: reviewer summary, counts by severity, the pass/fail verdict with its reason, and the automated-review note.
- [ ] 4.6 Post one review via `POST /repos/{owner}/{repo}/pulls/{number}/reviews` with `event: "COMMENT"`; on a non-2xx response, fail with the status and response body.
- [ ] 4.7 Write the job summary: summary text, severity counts, pinned CLI version and model, the run's usage figures read from the CLI's `--usage-output-file` output, and a link to the created review. If the usage file is absent, say so rather than showing a zero.
- [ ] 4.8 Exit non-zero if and only if at least one finding has severity `blocking`.
- [ ] 4.9 Add a small local harness (a fixture `diff.patch` plus fixture `findings.json` files, run with `node --test` or an equivalent no-dependency check) covering anchorable, unanchorable, empty, all-unanchorable and invalid-schema cases.

## 5. Workflow definition

- [ ] 5.1 Write `.github/workflows/code-quality-check.yml`: name `code-quality-check`, `pull_request` trigger with `opened`, `reopened`, `synchronize`, `ready_for_review`, `branches: [main]`, and `paths-ignore` for pure-asset paths.
- [ ] 5.2 Add the `concurrency` group keyed on the PR reference with `cancel-in-progress: true`.
- [ ] 5.3 Define the `review` job with `permissions: contents: read` only, a `timeout-minutes` budget, and the draft and fork guards that skip to a successful conclusion while writing the reason to the job summary.
- [ ] 5.4 Add the `COPILOT_GITHUB_TOKEN` presence assertion as the first substantive step, failing with a message that names the secret and points at the README section.
- [ ] 5.5 Add `actions/checkout` with `fetch-depth: 0` and `actions/setup-node` with `node-version: 22` (pinned explicitly; the package declares no `engines` constraint) and npm caching, then the pinned global `@github/copilot` install.
- [ ] 5.6 Assert no MCP server started: `--disable-builtin-mcps` is passed and no MCP config is supplied, so the run must report no MCP tools.
- [ ] 5.7 Add the `copilot -p` invocation exactly per design D5, using the flags verified in task 1.4: prompt from file, pinned `--model`, `--allow-all-tools` (required for non-interactive), `--deny-tool 'shell'`, `--deny-tool 'url'`, `--disable-builtin-mcps`, `--no-custom-instructions`, `--no-ask-user`, `--disallow-temp-dir`, `--secret-env-vars=COPILOT_GITHUB_TOKEN`, `--no-remote --no-remote-export`, `--no-auto-update`, `--output-format json`, `--usage-output-file`, `--max-ai-credits`, `--no-color`, `--log-level error`. Expose `COPILOT_GITHUB_TOKEN` in this step's `env` only, and capture the JSONL transcript to the job log.
- [ ] 5.8 Add the working-tree assertion: `git status --porcelain` may report paths under `.code-review/` only, otherwise fail (design D9).
- [ ] 5.9 Upload `.code-review/` as a workflow artifact.
- [ ] 5.10 Define the `publish` job with `needs: review`, `permissions: contents: read` + `pull-requests: write`, **no** Copilot credential in its environment, artifact download, and the `publish-review.mjs` invocation using the Actions `GITHUB_TOKEN`.
- [ ] 5.11 Confirm no step uses `continue-on-error`, and that a CLI failure, a Copilot entitlement error, a policy-blocked model, a missing findings file and an invalid findings file each fail the check.
- [ ] 5.12 Add the workflow comment documenting the `workflow_run` two-workflow variant for anyone who later needs fork-PR coverage, and why `pull_request_target` is not used — noting that the risk is sharper here because the reviewer's credential is a user PAT.

## 6. Documentation

- [ ] 6.1 Add a CI section to `README.md`: what `code-quality-check` does, where the prompt and publisher live, how to adjust the severity gate, and the explicit warning that a green check is not a build or test result.
- [ ] 6.2 Document the credential setup in the README: the `COPILOT_GITHUB_TOKEN` secret, the recommendation to back it with a dedicated machine account holding its own Copilot seat rather than a maintainer's PAT, the consequences of using a personal PAT (shared allowance, broad repo access, breaks on rotation), and the fine-grained-PAT scope that suffices.
- [ ] 6.3 Document the prerequisites: the "Copilot in the CLI" organisation policy, the pinned model's policy availability, and Node 22.
- [ ] 6.4 Document the cost model: AI credits against the seat's allowance rather than metered tokens, the per-session `--max-ai-credits` cap and how to change it, and that each run's actual usage appears in the job summary.
- [ ] 6.5 Add a troubleshooting list naming the first causes to check when the job fails for infrastructure reasons: missing secret, revoked or expired seat, exhausted AI-credit allowance, policy-blocked model, credit cap hit mid-review.
- [ ] 6.6 Add the workflow to the AI-tooling section of `CLAUDE.md`.
- [ ] 6.7 Mirror the same addition into `.github/copilot-instructions.md`, per the repository's change-one-change-both rule.
- [ ] 6.8 Note in the README that fork pull requests and drafts are skipped by design, and that adding `code-quality-check` to `main`'s required checks is a deliberate follow-up once the gate has been calibrated.

## 7. Verification

- [ ] 7.1 Provision the reviewing identity and configure the `COPILOT_GITHUB_TOKEN` secret (manual step, outside the repo); confirm the seat is active and the CLI policy is enabled.
- [ ] 7.2 Open a probe pull request that confirms the security posture from task 1.4 empirically: the run completes without stalling, a shell command is actually refused, no MCP tool is available, no URL fetch succeeds, and the token does not appear in the transcript or log.
- [ ] 7.3 Open a throwaway pull request with a deliberate `blocking` violation — for example an endpoint mapping added to `Api/Program.cs` — and confirm an inline comment lands on the right line, attributed to the Actions bot, and the check fails.
- [ ] 7.4 Open a throwaway pull request with a clean, small change and confirm the check passes with a review stating that nothing was found.
- [ ] 7.5 Force an unanchorable finding (a finding on an unchanged line) and confirm it degrades into the review body without suppressing the anchorable findings.
- [ ] 7.6 Read the usage figures from the probe runs' job summaries, set `--max-ai-credits` to a cap that comfortably clears a normal review, and record both the observed usage and the chosen cap in the README.
- [ ] 7.7 Push a second commit while a run is in flight and confirm the superseded run is cancelled.
- [ ] 7.8 Temporarily unset the secret and confirm the check fails at the assertion step with the actionable message rather than an opaque CLI auth error.
- [ ] 7.9 Confirm the working-tree assertion fires: temporarily relax the `shell` deny rule, have the reviewer touch a tracked file, and verify the job fails and nothing is published.
- [ ] 7.10 Review the findings from the first few real pull requests and tighten the prompt where the reviewer over-reported, before proposing the check as required on `main`.
