import { TestBed } from '@angular/core/testing';

import { QueueGuest } from '../../../queue/models/queue.models';
import { LastOffload, SeatRider } from '../../models/ride.models';
import { RiderExperiencePanel } from './rider-experience-panel';

const rider = (
  guestNumber: number,
  happiness: number,
  preferredG: number,
  nausea: number,
): SeatRider => ({ guestNumber, happiness, preferredG, nausea });

const guest = (guestNumber: number, happiness: number): QueueGuest => ({
  guestNumber,
  name: `g${guestNumber}`,
  weightKg: 70,
  happiness,
  preferredG: 2,
  nausea: 0,
});

function render(
  inputs: {
    queuedGuests?: readonly QueueGuest[];
    riders?: readonly SeatRider[];
    lastOffload?: LastOffload;
  } = {},
) {
  const fixture = TestBed.createComponent(RiderExperiencePanel);
  fixture.componentRef.setInput('queuedGuests', inputs.queuedGuests ?? []);
  fixture.componentRef.setInput('riders', inputs.riders ?? []);
  if (inputs.lastOffload) {
    fixture.componentRef.setInput('lastOffload', inputs.lastOffload);
  }
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const bar = (label: string): HTMLElement =>
    Array.from(el.querySelectorAll('bb-mood-bar')).find((b) =>
      b.textContent?.includes(label),
    ) as HTMLElement;
  const valueOf = (label: string) => bar(label).querySelector('.value')?.textContent;
  const nowOf = (label: string) =>
    bar(label).querySelector('p-progressbar')?.getAttribute('aria-valuenow');
  return { fixture, el, valueOf, nowOf };
}

describe('RiderExperiencePanel', () => {
  it('shows the average rider happiness', () => {
    const { valueOf, nowOf } = render({ riders: [rider(1, 60, 3, 0), rider(2, 80, 3, 0)] });

    expect(valueOf('Rider happiness')).toBe('70');
    expect(nowOf('Rider happiness')).toBe('70');
  });

  it('shows the queue happiness from the queued guests', () => {
    const { valueOf } = render({ queuedGuests: [guest(1, 40), guest(2, 60)] });

    expect(valueOf('Queue happiness')).toBe('50');
  });

  it('shows the average rider nausea', () => {
    const { valueOf } = render({ riders: [rider(1, 50, 3, 10), rider(2, 50, 3, 30)] });

    expect(valueOf('Rider nausea')).toBe('20');
  });

  it('labels preferred G in g and fills against the max G', () => {
    const { valueOf, nowOf } = render({ riders: [rider(1, 50, 3.3, 0)] });

    expect(valueOf('Rider preferred G')).toBe('3.3 g');
    expect(Number(nowOf('Rider preferred G'))).toBeCloseTo((3.3 / 4.5) * 100, 5);
  });

  it('shows an em dash and an empty bar when the population is empty', () => {
    const { valueOf, nowOf } = render();

    for (const label of [
      'Queue happiness',
      'Rider happiness',
      'Rider preferred G',
      'Rider nausea',
    ]) {
      expect(valueOf(label)).toBe('—');
      expect(nowOf(label)).toBe('0');
    }
  });

  it('reports the mad, sick and left-sick counts as text', () => {
    const { el } = render({
      queuedGuests: [guest(1, 10), guest(2, 29), guest(3, 5), guest(4, 90)],
      riders: [rider(1, 50, 3, 70), rider(2, 50, 3, 10)],
      lastOffload: {
        counter: 2,
        riders: [
          { guestNumber: 1, happiness: 50, nausea: 80 },
          { guestNumber: 2, happiness: 50, nausea: 99 },
          { guestNumber: 3, happiness: 50, nausea: 10 },
        ],
      },
    });
    const text = el.querySelector('.counts')?.textContent ?? '';

    expect(text).toContain('3 mad in queue');
    expect(text).toContain('1 sick on ride');
    expect(text).toContain('2 left sick');
  });

  it('has no live region', () => {
    const { el } = render();

    expect(el.querySelector('[aria-live]')).toBeNull();
  });
});
