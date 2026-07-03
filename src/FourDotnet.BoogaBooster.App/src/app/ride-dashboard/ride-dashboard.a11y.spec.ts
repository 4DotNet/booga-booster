import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { WEATHER_SOURCE } from '../weather/data/weather-source';
import { FakeWeatherSource, createConditions } from '../weather/testing/fake-weather-source';
import { RIDE_TELEMETRY_SOURCE } from './data/ride-telemetry-source';
import { RideDashboard } from './ride-dashboard';
import {
  createGondolas,
  createTelemetry,
  FakeRideTelemetrySource,
} from './testing/fake-ride-telemetry-source';

/**
 * Automated accessibility audit. jsdom cannot compute layout or colour, so the
 * colour-contrast rule is exercised separately against the design tokens; every
 * structural WCAG A/AA rule axe supports runs here.
 */
describe('RideDashboard accessibility', () => {
  it('has no AXE violations', async () => {
    const source = new FakeRideTelemetrySource();
    source.setTelemetry(
      createTelemetry({ state: 'running', gondolas: createGondolas(10, true) }),
    );
    const weather = new FakeWeatherSource();
    weather.setStatus('ready');
    weather.setConditions(createConditions());
    TestBed.configureTestingModule({
      providers: [
        { provide: RIDE_TELEMETRY_SOURCE, useValue: source },
        { provide: WEATHER_SOURCE, useValue: weather },
      ],
    });

    const fixture = TestBed.createComponent(RideDashboard);
    fixture.detectChanges();
    await fixture.whenStable();

    const results = await axe.run(fixture.nativeElement as HTMLElement, {
      resultTypes: ['violations'],
      // Contrast needs real layout/colour, unavailable in jsdom.
      rules: { 'color-contrast': { enabled: false } },
    });

    expect(results.violations).toEqual([]);
  });
});
