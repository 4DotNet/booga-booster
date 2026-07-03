import { TestBed } from '@angular/core/testing';

import { RIDE_TELEMETRY_SOURCE } from '../data/ride-telemetry-source';
import { FakeRideTelemetrySource } from '../testing/fake-ride-telemetry-source';
import { OperationControls } from './operation-controls';

describe('OperationControls', () => {
  let source: FakeRideTelemetrySource;

  function render() {
    const fixture = TestBed.createComponent(OperationControls);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    source = new FakeRideTelemetrySource();
    TestBed.configureTestingModule({
      providers: [{ provide: RIDE_TELEMETRY_SOURCE, useValue: source }],
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
});
