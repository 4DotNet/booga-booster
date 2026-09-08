// Tests for the deterministic half of the two-model comparison.
//
//   node --test .github/code-review/compare-reviews.test.mjs
//
// No dependencies: node:test and node:assert only, same as publish-review.test.mjs.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  buildComparison,
  clusterFindings,
  jaccard,
  matchQuality,
  mergeClusters,
  mergedSummary,
  parseArgs,
  titleTokens,
  worstSeverity,
} from './compare-reviews.mjs';

const finding = (overrides = {}) => ({
  path: 'src/Queue/Handler.cs',
  line: 20,
  severity: 'major',
  category: 'observability',
  title: 'Handler starts no activity',
  detail: 'The handler emits no span.',
  standard: '.claude/skills/csharp-observability/SKILL.md',
  ...overrides,
});

const review = (model, findings) => ({ model, summary: `${model} looked at it.`, findings });

// ---------------------------------------------------------------------------
// matchQuality
// ---------------------------------------------------------------------------

test('same line and category is an exact match', () => {
  const quality = matchQuality(finding(), finding());
  assert.equal(quality.label, 'exact');
  assert.equal(quality.rank, 0);
});

test('a nearby line with the same category still matches', () => {
  const quality = matchQuality(finding(), finding({ line: 24 }));
  assert.equal(quality.label, 'same-category');
  assert.equal(quality.lineDelta, 4);
});

test('findings further apart than the window do not match', () => {
  assert.equal(matchQuality(finding(), finding({ line: 40 })), null);
});

test('different files never match', () => {
  assert.equal(matchQuality(finding(), finding({ path: 'src/Other.cs' })), null);
});

test('a similar title matches across differing categories', () => {
  const quality = matchQuality(
    finding(),
    finding({ category: 'tracing', title: 'Handler starts no activity for the span' }),
  );
  assert.equal(quality.label, 'similar-title');
});

test('unrelated problems on neighbouring lines are left apart', () => {
  assert.equal(
    matchQuality(
      finding(),
      finding({ line: 21, category: 'naming', title: 'Misleading parameter name' }),
    ),
    null,
  );
});

test('an empty category does not count as agreement', () => {
  assert.equal(
    matchQuality(
      finding({ category: '' }),
      finding({ category: '', title: 'Something else entirely happens here' }),
    ),
    null,
  );
});

test('title similarity ignores boilerplate words', () => {
  assert.equal(titleTokens('The missing test for the handler').has('missing'), false);
  assert.equal(jaccard(titleTokens('alpha beta'), titleTokens('alpha beta')), 1);
  assert.equal(jaccard(titleTokens('alpha'), titleTokens('gamma')), 0);
});

// ---------------------------------------------------------------------------
// clusterFindings
// ---------------------------------------------------------------------------

test('a problem both reviewers report becomes one cluster', () => {
  const clusters = clusterFindings([
    review('model-a', [finding()]),
    review('model-b', [finding({ line: 22, severity: 'blocking' })]),
  ]);

  assert.equal(clusters.length, 1);
  assert.deepEqual(
    clusters[0].members.map((member) => member.model),
    ['model-a', 'model-b'],
  );
});

test('a problem only one reviewer reports stays alone', () => {
  const clusters = clusterFindings([
    review('model-a', [finding()]),
    review('model-b', [finding({ path: 'src/Other.cs', line: 5 })]),
  ]);

  assert.equal(clusters.length, 2);
  assert.deepEqual(clusters.map((cluster) => cluster.members.length), [1, 1]);
});

test('one cluster never holds two findings from the same reviewer', () => {
  // Both of model-a's findings would match each other; they must not be merged, because
  // a reviewer already deduplicates its own list.
  const clusters = clusterFindings([
    review('model-a', [finding(), finding({ line: 21 })]),
    review('model-b', [finding({ line: 22 })]),
  ]);

  assert.equal(clusters.length, 2);
  for (const cluster of clusters) {
    const models = cluster.members.map((member) => member.model);
    assert.equal(new Set(models).size, models.length);
  }
  // The first cluster took the pairing; the second is model-a's alone.
  assert.deepEqual(clusters[0].members.map((m) => m.model), ['model-a', 'model-b']);
  assert.deepEqual(clusters[1].members.map((m) => m.model), ['model-a']);
});

test('the cluster records the loosest link that holds it together', () => {
  const clusters = clusterFindings([
    review('model-a', [finding()]),
    review('model-b', [
      finding({ category: 'tracing', title: 'Handler starts no activity at all' }),
    ]),
  ]);

  assert.equal(clusters[0].matchedBy.label, 'similar-title');
});

// ---------------------------------------------------------------------------
// mergeClusters
// ---------------------------------------------------------------------------

test('a merged finding inherits the worst severity and lists every reviewer', () => {
  const clusters = clusterFindings([
    review('model-a', [finding({ severity: 'minor' })]),
    review('model-b', [finding({ line: 21, severity: 'blocking' })]),
  ]);

  const [merged] = mergeClusters(clusters);

  assert.equal(merged.severity, 'blocking');
  assert.deepEqual(merged.models, ['model-a', 'model-b']);
  assert.deepEqual(merged.severities, { 'model-a': 'minor', 'model-b': 'blocking' });
});

test('a merged finding keeps both reviewers’ reasoning', () => {
  const clusters = clusterFindings([
    review('model-a', [finding({ detail: 'A says so.' })]),
    review('model-b', [finding({ detail: 'B says so.' })]),
  ]);

  const [merged] = mergeClusters(clusters);

  assert.match(merged.detail, /A says so\./);
  assert.match(merged.detail, /Also reported by `model-b`/);
  assert.match(merged.detail, /B says so\./);
});

