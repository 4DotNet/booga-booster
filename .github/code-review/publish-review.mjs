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

export function renderInlineComment(finding) {
  const label = SEVERITY_LABEL[finding.severity] ?? finding.severity;
  const category = finding.category ? ` · \`${finding.category}\`` : '';
  return `**${label}${category} — ${finding.title}**\n\n${finding.detail}${renderStandard(finding)}`;
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
 * Compose the review body: summary, counts, verdict and reason, unanchored findings, and
 * a note that the review is automated.
 */
export function renderReviewBody({
  summary,
  findings,
  unanchorable,
  blockingCount,
  truncatedInline,
  meta,
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

  parts.push('---');
  parts.push(
    `_Automated review by GitHub Copilot CLI \`${meta.cliVersion}\` (model \`${meta.model}\`), ` +
      'grounded in this repository\'s checked-in standards. It reviews the diff only — ' +
      'it does not build or test the code. Advisory except for blocking findings._',
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

async function postReview({ token, repository, prNumber, sha, body, comments }) {
  const url = `https://api.github.com/repos/${repository}/pulls/${prNumber}/reviews`;
  const payload = {
    commit_id: sha,
    body,
    event: 'COMMENT',
    comments: comments.map((finding) => ({
      path: finding.path,
      line: finding.line,
      side: 'RIGHT',
      body: renderInlineComment(finding),
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
  const meta = {
    cliVersion: process.env.COPILOT_CLI_VERSION ?? 'unknown',
    model: process.env.COPILOT_MODEL_PINNED ?? 'unknown',
  };

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
  });

  const review = await postReview({
    token,
    repository,
    prNumber,
    sha,
    body,
    comments: inline,
  });

  const usage = readUsage(usagePath);
  const reviewUrl = review.html_url ?? `https://github.com/${repository}/pull/${prNumber}`;

  writeJobSummary(
    [
      '## Code quality check',
      '',
      summary,
      '',
      `**Findings:** ${renderCounts(counts, findings.length)}`,
      `**Inline comments posted:** ${inline.length}`,
      `**Unanchored findings:** ${unanchorable.length + overflow.length}`,
      `**Review:** ${reviewUrl}`,
      '',
      `**Copilot CLI:** \`${meta.cliVersion}\` · **Model:** \`${meta.model}\``,
      '',
      '### Usage',
      '',
      renderUsage(usage),
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
