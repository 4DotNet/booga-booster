import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { RideTelemetryDto } from '../models/ride.models';
import { RideLifecycleService } from './ride-lifecycle.service';

const TELEMETRY_URL = '/api/ride/telemetry';
const STATE_URL = '/api/ride/state';

const POLL_MS = 1000;

describe('RideLifecycleService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('projects the polled telemetry onto state and availableTransitions signals', () => {
    const service = TestBed.inject(RideLifecycleService);

    const dto: RideTelemetryDto = {
      state: 'Started',
      availableTransitions: ['Stopping', 'EmergencyStop'],
    };
    http.expectOne(TELEMETRY_URL).flush(dto);

    expect(service.state()).toBe('started');
    expect(service.availableTransitions()).toEqual(['stopping', 'emergency-stop']);
  });

  it('maps numeric enum indices for state and availableTransitions', () => {
    const service = TestBed.inject(RideLifecycleService);

    const dto: RideTelemetryDto = {
      state: 2, // Safe
      availableTransitions: [3, 1, 6], // Started, Loading, EmergencyStop
    };
    http.expectOne(TELEMETRY_URL).flush(dto);

    expect(service.state()).toBe('safe');
    expect(service.availableTransitions()).toEqual(['started', 'loading', 'emergency-stop']);
  });

  it('seeds state as idle and availableTransitions as empty before the first response', () => {
    const service = TestBed.inject(RideLifecycleService);

    expect(service.state()).toBe('idle');
    expect(service.availableTransitions()).toEqual([]);

    http.expectOne(TELEMETRY_URL).flush({ state: 'Idle', availableTransitions: [] });
  });

  it('re-polls the telemetry endpoint on the polling interval', () => {
    TestBed.inject(RideLifecycleService);
    http.expectOne(TELEMETRY_URL).flush({ state: 'Idle', availableTransitions: ['Loading'] });

    vi.advanceTimersByTime(POLL_MS);
    http.expectOne(TELEMETRY_URL).flush({ state: 'Loading', availableTransitions: ['Safe'] });
  });

  it('requestTransition posts the mapped PascalCase state name and then refreshes', () => {
    const service = TestBed.inject(RideLifecycleService);
    http.expectOne(TELEMETRY_URL).flush({ state: 'Safe', availableTransitions: ['Started'] }); // init

    service.requestTransition('started');

    const postReq = http.expectOne(STATE_URL);
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual({ state: 'Started' });
    postReq.flush({});

    // requestTransition triggers an immediate refresh after the POST resolves.
    http
      .expectOne(TELEMETRY_URL)
      .flush({ state: 'Started', availableTransitions: ['Stopping', 'EmergencyStop'] });
    expect(service.state()).toBe('started');
  });

  it('tolerates a failed (illegal) transition POST without throwing, and still refreshes', () => {
    const service = TestBed.inject(RideLifecycleService);
    http.expectOne(TELEMETRY_URL).flush({ state: 'Idle', availableTransitions: ['Loading'] }); // init

    expect(() => service.requestTransition('started')).not.toThrow();

    const postReq = http.expectOne(STATE_URL);
    postReq.error(new ProgressEvent('error'), { status: 400 });

    // The next refresh simply reflects the ride's real, unchanged state.
    http.expectOne(TELEMETRY_URL).flush({ state: 'Idle', availableTransitions: ['Loading'] });
    expect(service.state()).toBe('idle');
  });
});
