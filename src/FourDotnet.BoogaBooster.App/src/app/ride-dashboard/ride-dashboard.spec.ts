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
  let http: HttpTestingController;

  beforeEach(() => {
    source = new FakeRideTelemetrySource();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(withFetch()),
        provideHttpClientTesting(),
        { provide: RIDE_TELEMETRY_SOURCE, useValue: source },
        { provide: WEATHER_SOURCE, useValue: new FakeWeatherSource() },
        { provide: QUEUE_SOURCE, useValue: new FakeQueueSource() },
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

  it('places the rider experience panel directly after the gondola panel', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/', RideDashboard);
    http
      .expectOne(TELEMETRY_URL)
      .flush({ state: 'Idle', availableTransitions: ['Loading'] } satisfies RideTelemetryDto);

    const rail = harness.routeNativeElement?.querySelector('.rail-right');
    const panels = Array.from(rail?.children ?? []).map((panel) =>
      panel.firstElementChild?.tagName.toLowerCase(),
    );

    expect(panels).toEqual([
      'bb-security-panel',
      'bb-speed-panel',
      'bb-gondola-panel',
      'bb-rider-experience-panel',
    ]);
  });

  it('feeds the rider experience panel from ride telemetry and the queue', async () => {
    const queueSource = TestBed.inject(QUEUE_SOURCE) as FakeQueueSource;
    queueSource.setStatus('ready');
    queueSource.setQueue(createQueueStatus({ averageHappiness: 72 }));
    source.setTelemetry(
      createTelemetry({
        riderExperience: {
          riderCount: 2,
          averageHappiness: 81,
          averagePreferredIntensity: 68,
          averageNausea: 12,
        },
      }),
    );
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/', RideDashboard);
    http
      .expectOne(TELEMETRY_URL)
      .flush({ state: 'Idle', availableTransitions: ['Loading'] } satisfies RideTelemetryDto);
    harness.detectChanges();

    const labels = Array.from(
      harness.routeNativeElement?.querySelectorAll(
        'bb-rider-experience-panel [role="progressbar"]',
      ) ?? [],
    ).map((bar) => bar.getAttribute('aria-label'));

    expect(labels).toEqual([
      'Queue happiness 72%',
      'Rider happiness 81%',
      'Rider intensity 68%',
      'Rider nausea 12%',
    ]);
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
