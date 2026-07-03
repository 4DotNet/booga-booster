import { InjectionToken, Signal } from '@angular/core';

import { QueueStatus } from '../models/queue.models';

/** Load status of the queue feed. */
export type QueueLoadStatus = 'idle' | 'loading' | 'ready' | 'error';

/**
 * The seam between the queue state service and whatever produces the queue
 * status. The HTTP implementation talks to the backend and polls; a fake
 * implementation drives tests. This feed is read-only: there are no commands
 * to post, just the current counts as signals.
 */
export interface QueueSource {
  /** Current queue status; `null` until the first successful load. */
  readonly queue: Signal<QueueStatus | null>;

  /** Current load status of the feed. */
  readonly status: Signal<QueueLoadStatus>;

  /** Force an immediate re-fetch of the current queue status. */
  refresh(): void;
}

/** DI token for the active {@link QueueSource} implementation. */
export const QUEUE_SOURCE = new InjectionToken<QueueSource>('QUEUE_SOURCE');
