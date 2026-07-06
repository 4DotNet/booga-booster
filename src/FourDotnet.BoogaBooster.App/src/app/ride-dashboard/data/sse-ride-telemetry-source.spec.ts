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

/**
 * A representative telemetry stream DTO: ride started, one hub/gondola
 * non-zero, and one occupied+secured seat per gondola (16 boarded total).
 */
function runningDto(overrides: Partial<RideTelemetryStreamDto> = {}): RideTelemetryStreamDto {
  return {
    state: 3, // Started
    availableTransitions: [4, 6],
    isSafeToStart: false,
    safetyReason: 0,
    simulationTimeSeconds: 12,
    mill: {
      powerWatts: 45000,
      rpm: 5,
      direction: 0,
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
      direction: 0,
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
        { position: 0, occupiedKg: 70, restraint: 2, isOccupied: true, isSecured: true },
        { position: 1, occupiedKg: 0, restraint: 0, isOccupied: false, isSecured: false },
      ],
    })),
    boardedPassengerCount: GONDOLA_COUNT,
    ...overrides,
  };
}

/**
 * A `Loading`-state telemetry stream DTO with every seat empty and no
 * passengers boarded yet; the backend now streams frames during boarding
 * too, so tests override individual gondolas/seats to simulate passengers
 * arriving and securing their restraints.
 */
