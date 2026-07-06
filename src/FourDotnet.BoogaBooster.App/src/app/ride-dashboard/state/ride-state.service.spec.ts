import { TestBed } from '@angular/core/testing';

import { RIDE_TELEMETRY_SOURCE } from '../data/ride-telemetry-source';
import { GONDOLA_COUNT } from '../models/ride.models';
import {
  createTelemetry,
  createGondolas,
  FakeRideTelemetrySource,
} from '../testing/fake-ride-telemetry-source';
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

  it('engages the engine brake by dispatching a brake-engines command', () => {
    service.setMillPower(60);
    service.setHubPower(60);

    service.setEngineBrakes(true);

    expect(source.commands).toContainEqual({ kind: 'brake-engines', engaged: true });
    expect(service.mill().power).toBe(0);
    expect(service.hubPower()).toBe(0);
    expect(service.brakesEngaged()).toBe(true);
  });

  it('releases the engine brake without restoring power', () => {
    service.setEngineBrakes(true);
    service.setEngineBrakes(false);

    expect(source.commands).toContainEqual({ kind: 'brake-engines', engaged: false });
    expect(service.brakesEngaged()).toBe(false);
    expect(service.mill().power).toBe(0);
  });

  it('reflects ride state from the telemetry source', () => {
    expect(service.state()).toBe('idle');
    service.setMillPower(60);
    expect(service.state()).toBe('started');
  });

  it('computes occupied seats and secured security state', () => {
    source.setTelemetry(createTelemetry({ gondolas: createGondolas(10) }));

    expect(service.occupiedSeats()).toBe(20);
    expect(service.securityState()).toBe('secured');
  });

  it('reports unsecured when any occupied seat is not secured', () => {
    source.setTelemetry(createTelemetry({ gondolas: createGondolas(10, true) }));

    expect(service.securityState()).toBe('unsecured');
  });

  it('computes total load in kg across every seat', () => {
    source.setTelemetry(createTelemetry({ gondolas: createGondolas(10) }));

    // 10 occupied gondolas x 2 seats x 70kg = 1400kg.
    expect(service.totalLoadKg()).toBe(1400);
  });

  it('reports a safe load balance for an empty ride', () => {
    expect(service.loadBalanceState()).toBe('safe');
  });

  it('reports a safe load balance for a fully, evenly loaded ride', () => {
    source.setTelemetry(createTelemetry({ gondolas: createGondolas(GONDOLA_COUNT) }));

    expect(service.loadBalanceState()).toBe('safe');
  });

  it('reports an unsafe load balance for a lopsided load', () => {
    // Only the first gondola carries any weight, so the load is entirely on
    // one side of the mill.
    const gondolas = createGondolas(1);
    source.setTelemetry(createTelemetry({ gondolas }));

    expect(service.loadBalanceState()).toBe('unsafe');
  });
});
