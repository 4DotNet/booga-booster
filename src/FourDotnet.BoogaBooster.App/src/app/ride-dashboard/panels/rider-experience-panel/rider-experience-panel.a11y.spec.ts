import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { RiderExperiencePanel } from './rider-experience-panel';

/**
 * Automated accessibility audit of the rider experience panel with data and in
 * its empty states. Colour contrast needs real layout/colour (unavailable in
 * jsdom) and is checked against the design tokens separately; every structural
 * WCAG A/AA rule axe supports runs here.
 */
describe('RiderExperiencePanel accessibility', () => {
  async function audit(values: (number | null)[]): Promise<{
    violations: axe.Result[];
    names: (string | null)[];
  }> {
    const fixture = TestBed.createComponent(RiderExperiencePanel);
    const [queue, happiness, intensity, nausea] = values;
    fixture.componentRef.setInput('queueHappiness', queue);
    fixture.componentRef.setInput('riderHappiness', happiness);
    fixture.componentRef.setInput('riderIntensity', intensity);
    fixture.componentRef.setInput('riderNausea', nausea);
    fixture.detectChanges();
    await fixture.whenStable();

    const host = fixture.nativeElement as HTMLElement;
    const results = await axe.run(host, {
      resultTypes: ['violations'],
      rules: { 'color-contrast': { enabled: false } },
    });
    const names = Array.from(host.querySelectorAll('[role="progressbar"]')).map((bar) =>
      bar.getAttribute('aria-label'),
    );
    return { violations: results.violations, names };
  }

  it('has no AXE violations with data, and every bar is named with its percent', async () => {
    const { violations, names } = await audit([72, 81, 68, 12]);

    expect(violations).toEqual([]);
    expect(names).toEqual([
      'Queue happiness 72%',
      'Rider happiness 81%',
      'Rider intensity 68%',
      'Rider nausea 12%',
    ]);
  });

  it('has no AXE violations on an empty ride', async () => {
    const { violations, names } = await audit([72, null, null, null]);

    expect(violations).toEqual([]);
    expect(names).toEqual(['Queue happiness 72%']);
  });

  it('has no AXE violations with an empty queue', async () => {
    const { violations } = await audit([null, 81, 68, 12]);

    expect(violations).toEqual([]);
  });

  it('has no AXE violations when everything is empty', async () => {
    const { violations, names } = await audit([null, null, null, null]);

    expect(violations).toEqual([]);
    expect(names).toEqual([]);
  });
});
