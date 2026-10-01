import {
  ENTRY_BOOTH,
  EXIT_BOOTH,
  EXIT_WALK_DISTANCE,
  Figure,
  HEAD_GREEN,
  HEAD_LEGO_YELLOW,
  HEAD_RED,
  PUDDLE_LIFETIME_S,
  QUEUE_CAPACITY,
  SHIRT_PALETTE,
  WALK_SPEED,
  advanceFigures,
  agePuddles,
  assignQueueTargets,
  figureId,
  gondolaSceneIndex,
  headColor,
  offloadReaction,
  overflowCount,
  puddlePosition,
  puddleScale,
  queueSlotPosition,
  queueSpawnPosition,
  seatOffset,
  shirtColor,
  spawnExitWalkers,
  spawnPuddles,
  walkStep,
} from './park-scene';

const guest = (guestNumber: number, happiness = 60, nausea = 0) => ({
  guestNumber,
  happiness,
  nausea,
});

describe('park-scene queue layout', () => {
  it('puts the front slot nearest the entry booth', () => {
    const distance = (i: number) => {
      const p = queueSlotPosition(i);
      return Math.hypot(p.x - ENTRY_BOOTH.x, p.z - ENTRY_BOOTH.z);
    };

    expect(distance(0)).toBeLessThan(distance(1));
    expect(distance(0)).toBeLessThan(distance(59));
    expect(QUEUE_CAPACITY).toBe(60);
  });

  it('spaces neighbours 0.6 m apart and rows 1 m apart', () => {
    const a = queueSlotPosition(0);
    const b = queueSlotPosition(1);
    expect(Math.hypot(b.x - a.x, b.z - a.z)).toBeCloseTo(0.6, 9);

    const rowEnd = queueSlotPosition(14);
    const nextRowStart = queueSlotPosition(15);
    expect(nextRowStart.x - rowEnd.x).toBeCloseTo(1, 9);
    expect(nextRowStart.z).toBeCloseTo(rowEnd.z, 9);
  });

  it('serpentines: odd rows run back', () => {
    expect(queueSlotPosition(15).z).toBeGreaterThan(queueSlotPosition(16).z);
  });

  it('gives all 60 slots distinct positions', () => {
    const keys = new Set(
      Array.from({ length: 60 }, (_, i) => queueSlotPosition(i)).map((p) => `${p.x}|${p.z}`),
    );
    expect(keys.size).toBe(60);
  });

  it('counts overflow beyond 60', () => {
    expect(overflowCount(0)).toBe(0);
    expect(overflowCount(12)).toBe(0);
    expect(overflowCount(60)).toBe(0);
    expect(overflowCount(87)).toBe(27);
  });

  it('keeps both booths outside the 9 m sweep and 90 degrees apart', () => {
    expect(Math.hypot(ENTRY_BOOTH.x, ENTRY_BOOTH.z)).toBeGreaterThan(9);
    expect(Math.hypot(EXIT_BOOTH.x, EXIT_BOOTH.z)).toBeGreaterThan(9);
    expect(ENTRY_BOOTH.x * EXIT_BOOTH.x + ENTRY_BOOTH.z * EXIT_BOOTH.z).toBe(0);
  });
});

describe('park-scene colours', () => {
  it('picks the shirt from the palette by guest number mod 8, stably', () => {
    expect(shirtColor(3)).toBe(SHIRT_PALETTE[3]);
    expect(shirtColor(11)).toBe(SHIRT_PALETTE[3]);
    expect(shirtColor(11)).toBe(shirtColor(11));
    expect(shirtColor(-1)).toBe(SHIRT_PALETTE[7]);
  });

  it('makes sick heads green, even when also mad', () => {
    expect(headColor({ happiness: 20, nausea: 80 })).toBe(HEAD_GREEN);
    expect(headColor({ happiness: 90, nausea: 70 })).toBe(HEAD_GREEN);
  });

  it('makes mad heads red and content heads LEGO yellow', () => {
    expect(headColor({ happiness: 25, nausea: 0 })).toBe(HEAD_RED);
    expect(headColor({ happiness: 30, nausea: 69 })).toBe(HEAD_LEGO_YELLOW);
    expect(headColor({ happiness: 60, nausea: 10 })).toBe(HEAD_LEGO_YELLOW);
    expect(HEAD_LEGO_YELLOW).toBe(0xf2cd37);
  });
});

