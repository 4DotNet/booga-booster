## ADDED Requirements

### Requirement: Workflow triggers on pull requests targeting main

The repository SHALL contain a GitHub Actions workflow named `code-quality-check` at `.github/workflows/code-quality-check.yml` that runs on the `pull_request` event for the `opened`, `reopened`, `synchronize` and `ready_for_review` activity types, restricted to pull requests whose base branch is `main`.

#### Scenario: Pull request opened against main

- **WHEN** a contributor opens a pull request from a branch in this repository into `main`
- **THEN** the `code-quality-check` workflow is queued and appears as a check on the pull request

#### Scenario: New commits pushed to an open pull request

- **WHEN** a contributor pushes an additional commit to the head branch of an open pull request into `main`
- **THEN** the workflow runs again against the updated diff

#### Scenario: Draft pull request

- **WHEN** a pull request is in draft state
- **THEN** the review is skipped and the check concludes successfully without invoking the Copilot CLI

#### Scenario: Draft pull request marked ready

- **WHEN** a draft pull request is marked ready for review
- **THEN** the `ready_for_review` activity type triggers a full review run

#### Scenario: Pull request targeting a branch other than main

- **WHEN** a pull request targets a branch other than `main`
- **THEN** the workflow does not run

### Requirement: Fork pull requests are skipped, not failed

Because the `pull_request` event grants no repository secrets to workflows running for pull requests from forks, the workflow SHALL detect that case and terminate with a successful conclusion rather than attempting a review it cannot perform.

#### Scenario: Pull request originates from a fork

- **WHEN** `github.event.pull_request.head.repo.full_name` differs from `github.repository`
- **THEN** no Copilot CLI invocation is attempted, the check concludes successfully, and the job summary states that automated review is unavailable for fork pull requests

### Requirement: Separate credentials for reviewing and publishing

The workflow SHALL consist of a `review` job and a `publish` job, where `publish` declares `needs: review`. The `review` job SHALL authenticate the Copilot CLI with a Copilot-entitled personal access token supplied through the `COPILOT_GITHUB_TOKEN` secret, set as the identically named environment variable that the CLI reads, and exposed to the CLI step only. The `publish` job SHALL authenticate to the GitHub API with the Actions-provided `GITHUB_TOKEN` and SHALL NOT receive `COPILOT_GITHUB_TOKEN`.

#### Scenario: The Actions token cannot drive the CLI

- **WHEN** the review step runs
- **THEN** it uses the `COPILOT_GITHUB_TOKEN` secret rather than the Actions `GITHUB_TOKEN`, because an Actions installation token carries no Copilot entitlement

#### Scenario: The Copilot token is not exposed job-wide

- **WHEN** the workflow is inspected
- **THEN** `COPILOT_GITHUB_TOKEN` appears only in the `env` of the step that invokes the CLI, not at job or workflow level

#### Scenario: Review comments are attributed to the bot

- **WHEN** the review is published
- **THEN** it is posted with the Actions `GITHUB_TOKEN`, so the comments are attributed to the Actions bot and not to the human or machine identity that owns the Copilot seat

### Requirement: Least-privilege permissions split across two jobs

The `review` job SHALL declare `permissions: contents: read` and nothing more. The `publish` job SHALL declare only `contents: read` and `pull-requests: write`. Neither job SHALL request `contents: write`.

#### Scenario: The job holding the Copilot token cannot write to the pull request

- **WHEN** the `review` job runs
- **THEN** its `GITHUB_TOKEN` has no `pull-requests: write` scope, so nothing in that job can post to the pull request

#### Scenario: The job holding the write token does not run the model

- **WHEN** the `publish` job runs with `pull-requests: write`
- **THEN** the Copilot CLI is not invoked and no Copilot credential is present in its environment

#### Scenario: Findings are handed between jobs as an artifact

- **WHEN** the `review` job completes
- **THEN** it uploads the `.code-review/` directory as a workflow artifact, and the `publish` job downloads that artifact as its only input

### Requirement: Required secret is validated before any work begins

The workflow SHALL verify that the `COPILOT_GITHUB_TOKEN` secret is non-empty as its first substantive step, and SHALL fail with a message naming the missing secret and referencing the README setup section when it is absent.

#### Scenario: Secret is not configured

- **WHEN** the workflow runs in a repository where `COPILOT_GITHUB_TOKEN` has not been configured
- **THEN** the job fails immediately with an explicit message naming `COPILOT_GITHUB_TOKEN`, without installing the CLI or consuming AI credits

#### Scenario: The token has no active Copilot seat

- **WHEN** the configured token belongs to an identity whose Copilot seat is absent, expired or revoked
- **THEN** the job fails with the CLI's own entitlement error surfaced in the log rather than a generic failure, and the check fails

### Requirement: Pinned Copilot CLI invoked non-interactively

