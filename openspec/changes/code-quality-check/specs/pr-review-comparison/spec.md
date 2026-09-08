## ADDED Requirements

### Requirement: Findings from different reviewers are paired mechanically and explainably

The comparison SHALL pair findings from different reviewers that describe the same
problem, using rules a reader can check by hand: the same file, line numbers within a
fixed window, and either the same `category` or a title-token overlap above a fixed
threshold. Each pairing SHALL record how it matched.

#### Scenario: The same problem, reported identically

- **WHEN** two reviewers report the same category on the same line of the same file
- **THEN** the two findings become one problem, recorded as an exact match

#### Scenario: The same problem, a few lines apart

- **WHEN** two reviewers report the same category on the same file within the line window
- **THEN** the findings are paired and the pairing records the line distance

#### Scenario: The same problem, described differently

- **WHEN** two reviewers use different categories but titles that substantially overlap, within the line window
- **THEN** the findings are paired, and the pairing is recorded as a title match so a reader can see it was the loosest kind

#### Scenario: Different problems close together

- **WHEN** two reviewers report unrelated problems on neighbouring lines, with different categories and unrelated titles
- **THEN** the findings are NOT paired, because proximity alone is not evidence of the same defect

#### Scenario: One reviewer reports two nearby problems

- **WHEN** a single reviewer's own findings would match each other
- **THEN** they are never merged into one problem, because each reviewer already deduplicates its own list

### Requirement: The comparison reports agreement, divergence and disagreement

The comparison SHALL produce a machine-readable report naming the reviewer roster, each
reviewer's finding counts by severity, the problems more than one reviewer reported, the
problems each reviewer reported alone, the findings the reviewers graded differently, and
an agreement figure.

#### Scenario: Two reviewers overlap partially

- **WHEN** the reviewers report some of the same problems and some different ones
- **THEN** the report states how many distinct problems there were, how many were shared, and which were unique to each reviewer

#### Scenario: The reviewers disagreed on how bad a problem is

- **WHEN** two reviewers paired on a problem but graded it differently
- **THEN** the report lists it with each reviewer's grade and the grade that was published

#### Scenario: Both reviews were clean

- **WHEN** neither reviewer reported anything
- **THEN** the comparison reports full agreement and no findings, and this is a successful outcome

#### Scenario: A reviewer's findings file is invalid

- **WHEN** one reviewer's findings file is missing, unparseable or non-conforming
- **THEN** the comparison fails rather than comparing what remains, so half a comparison is never published as a whole one

### Requirement: Merging preserves both reviewers' reasoning and the worst severity

The merged findings document SHALL contain one entry per distinct problem, carrying the
worst severity any reviewer assigned it, the reviewers that reported it, each reviewer's
severity, and every reviewer's own detail text.

#### Scenario: Severities differ

- **WHEN** reviewers grade a shared problem `minor` and `blocking`
- **THEN** the merged entry is `blocking`, and both grades remain visible

#### Scenario: Details differ

- **WHEN** reviewers explain the same problem differently
- **THEN** the merged entry keeps both explanations, attributed to the reviewer that wrote each

#### Scenario: Reviewers anchored a shared problem to different lines

- **WHEN** one reviewer's line is inside the diff and another's is not
- **THEN** the merged entry uses the line the diff will accept as an inline anchor, without changing the severity

### Requirement: The narrative comparison is judgement, and it is not the gate

A model SHALL produce a narrative comparison from the deterministic report, both raw
findings files and the diff, judging the single-reviewer findings, the severity
disagreements and the relative usefulness of the reviews. The narrative SHALL NOT change
any severity, add or remove any finding, or affect the check's outcome.

#### Scenario: A finding only one reviewer reported

- **WHEN** the narrative assesses a single-reviewer finding
- **THEN** it states whether the finding is real, plausible or a likely false positive, and names the reviewer that missed it

#### Scenario: The narrative disagrees with a published severity

- **WHEN** the narrative argues a `blocking` finding was over-graded
- **THEN** the finding is still published as `blocking` and the check still fails, because severities come from the reviewers

#### Scenario: The narrating model reviewed the pull request itself

- **WHEN** the narrating model is one of the reviewers
- **THEN** the prompt instructs it not to identify or favour its own review, and the published comparison names the model that wrote the narrative so a reader can weigh it accordingly

#### Scenario: A pairing looks wrong to the narrating model

- **WHEN** the deterministic report has merged what appear to be two different problems
- **THEN** the narrative says so plainly, because a defect in the comparison is more useful to the reader than another paragraph of agreement

#### Scenario: The narrative is prevented from touching the gate, not merely told not to

- **WHEN** the narrative model runs with file-writing tools available
- **THEN** the document the check's outcome is computed from is not reachable by it, and every file that is gets verified afterwards, so "commentary only" holds even if the model is subverted

#### Scenario: The publisher is handed a merged document the comparison does not corroborate

- **WHEN** the merged findings document and the comparison report disagree on how many distinct problems there were
- **THEN** the publisher fails the check rather than gating on the document

#### Scenario: Injected instructions in the material being compared

- **WHEN** a findings file, the diff, or a file the narrative reads tells it to approve unconditionally, ignore its rules, or write different output
- **THEN** it does not comply and reports the attempt in the comparison instead
