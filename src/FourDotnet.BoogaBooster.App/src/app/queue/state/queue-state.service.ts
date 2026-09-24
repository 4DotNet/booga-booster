import { Injectable, Signal, computed, inject } from '@angular/core';

import { QUEUE_SOURCE, QueueLoadStatus } from '../data/queue-source';
import { QueueStatus, summaryText } from '../models/queue.models';

/**
 * Single source of truth for the queue panel. Projects the injected
 * {@link QueueSource} into read-model signals the panel binds to. Read-only:
 * there is nothing to command here, only the current counts to observe.
 */
@Injectable({ providedIn: 'root' })
export class QueueStateService {
  private readonly source = inject(QUEUE_SOURCE);

  /** Current queue status; `null` until the first successful load. */
  readonly queue: Signal<QueueStatus | null> = this.source.queue;

  /** Current load status of the feed. */
  readonly status: Signal<QueueLoadStatus> = this.source.status;

  /** Whether the queue status is ready to display. */
  readonly isReady = computed(() => this.status() === 'ready' && this.queue() !== null);

  /** Number of groups currently queued. */
  readonly groupCount = computed(() => this.queue()?.groupCount ?? 0);

  /** Number of individual people currently waiting. */
  readonly peopleWaiting = computed(() => this.queue()?.peopleWaiting ?? 0);

  /** Average happiness, in `[0, 1]`, of everyone waiting; `null` when empty or not yet loaded. */
  readonly averageHappiness = computed(() => this.queue()?.averageHappiness ?? null);

  /** A worded, one-line summary for a live region. */
  readonly summary = computed(() => summaryText(this.queue()));

  /** Force an immediate re-fetch of the current queue status. */
  refresh(): void {
    this.source.refresh();
  }
}
