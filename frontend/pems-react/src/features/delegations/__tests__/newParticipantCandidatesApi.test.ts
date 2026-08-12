import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../../shared/api/httpClient', () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import { delegationsApi } from '../api/delegationsApi';

const get = httpClient.get as unknown as ReturnType<typeof vi.fn>;

/**
 * "Đồng bộ người mới" after deleting somebody.
 *
 * The deleted row is still in the database until the biên bản is saved, so the backend keeps counting
 * that person as already present and offers nothing back — the sync button appeared to do nothing.
 * The open editor therefore names the rows it has removed. They have to travel as REPEATED query keys:
 * axios' default array form (`ids[]=1`) is not what ASP.NET Core binds an array parameter from, so
 * getting this wrong fails silently — a request that succeeds and ignores the ids.
 */
describe('delegationsApi.minutes.newParticipantCandidates', () => {
  beforeEach(() => get.mockReset());

  const url = API_ENDPOINTS.meetingMinutes.newParticipantCandidates(12);

  it('sends no query string at all when nothing was removed', async () => {
    get.mockResolvedValueOnce({ data: [] });

    await delegationsApi.minutes.newParticipantCandidates(12);

    expect(get).toHaveBeenCalledWith(url, undefined);
  });

  it('repeats the key once per removed row', async () => {
    get.mockResolvedValueOnce({ data: [] });

    await delegationsApi.minutes.newParticipantCandidates(12, [4, 9]);

    const params = get.mock.calls[0][1].params as URLSearchParams;
    expect(params.toString()).toBe(
      'ignoredExistingParticipantIds=4&ignoredExistingParticipantIds=9');
  });

  it('drops ids that were never persisted, so a draft-only row is not sent as a real one', async () => {
    get.mockResolvedValueOnce({ data: [] });

    // 0 is the id a row carries before it is saved; NaN guards a bad caller.
    await delegationsApi.minutes.newParticipantCandidates(12, [0, Number.NaN, 5]);

    const params = get.mock.calls[0][1].params as URLSearchParams;
    expect(params.getAll('ignoredExistingParticipantIds')).toEqual(['5']);
  });

  it('returns the candidate list unchanged', async () => {
    get.mockResolvedValueOnce({ data: [{ minuteParticipantId: 0, userId: 7, guestMemberId: null }] });

    const rows = await delegationsApi.minutes.newParticipantCandidates(12, [4]);

    expect(rows).toHaveLength(1);
    expect(rows[0].userId).toBe(7);
  });
});
