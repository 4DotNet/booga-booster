import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';

import { RideTelemetrySource } from './ride-telemetry-source';
import {
  GONDOLAS_PER_HUB,
  GONDOLA_COUNT,
  Gondola,
  HUB_COUNT,
  MotorDirection,
  RideCommand,
  RideState,
  RideTelemetry,
  SEATS_PER_GONDOLA,
  Seat,
  clampPower,
} from '../models/ride.models';

/** How often the simulator advances its values, in milliseconds. */
const TICK_MS = 500;

/** Rotation speed of the mill at full power, in rpm. */
const MAX_MILL_RPM = 8;

/** Rotation speed of a hub at full power, in rpm. */
const MAX_HUB_RPM = 20;

/** Fraction of the gap toward the target speed closed each tick. */
const SPEED_EASING = 0.25;

interface CommandState {
  millPower: number;
  millDirection: MotorDirection;
  hubPower: number;
  hubDirection: MotorDirection;
  gondolaBrakeEngaged: boolean;
}

/**
 * Produces plausible, continuously-moving ride telemetry with no backend.
 *
 * Commanded power/direction are echoed into the telemetry immediately; sensed
 * speeds ease toward their target over successive ticks, hub speeds jitter with
 * simulated load, g-forces follow the speeds, and seat security occasionally
 * flips so the panels demonstrate live updates.
 */
@Injectable({ providedIn: 'root' })
export class SimulatedRideTelemetrySource implements RideTelemetrySource {
  private readonly destroyRef = inject(DestroyRef);

  private readonly commands: CommandState = {
    millPower: 0,
    millDirection: 'forward',
    hubPower: 0,
    hubDirection: 'forward',
    gondolaBrakeEngaged: false,
  };

  private millSpeed = 0;
  private readonly hubSpeeds = new Array<number>(HUB_COUNT).fill(0);
  private seatStates = this.createInitialSeatStates();

  private readonly telemetrySignal = signal<RideTelemetry>(this.buildTelemetry());

  readonly telemetry = this.telemetrySignal.asReadonly();

  constructor() {
    interval(TICK_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.tick());
  }

  applyCommand(command: RideCommand): void {
    switch (command.kind) {
      case 'set-mill-power':
        this.commands.millPower = clampPower(command.value);
        break;
      case 'set-hub-power':
        this.commands.hubPower = clampPower(command.value);
        break;
      case 'set-mill-direction':
        this.commands.millDirection = command.direction;
        break;
      case 'set-hub-direction':
        this.commands.hubDirection = command.direction;
        break;
      case 'set-gondola-brake':
        this.commands.gondolaBrakeEngaged = command.engaged;
        break;
    }
    // Echo the command immediately so controls and status reflect it at once.
    this.telemetrySignal.set(this.buildTelemetry());
  }

  private tick(): void {
    const millTarget = (this.commands.millPower / 100) * MAX_MILL_RPM;
    this.millSpeed += (millTarget - this.millSpeed) * SPEED_EASING;
    if (Math.abs(this.millSpeed) < 0.01) {
      this.millSpeed = 0;
    }

    const hubTargetBase = (this.commands.hubPower / 100) * MAX_HUB_RPM;
    for (let i = 0; i < this.hubSpeeds.length; i++) {
      // Per-hub load jitter so hubs run at slightly different speeds.
      const load = hubTargetBase > 0 ? (Math.random() - 0.5) * 2 : 0;
      const target = Math.max(0, hubTargetBase + load);
      this.hubSpeeds[i] += (target - this.hubSpeeds[i]) * SPEED_EASING;
      if (this.hubSpeeds[i] < 0.01) {
        this.hubSpeeds[i] = 0;
      }
    }

    this.jostleSeats();
    this.telemetrySignal.set(this.buildTelemetry());
  }

  /** Occasionally flip a random occupied seat's secured/unsecured state. */
  private jostleSeats(): void {
    if (Math.random() > 0.3) {
      return;
    }
    const index = Math.floor(Math.random() * this.seatStates.length);
    const current = this.seatStates[index];
    if (current === 'secured') {
      this.seatStates[index] = 'occupied-unsecured';
    } else if (current === 'occupied-unsecured') {
      this.seatStates[index] = 'secured';
    }
  }

  private buildTelemetry(): RideTelemetry {
    const gondolas = this.buildGondolas();
    return {
      state: this.deriveState(),
      mill: {
        power: this.commands.millPower,
        direction: this.commands.millDirection,
        speedRpm: round(this.millSpeed, 2),
      },
      hubs: this.hubSpeeds.map((speed, index) => ({
        id: index + 1,
        power: this.commands.hubPower,
        direction: this.commands.hubDirection,
        speedRpm: round(speed, 2),
      })),
      gondolas,
      gondolaBrakeEngaged: this.commands.gondolaBrakeEngaged,
    };
  }

  private buildGondolas(): Gondola[] {
    const gondolas: Gondola[] = [];
    for (let g = 0; g < GONDOLA_COUNT; g++) {
      const seats: Seat[] = [];
      for (let s = 0; s < SEATS_PER_GONDOLA; s++) {
        const seatIndex = g * SEATS_PER_GONDOLA + s;
        seats.push({ id: s + 1, state: this.seatStates[seatIndex] });
      }
      // Signs alternate per gondola and follow the motor directions so the
      // panels show both push-back/push-forward and left/right forces. Each
      // gondola takes the speed of the hub it is mounted on.
      const hubSpeed = this.hubSpeeds[Math.floor(g / GONDOLAS_PER_HUB)];
      const verticalSign = this.commands.hubDirection === 'forward' ? 1 : -1;
      const lateralSign = g % 2 === 0 ? 1 : -1;
      const vertical = (hubSpeed / MAX_HUB_RPM) * 3 * verticalSign;
      const lateral = (this.millSpeed / MAX_MILL_RPM) * 1.5 * lateralSign;
      gondolas.push({
        id: g + 1,
        seats,
        gForce: { vertical: round(vertical, 1), lateral: round(lateral, 1) },
      });
    }
    return gondolas;
  }

  private deriveState(): RideState {
    return this.commands.millPower > 0 || this.commands.hubPower > 0 ? 'running' : 'stopped';
  }

  /** Ten gondolas start fully occupied and secured (40 riders); rest empty. */
  private createInitialSeatStates(): Seat['state'][] {
    const states: Seat['state'][] = [];
    for (let g = 0; g < GONDOLA_COUNT; g++) {
      for (let s = 0; s < SEATS_PER_GONDOLA; s++) {
        states.push(g < 10 ? 'secured' : 'empty');
      }
    }
    return states;
  }
}

function round(value: number, decimals: number): number {
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}
