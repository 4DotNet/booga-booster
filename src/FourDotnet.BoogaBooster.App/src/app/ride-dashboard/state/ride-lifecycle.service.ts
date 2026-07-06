import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, Signal, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, interval, merge, of } from 'rxjs';
import { catchError, finalize, map, startWith, switchMap, tap } from 'rxjs/operators';

import { RideState, RideTelemetryDto, toRideState, toRideStateName } from '../models/ride.models';

/** Relative API base; the dev-server proxy forwards `/api` to the backend. */
const TELEMETRY_URL = '/api/ride/telemetry';
const STATE_URL = '/api/ride/state';

/** How often to poll for the ride's lifecycle state, in milliseconds. */
const POLL_MS = 1000;

/**
 * Server-backed source of truth for the ride's lifecycle state (the
 * operator-facing state machine: Idle/Loading/Safe/Started/Stopping/
 * Offloading/EmergencyStop). Polls `GET /api/ride/telemetry` so the
 * status-summary panel tracks the live lifecycle, and posts operator
 * transition requests to `POST /api/ride/state`. Modelled on
 * `HttpQueueSource`.
 *
 * This is deliberately separate from `RideStateService`/
 * `RIDE_TELEMETRY_SOURCE`, which keep driving the client-simulated physics
 * telemetry (mill/hub speeds, gondolas, g-forces) — only the lifecycle state
 * is server-authoritative today.
 */
@Injectable({ providedIn: 'root' })
export class RideLifecycleService {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  private readonly stateSignal = signal<RideState>('idle');
  private readonly availableTransitionsSignal = signal<readonly RideState[]>([]);
  private readonly refreshTrigger = new Subject<void>();

  readonly state: Signal<RideState> = this.stateSignal.asReadonly();
  readonly availableTransitions: Signal<readonly RideState[]> =
    this.availableTransitionsSignal.asReadonly();

  constructor() {
    merge(interval(POLL_MS).pipe(startWith(0)), this.refreshTrigger)
      .pipe(
        switchMap(() => this.fetch()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  /** Trigger an immediate re-fetch of the lifecycle state. */
  refresh(): void {
    this.refreshTrigger.next();
  }

  /**
   * Request an operator transition to `state`. An illegal transition is
   * rejected by the backend (400) and tolerated here without throwing — the
   * following refresh simply reflects the ride's real, unchanged state.
   */
  requestTransition(state: RideState): void {
    this.http
      .post<unknown>(STATE_URL, { state: toRideStateName(state) })
      .pipe(
        catchError(() => of(null)),
        finalize(() => this.refresh()),
      )
      .subscribe();
  }

  private fetch(): Observable<void> {
    return this.http.get<RideTelemetryDto>(TELEMETRY_URL).pipe(
      tap((dto) => {
        this.stateSignal.set(toRideState(dto.state));
        this.availableTransitionsSignal.set(dto.availableTransitions.map(toRideState));
      }),
      map(() => undefined),
      catchError(() => of(undefined)),
    );
  }
}
