#!/usr/bin/env node
// Compares the independent Copilot CLI reviews of one diff and merges them for publication.
//
// Two models review the same pull request without seeing each other's output (one matrix
// leg each). This script is the deterministic half of the comparison: it pairs findings
// that describe the same problem, reports what the reviewers agreed and disagreed on, and
// produces a single merged findings document for the publisher.
//
// Design notes (openspec/changes/code-quality-check/design.md):
//   D13 Reviewers run in a matrix and never see each other's findings, so agreement means
//       something. Adding or removing a model is a matrix edit; nothing here is hardcoded
//       to two reviewers -- the roster comes from the `--review` arguments.
//   D14 The pairing below is mechanical and explainable: same file, close line, same
//       category or a similar title. The semantic comparison is written separately by a
//       model into comparison.md, and it is commentary only -- it cannot change a
//       severity, add a finding, or move the gate.
//   D15 The gate stays a union: a cluster inherits the WORST severity any reviewer gave
//       it, so one model catching a blocking violation alone still fails the check.
//
// No dependencies. Node 22+.

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

import { parseDiffPositions, validateFindings } from './publish-review.mjs';

const SEVERITIES = ['blocking', 'major', 'minor', 'nit'];

/** How far apart two line numbers may be and still describe the same problem. */
export const LINE_WINDOW = 5;

/** Minimum title-token overlap to pair findings that disagree on category. */
export const TITLE_SIMILARITY = 0.5;

/** Tokens too generic to carry any signal about what a finding is about. */
const STOP_WORDS = new Set([
  'the', 'and', 'for', 'not', 'with', 'without', 'from', 'into', 'this', 'that', 'its',
  'has', 'have', 'are', 'was', 'were', 'but', 'any', 'all', 'new', 'use', 'uses', 'used',
  'missing', 'should', 'must',
]);

function fail(message) {
  console.error(`::error::${message}`);
  process.exit(1);
}

// ---------------------------------------------------------------------------
// Matching
// ---------------------------------------------------------------------------

export function titleTokens(title) {
  return new Set(
    String(title)
      .toLowerCase()
      .split(/[^a-z0-9]+/)
      .filter((token) => token.length >= 3 && !STOP_WORDS.has(token)),
  );
}

export function jaccard(a, b) {
  if (a.size === 0 || b.size === 0) return 0;
  let shared = 0;
  for (const token of a) if (b.has(token)) shared += 1;
  return shared / (a.size + b.size - shared);
}

/**
 * How confidently two findings describe the same problem, or `null` when they do not.
 * Lower `rank` is a stronger match; `label` is what the comparison report shows, so the
 * reader can see how loose the pairing was.
 */
export function matchQuality(a, b) {
  if (a.path !== b.path) return null;

  const lineDelta = Math.abs(a.line - b.line);
  if (lineDelta > LINE_WINDOW) return null;

  const sameCategory = a.category !== '' && a.category === b.category;

  if (lineDelta === 0 && sameCategory) return { rank: 0, label: 'exact', lineDelta };
  if (sameCategory) return { rank: 1, label: 'same-category', lineDelta };

  const similarity = jaccard(titleTokens(a.title), titleTokens(b.title));
  if (similarity >= TITLE_SIMILARITY) {
    return { rank: 2, label: 'similar-title', lineDelta, similarity };
  }

  // Same file, same neighbourhood, but nothing says it is the same problem. Two
  // different findings on adjacent lines are common; do not merge them.
  return null;
}

/**
 * Group findings across reviews into clusters, where a cluster is one problem and its
 * members are the reviewers that reported it.
 *
 * A cluster holds at most one finding per model: each reviewer already deduplicates its
 * own list, so a second finding from the same model is a different problem by definition.
 * Reviews and findings are processed in argument and file order, which makes the result
 * deterministic for a given pair of findings files.
 */
export function clusterFindings(reviews) {
  const clusters = [];

  for (const review of reviews) {
    for (const finding of review.findings) {
      let best = null;

      for (const cluster of clusters) {
        if (cluster.members.some((member) => member.model === review.model)) continue;

        // Compare against every member and keep that cluster's strongest match, so a
        // three-way cluster is not decided by whichever member happens to be first.
        let strongest = null;
        for (const member of cluster.members) {
          const quality = matchQuality(finding, member.finding);
          if (quality !== null && (strongest === null || quality.rank < strongest.rank)) {
            strongest = quality;
          }
        }

        if (strongest !== null && (best === null || strongest.rank < best.quality.rank)) {
          best = { cluster, quality: strongest };
        }
      }

      if (best === null) {
        clusters.push({
          members: [{ model: review.model, finding }],
          matchedBy: null,
        });
      } else {
        best.cluster.members.push({ model: review.model, finding });
        // Record the loosest link that holds the cluster together: it is the honest
        // description of how confident the pairing is.
        best.cluster.matchedBy =
          best.cluster.matchedBy === null || best.quality.rank > best.cluster.matchedBy.rank
            ? best.quality
            : best.cluster.matchedBy;
      }
    }
  }

  return clusters;
}

