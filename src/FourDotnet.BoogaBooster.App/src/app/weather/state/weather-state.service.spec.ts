import { TestBed } from '@angular/core/testing';

import { WEATHER_SOURCE } from '../data/weather-source';
import { FakeWeatherSource, createConditions } from '../testing/fake-weather-source';
import { WeatherStateService } from './weather-state.service';

describe('WeatherStateService', () => {
  let source: FakeWeatherSource;
  let service: WeatherStateService;

  beforeEach(() => {
    source = new FakeWeatherSource();
    TestBed.configureTestingModule({
      providers: [{ provide: WEATHER_SOURCE, useValue: source }],
    });
    service = TestBed.inject(WeatherStateService);
  });

  it('derives niceness, labels and scene from the conditions', () => {
    source.setConditions(
      createConditions({
        temperatureCelsius: 12,
        windBeaufort: 4,
        precipitation: 'Snow',
        regime: 'Precipitation',
        niceWeather: 0.2,
      }),
    );

    expect(service.nicenessPercent()).toBe(20);
    expect(service.nicenessLabel()).toBe('Severe');
    expect(service.regimeLabel()).toBe('Precipitation');
    expect(service.precipitationLabel()).toBe('Snow');
    expect(service.windDescription()).toBe('Moderate breeze');
    expect(service.scene().kind).toBe('snow');
  });

  it('reports niceness as 0 with no conditions', () => {
    expect(service.nicenessPercent()).toBe(0);
    expect(service.isReady()).toBe(false);
  });

  it('records a precipitation disturbance with the default type', async () => {
    await service.startPrecipitation();

    expect(source.precipitationCalls).toEqual(['Rain']);
    expect(service.precipitationPending()).toBe(false);
    expect(service.errorMessage()).toBeNull();
  });

  it('records a strong-wind disturbance', async () => {
    await service.startStrongWind();

    expect(source.strongWindCalls).toBe(1);
    expect(service.strongWindPending()).toBe(false);
  });

  it('surfaces an error message when a disturbance fails', async () => {
    source.failNext = true;

    await service.startPrecipitation();

    expect(service.errorMessage()).not.toBeNull();
    expect(service.precipitationPending()).toBe(false);
  });

  it('marks pending while a disturbance is in flight', async () => {
    source.hold = true;

    const done = service.startStrongWind();
    expect(service.strongWindPending()).toBe(true);

    source.release();
    await done;
    expect(service.strongWindPending()).toBe(false);
  });
});
