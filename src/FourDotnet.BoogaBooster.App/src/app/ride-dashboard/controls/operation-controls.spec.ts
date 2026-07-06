import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { RIDE_TELEMETRY_SOURCE } from '../data/ride-telemetry-source';
import { RideState } from '../models/ride.models';
import { RideLifecycleService } from '../state/ride-lifecycle.service';
import { FakeRideTelemetrySource } from '../testing/fake-ride-telemetry-source';
import { OperationControls } from './operation-controls';

/**
 * Minimal stand-in for {@link RideLifecycleService} exposing just the
 * settable `state` signal `OperationControls` reads, so specs can drive the
 * lifecycle state synchronously without the real service's HTTP polling.
 */
class StubRideLifecycleService {
  private readonly stateSignal = signal<RideState>('started');

  readonly state = this.stateSignal.asReadonly();
  readonly availableTransitions = signal<readonly RideState[]>([]).asReadonly();

  setState(state: RideState): void {
    this.stateSignal.set(state);
  }

  requestTransition(): void {
    // Unused by OperationControls; present only to shape-match the real service.
  }
}

describe('OperationControls', () => {
  let source: FakeRideTelemetrySource;
  let lifecycle: StubRideLifecycleService;

  function render() {
    const fixture = TestBed.createComponent(OperationControls);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    source = new FakeRideTelemetrySource();
    lifecycle = new StubRideLifecycleService();
    TestBed.configureTestingModule({
      providers: [
        { provide: RIDE_TELEMETRY_SOURCE, useValue: source },
        { provide: RideLifecycleService, useValue: lifecycle },
      ],
    });
  });

  it('emits a clamped mill-power command when the slider changes', () => {
    const fixture = render();
    const slider = fixture.nativeElement.querySelector('#mill-power') as HTMLInputElement;

    slider.value = '150';
    slider.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(source.commands).toContainEqual({ kind: 'set-mill-power', value: 100 });
  });

  it('emits a hub-power command when the hub slider changes', () => {
    const fixture = render();
    const slider = fixture.nativeElement.querySelector('#hub-power') as HTMLInputElement;

    slider.value = '45';
    slider.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(source.commands).toContainEqual({ kind: 'set-hub-power', value: 45 });
  });

  it('toggles mill direction and exposes the pressed state', () => {
    const fixture = render();
    const toggle = fixture.nativeElement.querySelector(
      '.toggle[aria-labelledby="mill-dir-label"]',
    ) as HTMLButtonElement;

    expect(toggle.getAttribute('aria-pressed')).toBe('false');

    toggle.click();
    fixture.detectChanges();

    expect(source.commands).toContainEqual({ kind: 'set-mill-direction', direction: 'reverse' });
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
    expect(toggle.textContent?.trim()).toBe('Reverse');
  });

  it('stays in sync when power changes elsewhere', () => {
    const fixture = render();
    source.applyCommand({ kind: 'set-mill-power', value: 70 });
    fixture.detectChanges();

    const slider = fixture.nativeElement.querySelector('#mill-power') as HTMLInputElement;
    expect(slider.value).toBe('70');
  });

  it('toggles the gondola brake and reflects its label and pressed state', () => {
    const fixture = render();
    const brake = fixture.nativeElement.querySelector('.toggle.brake') as HTMLButtonElement;

    expect(brake.getAttribute('aria-pressed')).toBe('false');
    expect(brake.textContent?.trim()).toBe('Gondolas Released');

    brake.click();
    fixture.detectChanges();

    expect(source.commands).toContainEqual({ kind: 'set-gondola-brake', engaged: true });
    expect(brake.getAttribute('aria-pressed')).toBe('true');
    expect(brake.textContent?.trim()).toBe('Gondolas Break');
  });

  it('disables every control and shows the hint when the ride is not started', () => {
    lifecycle.setState('idle');
    const fixture = render();
    const host = fixture.nativeElement as HTMLElement;

    expect((host.querySelector('#mill-power') as HTMLInputElement).disabled).toBe(true);
    expect((host.querySelector('#hub-power') as HTMLInputElement).disabled).toBe(true);
    expect(
      (host.querySelector('.toggle[aria-labelledby="mill-dir-label"]') as HTMLButtonElement)
        .disabled,
    ).toBe(true);
    expect(
      (host.querySelector('.toggle[aria-labelledby="hub-dir-label"]') as HTMLButtonElement)
        .disabled,
    ).toBe(true);
    expect((host.querySelector('.toggle.brake') as HTMLButtonElement).disabled).toBe(true);
    expect((host.querySelector('.apply-brakes') as HTMLButtonElement).disabled).toBe(true);

    const hint = host.querySelector('#controls-hint');
    expect(hint).not.toBeNull();
    expect(hint?.textContent).toContain('Controls become available once the ride is started.');
    expect(host.querySelector('.controls')?.getAttribute('aria-describedby')).toBe('controls-hint');
  });

  it('enables every control and hides the hint once the ride is started', () => {
    lifecycle.setState('started');
    const fixture = render();
    const host = fixture.nativeElement as HTMLElement;

    expect((host.querySelector('#mill-power') as HTMLInputElement).disabled).toBe(false);
    expect((host.querySelector('#hub-power') as HTMLInputElement).disabled).toBe(false);
    expect((host.querySelector('.toggle.brake') as HTMLButtonElement).disabled).toBe(false);
    expect((host.querySelector('.apply-brakes') as HTMLButtonElement).disabled).toBe(false);
    expect(host.querySelector('#controls-hint')).toBeNull();
  });

  it('applies engine brakes when the "Apply brakes" button is clicked while started', () => {
    lifecycle.setState('started');
    const fixture = render();
    const button = fixture.nativeElement.querySelector('.apply-brakes') as HTMLButtonElement;

    button.click();
    fixture.detectChanges();

    expect(source.commands).toContainEqual({ kind: 'brake-engines' });
  });
});
