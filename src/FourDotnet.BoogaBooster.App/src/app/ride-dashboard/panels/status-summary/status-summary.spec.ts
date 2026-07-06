import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RideState, SecurityState } from '../../models/ride.models';
import { StatusSummary } from './status-summary';

describe('StatusSummary', () => {
  function render(
    state: RideState,
    availableTransitions: readonly RideState[],
    occupiedSeats = 4,
    securityState: SecurityState = 'secured',
  ): { host: HTMLElement; fixture: ComponentFixture<StatusSummary> } {
    const fixture = TestBed.createComponent(StatusSummary);
    fixture.componentRef.setInput('state', state);
    fixture.componentRef.setInput('availableTransitions', availableTransitions);
    fixture.componentRef.setInput('occupiedSeats', occupiedSeats);
    fixture.componentRef.setInput('securityState', securityState);
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

  it('marks the current state with aria-current and a blinking, non-colour visual cue', () => {
    const { host } = render('safe', ['loading', 'started']);

    const current = buttonFor(host, 'Safe');
    expect(current.getAttribute('aria-current')).toBe('true');
    expect(current.getAttribute('data-visual')).toBe('blinking');

    const other = buttonFor(host, 'Loading');
    expect(other.getAttribute('aria-current')).toBeNull();
  });

  it('renders an available, non-current button as lit and enabled', () => {
    const { host } = render('safe', ['loading', 'started']);

    const loading = buttonFor(host, 'Loading');
    expect(loading.getAttribute('data-visual')).toBe('lit');
    expect(loading.disabled).toBe(false);
  });

  it('renders an unavailable, non-current button as unlit and disabled', () => {
    const { host } = render('safe', ['loading', 'started']);

    const stopping = buttonFor(host, 'Stopping');
    expect(stopping.getAttribute('data-visual')).toBe('unlit');
    expect(stopping.disabled).toBe(true);
  });

  it('assigns the correct base colour per lifecycle state', () => {
    const { host } = render('idle', []);

    expect(buttonFor(host, 'Idle').getAttribute('data-color')).toBe('blue');
    expect(buttonFor(host, 'Loading').getAttribute('data-color')).toBe('blue');
    expect(buttonFor(host, 'Safe').getAttribute('data-color')).toBe('green');
    expect(buttonFor(host, 'Started').getAttribute('data-color')).toBe('green');
    expect(buttonFor(host, 'Stopping').getAttribute('data-color')).toBe('orange');
    expect(buttonFor(host, 'Offloading').getAttribute('data-color')).toBe('blue');
    expect(buttonFor(host, 'Emergency stop').getAttribute('data-color')).toBe('red');
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

  it('renders the streamed occupied-seat count during boarding', () => {
    const { host } = render('loading', ['safe'], 7, 'unsecured');

    // The second metric row ("Occupied seats"); the first is "Ride state".
    const occupiedSeatsValue = host.querySelectorAll('.metric dd')[1];
    expect(occupiedSeatsValue.textContent).toContain('7');
  });

  it('renders occupied-but-unsecured seats as unsecured during the restraint countdown', () => {
    const { host } = render('loading', ['safe'], 2, 'unsecured');

    const security = host.querySelector('.security');
    expect(security?.getAttribute('data-security')).toBe('unsecured');
    expect(security?.textContent).toContain('⚠');
    expect(security?.textContent).toContain('Unsecured');
    expect(security?.getAttribute('aria-label')).toBe('One or more occupied seats not secured');
  });

  it('flips to secured once every occupied seat closes its restraint on a later frame', () => {
    const { host, fixture } = render('loading', ['safe'], 2, 'unsecured');
    expect(host.querySelector('.security')?.getAttribute('data-security')).toBe('unsecured');

    fixture.componentRef.setInput('securityState', 'secured');
    fixture.detectChanges();

    const security = host.querySelector('.security');
    expect(security?.getAttribute('data-security')).toBe('secured');
    expect(security?.textContent).toContain('✓');
    expect(security?.textContent).toContain('Secured');
  });
});
