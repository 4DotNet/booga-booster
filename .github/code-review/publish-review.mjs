#!/usr/bin/env node
// Publishes the Copilot CLI code review as a single pull request review.
//
// Reads the findings file produced by the review job, anchors each finding to a line the
// GitHub review API will accept, and posts ONE review with `event: "COMMENT"`.
//
// Design notes (openspec/changes/code-quality-check/design.md):
//   D3  This runs in the `publish` job, under the Actions GITHUB_TOKEN. It holds no
//       Copilot credential, and the job that ran Copilot held no write permission.
//   D8  One review per run. Findings that cannot be anchored to a diff line degrade into
//       the review body rather than being dropped -- and a single bad line number must
//       never 422 the whole review away.
//   D10 Exit 1 if and only if at least one finding is `blocking`. A review that did not
//       happen (missing/invalid findings file) also exits non-zero: failing open would
//       make the gate meaningless.
//   D13 Several models review the same diff. This script publishes the MERGED findings
//       document written by compare-reviews.mjs, so every comment says which reviewers
//       reported it and a finding only one model saw is visibly marked as such.
//   D14 The comparison narrative is commentary: it is rendered into the review body and
//       nothing more. Severities, and therefore the gate, come from the reviewers.
//
// No dependencies. Node 22+.

import { readFileSync, existsSync, appendFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const SEVERITIES = ['blocking', 'major', 'minor', 'nit'];
const SEVERITY_LABEL = {
  blocking: '🚫 blocking',
  major: '⚠️ major',
  minor: '💬 minor',
  nit: '🔹 nit',
};
const MAX_INLINE_COMMENTS = 50;
// The GitHub review body caps out at 65536 characters, and the reviewers' own findings
// have first claim on it. The full narrative is always in the workflow artifact.
const MAX_NARRATIVE_CHARS = 8000;

/** Fail the check with a diagnosable message. Never fail open. */
function fail(message) {
  console.error(`::error::${message}`);
  process.exit(1);
}

function requireEnv(name) {
  const value = process.env[name];
  if (!value) fail(`${name} is not set. This script must run inside the workflow.`);
  return value;
}

// ---------------------------------------------------------------------------
// Findings file validation (spec: pr-review-publication)
// ---------------------------------------------------------------------------

/**
 * Attribution, when the document is a merged one: which reviewers reported this finding.
 * Absent on a single-reviewer document, which is still valid input.
 */
function normaliseModels(value, at) {
  if (value === undefined || value === null) return [];
  if (!Array.isArray(value)) {
    throw new Error(`${at}.models must be an array of model ids.`);
  }
  return value.map((model, index) => {
    if (typeof model !== 'string' || model.trim() === '') {
      throw new Error(`${at}.models[${index}] must be a non-empty string.`);
    }
    return model.trim();
  });
}

/** Per-reviewer severities, so a comment can show where the reviewers graded differently. */
function normaliseSeverities(value, at) {
  if (value === undefined || value === null) return {};
  if (typeof value !== 'object' || Array.isArray(value)) {
    throw new Error(`${at}.severities must be an object of model to severity.`);
  }
  return Object.fromEntries(
    Object.entries(value).map(([model, severity]) => {
      if (!SEVERITIES.includes(severity)) {
        throw new Error(
          `${at}.severities.${model} must be one of ${SEVERITIES.join(', ')}, ` +
            `got ${JSON.stringify(severity)}.`,
        );
      }
      return [model, severity];
    }),
  );
}

/**
 * Validate the findings document strictly. A missing, unparseable or non-conforming file
 * fails the check -- a review that did not happen must never look like a clean review.
 */
export function validateFindings(raw) {
  let doc;
  try {
    doc = JSON.parse(raw);
  } catch (error) {
    throw new Error(`findings.json is not valid JSON: ${error.message}`);
  }

  if (doc === null || typeof doc !== 'object' || Array.isArray(doc)) {
    throw new Error('findings.json must contain a JSON object.');
  }
  if (typeof doc.summary !== 'string' || doc.summary.trim() === '') {
    throw new Error('findings.json is missing a non-empty "summary" string.');
  }
  if (!Array.isArray(doc.findings)) {
    throw new Error('findings.json is missing a "findings" array.');
  }

  const findings = doc.findings.map((finding, index) => {
    const at = `findings[${index}]`;
    if (finding === null || typeof finding !== 'object' || Array.isArray(finding)) {
      throw new Error(`${at} must be an object.`);
    }

    const { path, line, severity, title, detail } = finding;

    if (typeof path !== 'string' || path.trim() === '') {
      throw new Error(`${at}.path must be a non-empty string.`);
    }
    // Reject anything that is not plainly repository-relative. A finding may only point
    // at a file in the checkout.
    const normalised = path.replace(/\\/g, '/');
    if (
      normalised.startsWith('/') ||
      /^[A-Za-z]:/.test(normalised) ||
      normalised.split('/').includes('..')
    ) {
      throw new Error(
        `${at}.path must be repository-relative and must not escape the repository root: ${path}`,
      );
    }

    if (!Number.isInteger(line) || line < 1) {
      throw new Error(`${at}.line must be a positive integer, got ${JSON.stringify(line)}.`);
    }
    if (!SEVERITIES.includes(severity)) {
      throw new Error(
        `${at}.severity must be one of ${SEVERITIES.join(', ')}, got ${JSON.stringify(severity)}.`,
      );
    }
    if (typeof title !== 'string' || title.trim() === '') {
      throw new Error(`${at}.title must be a non-empty string.`);
    }
    if (typeof detail !== 'string' || detail.trim() === '') {
      throw new Error(`${at}.detail must be a non-empty string.`);
    }

    return {
      path: normalised,
      line,
      severity,
      title: title.trim(),
      detail: detail.trim(),
      category: typeof finding.category === 'string' ? finding.category.trim() : '',
      standard: typeof finding.standard === 'string' ? finding.standard.trim() : '',
      models: normaliseModels(finding.models, at),
      severities: normaliseSeverities(finding.severities, at),
    };
  });

  return { summary: doc.summary.trim(), findings };
}

// ---------------------------------------------------------------------------
// Diff parsing (spec: pr-review-publication)
// ---------------------------------------------------------------------------

/**
 * Build the set of `(path, line)` positions the review API will accept as inline comment
 * anchors: lines present on the RIGHT side of the diff (added or context lines).
 *
 * Returns a Map of path -> Set of line numbers.
 */
export function parseDiffPositions(patch) {
  const positions = new Map();
  let currentPath = null;
  let newLine = 0;

  for (const rawLine of patch.split('\n')) {
    if (rawLine.startsWith('+++ ')) {
      // "+++ b/path/to/file" -- or "+++ /dev/null" for a deletion.
      const target = rawLine.slice(4).trim();
      currentPath = target === '/dev/null' ? null : target.replace(/^[ab]\//, '');
      continue;
    }
    if (rawLine.startsWith('--- ')) continue;
    if (rawLine.startsWith('diff --git') || rawLine.startsWith('index ')) {
      continue;
    }

    if (rawLine.startsWith('@@')) {
      // "@@ -oldStart,oldCount +newStart,newCount @@"
      const match = /^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(rawLine);
      newLine = match ? Number(match[1]) : 0;
      continue;
    }

    if (currentPath === null || newLine === 0) continue;

    if (rawLine.startsWith('+')) {
      if (!positions.has(currentPath)) positions.set(currentPath, new Set());
      positions.get(currentPath).add(newLine);
      newLine += 1;
    } else if (rawLine.startsWith(' ')) {
      // Context lines are also valid anchors.
      if (!positions.has(currentPath)) positions.set(currentPath, new Set());
      positions.get(currentPath).add(newLine);
      newLine += 1;
    }
    // '-' lines and '\ No newline at end of file' do not advance the new-side counter.
  }

  return positions;
}

/** Split findings into those the API will accept inline and those it will not. */
export function partitionFindings(findings, positions) {
  const anchorable = [];
  const unanchorable = [];

  for (const finding of findings) {
    const lines = positions.get(finding.path);
    if (lines && lines.has(finding.line)) anchorable.push(finding);
    else unanchorable.push(finding);
  }

  // Most severe first, so a comment cap keeps what matters.
  const bySeverity = (a, b) =>
    SEVERITIES.indexOf(a.severity) - SEVERITIES.indexOf(b.severity);
  anchorable.sort(bySeverity);
  unanchorable.sort(bySeverity);

  return { anchorable, unanchorable };
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

function renderStandard(finding) {
  if (!finding.standard) return '';
  return `\nStandard: \`${finding.standard}\``;
}

const quoted = (models) => models.map((model) => `\`${model}\``).join(', ');

/**
 * Say which reviewers reported this finding, and where they graded it differently.
 *
 * A finding only one of several reviewers saw is not weaker -- the gate treats it exactly
 * the same (D15) -- but the reader deserves to know, because a lone finding is where a
 * false positive is most likely to be hiding.
 */
export function renderAttribution(finding, reviewers = []) {
  const models = finding.models ?? [];
  if (models.length === 0) return '';

  const severities = finding.severities ?? {};
  const graded =
    new Set(Object.values(severities)).size > 1
      ? ` — graded ${Object.entries(severities)
          .map(([model, severity]) => `${severity} by \`${model}\``)
          .join(', ')}, published as ${finding.severity}`
      : '';

  const missing = reviewers.filter((reviewer) => !models.includes(reviewer));

  if (missing.length > 0) {
    return (
      `\n\n_Reported by ${quoted(models)} only — not flagged by ` +
      `${quoted(missing)}${graded}._`
    );
  }
  if (models.length > 1) {
    return `\n\n_Reported by all ${models.length} reviewers: ${quoted(models)}${graded}._`;
  }
  return `\n\n_Reported by ${quoted(models)}${graded}._`;
}

export function renderInlineComment(finding, reviewers = []) {
  const label = SEVERITY_LABEL[finding.severity] ?? finding.severity;
  const category = finding.category ? ` · \`${finding.category}\`` : '';
  return (
    `**${label}${category} — ${finding.title}**\n\n${finding.detail}` +
    `${renderStandard(finding)}${renderAttribution(finding, reviewers)}`
  );
}

export function countBySeverity(findings) {
  const counts = Object.fromEntries(SEVERITIES.map((s) => [s, 0]));
  for (const finding of findings) counts[finding.severity] += 1;
  return counts;
}

function renderCounts(counts, total) {
  if (total === 0) return 'No findings.';
  return SEVERITIES.filter((s) => counts[s] > 0)
    .map((s) => `${counts[s]} ${s}`)
    .join(' · ');
}

/**
 * Render the side-by-side reviewer comparison: what each model reported, how much they
 * agreed, and where they graded the same problem differently.
 *
 * Comes from compare-reviews.mjs, which is deterministic. The narrative below it is a
 * model's reading of the same data and is clearly attributed as such.
 */
export function renderComparison(comparison, narrative) {
  if (comparison === null || comparison === undefined) return [];

  const { models = [], perModel = {}, totals = {}, agreementRate } = comparison;
  if (models.length < 2) return [];

  const parts = ['### Reviewer comparison', ''];
  parts.push(`| Reviewer | ${SEVERITIES.join(' | ')} | total |`);
  parts.push(`| --- | ${SEVERITIES.map(() => '---').join(' | ')} | --- |`);
  for (const model of models) {
    const stats = perModel[model] ?? { bySeverity: {}, total: 0 };
    const cells = SEVERITIES.map((severity) => stats.bySeverity?.[severity] ?? 0);
    parts.push(`| \`${model}\` | ${cells.join(' | ')} | ${stats.total ?? 0} |`);
  }
  parts.push('');

  const unique = Object.entries(totals.uniqueByModel ?? {})
    .map(([model, count]) => `\`${model}\` alone: ${count}`)
    .join(' · ');
  parts.push(
    `**${totals.shared ?? 0} of ${totals.clusters ?? 0}** distinct problems were reported ` +
      `by more than one reviewer (agreement ${agreementRate ?? 'n/a'}). ` +
      (unique ? `Found by ${unique}.` : ''),
  );
  parts.push('');

  const disagreements = comparison.severityDisagreements ?? [];
  if (disagreements.length > 0) {
    parts.push('Same problem, different severity — the worst grade is what the gate used:');
    parts.push('');
    for (const entry of disagreements) {
      const grades = Object.entries(entry.severities)
        .map(([model, severity]) => `${severity} per \`${model}\``)
        .join(', ');
      parts.push(`- \`${entry.path}\` — **${entry.title}**: ${grades} → published as ${entry.merged}`);
    }
    parts.push('');
  }

  if (typeof narrative === 'string' && narrative.trim() !== '') {
    const trimmed = narrative.trim();
    parts.push(
      trimmed.length > MAX_NARRATIVE_CHARS
        ? `${trimmed.slice(0, MAX_NARRATIVE_CHARS)}\n\n_…truncated; the full comparison is in the workflow artifact._`
        : trimmed,
    );
    parts.push('');
  } else {
    parts.push(
      '_The narrative comparison was not produced for this run; the figures above are ' +
        'from the deterministic comparison only._',
    );
    parts.push('');
  }

  return parts;
}

/**
 * Compose the review body: summary, counts, verdict and reason, unanchored findings, the
 * reviewer comparison, and a note that the review is automated.
 */
export function renderReviewBody({
  summary,
  findings,
  unanchorable,
  blockingCount,
  truncatedInline,
  meta,
  comparison = null,
  narrative = null,
}) {
  const counts = countBySeverity(findings);
  const parts = [];

  parts.push('## Automated code review');
  parts.push('');
  parts.push(summary);
  parts.push('');
  parts.push(`**Findings:** ${renderCounts(counts, findings.length)}`);
  parts.push('');

  if (blockingCount > 0) {
    const one = blockingCount === 1;
    parts.push(
      `### ❌ Check failed\n\n${blockingCount} blocking finding${one ? '' : 's'} ` +
        `must be resolved. Push a commit addressing ${one ? 'it' : 'them'} and this ` +
        'check runs again.',
    );
  } else if (findings.length > 0) {
    parts.push(
      '### ✅ Check passed\n\nNo blocking findings. Everything above is advisory — ' +
        'use your judgement.',
    );
  } else {
    parts.push('### ✅ Check passed\n\nThe automated review found nothing to report.');
  }
  parts.push('');

  if (unanchorable.length > 0) {
    parts.push('### Unanchored findings');
    parts.push('');
    parts.push(
      'These could not be attached to a line in this diff, so they are listed here instead:',
    );
    parts.push('');
    for (const finding of unanchorable) {
      const label = SEVERITY_LABEL[finding.severity] ?? finding.severity;
      parts.push(
        `- **${label}** \`${finding.path}:${finding.line}\` — **${finding.title}**  \n  ` +
          `${finding.detail.replace(/\n/g, '\n  ')}` +
          (finding.standard ? `  \n  Standard: \`${finding.standard}\`` : ''),
      );
    }
    parts.push('');
  }

  if (truncatedInline > 0) {
    parts.push(
      `_${truncatedInline} further inline comment${truncatedInline === 1 ? '' : 's'} ` +
        'omitted to stay within the review comment limit._',
    );
    parts.push('');
  }

  parts.push(...renderComparison(comparison, narrative));

  const reviewers = meta.models ?? (meta.model ? [meta.model] : []);
  const engine =
    reviewers.length > 1
      ? `models ${quoted(reviewers)}, reviewing independently`
      : `model \`${reviewers[0] ?? 'unknown'}\``;
  const narratedBy =
    meta.compareModel && reviewers.length > 1
      ? ` The comparison narrative was written by \`${meta.compareModel}\` and is ` +
        'commentary only — it cannot change a severity or the check result.'
      : '';

  parts.push('---');
  parts.push(
    `_Automated review by GitHub Copilot CLI \`${meta.cliVersion}\` (${engine}), ` +
      'grounded in this repository\'s checked-in standards. It reviews the diff only — ' +
      `it does not build or test the code. Advisory except for blocking findings.${narratedBy}_`,
  );

  return parts.join('\n');
}

// ---------------------------------------------------------------------------
// Usage reporting
// ---------------------------------------------------------------------------

/**
 * Read the CLI's usage statistics. Absent or unreadable is not an error -- the summary
 * says so rather than showing a zero or a guess.
 */
function readUsage(usagePath) {
  if (!usagePath || !existsSync(usagePath)) return null;
  try {
    return JSON.parse(readFileSync(usagePath, 'utf8'));
  } catch {
    return null;
  }
}

function renderUsage(usage) {
  if (usage === null) {
    return 'Usage was not reported by the CLI for this run.';
  }
  const lines = [];
  for (const [key, value] of Object.entries(usage)) {
    if (value === null || typeof value === 'object') continue;
    lines.push(`- \`${key}\`: ${value}`);
  }
  return lines.length > 0 ? lines.join('\n') : 'Usage file contained no scalar figures.';
}

/**
 * Every Copilot session this run spent credits on, reported separately: one per reviewer
 * plus the comparison. `USAGE_PATHS` is a comma-separated list of `label=path`.
 */
function renderAllUsage(spec) {
  const entries = String(spec)
    .split(',')
    .map((entry) => entry.trim())
    .filter((entry) => entry !== '')
    .map((entry) => {
      const separator = entry.indexOf('=');
      return separator > 0
        ? { label: entry.slice(0, separator), path: entry.slice(separator + 1) }
        : { label: 'session', path: entry };
    });

  if (entries.length === 0) return 'No usage files were provided for this run.';

  return entries
    .map(({ label, path }) => `**\`${label}\`**\n\n${renderUsage(readUsage(path))}`)
    .join('\n\n');
}

function writeJobSummary(text) {
  const target = process.env.GITHUB_STEP_SUMMARY;
  if (!target) {
    console.log(text);
    return;
  }
  appendFileSync(target, `${text}\n`, 'utf8');
}

// ---------------------------------------------------------------------------
// GitHub API
// ---------------------------------------------------------------------------

async function postReview({ token, repository, prNumber, sha, body, comments, reviewers }) {
  const url = `https://api.github.com/repos/${repository}/pulls/${prNumber}/reviews`;
  const payload = {
    commit_id: sha,
    body,
    event: 'COMMENT',
    comments: comments.map((finding) => ({
      path: finding.path,
      line: finding.line,
      side: 'RIGHT',
      body: renderInlineComment(finding, reviewers),
    })),
  };

  const response = await fetch(url, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
      'Content-Type': 'application/json',
      'User-Agent': 'boogabooster-code-quality-check',
    },
    body: JSON.stringify(payload),
  });

  const text = await response.text();
  if (!response.ok) {
    // Surface status and body: the position rules are fiddly and a 422 must be diagnosable.
    fail(
      `GitHub refused the review (HTTP ${response.status}). Response: ${text.slice(0, 2000)}`,
    );
  }

  try {
    return JSON.parse(text);
  } catch {
    return {};
  }
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