The `review` job SHALL install `@github/copilot` from npm at an exact pinned version on Node 22 or later, and invoke it non-interactively with `copilot -p`. The invocation SHALL pass an explicitly pinned `--model`, and SHALL NOT rely on the CLI's `auto` model selection. Because the CLI documents `--allow-all-tools` as required for non-interactive mode, the invocation SHALL pass it and SHALL derive its restrictions from deny rules instead, per the tool-restriction requirement below.

#### Scenario: CLI version is pinned

- **WHEN** the install step runs
- **THEN** it installs `@github/copilot` at an exact version, so two runs of the same commit use the same CLI

#### Scenario: Node version is pinned explicitly

- **WHEN** the runner is provisioned
- **THEN** Node 22 or later is installed explicitly rather than inherited from the runner default, because the package declares no `engines` constraint that would enforce it

#### Scenario: The pinned model is unavailable under org policy

- **WHEN** the pinned model is not permitted for the token's organisation
- **THEN** the job fails with a message identifying the model and pointing at the Copilot policy prerequisite in the README

#### Scenario: The run does not stall awaiting input

- **WHEN** the reviewer would otherwise ask the operator a question
- **THEN** the `ask_user` tool is disabled so the agent proceeds autonomously, and the job `timeout-minutes` bounds any residual stall

#### Scenario: A CLI version bump is treated as a behavioural change

- **WHEN** the pinned CLI version is raised
- **THEN** the probe verification is re-run before the new pin is relied upon, because the CLI's flag surface has already been observed to differ from expectation

### Requirement: The reviewer has no shell, network, GitHub or MCP tools

The review invocation SHALL deny the `shell` and `url` permission kinds outright, SHALL disable the CLI's built-in MCP servers, and SHALL pass no MCP server configuration. Because deny rules take precedence over allow rules — including over `--allow-all-tools` — these denials SHALL be expressed as deny rules rather than as omissions from an allowlist. File access SHALL remain confined to the working directory, with the system temporary directory excluded.

#### Scenario: The reviewer cannot run any command

- **WHEN** the review attempts to run `git`, `gh`, `dotnet`, `npm`, or any other command
- **THEN** the call is refused, because the whole `shell` kind is denied rather than selectively allowed

#### Scenario: The reviewer does not need a shell

- **WHEN** the reviewer requires the diff or the changed-file list
- **THEN** it reads them from files the workflow wrote before the CLI started, so denying all shell access removes a capability it never needs

#### Scenario: The built-in GitHub MCP server is disabled

- **WHEN** the review runs
- **THEN** no GitHub MCP tool is available to it, so it cannot comment, label, close or push using the Copilot token's permissions

#### Scenario: No MCP server starts in CI

- **WHEN** the review runs in a fresh checkout
- **THEN** built-in MCP servers are disabled and no MCP configuration is supplied, so the `4dotnet-csharp-style-guide` executable — which is not present on the runner — is never launched

#### Scenario: The reviewer cannot reach the network

- **WHEN** the review attempts to fetch a URL
- **THEN** the call is refused, because the `url` kind is denied

#### Scenario: The credential is not recoverable from output

- **WHEN** the review's transcript, findings file or job log is inspected
- **THEN** the Copilot token's value is absent, having been stripped from tool environments and redacted from output

#### Scenario: The session is not exported off the runner

- **WHEN** the review runs
- **THEN** remote control and session export to GitHub web and mobile are disabled, so an unmerged diff is not published outside the run

#### Scenario: Tool restrictions are verified against the pinned version

- **WHEN** the pinned CLI version is introduced or bumped
- **THEN** a probe run confirms that a shell command is actually refused and that no MCP tool is present, rather than the restriction being assumed

### Requirement: The review configuration is taken from the base branch

The review prompt and the findings publisher SHALL be read from the pull request's merge
base or base branch, never from the pull request head, so that a pull request cannot alter
the rules by which it is itself reviewed. Where the base branch does not yet contain the
review prompt, the workflow MAY fall back to the head's copy but SHALL emit a warning
saying so.

#### Scenario: A pull request rewrites the review prompt

- **WHEN** the diff replaces or weakens `.github/code-review/review-prompt.md`
- **THEN** the review still runs under the base branch's prompt, so the rule requiring
  such a change to be flagged is still in force and cannot delete itself

#### Scenario: A pull request modifies the publisher

- **WHEN** the diff changes `.github/code-review/publish-review.mjs`, including the
  condition that fails the check
- **THEN** the `publish` job runs the base branch's version of the script

#### Scenario: The base branch predates the workflow

- **WHEN** no review prompt exists at the merge base, as when the pull request introduces
  the workflow itself
- **THEN** the head's copy is used and a warning states that the prompt must be reviewed
  by hand, rather than the job failing and making the workflow unable to land

#### Scenario: Extracting the base configuration does not dirty the tree

- **WHEN** the base prompt is materialised for the run
- **THEN** it is written outside the working tree, so the working-tree assertion still
  holds

### Requirement: Repository instruction files do not shape the reviewer's system prompt

