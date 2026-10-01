/** Raw wire shape of one queued guest. Mood fields are absent on older backends. */
export interface QueuePersonDto {
  readonly number: number;
  readonly name?: string;
  readonly weightInKilograms?: number;
  readonly happiness?: number;
  readonly preferredG?: number;
  readonly nausea?: number;
}

/** Raw wire shape of one queued group. */
export interface QueuedGroupDto {
  readonly groupId: string;
  readonly people?: readonly QueuePersonDto[];
}

/** Raw wire shape returned by `GET /rides/{rideId}/queue`. */
export interface QueueStatusDto {
  readonly rideId: string;
  readonly groupCount: number;
  readonly peopleWaiting: number;
  readonly groups?: readonly QueuedGroupDto[];
}

/** A guest waiting in the queue; `guestNumber` is shared with the seated rider. */
export interface QueueGuest {
  readonly guestNumber: number;
  readonly name: string;
  readonly weightKg: number;
  /** Happiness 0–100, with the queue-wait decay already applied by the backend. */
  readonly happiness: number;
  readonly preferredG: number;
  readonly nausea: number;
}

/** A group of guests that will board together. */
export interface QueueGroup {
  readonly groupId: string;
  readonly guests: readonly QueueGuest[];
}

/** Normalized queue status used across the UI. */
export interface QueueStatus {
  readonly groupCount: number;
  readonly peopleWaiting: number;
  /** Groups in queue order, front first. */
  readonly groups: readonly QueueGroup[];
}

/** Projects the raw server DTO onto the normalized {@link QueueStatus}. */
export function toQueueStatus(dto: QueueStatusDto): QueueStatus {
  return {
    groupCount: dto.groupCount,
    peopleWaiting: dto.peopleWaiting,
    groups: (dto.groups ?? []).map((group) => ({
      groupId: group.groupId,
      guests: (group.people ?? []).map((person) => ({
        guestNumber: person.number,
        name: person.name ?? '',
        weightKg: person.weightInKilograms ?? 0,
        happiness: person.happiness ?? 0,
        preferredG: person.preferredG ?? 0,
        nausea: person.nausea ?? 0,
      })),
    })),
  };
}

/** Every queued guest in queue order (front of the queue first). */
export function queuedGuests(queue: QueueStatus | null): readonly QueueGuest[] {
  return queue?.groups.flatMap((group) => group.guests) ?? [];
}

/** Picks the singular or plural noun for a count (irregular plurals via `plural`). */
export function pluralize(
  count: number,
  singular: string,
  plural: string = `${singular}s`,
): string {
  return count === 1 ? singular : plural;
}

/** A one-line, worded summary of the queue for a live region. */
export function summaryText(queue: QueueStatus | null): string {
  if (!queue) {
    return 'Queue status is unavailable.';
  }
  const groups = `${queue.groupCount} ${pluralize(queue.groupCount, 'group')} queued`;
  const people = `${queue.peopleWaiting} ${pluralize(queue.peopleWaiting, 'person', 'people')} waiting`;
  return `${groups}, ${people}.`;
}