// ---------------------------------------------------------------------------
// Merging
// ---------------------------------------------------------------------------

const severityRank = (severity) => SEVERITIES.indexOf(severity);

export function worstSeverity(severities) {
  return severities.reduce((worst, severity) =>
    severityRank(severity) < severityRank(worst) ? severity : worst,
  );
}

/**
 * The member whose wording represents the cluster: the one that graded it worst, since
 * that is the grade the gate uses. Both the merged finding and the comparison report use
 * this, so a problem is described the same way inline and in the summary table.
 */
export function primaryMember(cluster) {
  return [...cluster.members].sort(
    (a, b) => severityRank(a.finding.severity) - severityRank(b.finding.severity),
  )[0];
}

/**
 * Turn clusters into one findings document in the publisher's schema.
 *
 * Each merged finding carries the worst severity any reviewer gave it (D15) and the
 * `models` that reported it, which is what the publisher attributes in the comment.
 * Where reviewers disagree on the line, prefer one the diff will accept as an anchor --
 * an accurate finding is worth little if it degrades into the review body.
 */
export function mergeClusters(clusters, positions = new Map()) {
  return clusters.map((cluster) => {
    const members = [...cluster.members].sort(
      (a, b) => severityRank(a.finding.severity) - severityRank(b.finding.severity),
    );
    const primary = primaryMember(cluster).finding;
    const severity = primary.severity;

    const anchorable = members.find((member) =>
      positions.get(member.finding.path)?.has(member.finding.line),
    );
    const line = anchorable ? anchorable.finding.line : primary.line;

    const others = members.slice(1);
    const detail = [
      primary.detail,
      ...others.map(
        (member) =>
          `**Also reported by \`${member.model}\`** (${member.finding.severity}): ` +
          member.finding.detail,
      ),
    ].join('\n\n');

    return {
      path: primary.path,
      line,
      severity,
      category: members.map((m) => m.finding.category).find((c) => c !== '') ?? '',
      title: primary.title,
      detail,
      standard: members.map((m) => m.finding.standard).find((s) => s !== '') ?? '',
      models: cluster.members.map((member) => member.model),
      severities: Object.fromEntries(
        cluster.members.map((member) => [member.model, member.finding.severity]),
      ),
    };
  });
}

export function countBySeverity(findings) {
  const counts = Object.fromEntries(SEVERITIES.map((severity) => [severity, 0]));
  for (const finding of findings) counts[finding.severity] += 1;
  return counts;
}

/**
 * The comparison report: per-reviewer totals, what they agreed on, what only one of them
 * saw, and where they agreed on the problem but not on how bad it is.
 */
export function buildComparison(reviews, clusters) {
  const models = reviews.map((review) => review.model);

  // Always describe a cluster through its primary member, so the report names a problem
  // exactly as the published comment does.
  const describe = (cluster) => ({
    path: primaryMember(cluster).finding.path,
    line: primaryMember(cluster).finding.line,
    severity: primaryMember(cluster).finding.severity,
    category: primaryMember(cluster).finding.category,
    title: primaryMember(cluster).finding.title,
    standard: primaryMember(cluster).finding.standard,
    models: cluster.members.map((m) => m.model),
    severities: Object.fromEntries(
      cluster.members.map((m) => [m.model, m.finding.severity]),
    ),
    matchedBy: cluster.matchedBy?.label ?? null,
    lineDelta: cluster.matchedBy?.lineDelta ?? null,
  });

  const shared = clusters.filter((cluster) => cluster.members.length > 1);
  const unanimous = clusters.filter((cluster) => cluster.members.length === models.length);

  const unique = Object.fromEntries(
    models.map((model) => [
      model,
      clusters
        .filter(
          (cluster) => cluster.members.length === 1 && cluster.members[0].model === model,
        )
        .map((cluster) => describe(cluster)),
    ]),
  );

  const disagreements = shared
    .map((cluster) => {
      const severities = Object.fromEntries(
        cluster.members.map((member) => [member.model, member.finding.severity]),
      );
      const distinct = new Set(Object.values(severities));
      if (distinct.size === 1) return null;
      return {
        path: primaryMember(cluster).finding.path,
        title: primaryMember(cluster).finding.title,
        severities,
        merged: worstSeverity([...distinct]),
      };
    })
    .filter((entry) => entry !== null);

  return {
    models,
    perModel: Object.fromEntries(
      reviews.map((review) => [
        review.model,
        {
          total: review.findings.length,
          bySeverity: countBySeverity(review.findings),
          summary: review.summary,
        },
      ]),
    ),
    totals: {
      clusters: clusters.length,
      shared: shared.length,
      unanimous: unanimous.length,
      uniqueByModel: Object.fromEntries(
        Object.entries(unique).map(([model, findings]) => [model, findings.length]),
      ),
    },
    // Share of distinct problems that more than one reviewer found. 1 means the reviewers
    // reported the same set; 0 means they overlapped on nothing.
    agreementRate:
      clusters.length === 0 ? 1 : Number((shared.length / clusters.length).toFixed(3)),
    shared: shared.map((cluster) => describe(cluster)),
    unique,
    severityDisagreements: disagreements,
  };
}

