// Tests for the findings publisher's pure functions.
//
// Run with:  node --test .github/code-review/publish-review.test.mjs
//
// The diff-position rules are the fiddliest part of this workflow (design D8), so they
// get direct coverage: one bad line number must never suppress the other findings.


import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  validateFindings,
  parseDiffPositions,
  partitionFindings,
  countBySeverity,
  renderReviewBody,
  renderInlineComment,
} from './publish-review.mjs';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

// A realistic diff: one modified file, one added file, one deleted file, one rename.
const DIFF = `diff --git a/src/Ride.cs b/src/Ride.cs
index 1111111..2222222 100644
--- a/src/Ride.cs
+++ b/src/Ride.cs
@@ -10,6 +10,8 @@ public sealed class Ride
     private double _speed;

     public double Speed { get; set; }
+    public double Target { get; private set; }
+
     public void Tick()
     {
diff --git a/src/New.cs b/src/New.cs
new file mode 100644
index 0000000..3333333
--- /dev/null
+++ b/src/New.cs
@@ -0,0 +1,3 @@
+public sealed class New
+{
+}
diff --git a/src/Gone.cs b/src/Gone.cs
deleted file mode 100644
index 4444444..0000000
--- a/src/Gone.cs
+++ /dev/null
@@ -1,2 +0,0 @@
-public sealed class Gone
-{
diff --git a/src/Old.cs b/src/Renamed.cs
similarity index 95%
rename from src/Old.cs
rename to src/Renamed.cs
index 5555555..6666666 100644
--- a/src/Old.cs
+++ b/src/Renamed.cs
@@ -1,4 +1,4 @@
-public sealed class Old
+public sealed class Renamed
 {
     public int Value { get; }
 }
`;

const finding = (over = {}) => ({
  path: 'src/Ride.cs',
  line: 13,
  severity: 'major',
  category: 'domain-model',
  title: 'A title',
  detail: 'A detail.',
  standard: '.claude/skills/csharp-domain-model/SKILL.md',
  ...over,
});

const doc = (findings, summary = 'A summary.') =>
  JSON.stringify({ summary, findings });

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

test('accepts a well-formed document', () => {
  const result = validateFindings(doc([finding()]));
  assert.equal(result.summary, 'A summary.');
  assert.equal(result.findings.length, 1);
  assert.equal(result.findings[0].severity, 'major');
});

test('accepts an empty findings list', () => {
  const result = validateFindings(doc([], 'Nothing to report.'));
  assert.deepEqual(result.findings, []);
});

test('rejects invalid JSON', () => {
  assert.throws(() => validateFindings('{not json'), /not valid JSON/);
});

test('rejects a missing summary', () => {
  assert.throws(
    () => validateFindings(JSON.stringify({ findings: [] })),
    /non-empty "summary"/,
  );
});

test('rejects a missing findings array', () => {
  assert.throws(
    () => validateFindings(JSON.stringify({ summary: 'x' })),
    /"findings" array/,
  );
});

test('rejects an unknown severity', () => {
  assert.throws(
    () => validateFindings(doc([finding({ severity: 'catastrophic' })])),
    /severity must be one of/,
  );
});

test('rejects a non-integer line', () => {
  assert.throws(() => validateFindings(doc([finding({ line: 3.5 })])), /positive integer/);
  assert.throws(() => validateFindings(doc([finding({ line: 0 })])), /positive integer/);
});

test('rejects an empty detail', () => {
  assert.throws(() => validateFindings(doc([finding({ detail: '  ' })])), /detail/);
});

test('rejects paths that escape the repository', () => {
  for (const path of ['/etc/passwd', 'C:/Windows/system.ini', '../../secrets.txt']) {
    assert.throws(
      () => validateFindings(doc([finding({ path })])),
      /must not escape the repository root/,
      `expected ${path} to be rejected`,
    );
  }
});

test('normalises backslash paths', () => {
  const result = validateFindings(doc([finding({ path: 'src\\Ride.cs' })]));
  assert.equal(result.findings[0].path, 'src/Ride.cs');
});

// ---------------------------------------------------------------------------
// Diff parsing
// ---------------------------------------------------------------------------

test('collects added and context lines as valid anchors', () => {
  const positions = parseDiffPositions(DIFF);
  const ride = positions.get('src/Ride.cs');
  assert.ok(ride, 'expected positions for src/Ride.cs');
  // Hunk starts at new line 10: 10,11,12 context; 13,14 added; 15,16 context.
  assert.ok(ride.has(13), 'added line 13 should anchor');
  assert.ok(ride.has(10), 'context line 10 should anchor');
  assert.ok(!ride.has(99), 'line outside the hunk should not anchor');
});

test('collects lines from an added file', () => {
  const positions = parseDiffPositions(DIFF);
  const added = positions.get('src/New.cs');
  assert.ok(added.has(1) && added.has(3));
});

