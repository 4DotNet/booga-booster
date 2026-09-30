import { signal } from '@angular/core';

import { QueueLoadStatus, QueueSource } from '../data/queue-source';
import { QueueStatus } from '../models/queue.models';

/**
 * Deterministic {@link QueueSource} for tests: no HTTP, no timers. Queue
 * status and load status are set directly, and refresh calls are counted so
 * specs can assert on what was dispatched.
 */
export class FakeQueueSource implements QueueSource {
  refreshCount = 0;

  private readonly queueSignal = signal<QueueStatus | null>(null);
  private readonly statusSignal = signal<QueueLoadStatus>('idle');

  readonly queue = this.queueSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();

  setQueue(queue: QueueStatus | null): void {
    this.queueSignal.set(queue);
  }

  setStatus(status: QueueLoadStatus): void {
    this.statusSignal.set(status);
  }

  refresh(): void {
    this.refreshCount++;
  }
}

/** Build a queue status snapshot, overriding any fields for a specific scenario. */
export function createQueueStatus(overrides: Partial<QueueStatus> = {}): QueueStatus {
  return {
    groupCount: 12,
    peopleWaiting: 34,
    ...overrides,
  };
}
