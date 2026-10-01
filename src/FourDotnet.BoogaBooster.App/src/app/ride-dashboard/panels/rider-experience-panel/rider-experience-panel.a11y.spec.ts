import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { SeatRider } from '../../models/ride.models';
import { RiderExperiencePanel } from './rider-experience-panel';

/**
 * Structural axe audit of the Rider Experience panel, populated and empty.
 * Contrast needs real layout/colour (unavailable in jsdom) and is checked
 * against the design tokens separately.
 */
describe('RiderExperiencePanel accessibility', () => {
  async function audit(riders: readonly SeatRider[]) {
    const fixture = TestBed.createComponent(RiderExperiencePanel);
    fixture.componentRef.setInput('queuedGuests', []);
    fixture.componentRef.setInput('riders', riders);
    fixture.detectChanges();
    await fixture.whenStable();

    return axe.run(fixture.nativeElement as HTMLElement, {
      resultTypes: ['violations'],
      rules: { 'color-contrast': { enabled: false } },
    });
  }

  it('has no AXE violations with riders', async () => {
    const results = await audit([{ guestNumber: 1, happiness: 60, preferredG: 3.3, nausea: 20 }]);

    expect(results.violations).toEqual([]);
  });

  it('has no AXE violations when empty', async () => {
    const results = await audit([]);

    expect(results.violations).toEqual([]);
  });
});