test('ignores deleted files', () => {
  const positions = parseDiffPositions(DIFF);
  assert.equal(positions.get('src/Gone.cs'), undefined);
});

test('uses the new path for a rename', () => {
  const positions = parseDiffPositions(DIFF);
  assert.ok(positions.has('src/Renamed.cs'));
  assert.equal(positions.get('src/Old.cs'), undefined);
});

test('an empty patch yields no positions', () => {
  assert.equal(parseDiffPositions('').size, 0);
});

// ---------------------------------------------------------------------------
// Partitioning
// ---------------------------------------------------------------------------

test('partitions anchorable from unanchorable findings', () => {
  const positions = parseDiffPositions(DIFF);
  const findings = [
    finding({ line: 13 }), // in the diff
    finding({ line: 900 }), // outside the diff
    finding({ path: 'src/Untouched.cs', line: 1 }), // file not in the diff
  ];
  const { anchorable, unanchorable } = partitionFindings(findings, positions);
  assert.equal(anchorable.length, 1);
  assert.equal(unanchorable.length, 2);
});

test('one unanchorable finding does not suppress the anchorable ones', () => {
  const positions = parseDiffPositions(DIFF);
  const findings = [finding({ line: 12345 }), finding({ line: 13 }), finding({ line: 14 })];
  const { anchorable } = partitionFindings(findings, positions);
  assert.equal(anchorable.length, 2, 'both valid findings survive');
});

test('all findings unanchorable still yields an empty inline set, not an error', () => {
  const positions = parseDiffPositions(DIFF);
  const { anchorable, unanchorable } = partitionFindings(
    [finding({ line: 5000 }), finding({ line: 5001 })],
    positions,
  );
  assert.deepEqual(anchorable, []);
  assert.equal(unanchorable.length, 2);
});

test('orders findings most severe first', () => {
  const positions = parseDiffPositions(DIFF);
  const { anchorable } = partitionFindings(
    [
      finding({ line: 13, severity: 'nit' }),
      finding({ line: 14, severity: 'blocking' }),
      finding({ line: 10, severity: 'minor' }),
    ],
    positions,
  );
  assert.deepEqual(
    anchorable.map((f) => f.severity),
    ['blocking', 'minor', 'nit'],
  );
});

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

test('counts findings by severity', () => {
  const counts = countBySeverity([
    finding({ severity: 'blocking' }),
    finding({ severity: 'blocking' }),
    finding({ severity: 'nit' }),
  ]);
  assert.equal(counts.blocking, 2);
  assert.equal(counts.nit, 1);
  assert.equal(counts.major, 0);
});

test('inline comment carries severity, title, detail and standard', () => {
  const body = renderInlineComment(finding());
  assert.match(body, /major/);
  assert.match(body, /A title/);
  assert.match(body, /A detail\./);
  assert.match(body, /csharp-domain-model/);
});

const meta = { cliVersion: '1.0.83', model: 'test-model' };

test('a failing review body states the failure and the blocking count', () => {
  const body = renderReviewBody({
    summary: 'Summary.',
    findings: [finding({ severity: 'blocking' })],
    unanchorable: [],
    blockingCount: 1,
    truncatedInline: 0,
    meta,
  });
  assert.match(body, /Check failed/);
  assert.match(body, /1 blocking finding\b/);
  assert.match(body, /runs again/);
});

test('a passing review body with advisory findings says so', () => {
  const body = renderReviewBody({
    summary: 'Summary.',
    findings: [finding({ severity: 'minor' })],
    unanchorable: [],
    blockingCount: 0,
    truncatedInline: 0,
    meta,
  });
  assert.match(body, /Check passed/);
  assert.match(body, /advisory/);
});

test('a clean review body distinguishes "found nothing" from "never ran"', () => {
  const body = renderReviewBody({
    summary: 'Looks good.',
    findings: [],
    unanchorable: [],
    blockingCount: 0,
    truncatedInline: 0,
    meta,
  });
  assert.match(body, /found nothing to report/);
  assert.match(body, /No findings\./);
});

test('unanchored findings are rendered in the body rather than dropped', () => {
  const body = renderReviewBody({
    summary: 'Summary.',
    findings: [finding({ line: 900 })],
    unanchorable: [finding({ line: 900, title: 'Orphan finding' })],
    blockingCount: 0,
    truncatedInline: 0,
    meta,
  });
  assert.match(body, /Unanchored findings/);
  assert.match(body, /Orphan finding/);
  assert.match(body, /src\/Ride\.cs:900/);
});

test('the body records the CLI version and model, and that nothing was built', () => {
  const body = renderReviewBody({
    summary: 'Summary.',
    findings: [],
    unanchorable: [],
    blockingCount: 0,
    truncatedInline: 0,
    meta,
  });
  assert.match(body, /1\.0\.83/);
  assert.match(body, /test-model/);
  assert.match(body, /does not build or test/);
});
