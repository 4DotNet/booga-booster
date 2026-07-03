import { ComponentRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WeatherStatus } from '../data/weather-source';
import { WeatherConditions } from '../models/weather.models';
import { createConditions } from '../testing/fake-weather-source';
import { WeatherConditionsView } from './weather-conditions-view';

describe('WeatherConditionsView', () => {
  let fixture: ComponentFixture<WeatherConditionsView>;
  let ref: ComponentRef<WeatherConditionsView>;

  function render(conditions: WeatherConditions | null, status: WeatherStatus): HTMLElement {
    fixture = TestBed.createComponent(WeatherConditionsView);
    ref = fixture.componentRef;
    ref.setInput('conditions', conditions);
    ref.setInput('status', status);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the readouts for the current conditions', () => {
    const el = render(
      createConditions({ temperatureCelsius: 20, windBeaufort: 2, sunshinePercent: 70 }),
      'ready',
    );

    const text = el.textContent ?? '';
    expect(text).toContain('20 °C');
    expect(text).toContain('2 bft');
    expect(text).toContain('70%');
    expect(el.querySelector('[role="meter"]')?.getAttribute('aria-valuenow')).toBe('100');
  });

  it('marks the animated scene decorative', () => {
    const el = render(createConditions({ regime: 'Precipitation', precipitation: 'Rain' }), 'ready');

    const scene = el.querySelector('.scene');
    expect(scene?.getAttribute('aria-hidden')).toBe('true');
    expect(scene?.getAttribute('data-kind')).toBe('rain');
    expect(el.querySelector('.particles[data-kind="rain"]')).not.toBeNull();
  });

  it('reflects updated conditions', () => {
    render(createConditions({ niceWeather: 1 }), 'ready');
    expect(fixture.nativeElement.querySelector('[role="meter"]').getAttribute('aria-valuenow')).toBe(
      '100',
    );

    ref.setInput('conditions', createConditions({ niceWeather: 0.2, regime: 'StrongWind' }));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="meter"]')?.getAttribute('aria-valuenow')).toBe('20');
    expect(el.querySelector('.scene')?.getAttribute('data-kind')).toBe('wind');
  });

  it('shows an unavailable state on error', () => {
    const el = render(null, 'error');

    expect(el.querySelector('.unavailable')?.textContent).toContain('unavailable');
    expect(el.querySelector('.scene')).toBeNull();
  });

  it('shows a loading state before the first load', () => {
    const el = render(null, 'loading');

    expect(el.querySelector('.unavailable')?.textContent).toContain('Loading');
  });
});
