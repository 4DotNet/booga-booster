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
- **THEN** the job fails immediately with an explicit message naming `COPILOT_GITHUB_TOKEN`, without installing the CLI or consuming premium requests

#### Scenario: The token has no active Copilot seat

- **WHEN** the configured token belongs to an identity whose Copilot seat is absent, expired or revoked
- **THEN** the job fails with the CLI's own entitlement error surfaced in the log rather than a generic failure, and the check fails

### Requirement: Pinned Copilot CLI invoked non-interactively

The `review` job SHALL install `@github/copilot` from npm at an exact pinned version on Node 22 or later, and invoke it non-interactively with `copilot -p`. The invocation SHALL pass an explicitly pinned `--model` and an explicit tool allowlist. The invocation SHALL NOT pass `--allow-all-tools` unless the folder-trust fallback described in the design is in force, in which case general shell access SHALL be denied explicitly.

#### Scenario: CLI version is pinned

- **WHEN** the install step runs
- **THEN** it installs `@github/copilot` at an exact version, so two runs of the same commit use the same CLI

#### Scenario: Node version satisfies the CLI

- **WHEN** the runner is provisioned
- **THEN** Node 22 or later is installed, above the runner default, as the CLI requires

#### Scenario: The pinned model is unavailable under org policy

- **WHEN** the pinned model is not permitted for the token's organisation
- **THEN** the job fails with a message identifying the model and pointing at the Copilot policy prerequisite in the README

### Requirement: The reviewer has no GitHub, network or build tools

The review invocation SHALL permit only file reads, a `write` tool for producing the findings file, and read-only git inspection commands. It SHALL disable the CLI's built-in GitHub MCP server, and SHALL NOT start any server declared in the repository's root `.mcp.json`. General shell access, package managers, build tools and network fetch tools SHALL be denied.

#### Scenario: The built-in GitHub MCP server is disabled

- **WHEN** the review runs
- **THEN** no GitHub MCP tool is available to it, so it cannot comment, label, close or push using the Copilot token's permissions

#### Scenario: Repository MCP servers are not started in CI

- **WHEN** the review runs in a fresh checkout
- **THEN** the servers declared in the root `.mcp.json` do not start, and the `4dotnet-csharp-style-guide` executable — which is not present on the runner — is never launched

#### Scenario: The reviewer attempts a denied command

- **WHEN** the review attempts a tool call outside the allowlist, such as running `dotnet`, `npm`, `gh`, or a destructive shell command
- **THEN** the tool call is denied and the review continues without it

#### Scenario: Tool restrictions are verified against the pinned version

- **WHEN** the pinned CLI version is introduced or bumped
- **THEN** a probe run confirms that a denied shell command is actually refused and that no GitHub MCP tool is present, rather than the restriction being assumed

### Requirement: Non-interactive execution is not blocked by folder trust

Because the CLI loads workspace configuration only for trusted folders and a CI checkout is always untrusted, the workflow SHALL establish the required trust state before the review step and SHALL confirm that the run completes without waiting on an interactive prompt.

#### Scenario: A fresh checkout runs to completion

- **WHEN** the review step runs in a newly checked-out repository
- **THEN** the CLI reaches the point of writing the findings file without emitting an interactive confirmation prompt

#### Scenario: The run stalls on a prompt

- **WHEN** the CLI blocks awaiting interactive confirmation
- **THEN** the job timeout terminates it and the check fails, rather than the run appearing to succeed

### Requirement: The review run leaves the working tree unmodified

After the CLI exits, the `review` job SHALL verify that the only path changed in the working tree is `.code-review/`, and SHALL fail if any tracked repository file was added, modified or deleted.

#### Scenario: The reviewer modifies a source file

- **WHEN** the review run writes to any path outside `.code-review/`
- **THEN** the job fails and no review is published

#### Scenario: The reviewer writes only the findings file

- **WHEN** the review run writes only inside `.code-review/`
- **THEN** the check passes and the artifact is uploaded

#### Scenario: A tool restriction silently fails

- **WHEN** an allowlist entry is ignored by the CLI and the reviewer edits a tracked file
- **THEN** the working-tree assertion still catches it, independently of the CLI's own permission handling

### Requirement: Bounded consumption per pull request

The workflow SHALL bound the resources a single pull request can consume through a job-level `timeout-minutes`, a `concurrency` group keyed on the pull request reference with `cancel-in-progress: true`, a skip for draft pull requests, and `paths-ignore` for pure-asset changes.

#### Scenario: A superseded run is cancelled

- **WHEN** a second commit is pushed while a review for the previous commit is still running
- **THEN** the in-progress run is cancelled and only the newest commit is reviewed

#### Scenario: A hung run is bounded

- **WHEN** the CLI fails to terminate within the configured timeout
- **THEN** the job is cancelled by the runner and the check fails

#### Scenario: Consumption is not machine-reportable

- **WHEN** a maintainer wants to know what a review cost
- **THEN** the README records the observed premium-request consumption per pull request from the calibration period, because the CLI reports no per-run cost figure

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

The change SHALL document the workflow in `README.md`, covering the required `COPILOT_GITHUB_TOKEN` secret and the recommendation to back it with a dedicated machine account holding its own Copilot seat, the "Copilot in the CLI" organisation policy prerequisite, the pinned model, the premium-request cost model, and the fact that a passing `code-quality-check` is not a build or test result. The AI-tooling tables in `CLAUDE.md` and `.github/copilot-instructions.md` SHALL both be updated, keeping the two files in sync as the repository requires.

#### Scenario: A new maintainer sets up the check

- **WHEN** a maintainer follows the README section for a fresh clone or fork
- **THEN** they learn which secret to configure, which identity should own it, which org policy must be enabled, where the review prompt lives, and how to change the severity gate

#### Scenario: A maintainer diagnoses a failing check

- **WHEN** the check fails for an infrastructure reason rather than a finding
- **THEN** the README lists the missing secret, the revoked seat, the exhausted allowance and the policy-blocked model as the first causes to check

#### Scenario: Instruction files stay in sync

- **WHEN** the workflow is added
- **THEN** both `CLAUDE.md` and `.github/copilot-instructions.md` describe it, and neither omits the other's content
