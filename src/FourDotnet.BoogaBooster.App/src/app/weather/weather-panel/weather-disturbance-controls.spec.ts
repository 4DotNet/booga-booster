import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WEATHER_SOURCE } from '../data/weather-source';
import { FakeWeatherSource } from '../testing/fake-weather-source';
import { WeatherDisturbanceControls } from './weather-disturbance-controls';

const flush = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

describe('WeatherDisturbanceControls', () => {
  let source: FakeWeatherSource;
  let fixture: ComponentFixture<WeatherDisturbanceControls>;

  function button(action: 'precipitation' | 'strong-wind'): HTMLButtonElement {
    return fixture.nativeElement.querySelector(
      `.disturb-btn[data-action="${action}"]`,
    ) as HTMLButtonElement;
  }

  beforeEach(() => {
    source = new FakeWeatherSource();
    TestBed.configureTestingModule({
      providers: [{ provide: WEATHER_SOURCE, useValue: source }],
    });
    fixture = TestBed.createComponent(WeatherDisturbanceControls);
    fixture.detectChanges();
  });

  it('starts precipitation (rain) when the precipitation button is clicked', () => {
    button('precipitation').click();
    expect(source.precipitationCalls).toEqual(['Rain']);
  });

  it('summons strong wind when the wind button is clicked', () => {
    button('strong-wind').click();
    expect(source.strongWindCalls).toBe(1);
  });

  it('disables and marks the button busy while pending, then restores it', async () => {
    source.hold = true;

    button('strong-wind').click();
    fixture.detectChanges();

    let btn = button('strong-wind');
    expect(btn.disabled).toBe(true);
    expect(btn.getAttribute('aria-busy')).toBe('true');

    source.release();
    await flush();
    fixture.detectChanges();

    btn = button('strong-wind');
    expect(btn.disabled).toBe(false);
    expect(btn.getAttribute('aria-busy')).toBe('false');
  });

  it('surfaces a failure message without breaking the panel', async () => {
    source.failNext = true;

    button('precipitation').click();
    await flush();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement | null;
    expect(alert?.textContent).toContain('Could not disturb the weather');
    expect(button('precipitation').disabled).toBe(false);
  });

  it('exposes accessible names on both buttons', () => {
    expect(button('precipitation').getAttribute('aria-label')).toBe('Start precipitation');
    expect(button('strong-wind').getAttribute('aria-label')).toBe('Summon strong wind');
  });
});
