import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RideState } from '../../models/ride.models';
import { StatusSummary } from './status-summary';

describe('StatusSummary', () => {
  function render(
    state: RideState,
    availableTransitions: readonly RideState[],
  ): { host: HTMLElement; fixture: ComponentFixture<StatusSummary> } {
    const fixture = TestBed.createComponent(StatusSummary);
    fixture.componentRef.setInput('state', state);
    fixture.componentRef.setInput('availableTransitions', availableTransitions);
    fixture.componentRef.setInput('occupiedSeats', 4);
    fixture.componentRef.setInput('securityState', 'secured');
    fixture.detectChanges();
    return { host: fixture.nativeElement as HTMLElement, fixture };
  }

  function buttonFor(host: HTMLElement, label: string): HTMLButtonElement {
    const button = Array.from(host.querySelectorAll<HTMLButtonElement>('.transition-button')).find(
      (candidate) => candidate.textContent?.includes(label),
    );
    if (!button) {
      throw new Error(`No transition button found for "${label}"`);
    }
    return button;
  }

  it('renders exactly seven lifecycle transition buttons', () => {
    const { host } = render('idle', []);

    expect(host.querySelectorAll('.transition-button').length).toBe(7);
  });

  it('enables only the buttons present in availableTransitions', () => {
    const { host } = render('safe', ['loading', 'started', 'emergency-stop']);

    expect(buttonFor(host, 'Loading').disabled).toBe(false);
    expect(buttonFor(host, 'Started').disabled).toBe(false);
    expect(buttonFor(host, 'Emergency stop').disabled).toBe(false);

    expect(buttonFor(host, 'Idle').disabled).toBe(true);
    expect(buttonFor(host, 'Safe').disabled).toBe(true);
    expect(buttonFor(host, 'Stopping').disabled).toBe(true);
    expect(buttonFor(host, 'Offloading').disabled).toBe(true);
  });

  it('marks the current state with aria-current and a textual cue, never colour alone', () => {
    const { host } = render('safe', ['loading', 'started']);

    const current = buttonFor(host, 'Safe');
    expect(current.getAttribute('aria-current')).toBe('true');
    expect(current.textContent).toContain('current');

    const other = buttonFor(host, 'Loading');
    expect(other.getAttribute('aria-current')).toBeNull();
  });

  it('emits transition with the correct state when an enabled button is clicked', () => {
    const { host, fixture } = render('safe', ['loading', 'started']);
    const events: RideState[] = [];
    fixture.componentInstance.transition.subscribe((state) => events.push(state));

    buttonFor(host, 'Started').click();

    expect(events).toEqual(['started']);
  });

  it('emits nothing when a disabled button is clicked', () => {
    const { host, fixture } = render('safe', ['loading', 'started']);
    const events: RideState[] = [];
    fixture.componentInstance.transition.subscribe((state) => events.push(state));

    buttonFor(host, 'Stopping').click();

    expect(events).toEqual([]);
  });

  it('renders a human-readable state label, e.g. "Emergency stop"', () => {
    const { host } = render('emergency-stop', []);

    expect(host.querySelector('.state')?.textContent).toContain('Emergency stop');
  });
});
