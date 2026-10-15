import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RiderExperiencePanel } from './rider-experience-panel';

describe('RiderExperiencePanel', () => {
  let fixture: ComponentFixture<RiderExperiencePanel>;

  function render(values: {
    queue: number | null;
    happiness: number | null;
    intensity: number | null;
    nausea: number | null;
  }): void {
    fixture.componentRef.setInput('queueHappiness', values.queue);
    fixture.componentRef.setInput('riderHappiness', values.happiness);
    fixture.componentRef.setInput('riderIntensity', values.intensity);
    fixture.componentRef.setInput('riderNausea', values.nausea);
    fixture.detectChanges();
  }

  const host = () => fixture.nativeElement as HTMLElement;
  const bars = () => Array.from(host().querySelectorAll<HTMLElement>('[role="progressbar"]'));

  beforeEach(() => {
    fixture = TestBed.createComponent(RiderExperiencePanel);
  });

  it('shows four labelled bars with the live values', () => {
    render({ queue: 72, happiness: 81, intensity: 68, nausea: 12 });

    expect(host().querySelector('h2')?.textContent).toContain('Rider Experience');
    expect(bars().map((bar) => bar.getAttribute('aria-label'))).toEqual([
      'Queue happiness 72%',
      'Rider happiness 81%',
      'Rider intensity 68%',
      'Rider nausea 12%',
    ]);
    expect(bars().map((bar) => bar.getAttribute('aria-valuenow'))).toEqual([
      '72',
      '81',
      '68',
      '12',
    ]);
    bars().forEach((bar) => {
      expect(bar.getAttribute('aria-valuemin')).toBe('0');
      expect(bar.getAttribute('aria-valuemax')).toBe('100');
    });
    expect(host().textContent).toContain('72%');
  });

  it('rounds fractional averages', () => {
    render({ queue: 71.6, happiness: 80.2, intensity: 67.5, nausea: 12.4 });

    expect(bars().map((bar) => bar.getAttribute('aria-valuenow'))).toEqual([
      '72',
      '80',
      '68',
      '12',
    ]);
  });

  it('updates the bars when the inputs change', () => {
    render({ queue: 72, happiness: 81, intensity: 68, nausea: 12 });
    render({ queue: 60, happiness: 90, intensity: 70, nausea: 30 });

    expect(bars()[3].getAttribute('aria-label')).toBe('Rider nausea 30%');
    expect(bars()[0].getAttribute('aria-valuenow')).toBe('60');
  });

  it('shows "No riders" instead of three zero bars on an empty ride', () => {
    render({ queue: 72, happiness: null, intensity: null, nausea: null });

    expect(bars()).toHaveLength(1);
    expect(host().textContent?.match(/No riders/g)).toHaveLength(3);
    expect(host().textContent).not.toContain('0%');
  });

  it('shows "Queue empty" instead of a zero bar for an empty queue', () => {
    render({ queue: null, happiness: 81, intensity: 68, nausea: 12 });

    expect(bars()).toHaveLength(3);
    expect(host().textContent).toContain('Queue empty');
  });

  it('still draws a real 0 as a bar, not as an empty state', () => {
    render({ queue: 72, happiness: 81, intensity: 68, nausea: 0 });

    expect(bars()[3].getAttribute('aria-label')).toBe('Rider nausea 0%');
    expect(host().textContent).not.toContain('No riders');
  });
});
