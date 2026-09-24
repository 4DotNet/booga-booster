import { ComponentFixture, TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { RiderMoodPanel } from './rider-mood-panel';

/**
 * Automated accessibility audit of the rider mood panel, with real values
 * and with each empty/unavailable state. Colour contrast needs real
 * layout/colour (unavailable in jsdom) and is checked against the design
 * tokens separately; every structural WCAG A/AA rule axe supports runs here.
 */
describe('RiderMoodPanel accessibility', () => {
  async function audit(fixture: ComponentFixture<RiderMoodPanel>): Promise<axe.Result[]> {
    fixture.detectChanges();
    await fixture.whenStable();

    const results = await axe.run(fixture.nativeElement as HTMLElement, {
      resultTypes: ['violations'],
      rules: { 'color-contrast': { enabled: false } },
    });
    return results.violations;
  }

  it('has no AXE violations with values', async () => {
    const fixture = TestBed.createComponent(RiderMoodPanel);
    fixture.componentRef.setInput('queueHappiness', 0.72);
    fixture.componentRef.setInput('riderHappiness', 0.6);
    fixture.componentRef.setInput('nausea', 0.25);
    fixture.componentRef.setInput('queueUnavailable', false);

    expect(await audit(fixture)).toEqual([]);
  });

  it('has no AXE violations with the queue-empty and no-riders states', async () => {
    const fixture = TestBed.createComponent(RiderMoodPanel);
    fixture.componentRef.setInput('queueHappiness', null);
    fixture.componentRef.setInput('riderHappiness', null);
    fixture.componentRef.setInput('nausea', null);
    fixture.componentRef.setInput('queueUnavailable', false);

    expect(await audit(fixture)).toEqual([]);
  });

  it('has no AXE violations with the queue-unavailable state', async () => {
    const fixture = TestBed.createComponent(RiderMoodPanel);
    fixture.componentRef.setInput('queueHappiness', null);
    fixture.componentRef.setInput('riderHappiness', 0.6);
    fixture.componentRef.setInput('nausea', 0.25);
    fixture.componentRef.setInput('queueUnavailable', true);

    expect(await audit(fixture)).toEqual([]);
  });
});
