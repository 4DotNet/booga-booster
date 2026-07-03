import { InjectionToken, Signal } from '@angular/core';

import { RideCommand, RideTelemetry } from '../models/ride.models';

/**
 * The seam between the ride-state service and whatever produces telemetry.
 *
 * Today the only implementation simulates data; later a real backend feed
 * (SignalR/HTTP) can implement the same contract without any component or
 * service change. Implementations expose telemetry as a signal and accept
 * operator commands.
 */
export interface RideTelemetrySource {
  /** Live telemetry snapshot; changes whenever the source advances. */
  readonly telemetry: Signal<RideTelemetry>;

  /** Apply an operator command so the telemetry reflects it. */
  applyCommand(command: RideCommand): void;
}

/** DI token for the active {@link RideTelemetrySource} implementation. */
export const RIDE_TELEMETRY_SOURCE = new InjectionToken<RideTelemetrySource>(
  'RIDE_TELEMETRY_SOURCE',
);
