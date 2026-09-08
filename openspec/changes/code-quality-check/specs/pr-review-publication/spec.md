## ADDED Requirements

### Requirement: The findings file is validated before anything is published

The publisher SHALL parse `.code-review/findings.json` and validate it before contacting the GitHub API: the document MUST be an object with a string `summary` and an array `findings`; every entry MUST carry a repository-relative `path`, an integer `line`, a `severity` drawn from `blocking`, `major`, `minor`, `nit`, and non-empty `title` and `detail` strings. A missing, unparseable or non-conforming file SHALL fail the check.

#### Scenario: The findings file is missing

- **WHEN** the artifact does not contain `.code-review/findings.json`
- **THEN** the publisher fails with a message stating the review did not produce output, and no review is posted

#### Scenario: The findings file is not valid JSON

- **WHEN** the file cannot be parsed
- **THEN** the publisher fails and reports the parse error

#### Scenario: A finding carries an unknown severity

- **WHEN** an entry's `severity` is not one of the four permitted values
- **THEN** the publisher fails rather than guessing the intended severity

#### Scenario: A finding names a path outside the repository

- **WHEN** an entry's `path` is absolute or escapes the repository root
- **THEN** that entry is rejected and the publisher fails

#### Scenario: The reviewer ended without writing the file

- **WHEN** the CLI exits early, loses its credential mid-run, or produces only terminal prose
- **THEN** the absence of a valid findings file fails the check, so a review that did not happen is never reported as a review that found nothing

### Requirement: Findings are posted as one pull request review with inline comments

The publisher SHALL create a single pull request review via `POST /repos/{owner}/{repo}/pulls/{number}/reviews`, carrying every anchorable finding as an inline comment on the `RIGHT` side at the finding's line, and the summary in the review body. The review SHALL use `event: "COMMENT"`.

#### Scenario: Several findings on different files

- **WHEN** the findings span multiple changed files
- **THEN** one review is created containing one inline comment per finding, each on its own file and line

#### Scenario: The review does not formally block the pull request

- **WHEN** the review is created
- **THEN** it is submitted as a comment, not as `REQUEST_CHANGES`, so GitHub does not require a human dismissal before merging

#### Scenario: A comment identifies its severity and standard

- **WHEN** an inline comment is rendered
- **THEN** its body states the finding's severity, title, detail, and the standard it cites

### Requirement: Publishing is performed by the workflow, not by the reviewer

The publisher SHALL post the review itself using the Actions `GITHUB_TOKEN`. The review SHALL NOT be posted by the Copilot CLI through a GitHub MCP tool, and the publisher SHALL NOT be granted any Copilot credential.

#### Scenario: The model is never given write capability

- **WHEN** the review is published
- **THEN** the posting is done by the workflow's own script, so no model-driven tool call is capable of writing to the pull request

#### Scenario: Attribution is to the bot

- **WHEN** a contributor reads the posted review
- **THEN** it is attributed to the Actions bot, not to the human or machine identity whose Copilot seat performed the analysis

### Requirement: Comment positions are derived from the diff, and unanchorable findings degrade gracefully

The publisher SHALL parse the unified diff to determine which `(path, line)` pairs the review API accepts, and SHALL partition findings accordingly. Findings that cannot be anchored SHALL be rendered as a list in the review body instead of being dropped. A single unanchorable finding SHALL NOT prevent the remaining findings from being posted.

#### Scenario: A finding points at a line outside the diff

- **WHEN** a finding's line is not part of the pull request's diff
- **THEN** that finding appears in the review body under an "unanchored findings" heading, with its path and line stated, and the anchorable findings still post inline

#### Scenario: A finding points at a file the pull request does not change

- **WHEN** a finding's path is absent from the changed-file set
- **THEN** the finding is treated as unanchorable and reported in the review body

#### Scenario: Every finding is unanchorable

- **WHEN** no finding can be anchored to a diff line
- **THEN** a review with no inline comments is still created, carrying the summary and the full finding list in its body

#### Scenario: The API rejects the review

- **WHEN** the review API returns an error
- **THEN** the publisher fails with the API's status and response body, so the failure is diagnosable rather than silent

### Requirement: The review body reports the verdict and the finding counts

The review body SHALL contain the reviewer's summary, a count of findings by severity, an explicit statement of whether the check passed or failed and why, and a note that the review is automated and advisory outside the blocking gate.

#### Scenario: A failing review

- **WHEN** at least one `blocking` finding was reported
- **THEN** the review body states that the check failed, names the number of blocking findings, and explains that resolving them and pushing again re-runs the check

#### Scenario: A passing review with advisory findings

- **WHEN** findings were reported but none are `blocking`
- **THEN** the review body states that the check passed and that the findings are advisory

#### Scenario: A clean review

- **WHEN** no findings were reported
- **THEN** a review is still created stating that the automated review found nothing, so the absence of comments is distinguishable from a review that never ran

### Requirement: Each reviewed commit produces its own review

The publisher SHALL post a new review per workflow run and SHALL NOT delete, edit or resolve reviews or comments from earlier runs, so that the review history of a pull request remains an accurate record of what was said about each commit.

