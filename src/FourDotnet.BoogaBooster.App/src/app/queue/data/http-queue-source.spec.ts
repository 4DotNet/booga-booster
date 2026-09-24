import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { QueueStatusDto } from '../models/queue.models';
import { HttpQueueSource } from './http-queue-source';

const RIDE_ID = '11111111-1111-1111-1111-111111111111';
const QUEUE_URL = `/api/rides/${RIDE_ID}/queue`;

const DTO: QueueStatusDto = {
  rideId: RIDE_ID,
  groupCount: 12,
  peopleWaiting: 34,
  averageHappiness: 0.72,
};

const POLL_MS = 5000;

describe('HttpQueueSource', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('loads the current queue status on init and sets status ready', () => {
    const source = TestBed.inject(HttpQueueSource);

    http.expectOne(QUEUE_URL).flush(DTO);

    expect(source.queue()?.groupCount).toBe(12);
    expect(source.queue()?.peopleWaiting).toBe(34);
    expect(source.queue()?.averageHappiness).toBe(0.72);
    expect(source.status()).toBe('ready');
  });

  it('re-fetches on the polling interval', () => {
    TestBed.inject(HttpQueueSource);
    http.expectOne(QUEUE_URL).flush(DTO); // init

    vi.advanceTimersByTime(POLL_MS);
    http.expectOne(QUEUE_URL).flush(DTO); // polling re-fetch
  });

  it('sets status error when the fetch fails, then recovers on the next poll', () => {
    const source = TestBed.inject(HttpQueueSource);

    http.expectOne(QUEUE_URL).error(new ProgressEvent('error'));
    expect(source.status()).toBe('error');

    vi.advanceTimersByTime(POLL_MS);
    http.expectOne(QUEUE_URL).flush(DTO);
    expect(source.status()).toBe('ready');
  });

  it('refresh() triggers an immediate re-fetch', () => {
    const source = TestBed.inject(HttpQueueSource);
    http.expectOne(QUEUE_URL).flush(DTO); // init

    source.refresh();
    http.expectOne(QUEUE_URL).flush(DTO);
  });

  it('stops polling once the injector is destroyed', () => {
    TestBed.inject(HttpQueueSource);
    http.expectOne(QUEUE_URL).flush(DTO);
    expect(vi.getTimerCount()).toBeGreaterThan(0);

    TestBed.resetTestingModule(); // destroys the injector → takeUntilDestroyed unsubscribes

    expect(vi.getTimerCount()).toBe(0);
  });
});