async function main() {
  const token = requireEnv('GITHUB_TOKEN');
  const repository = requireEnv('PR_REPOSITORY');
  const prNumber = requireEnv('PR_NUMBER');
  const sha = requireEnv('PR_HEAD_SHA');
  const findingsPath = process.env.FINDINGS_PATH ?? '.code-review/findings.json';
  const diffPath = process.env.DIFF_PATH ?? '.code-review/diff.patch';
  const usagePath = process.env.USAGE_PATH ?? '.code-review/usage.json';
  const usagePaths = process.env.USAGE_PATHS ?? '';
  const comparisonPath = process.env.COMPARISON_PATH ?? '';
  const narrativePath = process.env.COMPARISON_MD_PATH ?? '';

  // The reviewer roster comes from the comparison document, not from an environment
  // variable duplicating the workflow's matrix: one source of truth, no drift.
  let comparison = null;
  if (comparisonPath !== '') {
    if (!existsSync(comparisonPath)) {
      fail(
        `Missing ${comparisonPath}. The comparison step did not produce its report, so ` +
          'this check fails rather than publishing an unattributed review.',
      );
    }
    try {
      comparison = JSON.parse(readFileSync(comparisonPath, 'utf8'));
    } catch (error) {
      fail(`Invalid ${comparisonPath}: ${error.message}`);
    }
  }

  const narrative =
    narrativePath !== '' && existsSync(narrativePath)
      ? readFileSync(narrativePath, 'utf8')
      : null;
  if (narrativePath !== '' && narrative === null) {
    console.log(`::warning::No comparison narrative at ${narrativePath}; publishing the figures only.`);
  }

  const meta = {
    cliVersion: process.env.COPILOT_CLI_VERSION ?? 'unknown',
    model: process.env.COPILOT_MODEL_PINNED ?? 'unknown',
    models: comparison?.models,
    compareModel: process.env.COMPARE_MODEL ?? '',
  };
  const reviewers = comparison?.models ?? [];

  if (!existsSync(findingsPath)) {
    fail(
      `The review produced no findings file at ${findingsPath}. The review did not complete, ` +
        'so this check fails rather than reporting a clean review. Inspect the review job log.',
    );
  }
  if (!existsSync(diffPath)) {
    fail(`Missing ${diffPath}; cannot determine which lines accept inline comments.`);
  }

  let document;
  try {
    document = validateFindings(readFileSync(findingsPath, 'utf8'));
  } catch (error) {
    fail(`Invalid ${findingsPath}: ${error.message}`);
  }

  const { summary, findings } = document;
  const positions = parseDiffPositions(readFileSync(diffPath, 'utf8'));
  const { anchorable, unanchorable } = partitionFindings(findings, positions);

  const inline = anchorable.slice(0, MAX_INLINE_COMMENTS);
  const truncatedInline = anchorable.length - inline.length;
  const overflow = anchorable.slice(MAX_INLINE_COMMENTS);
  const counts = countBySeverity(findings);
  const blockingCount = counts.blocking;

  const body = renderReviewBody({
    summary,
    findings,
    // Anything not posted inline still has to be visible somewhere.
    unanchorable: [...unanchorable, ...overflow],
    blockingCount,
    truncatedInline,
    meta,
    comparison,
    narrative,
  });

  const review = await postReview({
    token,
    repository,
    prNumber,
    sha,
    body,
    comments: inline,
    reviewers,
  });

  const reviewUrl = review.html_url ?? `https://github.com/${repository}/pull/${prNumber}`;
  const engineLine =
    reviewers.length > 1
      ? `**Copilot CLI:** \`${meta.cliVersion}\` · **Reviewers:** ${quoted(reviewers)}` +
        (meta.compareModel ? ` · **Comparison by:** \`${meta.compareModel}\`` : '')
      : `**Copilot CLI:** \`${meta.cliVersion}\` · **Model:** \`${meta.model}\``;

  const agreementLine =
    comparison !== null && reviewers.length > 1
      ? `**Reviewer agreement:** ${comparison.totals?.shared ?? 0}/` +
        `${comparison.totals?.clusters ?? 0} problems reported by more than one reviewer ` +
        `(${comparison.agreementRate ?? 'n/a'})`
      : null;

  writeJobSummary(
    [
      '## Code quality check',
      '',
      summary,
      '',
      `**Findings:** ${renderCounts(counts, findings.length)}`,
      `**Inline comments posted:** ${inline.length}`,
      `**Unanchored findings:** ${unanchorable.length + overflow.length}`,
      ...(agreementLine ? [agreementLine] : []),
      `**Review:** ${reviewUrl}`,
      '',
      engineLine,
      '',
      '### Usage',
      '',
      usagePaths !== '' ? renderAllUsage(usagePaths) : renderUsage(readUsage(usagePath)),
    ].join('\n'),
  );

  console.log(
    `Posted review with ${inline.length} inline comment(s); ` +
      `${unanchorable.length + overflow.length} rendered in the body.`,
  );

  if (blockingCount > 0) {
    fail(
      `${blockingCount} blocking finding${blockingCount === 1 ? '' : 's'} reported. See ${reviewUrl}`,
    );
  }
}

// Only run when invoked directly, so the pure functions above stay importable from tests.
// An env-var guard would not work here: ESM imports are hoisted, so a test setting one
// before its import statement would still run main().
const invokedDirectly =
  process.argv[1] !== undefined &&
  pathToFileURL(process.argv[1]).href === import.meta.url;

if (invokedDirectly) {
  main().catch((error) => fail(`Unexpected failure: ${error?.stack ?? error}`));
}
