import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RiderExperiencePanel } from './rider-experience-panel';

interface Averages {
  queueHappiness: number | null;
  riderHappiness: number | null;
  riderIntensity: number | null;
  riderNausea: number | null;
}

/** The accessible bar; PrimeNG's own (decorative) bar inside it is aria-hidden. */
const BAR = '[role="progressbar"]:not([aria-hidden])';

describe('RiderExperiencePanel', () => {
  function render(averages: Averages): ComponentFixture<RiderExperiencePanel> {
    const fixture = TestBed.createComponent(RiderExperiencePanel);
    update(fixture, averages);
    return fixture;
  }

  function update(fixture: ComponentFixture<RiderExperiencePanel>, averages: Averages): void {
    Object.entries(averages).forEach(([name, value]) => fixture.componentRef.setInput(name, value));
    fixture.detectChanges();
  }

  function bars(fixture: ComponentFixture<RiderExperiencePanel>): HTMLElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>(BAR));
  }

  const full: Averages = {
    queueHappiness: 72,
    riderHappiness: 64,
    riderIntensity: 81,
    riderNausea: 12,
  };
  const empty: Averages = {
    queueHappiness: null,
    riderHappiness: null,
    riderIntensity: null,
    riderNausea: null,
  };

  it('is headed "Rider Experience"', () => {
    const host = render(full).nativeElement as HTMLElement;

    expect(host.querySelector('h2')?.textContent).toBe('Rider Experience');
  });

  it('shows four labelled bars in order with visible values', () => {
    const fixture = render(full);
    const host = fixture.nativeElement as HTMLElement;

    const rows = Array.from(host.querySelectorAll('bb-mood-bar')).map(
      (bar) =>
        `${bar.querySelector('.label')?.textContent} ${bar.querySelector('.value')?.textContent}`,
    );
    expect(rows).toEqual([
      'Queue happiness 72',
      'Rider happiness 64',
      'Rider intensity 81',
      'Rider nausea 12',
    ]);
    expect(bars(fixture).map((bar) => bar.getAttribute('aria-valuenow'))).toEqual([
      '72',
      '64',
      '81',
      '12',
    ]);
  });

  it('rounds the displayed value', () => {
    const fixture = render({ ...full, riderNausea: 12.6 });

    expect(bars(fixture)[3].getAttribute('aria-valuenow')).toBe('13');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('13');
  });

  it('gives each bar an accessible name matching its label and a 0-100 range', () => {
    const fixture = render(full);
    const host = fixture.nativeElement as HTMLElement;

    const nausea = bars(fixture)[3];
    const labelId = nausea.getAttribute('aria-labelledby') ?? '';
    expect(host.querySelector(`#${labelId}`)?.textContent).toBe('Rider nausea');
    expect(nausea.getAttribute('aria-valuemin')).toBe('0');
    expect(nausea.getAttribute('aria-valuemax')).toBe('100');
    expect(nausea.getAttribute('aria-valuetext')).toBe('12 out of 100');
  });

  it('shows "No guests" and no value when there is nobody', () => {
    const fixture = render(empty);
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelectorAll('.value')).toHaveLength(4);
    host.querySelectorAll('.value').forEach((value) => expect(value.textContent).toBe('No guests'));
    bars(fixture).forEach((bar) => {
      expect(bar.hasAttribute('aria-valuenow')).toBe(false);
      expect(bar.getAttribute('aria-valuetext')).toBe('No guests');
    });
  });

  it('treats a genuine 0 as a value, not as no data', () => {
    const fixture = render({ ...empty, riderNausea: 0 });

    expect(bars(fixture)[3].getAttribute('aria-valuenow')).toBe('0');
    expect(bars(fixture)[2].hasAttribute('aria-valuenow')).toBe(false);
  });

  it('re-renders when the inputs change', () => {
    const fixture = render(full);

    update(fixture, { ...full, riderNausea: 55, queueHappiness: null });

    expect(bars(fixture)[3].getAttribute('aria-valuenow')).toBe('55');
    expect(bars(fixture)[0].hasAttribute('aria-valuenow')).toBe(false);
    expect(bars(fixture)[0].getAttribute('aria-valuetext')).toBe('No guests');
  });
});
