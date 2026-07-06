import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { QUEUE_SOURCE } from '../queue/data/queue-source';
import { createQueueStatus, FakeQueueSource } from '../queue/testing/fake-queue-source';
import { WEATHER_SOURCE } from '../weather/data/weather-source';
import { FakeWeatherSource, createConditions } from '../weather/testing/fake-weather-source';
import { RIDE_TELEMETRY_SOURCE } from './data/ride-telemetry-source';
import { RideTelemetryDto } from './models/ride.models';
import { RideDashboard } from './ride-dashboard';
import {
  createGondolas,
  createTelemetry,
  FakeRideTelemetrySource,
} from './testing/fake-ride-telemetry-source';

const TELEMETRY_URL = '/api/ride/telemetry';

/**
 * Automated accessibility audit. jsdom cannot compute layout or colour, so the
 * colour-contrast rule is exercised separately against the design tokens; every
 * structural WCAG A/AA rule axe supports runs here.
 */
describe('RideDashboard accessibility', () => {
  it('has no AXE violations', async () => {
    const source = new FakeRideTelemetrySource();
    source.setTelemetry(createTelemetry({ state: 'started', gondolas: createGondolas(10, true) }));
    const weather = new FakeWeatherSource();
    weather.setStatus('ready');
    weather.setConditions(createConditions());
    const queue = new FakeQueueSource();
    queue.setStatus('ready');
    queue.setQueue(createQueueStatus());
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withFetch()),
        provideHttpClientTesting(),
        { provide: RIDE_TELEMETRY_SOURCE, useValue: source },
        { provide: WEATHER_SOURCE, useValue: weather },
        { provide: QUEUE_SOURCE, useValue: queue },
      ],
    });

    const fixture = TestBed.createComponent(RideDashboard);

    const http = TestBed.inject(HttpTestingController);
    const dto: RideTelemetryDto = {
      state: 'Started',
      availableTransitions: ['Stopping', 'EmergencyStop'],
    };
    http.expectOne(TELEMETRY_URL).flush(dto);

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
