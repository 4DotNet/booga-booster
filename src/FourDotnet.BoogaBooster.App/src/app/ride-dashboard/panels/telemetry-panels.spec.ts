import { TestBed } from '@angular/core/testing';

import { Gondola, HUB_COUNT, Hub } from '../models/ride.models';
import { createGondolas } from '../testing/fake-ride-telemetry-source';
import { GondolaPanel } from './gondola-panel/gondola-panel';
import { SecurityPanel } from './security-panel/security-panel';
import { SpeedPanel } from './speed-panel/speed-panel';

describe('SecurityPanel', () => {
  it('shows occupied total and distinguishes seat states', () => {
    const gondolas = createGondolas(10, true);
    const fixture = TestBed.createComponent(SecurityPanel);
    fixture.componentRef.setInput('gondolas', gondolas);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('40 occupied seats');
    expect(host.querySelector('.seat[data-state="secured"]')).not.toBeNull();
    expect(host.querySelector('.seat[data-state="occupied-unsecured"]')).not.toBeNull();
    expect(host.querySelector('.seat[data-state="empty"]')).not.toBeNull();
  });
});

describe('SpeedPanel', () => {
  it('shows the mill speed and each hub speed independently with units', () => {
    const hubs: Hub[] = Array.from({ length: HUB_COUNT }, (_, i) => ({
      id: i + 1,
      power: 50,
      direction: 'forward',
      speedRpm: (i + 1) * 3,
    }));
    const fixture = TestBed.createComponent(SpeedPanel);
    fixture.componentRef.setInput('mill', {
      power: 60,
      direction: 'forward',
      speedRpm: 4.5,
    });
    fixture.componentRef.setInput('hubs', hubs);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('4.5 rpm');
    expect(host.querySelectorAll('.hub').length).toBe(HUB_COUNT);
    expect(host.textContent).toContain('12 rpm');
  });
});

describe('GondolaPanel', () => {
  it('lists all 16 gondolas with signed g-forces', () => {
    const gondolas: Gondola[] = createGondolas(16).map((g, i) => ({
      ...g,
      gForce: { vertical: i % 2 === 0 ? 2 : -1, lateral: i % 2 === 0 ? -0.5 : 0.5 },
    }));
    const fixture = TestBed.createComponent(GondolaPanel);
    fixture.componentRef.setInput('gondolas', gondolas);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('.row').length).toBe(16);
    expect(host.textContent).toContain('+2.0 g');
    expect(host.textContent).toContain('-0.5 g');
    expect(host.textContent).toContain('4 seated');
  });
});
