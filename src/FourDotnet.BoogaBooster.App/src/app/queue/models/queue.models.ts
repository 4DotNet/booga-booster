/** Raw wire shape returned by `GET /rides/{rideId}/queue`. */
export interface QueueStatusDto {
  readonly rideId: string;
  readonly groupCount: number;
  readonly peopleWaiting: number;
  /** Average happiness (0-100) of everyone queued; `null`/absent when the queue is empty. */
  readonly averageHappiness?: number | null;
}

/** Normalized queue status used across the UI. */
export interface QueueStatus {
  readonly groupCount: number;
  readonly peopleWaiting: number;
  /** Average happiness (0-100) of everyone queued; `null` when there is no data. */
  readonly averageHappiness: number | null;
}

/** Projects the raw server DTO onto the normalized {@link QueueStatus}. */
export function toQueueStatus(dto: QueueStatusDto): QueueStatus {
  return {
    groupCount: dto.groupCount,
    peopleWaiting: dto.peopleWaiting,
    averageHappiness: dto.averageHappiness ?? null,
  };
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