export function mergedSummary(reviews, comparison) {
  const { totals } = comparison;
  const uniqueTotal = Object.values(totals.uniqueByModel).reduce((a, b) => a + b, 0);

  const lines = [
    `${reviews.length} independent Copilot CLI reviews examined this diff and were ` +
      `compared: ${totals.clusters} distinct problem${totals.clusters === 1 ? '' : 's'}, ` +
      `${totals.shared} reported by more than one reviewer, ${uniqueTotal} by a single one.`,
    '',
  ];

  for (const review of reviews) {
    lines.push(`**\`${review.model}\`** — ${review.summary}`);
    lines.push('');
  }

  return lines.join('\n').trim();
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

export function parseArgs(argv) {
  const options = { reviews: [], diff: null, outJson: null, outFindings: null };

  for (let index = 0; index < argv.length; index += 1) {
    const flag = argv[index];
    const value = argv[index + 1];

    switch (flag) {
      case '--review': {
        if (value === undefined) throw new Error('--review needs a model=path value.');
        const separator = value.indexOf('=');
        if (separator <= 0) {
          throw new Error(`--review expects model=path, got "${value}".`);
        }
        options.reviews.push({
          model: value.slice(0, separator),
          path: value.slice(separator + 1),
        });
        index += 1;
        break;
      }
      case '--diff':
        options.diff = value;
        index += 1;
        break;
      case '--out-json':
        options.outJson = value;
        index += 1;
        break;
      case '--out-findings':
        options.outFindings = value;
        index += 1;
        break;
      default:
        throw new Error(`Unknown argument "${flag}".`);
    }
  }

  if (options.reviews.length < 2) {
    throw new Error('At least two --review arguments are required to compare reviews.');
  }
  if (!options.outJson || !options.outFindings) {
    throw new Error('--out-json and --out-findings are both required.');
  }

  return options;
}

function writeJson(path, document) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, `${JSON.stringify(document, null, 2)}\n`, 'utf8');
}

function main() {
  let options;
  try {
    options = parseArgs(process.argv.slice(2));
  } catch (error) {
    fail(error.message);
  }

  const reviews = options.reviews.map(({ model, path }) => {
    if (!existsSync(path)) {
      fail(
        `\`${model}\` produced no findings file at ${path}. Both reviews must complete ` +
          'before they can be compared; failing rather than publishing half a comparison.',
      );
    }
    try {
      const { summary, findings } = validateFindings(readFileSync(path, 'utf8'));
      return { model, summary, findings };
    } catch (error) {
      return fail(`Invalid findings file from \`${model}\` (${path}): ${error.message}`);
    }
  });

  const positions =
    options.diff && existsSync(options.diff)
      ? parseDiffPositions(readFileSync(options.diff, 'utf8'))
      : new Map();

  const clusters = clusterFindings(reviews);
  const comparison = buildComparison(reviews, clusters);
  const findings = mergeClusters(clusters, positions);

  writeJson(options.outJson, comparison);
  writeJson(options.outFindings, {
    summary: mergedSummary(reviews, comparison),
    findings,
  });

  for (const review of reviews) {
    const { total, bySeverity } = comparison.perModel[review.model];
    const breakdown = SEVERITIES.filter((severity) => bySeverity[severity] > 0)
      .map((severity) => `${bySeverity[severity]} ${severity}`)
      .join(' · ');
    console.log(`${review.model}: ${total} finding(s)${breakdown ? ` (${breakdown})` : ''}`);
  }
  console.log(
    `Merged into ${findings.length} distinct problem(s); ${comparison.totals.shared} ` +
      `shared, agreement rate ${comparison.agreementRate}.`,
  );
}

const invokedDirectly =
  process.argv[1] !== undefined &&
  pathToFileURL(process.argv[1]).href === import.meta.url;

if (invokedDirectly) main();
