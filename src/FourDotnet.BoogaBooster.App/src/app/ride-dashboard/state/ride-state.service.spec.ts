import { TestBed } from '@angular/core/testing';

import { RIDE_TELEMETRY_SOURCE } from '../data/ride-telemetry-source';
import { createTelemetry, createGondolas, FakeRideTelemetrySource } from '../testing/fake-ride-telemetry-source';
import { RideStateService } from './ride-state.service';

describe('RideStateService', () => {
  let source: FakeRideTelemetrySource;
  let service: RideStateService;

  beforeEach(() => {
    source = new FakeRideTelemetrySource();
    TestBed.configureTestingModule({
      providers: [{ provide: RIDE_TELEMETRY_SOURCE, useValue: source }],
    });
    service = TestBed.inject(RideStateService);
  });

  it('clamps mill power above 100 before dispatching', () => {
    service.setMillPower(150);

    expect(source.commands).toContainEqual({ kind: 'set-mill-power', value: 100 });
    expect(service.mill().power).toBe(100);
  });

  it('clamps mill power below 0 before dispatching', () => {
    service.setMillPower(-20);

    expect(source.commands).toContainEqual({ kind: 'set-mill-power', value: 0 });
    expect(service.mill().power).toBe(0);
  });

  it('clamps hub power into range', () => {
    service.setHubPower(45);
    expect(service.hubPower()).toBe(45);

    service.setHubPower(999);
    expect(service.hubPower()).toBe(100);
  });

  it('toggles mill and hub direction', () => {
    service.setMillDirection('reverse');
    service.setHubDirection('reverse');

    expect(service.mill().direction).toBe('reverse');
    expect(service.hubDirection()).toBe('reverse');
  });

  it('engages and releases the gondola brake', () => {
    expect(service.gondolaBrakeEngaged()).toBe(false);

    service.setGondolaBrake(true);

    expect(source.commands).toContainEqual({ kind: 'set-gondola-brake', engaged: true });
    expect(service.gondolaBrakeEngaged()).toBe(true);
  });

  it('reflects ride state from the telemetry source', () => {
    expect(service.state()).toBe('stopped');
    service.setMillPower(60);
    expect(service.state()).toBe('running');
  });

  it('computes occupied seats and secured security state', () => {
    source.setTelemetry(createTelemetry({ gondolas: createGondolas(10) }));

    expect(service.occupiedSeats()).toBe(40);
    expect(service.securityState()).toBe('secured');
  });

  it('reports unsecured when any occupied seat is not secured', () => {
    source.setTelemetry(createTelemetry({ gondolas: createGondolas(10, true) }));

    expect(service.securityState()).toBe('unsecured');
  });
});
