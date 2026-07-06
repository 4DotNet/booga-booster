import {
  GONDOLA_COUNT,
  GONDOLAS_PER_HUB,
  HUB_COUNT,
  HUB_MAX_POWER_WATTS,
  MILL_MAX_POWER_WATTS,
  RideTelemetryGondolaStreamDto,
  RideTelemetryHubStreamDto,
  RideTelemetryStreamDto,
  SEATS_PER_GONDOLA,
  mapRideTelemetry,
} from './ride.models';

/** A hub DTO with the given index and no load. */
function hubDto(index: number, powerWatts: number, rpm: number): RideTelemetryHubStreamDto {
  return { index, powerWatts, rpm, loadKg: 0 };
}

/** A gondola DTO with two seats at the given hub/index, brake state and seat data. */
function gondolaDto(
  hubIndex: number,
  index: number,
  overrides: Partial<RideTelemetryGondolaStreamDto> = {},
): RideTelemetryGondolaStreamDto {
  return {
    hubIndex,
    index,
    brake: 0, // Engaged
    angleDegrees: 0,
    rpm: 0,
    lateralG: 0,
    forwardG: 0,
    loadKg: 0,
    isSafeToDispatch: true,
    seats: [
      { position: 0, occupiedKg: 0, restraint: 0 },
      { position: 1, occupiedKg: 0, restraint: 0 },
    ],
    ...overrides,
  };
}

/** A full, representative telemetry stream DTO; every gondola/hub defaults to at-rest. */
function buildDto(overrides: Partial<RideTelemetryStreamDto> = {}): RideTelemetryStreamDto {
  const hubs = Array.from({ length: HUB_COUNT }, (_, i) => hubDto(i, 0, 0));
  const gondolas = Array.from({ length: GONDOLA_COUNT }, (_, g) =>
    gondolaDto(Math.floor(g / GONDOLAS_PER_HUB), g % GONDOLAS_PER_HUB),
  );
  return {
    state: 0,
    availableTransitions: [1],
    isSafeToStart: true,
    safetyReason: 0,
    simulationTimeSeconds: 0,
    mill: {
      powerWatts: 0,
      rpm: 0,
      loadKg: 0,
      passengerLoadKg: 0,
      imbalanceMillimeters: 0,
      isBalanced: true,
      isOverloaded: false,
    },
    hubs,
    gondolas,
    ...overrides,
  };
}

