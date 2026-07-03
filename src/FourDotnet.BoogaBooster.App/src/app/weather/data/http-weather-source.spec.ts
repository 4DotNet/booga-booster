import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { WeatherConditionDto } from '../models/weather.models';
import { HttpWeatherSource } from './http-weather-source';

const DTO: WeatherConditionDto = {
  temperatureCelsius: 20,
  windBeaufort: 2,
  sunshinePercent: 70,
  precipitation: 0,
  regime: 0,
  niceWeather: 1,
};

const POLL_MS = 5000;
const flush = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

describe('HttpWeatherSource', () => {
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

  it('loads the current conditions on init and sets status ready', () => {
    const source = TestBed.inject(HttpWeatherSource);

    http.expectOne('/api/weather').flush(DTO);

    expect(source.conditions()?.temperatureCelsius).toBe(20);
    expect(source.conditions()?.precipitation).toBe('None');
    expect(source.status()).toBe('ready');
  });

  it('re-fetches on the polling interval', () => {
    TestBed.inject(HttpWeatherSource);
    http.expectOne('/api/weather').flush(DTO); // init

    vi.advanceTimersByTime(POLL_MS);
    http.expectOne('/api/weather').flush(DTO); // polling re-fetch
  });

  it('sets status error when the fetch fails, then recovers on the next poll', () => {
    const source = TestBed.inject(HttpWeatherSource);

    http.expectOne('/api/weather').error(new ProgressEvent('error'));
    expect(source.status()).toBe('error');

    vi.advanceTimersByTime(POLL_MS);
    http.expectOne('/api/weather').flush(DTO);
    expect(source.status()).toBe('ready');
  });

  it('posts precipitation with the type and refreshes', async () => {
    const source = TestBed.inject(HttpWeatherSource);
    http.expectOne('/api/weather').flush(DTO); // init

    const done = source.startPrecipitation('Rain');
    const post = http.expectOne('/api/weather/precipitation');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ type: 'Rain' });
    post.flush(null, { status: 202, statusText: 'Accepted' });
    await done;

    http.expectOne('/api/weather').flush(DTO); // eager refresh
  });

  it('posts strong wind and refreshes', async () => {
    const source = TestBed.inject(HttpWeatherSource);
    http.expectOne('/api/weather').flush(DTO); // init

    const done = source.startStrongWind();
    const post = http.expectOne('/api/weather/strong-wind');
    expect(post.request.method).toBe('POST');
    post.flush(null, { status: 202, statusText: 'Accepted' });
    await done;

    http.expectOne('/api/weather').flush(DTO); // eager refresh
  });

  it('stops polling once the injector is destroyed', () => {
    TestBed.inject(HttpWeatherSource);
    http.expectOne('/api/weather').flush(DTO);
    expect(vi.getTimerCount()).toBeGreaterThan(0);

    TestBed.resetTestingModule(); // destroys the injector → takeUntilDestroyed unsubscribes

    expect(vi.getTimerCount()).toBe(0);
  });
});
