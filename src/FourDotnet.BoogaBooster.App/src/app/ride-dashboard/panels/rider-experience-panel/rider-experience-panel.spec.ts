import { TestBed } from '@angular/core/testing';

import { RiderExperience } from '../../models/ride.models';
import { RiderExperiencePanel } from './rider-experience-panel';

const RIDERS: RiderExperience = {
  averageHappiness: 81,
  averagePreferredIntensity: 74,
  averageNausea: 12,
};

function render(queueHappiness: number | null, riderExperience: RiderExperience) {
  const fixture = TestBed.createComponent(RiderExperiencePanel);
  fixture.componentRef.setInput('queueHappiness', queueHappiness);
  fixture.componentRef.setInput('riderExperience', riderExperience);
  fixture.detectChanges();
  return fixture;
}

function rowOf(host: HTMLElement, metric: string): HTMLElement {
  return host.querySelector<HTMLElement>(`[data-metric="${metric}"]`)!;
}

describe('RiderExperiencePanel', () => {
  it('shows four labelled bars with their values as visible text', () => {
    const host = render(72, RIDERS).nativeElement as HTMLElement;

    expect(host.querySelectorAll('p-progressbar').length).toBe(4);
    const expected: [string, string, string][] = [
      ['queue-happiness', 'Queue happiness', '72'],
      ['rider-happiness', 'Rider happiness', '81'],
      ['rider-intensity', 'Rider intensity', '74'],
      ['rider-nausea', 'Rider nausea', '12'],
    ];
    for (const [metric, label, value] of expected) {
      const row = rowOf(host, metric);
      expect(row.querySelector('.label')?.textContent).toBe(label);
      expect(row.querySelector('.value')?.textContent).toBe(value);
    }
  });

  it('exposes label, value and 0-100 range to assistive technology', () => {
    const host = render(72, RIDERS).nativeElement as HTMLElement;
    const bar = rowOf(host, 'rider-nausea').querySelector('p-progressbar')!;
    const label = rowOf(host, 'rider-nausea').querySelector('.label')!;

    expect(bar.getAttribute('role')).toBe('progressbar');
    expect(bar.getAttribute('aria-labelledby')).toBe(label.id);
    expect(bar.getAttribute('aria-valuenow')).toBe('12');
    expect(bar.getAttribute('aria-valuemin')).toBe('0');
    expect(bar.getAttribute('aria-valuemax')).toBe('100');
    expect(bar.getAttribute('aria-valuetext')).toBe('12');
  });

  it('rounds values to whole numbers', () => {
    const host = render(72.4, { ...RIDERS, averageNausea: 11.6 }).nativeElement as HTMLElement;

    expect(rowOf(host, 'queue-happiness').querySelector('.value')?.textContent).toBe('72');
    expect(rowOf(host, 'rider-nausea').querySelector('.value')?.textContent).toBe('12');
  });

  it('shows an explicit "No data" state rather than 0 when values are null', () => {
    const host = render(null, {
      averageHappiness: null,
      averagePreferredIntensity: null,
      averageNausea: null,
    }).nativeElement as HTMLElement;

    for (const value of Array.from(host.querySelectorAll('.value'))) {
      expect(value.textContent).toBe('No data');
    }
    const bar = rowOf(host, 'rider-happiness').querySelector('p-progressbar')!;
    expect(bar.hasAttribute('aria-valuenow')).toBe(false);
    expect(bar.getAttribute('aria-valuetext')).toBe('No data');
  });

  it('keeps a genuine 0 distinct from no data', () => {
    const host = render(0, { ...RIDERS, averageNausea: 0 }).nativeElement as HTMLElement;

    expect(rowOf(host, 'queue-happiness').querySelector('.value')?.textContent).toBe('0');
    expect(
      rowOf(host, 'rider-nausea').querySelector('p-progressbar')?.getAttribute('aria-valuenow'),
    ).toBe('0');
  });

  it('updates live when the inputs change', () => {
    const fixture = render(null, RIDERS);
    const host = fixture.nativeElement as HTMLElement;
    expect(rowOf(host, 'queue-happiness').querySelector('.value')?.textContent).toBe('No data');

    fixture.componentRef.setInput('queueHappiness', 55);
    fixture.componentRef.setInput('riderExperience', { ...RIDERS, averageNausea: 30 });
    fixture.detectChanges();

    expect(rowOf(host, 'queue-happiness').querySelector('.value')?.textContent).toBe('55');
    expect(rowOf(host, 'rider-nausea').querySelector('.value')?.textContent).toBe('30');
  });

  it('places the rows in a polite live region', () => {
    const host = render(72, RIDERS).nativeElement as HTMLElement;

    expect(host.querySelector('ul[aria-live="polite"]')?.querySelectorAll('li').length).toBe(4);
  });
});
