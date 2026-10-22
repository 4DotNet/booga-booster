import { pluralize, summaryText, toQueueStatus } from './queue.models';

describe('queue.models', () => {
  it('maps a raw DTO onto the normalized queue status', () => {
    const queue = toQueueStatus({
      rideId: '11111111-1111-1111-1111-111111111111',
      groupCount: 12,
      peopleWaiting: 34,
    });

    expect(queue).toEqual({ groupCount: 12, peopleWaiting: 34, averageHappiness: null });
  });

  describe('averageHappiness', () => {
    const base = { rideId: 'r', groupCount: 1, peopleWaiting: 2 };

    it('passes a present average through', () => {
      expect(toQueueStatus({ ...base, averageHappiness: 72.5 }).averageHappiness).toBe(72.5);
    });

    it('keeps an explicit null for an empty queue', () => {
      expect(toQueueStatus({ ...base, averageHappiness: null }).averageHappiness).toBeNull();
    });

    it('treats an absent field (older backend) as null', () => {
      expect(toQueueStatus(base).averageHappiness).toBeNull();
    });
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
      expect(summaryText({ groupCount: 12, peopleWaiting: 34, averageHappiness: null })).toBe(
        '12 groups queued, 34 people waiting.',
      );
      expect(summaryText({ groupCount: 1, peopleWaiting: 1, averageHappiness: null })).toBe(
        '1 group queued, 1 person waiting.',
      );
    });
  });
});
