import { TestBed } from '@angular/core/testing';

import { podMotionFor } from '../models/ride.models';
import { RideVisualization, angularVelocity, rpmToRadPerSec } from './ride-visualization';

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

  it('signs angular velocity by motor direction', () => {
    expect(angularVelocity(60, 'forward')).toBeCloseTo(2 * Math.PI, 5);
    expect(angularVelocity(60, 'reverse')).toBeCloseTo(-2 * Math.PI, 5);
    expect(Math.abs(angularVelocity(0, 'reverse'))).toBe(0);
  });

  it('locks the pods when braked and frees them when released', () => {
    expect(podMotionFor(true)).toEqual({ freedom: 0, swing: 0 });
    expect(podMotionFor(false)).toEqual({ freedom: 1, swing: 1 });
  });
});
