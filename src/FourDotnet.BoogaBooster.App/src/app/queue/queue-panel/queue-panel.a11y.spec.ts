import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { QUEUE_SOURCE } from '../data/queue-source';
import { FakeQueueSource, createQueueStatus } from '../testing/fake-queue-source';
import { QueuePanel } from './queue-panel';

/**
 * Automated accessibility audit of the queue panel. Colour contrast needs
 * real layout/colour (unavailable in jsdom) and is checked against the
 * design tokens separately; every structural WCAG A/AA rule axe supports
 * runs here.
 */
describe('QueuePanel accessibility', () => {
  it('has no AXE violations', async () => {
    const source = new FakeQueueSource();
    source.setStatus('ready');
    source.setQueue(createQueueStatus());
    TestBed.configureTestingModule({
      providers: [{ provide: QUEUE_SOURCE, useValue: source }],
    });

    const fixture = TestBed.createComponent(QueuePanel);
    fixture.detectChanges();
    await fixture.whenStable();

    const results = await axe.run(fixture.nativeElement as HTMLElement, {
      resultTypes: ['violations'],
      rules: { 'color-contrast': { enabled: false } },
    });

    expect(results.violations).toEqual([]);
  });
});