describe('park-scene walking', () => {
  it('steps towards the target without overshooting', () => {
    expect(walkStep({ x: 0, z: 0 }, { x: 10, z: 0 }, 1.4)).toEqual({ x: 1.4, z: 0 });
    expect(walkStep({ x: 0, z: 0 }, { x: 1, z: 0 }, 1.4)).toEqual({ x: 1, z: 0 });
    const diagonal = walkStep({ x: 0, z: 0 }, { x: 3, z: 4 }, 1);
    expect(Math.hypot(diagonal.x, diagonal.z)).toBeCloseTo(1, 9);
  });

  it('assigns queue order to slots and spawns new guests at the far end', () => {
    const figures = assignQueueTargets([], [guest(1), guest(2)], false);

    expect(figures.map((f) => f.target)).toEqual([queueSlotPosition(0), queueSlotPosition(1)]);
    expect(figures[0].position).toEqual(queueSpawnPosition());
    expect(figures.every((f) => !f.leaving)).toBe(true);
  });

  it('keeps known guests walking from where they are', () => {
    const first = assignQueueTargets([], [guest(1), guest(2)], false);
    const moved = advanceFigures(first, 1);
    const next = assignQueueTargets(moved, [guest(2), guest(3)], false);

    const two = next.find((f) => f.guestNumber === 2)!;
    expect(two.position).toEqual(moved.find((f) => f.guestNumber === 2)!.position);
    expect(two.target).toEqual(queueSlotPosition(0));
  });

  it('sends guests who left the queue to the entry booth and removes them on arrival', () => {
    const first = assignQueueTargets([], [guest(1), guest(2)], true);
    const next = assignQueueTargets(first, [guest(2)], false);
    const boarded = next.find((f) => f.guestNumber === 1)!;

    expect(boarded.leaving).toBe(true);
    expect(boarded.target).toEqual(ENTRY_BOOTH);

    let figures: Figure[] = next;
    for (let i = 0; i < 100; i++) {
      figures = advanceFigures(figures, 1);
    }
    expect(figures.map((f) => f.guestNumber)).toEqual([2]);
  });

  it('only draws the first 60 guests', () => {
    const queue = Array.from({ length: 87 }, (_, i) => guest(i));

    expect(assignQueueTargets([], queue, false)).toHaveLength(60);
  });

  it('snaps everyone into place under reduced motion', () => {
    const first = assignQueueTargets([], [guest(1), guest(2)], true);
    expect(first.map((f) => f.position)).toEqual(first.map((f) => f.target));

    const next = assignQueueTargets(first, [guest(2)], true);
    expect(advanceFigures(next, 0)).toHaveLength(1);
  });

  it('walks at 1.4 m/s', () => {
    const [figure] = assignQueueTargets([], [guest(1)], false);
    const [after] = advanceFigures([figure], 1);
    const walked = Math.hypot(
      after.position.x - figure.position.x,
      after.position.z - figure.position.z,
    );

    expect(walked).toBeCloseTo(WALK_SPEED, 9);
  });
});

describe('park-scene exit and puddles', () => {
  it('spawns exit walkers at the exit booth walking 10 m away, then removes them', () => {
    const walkers = spawnExitWalkers([guest(1), guest(2)], false);

    for (const walker of walkers) {
      expect(walker.leaving).toBe(true);
      expect(walker.position.z).toBe(EXIT_BOOTH.z);
      expect(walker.target.z - walker.position.z).toBeCloseTo(EXIT_WALK_DISTANCE, 9);
    }
    let figures: Figure[] = walkers;
    for (let i = 0; i < 20; i++) {
      figures = advanceFigures(figures, 1);
    }
    expect(figures).toEqual([]);
  });

  it('places puddles deterministically near the exit path', () => {
    const a = puddlePosition(42);

    expect(puddlePosition(42)).toEqual(a);
    expect(puddlePosition(43)).not.toEqual(a);
    expect(Math.abs(a.x - EXIT_BOOTH.x)).toBeLessThanOrEqual(1);
    expect(a.z).toBeGreaterThan(EXIT_BOOTH.z);
    expect(a.z).toBeLessThan(EXIT_BOOTH.z + EXIT_WALK_DISTANCE);
  });

  it('spawns puddles only for riders with nausea 70 or more', () => {
    const puddles = spawnPuddles([guest(1, 50, 69), guest(2, 50, 70), guest(3, 50, 85)]);

    expect(puddles.map((p) => p.guestNumber)).toEqual([2, 3]);
    expect(puddles.every((p) => p.age === 0)).toBe(true);
  });

  it('falls back to the rider index for hand-boarded riders without a guest number', () => {
    const riders = [
      { guestNumber: null, happiness: 50, nausea: 90 },
      { guestNumber: null, happiness: 50, nausea: 10 },
      { guestNumber: 7, happiness: 50, nausea: 95 },
    ];

    expect(figureId(null, 0)).toBe(-1);
    expect(figureId(7, 0)).toBe(7);
    expect(spawnExitWalkers(riders, false).map((w) => w.guestNumber)).toEqual([-1, -2, 7]);
    const puddles = spawnPuddles(riders);
    expect(puddles.map((p) => p.guestNumber)).toEqual([-1, 7]);
    expect(puddles[0].position).toEqual(puddlePosition(-1));
  });

  it('shrinks puddles to nothing over 20 s and removes them', () => {
    expect(puddleScale(0)).toBe(1);
    expect(puddleScale(10)).toBeCloseTo(0.5, 9);
    expect(puddleScale(PUDDLE_LIFETIME_S)).toBe(0);

    const puddles = spawnPuddles([guest(1, 50, 90)]);
    expect(agePuddles(puddles, 10)).toHaveLength(1);
    expect(agePuddles(agePuddles(puddles, 10), 10)).toEqual([]);
  });
});

describe('park-scene offload detection and seating', () => {
  it('ignores the first counter seen and reacts only to increases', () => {
    const first = offloadReaction(null, 5);
    expect(first).toEqual({ spawn: false, seen: 5 });
    expect(offloadReaction(first.seen, 5).spawn).toBe(false);
    expect(offloadReaction(5, 6)).toEqual({ spawn: true, seen: 6 });
    expect(offloadReaction(6, 0)).toEqual({ spawn: false, seen: 0 });
  });

  it('maps telemetry gondolas onto scene hub/arm order and offsets seats', () => {
    expect(gondolaSceneIndex(1)).toBe(0);
    expect(gondolaSceneIndex(6)).toBe(5); // hub 1, index 1 -> k 1, j 1
    expect(gondolaSceneIndex(16)).toBe(15);
    expect(seatOffset(0).x).toBeLessThan(0);
    expect(seatOffset(1).x).toBeGreaterThan(0);
  });
});
