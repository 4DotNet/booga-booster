import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import {
  GONDOLA_COUNT,
  GONDOLAS_PER_HUB,
  HUB_COUNT,
  RideTelemetryStreamDto,
} from '../models/ride.models';
import {
  EVENT_SOURCE_FACTORY,
  EventSourceLike,
  SseRideTelemetrySource,
  atRestTelemetry,
} from './sse-ride-telemetry-source';

/** Deterministic {@link EventSourceLike} fake: dispatches events synchronously on demand. */
class FakeEventSource implements EventSourceLike {
  onmessage: ((event: MessageEvent) => void) | null = null;
  closed = false;

  private readonly listeners = new Map<string, Array<(event: MessageEvent) => void>>();

  addEventListener(type: string, listener: (event: MessageEvent) => void): void {
    const existing = this.listeners.get(type) ?? [];
    existing.push(listener);
    this.listeners.set(type, existing);
  }

  close(): void {
    this.closed = true;
  }

  /** Dispatch a named event (or the default `'message'` event) with a JSON payload. */
  emit(type: string, payload: unknown): void {
    const event = { data: JSON.stringify(payload) } as MessageEvent;
    if (type === 'message') {
      this.onmessage?.(event);
    }
    this.listeners.get(type)?.forEach((listener) => listener(event));
  }
}

/** A representative telemetry stream DTO: ride started, one hub/gondola non-zero. */
function runningDto(): RideTelemetryStreamDto {
  return {
    state: 3, // Started
    availableTransitions: [4, 6],
    isSafeToStart: false,
    safetyReason: 0,
    simulationTimeSeconds: 12,
    mill: {
      powerWatts: 45000,
      rpm: 5,
      loadKg: 0,
      passengerLoadKg: 0,
      imbalanceMillimeters: 0,
      isBalanced: true,
      isOverloaded: false,
    },
    hubs: Array.from({ length: HUB_COUNT }, (_, i) => ({
      index: i,
      powerWatts: 7500,
      rpm: 10,
      loadKg: 0,
    })),
    gondolas: Array.from({ length: GONDOLA_COUNT }, (_, g) => ({
      hubIndex: Math.floor(g / GONDOLAS_PER_HUB),
      index: g % GONDOLAS_PER_HUB,
      brake: 1, // Released
      angleDegrees: 0,
      rpm: 10,
      lateralG: 0.5,
      forwardG: 1,
      loadKg: 0,
      isSafeToDispatch: true,
      seats: [
        { position: 0, occupiedKg: 70, restraint: 2 },
        { position: 1, occupiedKg: 0, restraint: 0 },
      ],
    })),
  };
}

describe('SseRideTelemetrySource', () => {
  let fakeEventSource: FakeEventSource;
  let http: HttpTestingController;

  beforeEach(() => {
    fakeEventSource = new FakeEventSource();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withFetch()),
        provideHttpClientTesting(),
        { provide: EVENT_SOURCE_FACTORY, useValue: () => fakeEventSource },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('seeds telemetry with the at-rest snapshot before any frame arrives', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    expect(source.telemetry()).toEqual(atRestTelemetry());
  });

  it('maps a default "message" frame onto the telemetry signal', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    fakeEventSource.emit('message', runningDto());

    expect(source.telemetry().state).toBe('started');
    expect(source.telemetry().mill.power).toBe(50);
  });

  it('maps a named "ride-telemetry" frame onto the telemetry signal', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    fakeEventSource.emit('ride-telemetry', runningDto());

    expect(source.telemetry().state).toBe('started');
    expect(source.telemetry().hubs[0].power).toBe(50);
  });

  it('closes the EventSource when the injector is destroyed', () => {
    TestBed.inject(SseRideTelemetrySource);
    expect(fakeEventSource.closed).toBe(false);

    TestBed.resetTestingModule();

    expect(fakeEventSource.closed).toBe(true);
  });

  it('set-mill-power posts the percent and optimistically echoes it', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    source.applyCommand({ kind: 'set-mill-power', value: 60 });

    const req = http.expectOne('/api/ride/main-power');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ percent: 60 });
    req.flush(null, { status: 202, statusText: 'Accepted' });

    expect(source.telemetry().mill.power).toBe(60);
  });

  it('set-hub-power posts the percent and optimistically echoes it on every hub', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    source.applyCommand({ kind: 'set-hub-power', value: 40 });

    const req = http.expectOne('/api/ride/hub-power');
    expect(req.request.body).toEqual({ percent: 40 });
    req.flush(null, { status: 202, statusText: 'Accepted' });

    expect(source.telemetry().hubs.every((hub) => hub.power === 40)).toBe(true);
  });

  it('set-gondola-brake posts a per-gondola brake request for all 4x4 gondolas', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    source.applyCommand({ kind: 'set-gondola-brake', engaged: false });

    const requests = http.match('/api/ride/brake');
    expect(requests).toHaveLength(16);
    requests.forEach((req) => {
      expect(req.request.body).toEqual({
        hubIndex: req.request.body.hubIndex,
        gondolaIndex: req.request.body.gondolaIndex,
        brake: 'Released',
      });
      req.flush(null, { status: 202, statusText: 'Accepted' });
    });

    expect(source.telemetry().gondolaBrakeEngaged).toBe(false);
  });

  it('set-mill-direction and set-hub-direction make no HTTP call but echo locally', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    source.applyCommand({ kind: 'set-mill-direction', direction: 'reverse' });
    source.applyCommand({ kind: 'set-hub-direction', direction: 'reverse' });

    expect(source.telemetry().mill.direction).toBe('reverse');
    expect(source.telemetry().hubs.every((hub) => hub.direction === 'reverse')).toBe(true);

    http.verify(); // no requests issued for either command
  });

  it('brake-engines posts to the engine-brake endpoint and optimistically zeroes mill/hub power', () => {
    const source = TestBed.inject(SseRideTelemetrySource);
    fakeEventSource.emit('message', runningDto());
    expect(source.telemetry().mill.power).toBe(50);

    source.applyCommand({ kind: 'brake-engines' });

    const req = http.expectOne('/api/ride/engine-brake');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null, { status: 202, statusText: 'Accepted' });

    expect(source.telemetry().mill.power).toBe(0);
    expect(source.telemetry().hubs.every((hub) => hub.power === 0)).toBe(true);
  });

  it('request-state-transition posts the backend PascalCase state name', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    source.applyCommand({ kind: 'request-state-transition', state: 'loading' });

    const req = http.expectOne('/api/ride/state');
    expect(req.request.body).toEqual({ state: 'Loading' });
    req.flush(null, { status: 202, statusText: 'Accepted' });
  });
});
