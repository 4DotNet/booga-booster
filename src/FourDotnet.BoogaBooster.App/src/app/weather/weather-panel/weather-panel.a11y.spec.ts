import { TestBed } from '@angular/core/testing';
import axe from 'axe-core';

import { WEATHER_SOURCE } from '../data/weather-source';
import { FakeWeatherSource, createConditions } from '../testing/fake-weather-source';
import { WeatherPanel } from './weather-panel';

/**
 * Automated accessibility audit of the weather panel. Colour contrast needs real
 * layout/colour (unavailable in jsdom) and is checked against the design tokens
 * separately; every structural WCAG A/AA rule axe supports runs here.
 */
describe('WeatherPanel accessibility', () => {
  it('has no AXE violations', async () => {
    const source = new FakeWeatherSource();
    source.setStatus('ready');
    source.setConditions(
      createConditions({ regime: 'Precipitation', precipitation: 'Rain', niceWeather: 0.3 }),
    );
    TestBed.configureTestingModule({
      providers: [{ provide: WEATHER_SOURCE, useValue: source }],
    });

    const fixture = TestBed.createComponent(WeatherPanel);
    fixture.detectChanges();
    await fixture.whenStable();

    const results = await axe.run(fixture.nativeElement as HTMLElement, {
      resultTypes: ['violations'],
      rules: { 'color-contrast': { enabled: false } },
    });

    expect(results.violations).toEqual([]);
  });
});
