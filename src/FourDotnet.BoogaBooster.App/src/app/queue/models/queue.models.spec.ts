import { pluralize, queuedGuests, summaryText, toQueueStatus } from './queue.models';

describe('queue.models', () => {
  it('maps a raw DTO onto the normalized queue status', () => {
    const queue = toQueueStatus({
      rideId: '11111111-1111-1111-1111-111111111111',
      groupCount: 12,
      peopleWaiting: 34,
    });

    expect(queue).toEqual({ groupCount: 12, peopleWaiting: 34, groups: [] });
  });

  it('maps groups and guests, renaming number to guestNumber', () => {
    const queue = toQueueStatus({
      rideId: 'r',
      groupCount: 1,
      peopleWaiting: 2,
      groups: [
        {
          groupId: 'g1',
          people: [
            {
              number: 5,
              name: 'Ann',
              weightInKilograms: 60,
              happiness: 80,
              preferredG: 3,
              nausea: 1,
            },
            { number: 6 },
          ],
        },
      ],
    });

    expect(queue.groups[0].guests[0]).toEqual({
      guestNumber: 5,
      name: 'Ann',
      weightKg: 60,
      happiness: 80,
      preferredG: 3,
      nausea: 1,
    });
    expect(queue.groups[0].guests[1]).toEqual({
      guestNumber: 6,
      name: '',
      weightKg: 0,
      happiness: 0,
      preferredG: 0,
      nausea: 0,
    });
  });

  it('flattens guests in queue order', () => {
    const queue = toQueueStatus({
      rideId: 'r',
      groupCount: 2,
      peopleWaiting: 3,
      groups: [
        { groupId: 'a', people: [{ number: 1 }, { number: 2 }] },
        { groupId: 'b', people: [{ number: 3 }] },
      ],
    });

    expect(queuedGuests(queue).map((g) => g.guestNumber)).toEqual([1, 2, 3]);
    expect(queuedGuests(null)).toEqual([]);
  });

  describe('pluralize', () => {
    it('uses the singular for a count of one', () => {
      expect(pluralize(1, 'group')).toBe('group');
      expect(pluralize(1, 'person', 'people')).toBe('person');
    });

    it('uses the plural otherwise', () => {
      expect(pluralize(0, 'group')).toBe('groups');
      expect(pluralize(12, 'group')).toBe('groups');
      expect(pluralize(34, 'person', 'people')).toBe('people');
    });
  });

  describe('summaryText', () => {
    it('reports unavailable with no queue status', () => {
      expect(summaryText(null)).toBe('Queue status is unavailable.');
    });

    it('summarizes groups and people, pluralized', () => {
      expect(summaryText({ groupCount: 12, peopleWaiting: 34, groups: [] })).toBe(
        '12 groups queued, 34 people waiting.',
      );
      expect(summaryText({ groupCount: 1, peopleWaiting: 1, groups: [] })).toBe(
        '1 group queued, 1 person waiting.',
      );
    });
  });
});
