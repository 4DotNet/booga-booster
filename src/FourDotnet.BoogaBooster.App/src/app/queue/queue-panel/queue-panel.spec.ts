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
  });

  it('renders the group and people counts once ready', () => {
    source.setStatus('ready');
    source.setQueue(createQueueStatus({ groupCount: 12, peopleWaiting: 34 }));

    const text = render().textContent ?? '';

    expect(text).toContain('Groups queued');
    expect(text).toContain('12');
    expect(text).toContain('People waiting');
    expect(text).toContain('34');
  });

  it('pluralizes a single group and a single person correctly', () => {
    source.setStatus('ready');
    source.setQueue(createQueueStatus({ groupCount: 1, peopleWaiting: 1 }));

    const text = render().textContent ?? '';

    expect(text).toContain('1 group queued');
    expect(text).not.toContain('1 groups queued');
    expect(text).toContain('1 person waiting');
    expect(text).not.toContain('1 people waiting');
  });
});
