import { pluralize, summaryText, toQueueStatus } from './queue.models';

describe('queue.models', () => {
  it('maps a raw DTO onto the normalized queue status', () => {
    const queue = toQueueStatus({
      rideId: '11111111-1111-1111-1111-111111111111',
      groupCount: 12,
      peopleWaiting: 34,
      singleRidersWaiting: 5,
    });

    expect(queue).toEqual({ groupCount: 12, peopleWaiting: 34, singleRidersWaiting: 5 });
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

    it('summarizes groups, people and single riders, pluralized', () => {
      expect(summaryText({ groupCount: 12, peopleWaiting: 34, singleRidersWaiting: 5 })).toBe(
        '12 groups queued, 34 people waiting, 5 single riders.',
      );
      expect(summaryText({ groupCount: 1, peopleWaiting: 1, singleRidersWaiting: 1 })).toBe(
        '1 group queued, 1 person waiting, 1 single rider.',
      );
      expect(summaryText({ groupCount: 0, peopleWaiting: 0, singleRidersWaiting: 0 })).toBe(
        '0 groups queued, 0 people waiting, 0 single riders.',
      );
    });
  });
});
