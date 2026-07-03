import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from '../app.routes';
import { WEATHER_SOURCE } from '../weather/data/weather-source';
import { FakeWeatherSource } from '../weather/testing/fake-weather-source';
import { RIDE_TELEMETRY_SOURCE } from './data/ride-telemetry-source';
import { RideDashboard } from './ride-dashboard';
import {
  createGondolas,
  createTelemetry,
  FakeRideTelemetrySource,
} from './testing/fake-ride-telemetry-source';

describe('RideDashboard', () => {
  let source: FakeRideTelemetrySource;

  beforeEach(() => {
    source = new FakeRideTelemetrySource();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: RIDE_TELEMETRY_SOURCE, useValue: source },
        { provide: WEATHER_SOURCE, useValue: new FakeWeatherSource() },
      ],
    });
  });

  it('renders the dashboard on the default route', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/', RideDashboard);

    expect(component).toBeInstanceOf(RideDashboard);
    expect(harness.routeNativeElement?.querySelector('bb-ride-visualization')).not.toBeNull();
  });

  it('redirects an unknown route to the dashboard', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/does-not-exist', RideDashboard);

    expect(component).toBeInstanceOf(RideDashboard);
  });

  it('summary reflects ride state, occupancy and security', () => {
    source.setTelemetry(
      createTelemetry({ state: 'running', gondolas: createGondolas(10) }),
    );
    const fixture = TestBed.createComponent(RideDashboard);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('running');
    expect(text).toContain('40');
    expect(text).toContain('Secured');
  });
});
