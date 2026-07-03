import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, interval, merge, of } from 'rxjs';
import { catchError, map, startWith, switchMap, tap } from 'rxjs/operators';

import { QueueStatus, QueueStatusDto, toQueueStatus } from '../models/queue.models';
import { QueueLoadStatus, QueueSource } from './queue-source';

/**
 * The ride whose queue this dashboard represents. Mirrors the backend's
 * default `QueueModuleOptions.RideId`; a future iteration should make this
 * configurable (e.g. from the route or an operator-selected ride) instead of
 * hard-coding the single default ride.
 */
const RIDE_ID = '11111111-1111-1111-1111-111111111111';

/** Relative API base; the dev-server proxy forwards `/api` to the backend. */
const QUEUE_URL = `/api/rides/${RIDE_ID}/queue`;

/** How often to poll for queue arrivals, in milliseconds. */
const POLL_MS = 5000;

/**
 * {@link QueueSource} backed by the backend queue API. Polls `GET
 * /rides/{rideId}/queue` so the panel tracks arrivals, exposing the result as
 * signals. Read-only: unlike the weather source there are no disturbances to
 * post, only the current counts to observe.
 */
@Injectable({ providedIn: 'root' })
export class HttpQueueSource implements QueueSource {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  private readonly queueSignal = signal<QueueStatus | null>(null);
  private readonly statusSignal = signal<QueueLoadStatus>('idle');
  private readonly refreshTrigger = new Subject<void>();

  readonly queue = this.queueSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();

  constructor() {
    merge(interval(POLL_MS).pipe(startWith(0)), this.refreshTrigger)
      .pipe(
        switchMap(() => this.fetch()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  refresh(): void {
    this.refreshTrigger.next();
  }

  private fetch(): Observable<QueueStatus | null> {
    if (this.statusSignal() === 'idle') {
      this.statusSignal.set('loading');
    }
    return this.http.get<QueueStatusDto>(QUEUE_URL).pipe(
      map((dto) => toQueueStatus(dto)),
      tap((queue) => {
        this.queueSignal.set(queue);
        this.statusSignal.set('ready');
      }),
      catchError(() => {
        this.statusSignal.set('error');
        return of(null);
      }),
    );
  }
}