The review invocation SHALL disable the CLI's automatic loading of custom instruction files. The reviewer's grounding SHALL come from the standards the prompt names by explicit path, so that what informed a review is auditable from the prompt alone.

#### Scenario: A pull request edits an auto-loaded instruction file

- **WHEN** the diff modifies a file the CLI would otherwise load as custom instructions
- **THEN** that content does not enter the system prompt, and reaches the reviewer only as file content the prompt has framed as data

#### Scenario: Grounding survives a change in auto-discovery behaviour

- **WHEN** a CLI version changes which instruction files it discovers, or which skill directories it scans
- **THEN** the review is unaffected, because the prompt names the standards it must read by path

### Requirement: The review run leaves the working tree unmodified

After the CLI exits, the `review` job SHALL verify that the only path changed in the working tree is `.code-review/`, and SHALL fail if any tracked repository file was added, modified or deleted.

#### Scenario: The reviewer modifies a source file

- **WHEN** the review run writes to any path outside `.code-review/`
- **THEN** the job fails and no review is published

#### Scenario: The reviewer writes only the findings file

- **WHEN** the review run writes only inside `.code-review/`
- **THEN** the check passes and the artifact is uploaded

#### Scenario: A tool restriction silently fails

- **WHEN** a deny rule is ignored by the CLI and the reviewer edits a tracked file
- **THEN** the working-tree assertion still catches it, independently of the CLI's own permission handling

### Requirement: Bounded and reported consumption per pull request

The workflow SHALL bound what a single pull request can consume through an explicit per-session AI-credit cap, a job-level `timeout-minutes`, a `concurrency` group keyed on the pull request reference with `cancel-in-progress: true`, a skip for draft pull requests, and `paths-ignore` for pure-asset changes. The workflow SHALL also capture the CLI's own usage statistics for the run so that consumption is reported rather than estimated.

#### Scenario: A single run cannot exceed its credit cap

- **WHEN** the review would consume more AI credits than the configured cap
- **THEN** the CLI stops at the cap, bounding the spend of one run directly rather than through a proxy such as a turn count

#### Scenario: A superseded run is cancelled

- **WHEN** a second commit is pushed while a review for the previous commit is still running
- **THEN** the in-progress run is cancelled and only the newest commit is reviewed

#### Scenario: A hung run is bounded

- **WHEN** the CLI fails to terminate within the configured timeout
- **THEN** the job is cancelled by the runner and the check fails

#### Scenario: Actual consumption is recorded

- **WHEN** a review run completes
- **THEN** the CLI's usage statistics are written to a file in the review artifact and rendered into the job summary, so a maintainer can see what the run actually used

#### Scenario: The allowance is exhausted

- **WHEN** the seat's AI-credit allowance is spent
- **THEN** the CLI fails, the check fails, and the README's troubleshooting list names the exhausted allowance as a cause

### Requirement: The check fails when a blocking finding is reported

The `publish` job SHALL exit non-zero if and only if at least one reported finding carries severity `blocking`, or the workflow itself could not complete a valid review. Findings of severity `major`, `minor` and `nit` SHALL NOT fail the check. The workflow SHALL NOT use `continue-on-error` to mask failures.

#### Scenario: A blocking finding is reported

- **WHEN** the findings file contains one or more findings with severity `blocking`
- **THEN** the `code-quality-check` check concludes as failed after the review comments have been posted

#### Scenario: Only advisory findings are reported

- **WHEN** the findings file contains findings but none with severity `blocking`
- **THEN** the comments are posted and the check concludes successfully

#### Scenario: No findings are reported

- **WHEN** the findings file contains an empty findings list
- **THEN** the check concludes successfully

#### Scenario: The review could not be completed

- **WHEN** the CLI exits non-zero, or the findings file is missing or invalid
- **THEN** the check fails rather than reporting a clean review

### Requirement: Setup, prerequisites and limitations are documented

The change SHALL document the workflow in `README.md`, covering the required `COPILOT_GITHUB_TOKEN` secret and the recommendation to back it with a dedicated machine account holding its own Copilot seat, the "Copilot in the CLI" organisation policy prerequisite, the pinned model, the AI-credit cost model and the per-session credit cap, and the fact that a passing `code-quality-check` is not a build or test result. The AI-tooling tables in `CLAUDE.md` and `.github/copilot-instructions.md` SHALL both be updated, keeping the two files in sync as the repository requires.

#### Scenario: A new maintainer sets up the check

- **WHEN** a maintainer follows the README section for a fresh clone or fork
- **THEN** they learn which secret to configure, which identity should own it, which org policy must be enabled, where the review prompt lives, and how to change the severity gate

#### Scenario: A maintainer diagnoses a failing check

- **WHEN** the check fails for an infrastructure reason rather than a finding
- **THEN** the README lists the missing secret, the revoked seat, the exhausted allowance and the policy-blocked model as the first causes to check

#### Scenario: Instruction files stay in sync

- **WHEN** the workflow is added
- **THEN** both `CLAUDE.md` and `.github/copilot-instructions.md` describe it, and neither omits the other's content