test('an anchorable line wins over an unanchorable one', () => {
  const positions = new Map([['src/Queue/Handler.cs', new Set([21])]]);
  const clusters = clusterFindings([
    review('model-a', [finding({ line: 20, severity: 'blocking' })]),
    review('model-b', [finding({ line: 21, severity: 'minor' })]),
  ]);

  const [merged] = mergeClusters(clusters, positions);

  assert.equal(merged.line, 21, 'takes the line the diff will accept as an anchor');
  assert.equal(merged.severity, 'blocking', 'without weakening the severity');
});

test('a lone finding survives the merge untouched', () => {
  const clusters = clusterFindings([review('model-a', [finding()]), review('model-b', [])]);
  const [merged] = mergeClusters(clusters);

  assert.deepEqual(merged.models, ['model-a']);
  assert.equal(merged.detail, 'The handler emits no span.');
});

test('worstSeverity orders the taxonomy, not the alphabet', () => {
  assert.equal(worstSeverity(['nit', 'blocking', 'major']), 'blocking');
  assert.equal(worstSeverity(['minor', 'nit']), 'minor');
});

// ---------------------------------------------------------------------------
// buildComparison
// ---------------------------------------------------------------------------

test('the comparison reports totals, uniques and the agreement rate', () => {
  const reviews = [
    review('model-a', [finding(), finding({ path: 'src/A.cs', line: 3 })]),
    review('model-b', [finding({ line: 21 }), finding({ path: 'src/B.cs', line: 9 })]),
  ];
  const comparison = buildComparison(reviews, clusterFindings(reviews));

  assert.deepEqual(comparison.models, ['model-a', 'model-b']);
  assert.equal(comparison.totals.clusters, 3);
  assert.equal(comparison.totals.shared, 1);
  assert.equal(comparison.totals.unanimous, 1);
  assert.deepEqual(comparison.totals.uniqueByModel, { 'model-a': 1, 'model-b': 1 });
  assert.equal(comparison.agreementRate, Number((1 / 3).toFixed(3)));
  assert.equal(comparison.unique['model-a'][0].path, 'src/A.cs');
  assert.equal(comparison.perModel['model-b'].total, 2);
  assert.equal(comparison.perModel['model-b'].bySeverity.major, 2);
});

test('severity disagreements are listed with the grade that was published', () => {
  const reviews = [
    review('model-a', [finding({ severity: 'blocking' })]),
    review('model-b', [finding({ line: 21, severity: 'minor' })]),
  ];
  const comparison = buildComparison(reviews, clusterFindings(reviews));

  assert.equal(comparison.severityDisagreements.length, 1);
  assert.equal(comparison.severityDisagreements[0].merged, 'blocking');
  assert.deepEqual(comparison.severityDisagreements[0].severities, {
    'model-a': 'blocking',
    'model-b': 'minor',
  });
});

test('agreeing on the severity is not a disagreement', () => {
  const reviews = [
    review('model-a', [finding()]),
    review('model-b', [finding({ line: 21 })]),
  ];
  const comparison = buildComparison(reviews, clusterFindings(reviews));

  assert.deepEqual(comparison.severityDisagreements, []);
});

test('two clean reviews compare as full agreement', () => {
  const reviews = [review('model-a', []), review('model-b', [])];
  const comparison = buildComparison(reviews, clusterFindings(reviews));

  assert.equal(comparison.totals.clusters, 0);
  assert.equal(comparison.agreementRate, 1);
  assert.deepEqual(comparison.severityDisagreements, []);
});

test('the merged summary quotes every reviewer', () => {
  const reviews = [
    review('model-a', [finding()]),
    review('model-b', [finding({ line: 21 })]),
  ];
  const summary = mergedSummary(reviews, buildComparison(reviews, clusterFindings(reviews)));

  assert.match(summary, /2 independent Copilot CLI reviews/);
  assert.match(summary, /\*\*`model-a`\*\* — model-a looked at it\./);
  assert.match(summary, /\*\*`model-b`\*\* — model-b looked at it\./);
});

// ---------------------------------------------------------------------------
// parseArgs
// ---------------------------------------------------------------------------

test('parses repeated review arguments', () => {
  const options = parseArgs([
    '--review',
    'model-a=/tmp/a.json',
    '--review',
    'model-b=/tmp/b.json',
    '--diff',
    '/tmp/diff.patch',
    '--out-json',
    '/tmp/comparison.json',
    '--out-findings',
    '/tmp/merged.json',
  ]);

  assert.deepEqual(options.reviews, [
    { model: 'model-a', path: '/tmp/a.json' },
    { model: 'model-b', path: '/tmp/b.json' },
  ]);
  assert.equal(options.diff, '/tmp/diff.patch');
});

test('refuses to compare fewer than two reviews', () => {
  assert.throws(
    () =>
      parseArgs([
        '--review',
        'model-a=/tmp/a.json',
        '--out-json',
        '/tmp/c.json',
        '--out-findings',
        '/tmp/m.json',
      ]),
    /At least two --review/,
  );
});

test('rejects a review argument that is not model=path', () => {
  assert.throws(() => parseArgs(['--review', '/tmp/a.json']), /expects model=path/);
});

test('rejects an unknown argument rather than ignoring it', () => {
  assert.throws(() => parseArgs(['--reviewers', 'a']), /Unknown argument/);
});
