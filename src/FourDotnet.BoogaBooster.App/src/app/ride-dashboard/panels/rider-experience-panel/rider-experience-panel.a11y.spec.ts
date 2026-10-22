import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { RiderExperiencePanel } from './rider-experience-panel';

/**
 * Automated accessibility audit of the Rider Experience panel, with values and
 * with no guests. Colour contrast needs real layout/colour (unavailable in
 * jsdom); every structural WCAG A/AA rule axe supports runs here.
 */
describe('RiderExperiencePanel accessibility', () => {
  async function audit(averages: Record<string, number | null>): Promise<axe.Result[]> {
    const fixture = TestBed.createComponent(RiderExperiencePanel);
    Object.entries(averages).forEach(([name, value]) => fixture.componentRef.setInput(name, value));
    fixture.detectChanges();
    await fixture.whenStable();

    const results = await axe.run(fixture.nativeElement as HTMLElement, {
      resultTypes: ['violations'],
      rules: { 'color-contrast': { enabled: false } },
    });
    return results.violations;
  }

  it('has no AXE violations with values', async () => {
    expect(
      await audit({
        queueHappiness: 72,
        riderHappiness: 64,
        riderIntensity: 81,
        riderNausea: 12,
      }),
    ).toEqual([]);
  });

  it('has no AXE violations with no guests', async () => {
    expect(
      await audit({
        queueHappiness: null,
        riderHappiness: null,
        riderIntensity: null,
        riderNausea: null,
      }),
    ).toEqual([]);
  });
});
