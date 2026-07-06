import { TestBed } from '@angular/core/testing';

import { RideVisualization, angularVelocity, podFreeAngle, rpmToRadPerSec } from './ride-visualization';

describe('RideVisualization', () => {
  function render() {
    const fixture = TestBed.createComponent(RideVisualization);
    fixture.componentRef.setInput('millSpeedRpm', 6);
    fixture.componentRef.setInput('millDirection', 'forward');
    fixture.componentRef.setInput('hubSpeedRpm', 9);
    fixture.componentRef.setInput('hubDirection', 'reverse');
    fixture.componentRef.setInput('gondolaBrakeEngaged', false);
    fixture.detectChanges();
    return fixture;
  }

  it('mounts without a WebGL context and stays hidden from assistive tech', () => {
    const fixture = render();
    const host = fixture.nativeElement as HTMLElement;

    expect(host.getAttribute('aria-hidden')).toBe('true');
    expect(host.querySelector('.viz')).not.toBeNull();
  });

  it('maps rpm to radians per second', () => {
    expect(rpmToRadPerSec(60)).toBeCloseTo(2 * Math.PI, 5);
    expect(rpmToRadPerSec(0)).toBe(0);
  });

  it('derives angular velocity from signed rpm', () => {
    expect(angularVelocity(60)).toBeCloseTo(2 * Math.PI, 5);
    expect(angularVelocity(-60)).toBeCloseTo(-2 * Math.PI, 5);
    expect(angularVelocity(0)).toBe(0);
  });

  it('coerces a non-finite rpm to 0 instead of corrupting the rotation', () => {
    expect(angularVelocity(NaN)).toBe(0);
    expect(angularVelocity(Infinity)).toBe(0);
    expect(angularVelocity(-Infinity)).toBe(0);
  });

  it('advances the free pod trajectory with the combined rotation', () => {
    // At rest the pod sits at its phase-shifted swing baseline.
    expect(podFreeAngle(0, 0)).toBe(0);
    expect(podFreeAngle(0, Math.PI / 2)).toBeCloseTo(1, 5);
    // The trajectory counter-rotates against the combined spin.
    expect(podFreeAngle(Math.PI, 0)).toBeCloseTo(-Math.PI, 5);
  });
});
