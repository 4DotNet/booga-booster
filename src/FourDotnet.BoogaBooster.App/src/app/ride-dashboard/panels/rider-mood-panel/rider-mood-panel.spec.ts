import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RiderMoodPanel } from './rider-mood-panel';

describe('RiderMoodPanel', () => {
  function render(
    queueHappiness: number | null,
    riderHappiness: number | null,
    nausea: number | null,
    queueUnavailable = false,
  ): { host: HTMLElement; fixture: ComponentFixture<RiderMoodPanel> } {
    const fixture = TestBed.createComponent(RiderMoodPanel);
    fixture.componentRef.setInput('queueHappiness', queueHappiness);
    fixture.componentRef.setInput('riderHappiness', riderHappiness);
    fixture.componentRef.setInput('nausea', nausea);
    fixture.componentRef.setInput('queueUnavailable', queueUnavailable);
    fixture.detectChanges();
    return { host: fixture.nativeElement as HTMLElement, fixture };
  }

  function meters(host: HTMLElement): HTMLElement[] {
    return Array.from(host.querySelectorAll('p-metergroup'));
  }

  it('renders the "Rider mood" heading', () => {
    const { host } = render(0.72, 0.6, 0.25);
    expect(host.querySelector('h2')?.textContent).toContain('Rider mood');
  });

  it('shows the three labelled metrics', () => {
    const { host } = render(0.72, 0.6, 0.25);
    const text = host.textContent ?? '';
    expect(text).toContain('Queue happiness');
    expect(text).toContain('Rider happiness');
    expect(text).toContain('Nausea');
  });

  it('shows a whole-number percentage for the queue happiness metric', () => {
    const { host } = render(0.72, 0.6, 0.25);
    expect(host.textContent).toContain('72 %');
  });

  it('shows whole-number percentages for the rider metrics', () => {
    const { host } = render(0.72, 0.6, 0.25);
    expect(host.textContent).toContain('60 %');
    expect(host.textContent).toContain('25 %');
  });

  it('updates the displayed values live as inputs change', () => {
    const { host, fixture } = render(0.72, 0.6, 0.25);
    expect(host.textContent).toContain('72 %');

    fixture.componentRef.setInput('queueHappiness', 0.5);
    fixture.componentRef.setInput('riderHappiness', 0.4);
    fixture.componentRef.setInput('nausea', 0.1);
    fixture.detectChanges();

    expect(host.textContent).toContain('50 %');
    expect(host.textContent).toContain('40 %');
    expect(host.textContent).toContain('10 %');
    expect(host.textContent).not.toContain('72 %');
  });

  it('reads "Queue empty" when the queue reports no average happiness', () => {
    const { host } = render(null, 0.6, 0.25);
    expect(host.textContent).toContain('Queue empty');
  });

  it('reads "Unavailable" when the queue feed is unavailable, even with a queue value', () => {
    const { host } = render(0.72, 0.6, 0.25, true);
    expect(host.textContent).toContain('Unavailable');
    expect(host.textContent).not.toContain('72 %');
  });

  it('reads "No riders" for rider happiness and nausea when nobody is aboard', () => {
    const { host } = render(0.72, null, null);
    const text = host.textContent ?? '';
    expect(text.match(/No riders/g)).toHaveLength(2);
  });

  it('gives each meter an accessible name that includes the metric name and its value', () => {
    const { host } = render(0.72, 0.6, 0.25);
    const labels = meters(host).map((meter) => meter.getAttribute('aria-label'));

    expect(labels).toContain('Queue happiness: 72 %');
    expect(labels).toContain('Rider happiness: 60 %');
    expect(labels).toContain('Nausea: 25 %');
  });

  it('gives each meter an accessible name that includes the empty-state text', () => {
    const { host } = render(null, null, null, false);
    const labels = meters(host).map((meter) => meter.getAttribute('aria-label'));

    expect(labels).toContain('Queue happiness: Queue empty');
    expect(labels).toContain('Rider happiness: No riders');
    expect(labels).toContain('Nausea: No riders');
  });

  it('renders exactly three meters, one per metric', () => {
    const { host } = render(0.72, 0.6, 0.25);
    expect(meters(host)).toHaveLength(3);
  });
});
