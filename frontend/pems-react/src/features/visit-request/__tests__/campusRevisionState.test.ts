import { describe, expect, it } from 'vitest';
import { resolveCampusRevisionState } from '../components/v2/shared/campusRevisionState';

/**
 * The create service writes approvalRevision = 1 on the FIRST detail row, so that number is 1 on a
 * campus nobody has looked at yet. Every case below therefore passes approvalRevision: 1 — the point
 * is that the wording must come from the lifecycle, not from the number.
 */
describe('resolveCampusRevisionState', () => {
  it('says "not approved yet" while the campus is still waiting', () => {
    const state = resolveCampusRevisionState({
      instanceStatus: 'WAITING_REQUEST_APPROVAL', formRevision: 1, approvalRevision: 1, decidedAt: null,
    });

    expect(state.tone).toBe('waiting');
    expect(state.headlineKey).toBe('visitRequestV2:revision.current');
    expect(state.noteKey).toBe('visitRequestV2:revision.notApprovedYet');
  });

  it('does not treat a decided-looking status as approved without a recorded decision', () => {
    // Defence in depth: status and decidedAt should agree, but if they ever disagree the safe reading
    // is "not decided" rather than announcing an approval that was never recorded.
    const state = resolveCampusRevisionState({
      instanceStatus: 'ASSIGNED', formRevision: 2, approvalRevision: 1, decidedAt: null,
    });

    expect(state.noteKey).toBe('visitRequestV2:revision.notApprovedYet');
  });

  it('reports the applied content and approval round once the campus is decided', () => {
    const state = resolveCampusRevisionState({
      instanceStatus: 'ASSIGNED', formRevision: 2, approvalRevision: 1, decidedAt: '2026-07-20T09:00:00',
    });

    expect(state.tone).toBe('active');
    expect(state.headlineKey).toBe('visitRequestV2:revision.applied');
    expect(state.noteKey).toBe('visitRequestV2:revision.approvedAt');
    expect(state.values).toMatchObject({ form: 2, approval: 1 });
  });

  it.each(['BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT'])(
    'treats %s as live content in force',
    status => {
      const state = resolveCampusRevisionState({
        instanceStatus: status, formRevision: 3, approvalRevision: 2, decidedAt: '2026-07-20T09:00:00',
      });
      expect(state.tone).toBe('active');
      expect(state.headlineKey).toBe('visitRequestV2:revision.applied');
    },
  );

  it('keeps a pending proposal separate from the content in force', () => {
    const state = resolveCampusRevisionState({
      instanceStatus: 'ASSIGNED', formRevision: 2, approvalRevision: 1,
      decidedAt: '2026-07-20T09:00:00', activeAmendmentNo: 3,
    });

    // The headline still describes the ACTIVE content — a proposal is never presented as in force.
    expect(state.headlineKey).toBe('visitRequestV2:revision.applied');
    expect(state.noteKey).toBe('visitRequestV2:revision.amendmentPending');
    expect(state.values.amendmentNo).toBe(3);
  });

  it('describes a rejected campus as rejected, not as approved at some round', () => {
    const state = resolveCampusRevisionState({
      instanceStatus: 'REJECTED', formRevision: 1, approvalRevision: 1, decidedAt: '2026-07-20T09:00:00',
    });

    expect(state.tone).toBe('rejected');
    expect(state.headlineKey).toBe('visitRequestV2:revision.rejected');
    expect(state.noteKey).toBeNull();
  });

  it('states the cancellation alongside the content version', () => {
    const state = resolveCampusRevisionState({
      instanceStatus: 'CANCELLED', formRevision: 2, approvalRevision: 1, decidedAt: '2026-07-20T09:00:00',
    });

    expect(state.tone).toBe('cancelled');
    expect(state.noteKey).toBe('visitRequestV2:revision.cancelledNote');
  });

  it('states a closed visit rather than leaving it looking live', () => {
    const state = resolveCampusRevisionState({
      instanceStatus: 'CLOSED', formRevision: 2, approvalRevision: 2, decidedAt: '2026-07-20T09:00:00',
    });

    expect(state.tone).toBe('closed');
    expect(state.noteKey).toBe('visitRequestV2:revision.closedNote');
  });

  it('matches the status case-insensitively', () => {
    const state = resolveCampusRevisionState({
      instanceStatus: '  rejected ', formRevision: 1, approvalRevision: 1, decidedAt: '2026-07-20T09:00:00',
    });
    expect(state.tone).toBe('rejected');
  });
});
