import { LIGHT_PATTERNS } from './light-patterns';
import { showCycleSeconds } from './light-show-sequencer';
import { evaluateShow } from './light-show';
import { SPOT_CYCLE_SECONDS, spotBrightness } from './spot-effects';
import { sampleBulbs } from './testing/sample-bulbs';

/**
 * WCAG 2.3.1 (Three Flashes or Below Threshold): nothing may flash more than three
 * times in any one-second period. A flash is counted as a dim→bright crossing of the
 * mid level; the series is sampled finely enough to see every crossing.
 */
const SAMPLE_HZ = 120;
const MAX_FLASHES_PER_SECOND = 3;

function maxFlashesPerSecond(series: readonly number[]): number {
  const onsets: number[] = [];
  for (let i = 1; i < series.length; i++) {
    if (series[i - 1] < 0.5 && series[i] >= 0.5) {
      onsets.push(i);
    }
  }
  let worst = 0;
  let start = 0;
  for (let end = 0; end < onsets.length; end++) {
    while (onsets[end] - onsets[start] >= SAMPLE_HZ) {
      start++;
    }
    worst = Math.max(worst, end - start + 1);
  }
  return worst;
}

describe('light show flash safety', () => {
  const bulbs = sampleBulbs();

  it.each(LIGHT_PATTERNS.map((p) => [p.name, p] as const))(
    '%s flashes at most three times per second for any bulb',
    (_name, pattern) => {
      const samples = Math.ceil(pattern.loopSeconds * 3 * SAMPLE_HZ);
      for (const bulb of bulbs) {
        const series = Array.from({ length: samples }, (_, i) =>
          pattern.brightness(bulb, (i / SAMPLE_HZ / pattern.loopSeconds) % 1),
        );
        expect(maxFlashesPerSecond(series)).toBeLessThanOrEqual(MAX_FLASHES_PER_SECOND);
      }
    },
  );

  it('stays within the limit across pattern switches over a whole show cycle', () => {
    const subset = bulbs.filter((_b, i) => i % 4 === 0);
    const brightness = new Float32Array(subset.length);
    const colours = new Uint8Array(subset.length);
    const samples = Math.ceil(showCycleSeconds() * SAMPLE_HZ) + SAMPLE_HZ;
    const series = subset.map(() => new Array<number>(samples));
    for (let i = 0; i < samples; i++) {
      evaluateShow(subset, i / SAMPLE_HZ, false, brightness, colours);
      subset.forEach((_b, k) => (series[k][i] = brightness[k]));
    }
    for (const s of series) {
      expect(maxFlashesPerSecond(s)).toBeLessThanOrEqual(MAX_FLASHES_PER_SECOND);
    }
  });

  it('keeps every pole spotlight within the limit', () => {
    const samples = SPOT_CYCLE_SECONDS * 2 * SAMPLE_HZ;
    for (let pole = 0; pole < 8; pole++) {
      const series = Array.from({ length: samples }, (_, i) =>
        spotBrightness(pole, 8, i / SAMPLE_HZ),
      );
      expect(maxFlashesPerSecond(series)).toBeLessThanOrEqual(MAX_FLASHES_PER_SECOND);
    }
  });
});
