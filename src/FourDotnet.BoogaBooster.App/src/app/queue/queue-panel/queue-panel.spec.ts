import { TestBed } from '@angular/core/testing';

import { QUEUE_SOURCE } from '../data/queue-source';
import { FakeQueueSource, createQueueStatus } from '../testing/fake-queue-source';
import { QueuePanel } from './queue-panel';

describe('QueuePanel', () => {
  let source: FakeQueueSource;

  function render(): HTMLElement {
    TestBed.configureTestingModule({
      providers: [{ provide: QUEUE_SOURCE, useValue: source }],
    });
    const fixture = TestBed.createComponent(QueuePanel);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    source = new FakeQueueSource();
  });

  it('shows a loading message before the first load', () => {
    const el = render();

    expect(el.textContent).toContain('Loading queue');
  });

  it('shows an unavailable message on error', () => {
    source.setStatus('error');

    const el = render();

    expect(el.textContent).toContain('Queue unavailable');
    expect(el.textContent).not.toContain('Single riders');
  });

  it('renders the group, people and single-rider counts once ready', () => {
    source.setStatus('ready');
    source.setQueue(
      createQueueStatus({ groupCount: 12, peopleWaiting: 34, singleRidersWaiting: 4 }),
    );

    const el = render();
    const metrics = Array.from(el.querySelectorAll('.metric')).map((metric) => [
      metric.querySelector('dt')?.textContent?.trim(),
      metric.querySelector('dd')?.textContent?.trim(),
    ]);

    expect(metrics).toEqual([
      ['Groups queued', '12'],
      ['People waiting', '34'],
      ['Single riders', '4'],
    ]);
  });

  it('announces the single riders in the live-region summary', () => {
    source.setStatus('ready');
    source.setQueue(
      createQueueStatus({ groupCount: 12, peopleWaiting: 34, singleRidersWaiting: 4 }),
    );

    const summary = render().querySelector('[role="status"]');

    expect(summary?.textContent).toContain('4 single riders');
  });

  it('pluralizes a single group, person and single rider correctly', () => {
    source.setStatus('ready');
    source.setQueue(
      createQueueStatus({ groupCount: 1, peopleWaiting: 1, singleRidersWaiting: 1 }),
    );

    const text = render().querySelector('[role="status"]')?.textContent ?? '';

    expect(text).toContain('1 group queued');
    expect(text).not.toContain('1 groups queued');
    expect(text).toContain('1 person waiting');
    expect(text).not.toContain('1 people waiting');
    expect(text).toContain('1 single rider.');
  });
});
