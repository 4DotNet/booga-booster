import { TestBed } from '@angular/core/testing';

import { QUEUE_SOURCE } from '../data/queue-source';
import { FakeQueueSource, createQueueStatus } from '../testing/fake-queue-source';
import { QueueStateService } from './queue-state.service';

describe('QueueStateService', () => {
  let source: FakeQueueSource;
  let service: QueueStateService;

  beforeEach(() => {
    source = new FakeQueueSource();
    TestBed.configureTestingModule({
      providers: [{ provide: QUEUE_SOURCE, useValue: source }],
    });
    service = TestBed.inject(QueueStateService);
  });

  it('reports not ready with no queue status', () => {
    expect(service.isReady()).toBe(false);
    expect(service.groupCount()).toBe(0);
    expect(service.peopleWaiting()).toBe(0);
  });

  it('projects the group and people counts once ready', () => {
    source.setStatus('ready');
    source.setQueue(createQueueStatus({ groupCount: 12, peopleWaiting: 34 }));

    expect(service.isReady()).toBe(true);
    expect(service.groupCount()).toBe(12);
    expect(service.peopleWaiting()).toBe(34);
    expect(service.summary()).toContain('12 groups queued');
    expect(service.summary()).toContain('34 people waiting');
  });

  it('pluralizes a single group and a single person correctly', () => {
    source.setStatus('ready');
    source.setQueue(createQueueStatus({ groupCount: 1, peopleWaiting: 1 }));

    expect(service.summary()).toContain('1 group queued');
    expect(service.summary()).toContain('1 person waiting');
  });

  it('reports the underlying load status, including error', () => {
    source.setStatus('error');

    expect(service.status()).toBe('error');
    expect(service.isReady()).toBe(false);
  });

  it('delegates refresh to the source', () => {
    service.refresh();

    expect(source.refreshCount).toBe(1);
  });
});
