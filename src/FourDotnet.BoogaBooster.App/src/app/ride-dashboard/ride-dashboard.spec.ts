import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from '../app.routes';
import { QUEUE_SOURCE } from '../queue/data/queue-source';
import { FakeQueueSource, createQueueStatus } from '../queue/testing/fake-queue-source';
import { WEATHER_SOURCE } from '../weather/data/weather-source';
import { FakeWeatherSource } from '../weather/testing/fake-weather-source';
import { RIDE_TELEMETRY_SOURCE } from './data/ride-telemetry-source';
import { RideTelemetryDto } from './models/ride.models';
import { RideDashboard } from './ride-dashboard';
import {
  createGondolas,
  createTelemetry,
  FakeRideTelemetrySource,
} from './testing/fake-ride-telemetry-source';

const TELEMETRY_URL = '/api/ride/telemetry';

describe('RideDashboard', () => {
  let source: FakeRideTelemetrySource;
  let queue: FakeQueueSource;
  let http: HttpTestingController;

  beforeEach(() => {
    source = new FakeRideTelemetrySource();
    queue = new FakeQueueSource();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(withFetch()),
        provideHttpClientTesting(),
        { provide: RIDE_TELEMETRY_SOURCE, useValue: source },
        { provide: WEATHER_SOURCE, useValue: new FakeWeatherSource() },
        { provide: QUEUE_SOURCE, useValue: queue },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  it('renders the dashboard on the default route', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/', RideDashboard);
    http
      .expectOne(TELEMETRY_URL)
      .flush({ state: 'Idle', availableTransitions: ['Loading'] } satisfies RideTelemetryDto);

    expect(component).toBeInstanceOf(RideDashboard);
    expect(harness.routeNativeElement?.querySelector('bb-ride-visualization')).not.toBeNull();
  });

  it('redirects an unknown route to the dashboard', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/does-not-exist', RideDashboard);
    http
      .expectOne(TELEMETRY_URL)
      .flush({ state: 'Idle', availableTransitions: ['Loading'] } satisfies RideTelemetryDto);

    expect(component).toBeInstanceOf(RideDashboard);
  });

  it('places Rider Experience last in the right rail, under Gondolas, bound to live averages', () => {
    queue.setStatus('ready');
    queue.setQueue(createQueueStatus({ averageHappiness: 72 }));
    source.setTelemetry(
      createTelemetry({
        riderExperience: {
          riderCount: 2,
          averageHappiness: 64,
          averagePreferredRideIntensity: 81,
          averageNausea: 12,
        },
      }),
    );
    const fixture = TestBed.createComponent(RideDashboard);
    http.expectOne(TELEMETRY_URL).flush({
      state: 'Idle',
      availableTransitions: [],
    } satisfies RideTelemetryDto);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;

    const panels = Array.from(host.querySelectorAll('.rail-right > .panel > *')).map((el) =>
      el.tagName.toLowerCase(),
    );
    expect(panels.slice(-2)).toEqual(['bb-gondola-panel', 'bb-rider-experience-panel']);

    const values = Array.from(
      host.querySelectorAll('bb-rider-experience-panel [role="progressbar"]:not([aria-hidden])'),
    ).map((bar) => bar.getAttribute('aria-valuenow'));
    expect(values).toEqual(['72', '64', '81', '12']);

    source.setTelemetry(
      createTelemetry({
        riderExperience: {
          riderCount: 2,
          averageHappiness: 30,
          averagePreferredRideIntensity: 81,
          averageNausea: 12,
        },
      }),
    );
    fixture.detectChanges();
    expect(
      host
        .querySelectorAll('bb-rider-experience-panel [role="progressbar"]:not([aria-hidden])')[1]
        .getAttribute('aria-valuenow'),
    ).toBe('30');
  });

  it('summary reflects lifecycle state, occupancy and security', () => {
    source.setTelemetry(createTelemetry({ gondolas: createGondolas(10) }));
    const fixture = TestBed.createComponent(RideDashboard);
    http.expectOne(TELEMETRY_URL).flush({
      state: 'Started',
      availableTransitions: ['Stopping', 'EmergencyStop'],
    } satisfies RideTelemetryDto);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Started');
    expect(text).toContain('20');
    expect(text).toContain('Secured');
  });
});