function loadingDto(overrides: Partial<RideTelemetryStreamDto> = {}): RideTelemetryStreamDto {
  return {
    state: 1, // Loading
    availableTransitions: [2],
    isSafeToStart: false,
    safetyReason: 0,
    simulationTimeSeconds: 3,
    mill: {
      powerWatts: 0,
      rpm: 0,
      direction: 0,
      loadKg: 0,
      passengerLoadKg: 0,
      imbalanceMillimeters: 0,
      isBalanced: true,
      isOverloaded: false,
    },
    hubs: Array.from({ length: HUB_COUNT }, (_, i) => ({
      index: i,
      powerWatts: 0,
      rpm: 0,
      direction: 0,
      loadKg: 0,
    })),
    gondolas: Array.from({ length: GONDOLA_COUNT }, (_, g) => ({
      hubIndex: Math.floor(g / GONDOLAS_PER_HUB),
      index: g % GONDOLAS_PER_HUB,
      brake: 0, // Engaged
      angleDegrees: 0,
      rpm: 0,
      lateralG: 0,
      forwardG: 0,
      loadKg: 0,
      isSafeToDispatch: false,
      seats: [
        { position: 0, occupiedKg: 0, restraint: 0, isOccupied: false, isSecured: false },
        { position: 1, occupiedKg: 0, restraint: 0, isOccupied: false, isSecured: false },
      ],
    })),
    boardedPassengerCount: 0,
    ...overrides,
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

  it('set-mill-direction and set-hub-direction post the direction and optimistically echo it', () => {
    const source = TestBed.inject(SseRideTelemetrySource);

    source.applyCommand({ kind: 'set-mill-direction', direction: 'reverse' });
    source.applyCommand({ kind: 'set-hub-direction', direction: 'reverse' });

    expect(source.telemetry().mill.direction).toBe('reverse');
    expect(source.telemetry().hubs.every((hub) => hub.direction === 'reverse')).toBe(true);

    http.expectOne('/api/ride/main-direction').flush(null, { status: 202, statusText: 'Accepted' });
    http.expectOne('/api/ride/hub-direction').flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('brake-engines(true) posts { engaged: true } and optimistically zeroes mill/hub power', () => {
    const source = TestBed.inject(SseRideTelemetrySource);
    fakeEventSource.emit('message', runningDto());
    expect(source.telemetry().mill.power).toBe(50);

    source.applyCommand({ kind: 'brake-engines', engaged: true });

    const req = http.expectOne('/api/ride/engine-brake');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ engaged: true });
    req.flush(null, { status: 202, statusText: 'Accepted' });

    expect(source.telemetry().mill.power).toBe(0);
    expect(source.telemetry().hubs.every((hub) => hub.power === 0)).toBe(true);
    expect(source.telemetry().brakesEngaged).toBe(true);
  });

  it('brake-engines(false) posts { engaged: false } and leaves power at zero without restoring it', () => {
    const source = TestBed.inject(SseRideTelemetrySource);
    fakeEventSource.emit('message', runningDto());

    source.applyCommand({ kind: 'brake-engines', engaged: true });
    http.expectOne('/api/ride/engine-brake').flush(null, { status: 202, statusText: 'Accepted' });

    source.applyCommand({ kind: 'brake-engines', engaged: false });

    const req = http.expectOne('/api/ride/engine-brake');
    expect(req.request.body).toEqual({ engaged: false });
    req.flush(null, { status: 202, statusText: 'Accepted' });

    expect(source.telemetry().brakesEngaged).toBe(false);
    // Releasing does not restore the pre-brake power.
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

  describe('boarding during Loading/Safe/Offloading', () => {
    it('maps an occupied, not-yet-secured seat to occupied-unsecured', () => {
      const source = TestBed.inject(SseRideTelemetrySource);
      const dto = loadingDto();
      const gondolas = dto.gondolas.map((gondola, i) =>
        i === 0
          ? {
              ...gondola,
              seats: [
                { position: 0, occupiedKg: 68, restraint: 1, isOccupied: true, isSecured: false },
                gondola.seats[1],
              ],
            }
          : gondola,
      );

      fakeEventSource.emit('ride-telemetry', { ...dto, gondolas, boardedPassengerCount: 1 });

      expect(source.telemetry().gondolas[0].seats[0].state).toBe('occupied-unsecured');
    });

    it('maps an occupied and secured seat to secured', () => {
      const source = TestBed.inject(SseRideTelemetrySource);
      const dto = loadingDto();
      const gondolas = dto.gondolas.map((gondola, i) =>
        i === 0
          ? {
              ...gondola,
              seats: [
                { position: 0, occupiedKg: 68, restraint: 2, isOccupied: true, isSecured: true },
                gondola.seats[1],
              ],
            }
          : gondola,
      );

      fakeEventSource.emit('ride-telemetry', { ...dto, gondolas, boardedPassengerCount: 1 });

      expect(source.telemetry().gondolas[0].seats[0].state).toBe('secured');
    });

    it('carries the frame boarded-passenger count onto the mapped telemetry', () => {
      const source = TestBed.inject(SseRideTelemetrySource);

      fakeEventSource.emit('ride-telemetry', loadingDto({ boardedPassengerCount: 9 }));

      expect(source.telemetry().boardedPassengerCount).toBe(9);
    });

    it('updates the boarded count and total weight across two successive loading frames', () => {
      const source = TestBed.inject(SseRideTelemetrySource);

      // Frame 1: one passenger has boarded but not yet secured the restraint.
      const firstDto = loadingDto();
      const firstGondolas = firstDto.gondolas.map((gondola, i) =>
        i === 0
          ? {
              ...gondola,
              seats: [
                { position: 0, occupiedKg: 68, restraint: 0, isOccupied: true, isSecured: false },
                gondola.seats[1],
              ],
            }
          : gondola,
      );
      fakeEventSource.emit('ride-telemetry', {
        ...firstDto,
        gondolas: firstGondolas,
        boardedPassengerCount: 1,
      });

      expect(source.telemetry().boardedPassengerCount).toBe(1);
      expect(source.telemetry().gondolas[0].seats[0].occupiedKg).toBe(68);

      // Frame 2: a second passenger boards a different gondola and secures.
      const secondGondolas = firstGondolas.map((gondola, i) =>
        i === 1
          ? {
              ...gondola,
              seats: [
                { position: 0, occupiedKg: 75, restraint: 2, isOccupied: true, isSecured: true },
                gondola.seats[1],
              ],
            }
          : gondola,
      );
      fakeEventSource.emit('ride-telemetry', {
        ...firstDto,
        gondolas: secondGondolas,
        boardedPassengerCount: 2,
      });

      const telemetry = source.telemetry();
      expect(telemetry.boardedPassengerCount).toBe(2);
      expect(telemetry.gondolas[1].seats[0].state).toBe('secured');
      const totalKg = telemetry.gondolas.reduce(
        (total, gondola) =>
          total + gondola.seats.reduce((seatTotal, seat) => seatTotal + seat.occupiedKg, 0),
        0,
      );
      expect(totalKg).toBe(143);
    });
  });
});
