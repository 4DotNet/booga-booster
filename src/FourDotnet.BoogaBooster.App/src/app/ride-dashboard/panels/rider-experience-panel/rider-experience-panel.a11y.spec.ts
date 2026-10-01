import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { NO_RIDER_EXPERIENCE, RiderExperience } from '../../models/ride.models';
import { RiderExperiencePanel } from './rider-experience-panel';

/**
 * Automated accessibility audit of the rider experience panel. Colour
 * contrast needs real layout/colour (unavailable in jsdom) and is checked
 * against the design tokens separately; every structural WCAG A/AA rule axe
 * supports runs here.
 */
async function audit(queueHappiness: number | null, riderExperience: RiderExperience) {
  const fixture = TestBed.createComponent(RiderExperiencePanel);
  fixture.componentRef.setInput('queueHappiness', queueHappiness);
  fixture.componentRef.setInput('riderExperience', riderExperience);
  fixture.detectChanges();
  await fixture.whenStable();

  return axe.run(fixture.nativeElement as HTMLElement, {
    resultTypes: ['violations'],
    rules: { 'color-contrast': { enabled: false } },
  });
}

describe('RiderExperiencePanel accessibility', () => {
  it('has no AXE violations with data', async () => {
    const results = await audit(72, {
      averageHappiness: 81,
      averagePreferredIntensity: 74,
      averageNausea: 12,
    });

    expect(results.violations).toEqual([]);
  });

  it('has no AXE violations with no data', async () => {
    const results = await audit(null, NO_RIDER_EXPERIENCE);

    expect(results.violations).toEqual([]);
  });
});