#### Scenario: A contributor pushes a fix

- **WHEN** a new commit addresses a previously reported finding and the workflow runs again
- **THEN** a new review is posted for the new commit and the earlier review remains in the pull request's history

#### Scenario: Comments on changed lines become outdated

- **WHEN** a line carrying an inline comment is subsequently modified
- **THEN** GitHub marks that comment outdated by its normal behaviour and the publisher takes no action to remove it

### Requirement: The run's outcome is summarised in the Actions job summary

The publisher SHALL write the summary, the finding counts by severity, the pinned CLI and model versions used, the run's usage statistics as reported by the CLI, and a link to the created review into the GitHub Actions job summary, so the outcome is legible from the run page without opening the pull request.

#### Scenario: A maintainer inspects the workflow run

- **WHEN** a maintainer opens the `code-quality-check` run
- **THEN** the job summary shows what was found, which CLI version and model produced it, what the run consumed, and where the review was posted

#### Scenario: Usage is reported from the CLI's own output

- **WHEN** the job summary renders the run's consumption
- **THEN** the figures come from the usage file the CLI wrote, and are never estimated or inferred

#### Scenario: The usage file is absent

- **WHEN** the review artifact contains no usage file
- **THEN** the job summary states that usage was not reported rather than showing a zero or a guess, and the check's outcome is unaffected

#### Scenario: A skipped run

- **WHEN** the review was skipped because the pull request is a draft, comes from a fork, or changed no reviewable files
- **THEN** the job summary states which condition caused the skip

### Requirement: Every published finding says which reviewers reported it

The publisher SHALL publish the merged findings document, and each comment SHALL state
the models that reported that finding. Where a finding was reported by fewer than all
reviewers, the comment SHALL name the reviewers that did not report it. Where reviewers
graded the same finding differently, the comment SHALL state each grade and the grade
that was published.

#### Scenario: A finding both reviewers reported

- **WHEN** every reviewer reported the same problem
- **THEN** the inline comment says so and lists them, and carries each reviewer's own reasoning rather than only the first one's

#### Scenario: A finding only one reviewer reported

- **WHEN** one reviewer reported a problem the others did not
- **THEN** the inline comment names the reviewer that found it and the reviewers that did not, so a reader knows a lone finding when they see one

#### Scenario: The reviewers disagreed on severity

- **WHEN** two reviewers graded the same problem differently
- **THEN** the comment shows both grades and states which one was published

### Requirement: The gate is the union of the reviewers' blocking findings

The publisher SHALL treat a merged finding's severity as the worst severity any reviewer
assigned it, and SHALL exit non-zero if and only if at least one merged finding is
`blocking`.

#### Scenario: Only one reviewer called it blocking

- **WHEN** one reviewer grades a finding `blocking` and another grades the same finding `minor`
- **THEN** the finding is published as `blocking` and the check fails

#### Scenario: A blocking finding no other reviewer reported

- **WHEN** a single reviewer reports a `blocking` finding the others missed entirely
- **THEN** the check fails on it, because consensus is not a condition of the gate

### Requirement: The review body carries the reviewer comparison

The review body SHALL include the per-reviewer severity counts, how many distinct
problems more than one reviewer reported, what each reviewer found alone, and the
severity disagreements. Where a narrative comparison exists it SHALL be included and
attributed to the model that wrote it; where it does not, the body SHALL say so.

#### Scenario: A reader compares the two reviews at a glance

- **WHEN** the review is published
- **THEN** the body shows a table of each reviewer's findings by severity, the agreement figure, and the problems unique to each reviewer

#### Scenario: The narrative is marked as commentary

- **WHEN** the narrative comparison is included
- **THEN** it is attributed to the model that wrote it and stated to be commentary that cannot change a severity or the check result

#### Scenario: The narrative is missing

- **WHEN** no narrative was produced
- **THEN** the body presents the deterministic figures and states that the narrative was not produced, rather than implying the reviewers were not compared

#### Scenario: An over-long narrative

- **WHEN** the narrative would push the review body past the API's size limit
- **THEN** it is truncated with a pointer to the workflow artifact, and the reviewers' own findings are never truncated to make room for it

#### Scenario: Only one reviewer ran

- **WHEN** the findings document carries no reviewer attribution
- **THEN** the publisher omits the comparison section and publishes exactly as it would for a single-model review

### Requirement: The publisher refuses a merged document the comparison does not corroborate

The publisher SHALL check the merged findings document against the comparison report and
SHALL fail the check when they disagree on how many distinct problems there were, rather
than gating on a document nothing confirms.

#### Scenario: A finding was removed after the comparison ran

- **WHEN** the merged document lists fewer findings than the comparison reports distinct problems
- **THEN** the publisher fails with a diagnosable message and posts no review

#### Scenario: A finding was added after the comparison ran

- **WHEN** the merged document lists more findings than the comparison reports
- **THEN** the publisher fails in the same way

#### Scenario: A single-reviewer document

- **WHEN** no comparison report is supplied, as for a one-model review
- **THEN** there is nothing to corroborate and the publisher proceeds normally
