import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, firstValueFrom, interval, merge, of } from 'rxjs';
import { catchError, map, startWith, switchMap, tap } from 'rxjs/operators';

import {
  PrecipitationType,
  WeatherConditionDto,
  WeatherConditions,
  toWeatherConditions,
} from '../models/weather.models';
import { WeatherSource, WeatherStatus } from './weather-source';

/** Relative API base; the dev-server proxy forwards `/api` to the backend. */
const WEATHER_URL = '/api/weather';

/** How often to poll for the weather's autonomous drift, in milliseconds. */
const POLL_MS = 5000;

/**
 * {@link WeatherSource} backed by the backend weather API. Polls `GET /weather`
 * so the panel tracks the server's autonomous drift, exposes the result as
 * signals, and posts the precipitation/strong-wind disturbances — refreshing
 * eagerly after each so the change shows without waiting for the next poll.
 */
@Injectable({ providedIn: 'root' })
export class HttpWeatherSource implements WeatherSource {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  private readonly conditionsSignal = signal<WeatherConditions | null>(null);
  private readonly statusSignal = signal<WeatherStatus>('idle');
  private readonly refreshTrigger = new Subject<void>();

  readonly conditions = this.conditionsSignal.asReadonly();
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

  async startPrecipitation(type: PrecipitationType): Promise<void> {
    await firstValueFrom(this.http.post(`${WEATHER_URL}/precipitation`, { type }));
    this.refresh();
  }

  async startStrongWind(): Promise<void> {
    await firstValueFrom(this.http.post(`${WEATHER_URL}/strong-wind`, {}));
    this.refresh();
  }

  private fetch(): Observable<WeatherConditions | null> {
    if (this.statusSignal() === 'idle') {
      this.statusSignal.set('loading');
    }
    return this.http.get<WeatherConditionDto>(WEATHER_URL).pipe(
      map((dto) => toWeatherConditions(dto)),
      tap((conditions) => {
        this.conditionsSignal.set(conditions);
        this.statusSignal.set('ready');
      }),
      catchError(() => {
        this.statusSignal.set('error');
        return of(null);
      }),
    );
  }
}