describe('mapRideTelemetry', () => {
  it('maps lifecycle state and available transitions from numeric enums', () => {
    const model = mapRideTelemetry(buildDto({ state: 3, availableTransitions: [4, 6] }));

    expect(model.state).toBe('started');
    expect(model.availableTransitions).toEqual(['stopping', 'emergency-stop']);
  });

  it('converts mill power watts to a percent of the configured maximum, always forward', () => {
    const dto = buildDto();
    const model = mapRideTelemetry({
      ...dto,
      mill: { ...dto.mill, powerWatts: MILL_MAX_POWER_WATTS / 2, rpm: 4.567 },
    });

    expect(model.mill.power).toBe(50);
    expect(model.mill.direction).toBe('forward');
    expect(model.mill.speedRpm).toBe(4.57);
  });

  it('converts each hub power watts to a percent and assigns 1-based ids', () => {
    const dto = buildDto();
    const model = mapRideTelemetry({
      ...dto,
      hubs: [
        hubDto(0, HUB_MAX_POWER_WATTS, 10),
        hubDto(1, HUB_MAX_POWER_WATTS / 4, 5),
        hubDto(2, 0, 0),
        hubDto(3, 0, 0),
      ],
    });

    expect(model.hubs.map((hub) => hub.id)).toEqual([1, 2, 3, 4]);
    expect(model.hubs[0].power).toBe(100);
    expect(model.hubs[1].power).toBe(25);
    expect(model.hubs[0].direction).toBe('forward');
  });

  it('assigns gondola ids from hub index and gondola index, and maps angleDegrees', () => {
    const dto = buildDto();
    const gondolas = dto.gondolas.slice();
    gondolas[0] = gondolaDto(0, 0, { angleDegrees: 12.34 });
    gondolas[5] = gondolaDto(1, 1, { angleDegrees: 90 }); // hub 1, index 1 -> id 6

    const model = mapRideTelemetry({ ...dto, gondolas });

    const first = model.gondolas.find((g) => g.angleDegrees === 12.3);
    expect(first?.id).toBe(1);
    const sixth = model.gondolas.find((g) => g.id === 6);
    expect(sixth?.angleDegrees).toBe(90);
  });

  it('swaps forwardG/lateralG onto vertical/lateral g-force', () => {
    const dto = buildDto();
    const gondolas = dto.gondolas.slice();
    gondolas[0] = gondolaDto(0, 0, { forwardG: 1.23, lateralG: -0.55 });

    const model = mapRideTelemetry({ ...dto, gondolas });

    expect(model.gondolas[0].gForce.vertical).toBe(1.2);
    expect(model.gondolas[0].gForce.lateral).toBe(-0.5);
  });

  it('maps seat state: empty, occupied-unsecured and secured', () => {
    const dto = buildDto();
    const gondolas = dto.gondolas.slice();
    gondolas[0] = gondolaDto(0, 0, {
      seats: [
        { position: 0, occupiedKg: 0, restraint: 0 }, // empty regardless of restraint
        { position: 1, occupiedKg: 65, restraint: 1 }, // occupied, Closed -> unsecured
      ],
    });
    gondolas[1] = gondolaDto(0, 1, {
      seats: [
        { position: 'Left', occupiedKg: 80, restraint: 'Secured' },
        { position: 'Right', occupiedKg: 0, restraint: 'Open' },
      ],
    });

    const model = mapRideTelemetry({ ...dto, gondolas });

    expect(model.gondolas[0].seats).toEqual([
      { id: 1, state: 'empty', occupiedKg: 0 },
      { id: 2, state: 'occupied-unsecured', occupiedKg: 65 },
    ]);
    expect(model.gondolas[1].seats).toEqual([
      { id: 1, state: 'secured', occupiedKg: 80 },
      { id: 2, state: 'empty', occupiedKg: 0 },
    ]);
  });

  it('rounds occupiedKg to the nearest whole kilogram', () => {
    const dto = buildDto();
    const gondolas = dto.gondolas.slice();
    gondolas[0] = gondolaDto(0, 0, {
      seats: [
        { position: 0, occupiedKg: 65.6, restraint: 2 },
        { position: 1, occupiedKg: 0, restraint: 0 },
      ],
    });

    const model = mapRideTelemetry({ ...dto, gondolas });

    expect(model.gondolas[0].seats[0].occupiedKg).toBe(66);
  });

  it('reports gondolaBrakeEngaged true only when every gondola brake is Engaged (0)', () => {
    const allEngaged = mapRideTelemetry(buildDto());
    expect(allEngaged.gondolaBrakeEngaged).toBe(true);

    const dto = buildDto();
    const gondolas = dto.gondolas.slice();
    gondolas[0] = gondolaDto(0, 0, { brake: 1 }); // Released
    const oneReleased = mapRideTelemetry({ ...dto, gondolas });
    expect(oneReleased.gondolaBrakeEngaged).toBe(false);
  });

  it('accepts string brake values case-insensitively', () => {
    const dto = buildDto();
    const gondolas = dto.gondolas.slice();
    gondolas[0] = gondolaDto(0, 0, { brake: 'engaged' });
    gondolas.forEach((g, i) => {
      if (i !== 0) {
        gondolas[i] = { ...g, brake: 'Engaged' };
      }
    });

    const model = mapRideTelemetry({ ...dto, gondolas });
    expect(model.gondolaBrakeEngaged).toBe(true);
  });

  it('preserves the gondola and seat counts', () => {
    const model = mapRideTelemetry(buildDto());
    expect(model.gondolas).toHaveLength(GONDOLA_COUNT);
    model.gondolas.forEach((gondola) => expect(gondola.seats).toHaveLength(SEATS_PER_GONDOLA));
  });
});
