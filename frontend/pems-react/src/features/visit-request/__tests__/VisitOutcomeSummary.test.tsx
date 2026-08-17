import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { VisitOutcomeSummary } from '../components/v2/shared/VisitOutcomeSummary';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';
import { campusFixture } from './fixtures';

// jsdom's navigator.language is en-US → i18n initializes in EN; assertions use the EN strings.

/**
 * The backend's request-wide verdict, as it would compute it for a caller who sees every campus.
 * Mirrors VisitFormReadService.BuildRequestOutcome so a full-scope fixture stays self-consistent —
 * the whole point of the field is that the FRONTEND never derives it, so the fixture derives it here
 * once instead of every test restating it.
 */
const outcomeOf = (campuses: ResolvedVisitForm['campusVisits'], requestStatus: string) => {
  const n = (...s: string[]) => campuses.filter(c => s.includes(c.instanceStatus)).length;
  const accepted = n('ASSIGNED');
  const inProgress = n('BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT');
  const waiting = n('WAITING_CONTACT_CONFIRMATION', 'WAITING_REQUEST_APPROVAL');
  const rejected = n('REJECTED');
  const cancelled = n('CANCELLED');
  const closed = n('CLOSED');
  const total = campuses.length;
  const code =
    total === 0 ? 'NO_CAMPUS'
    : requestStatus === 'CANCELLED' ? 'ALL_CANCELLED'
    : rejected === total ? 'ALL_REJECTED'
    : waiting === total ? 'ALL_WAITING'
    : rejected + cancelled > 0 ? 'MIXED'
    : 'IN_PROGRESS';
  return { code, total, accepted, inProgress, waiting, rejected, cancelled, closed };
};

/** Full-request scope unless a test says otherwise: the verdict follows whatever campuses it set. */
const withOutcome = (f: ResolvedVisitForm, overrides: Partial<ResolvedVisitForm>): ResolvedVisitForm =>
  'requestOutcome' in overrides ? f : { ...f, requestOutcome: outcomeOf(f.campusVisits, f.requestStatus) };

const form = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => withOutcome({
  visitRequestId: 1,
  requestCode: 'VR-2026-001',
  rowVersion: 0,
  hasMixedCampusDetails: false,
  visitScope: 'SINGLE_CAMPUS',
  requestStatus: 'PENDING_APPROVAL',
  createdSource: 'PUBLIC',
  submittedAt: '2026-07-15T08:00:00',
  partnerId: null,
  cancelledByUserId: null,
  cancelledByName: null,
  cancelledAt: null,
  cancellationReason: null,
  registrant: {
    fullName: 'Người Đăng Ký', organization: 'ĐH ABC', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@x.vn', nationality: 'VN',
  },
  confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },

  requestOutcome: null,
  campusVisits: [campusFixture()],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
  ...overrides,
}, overrides);

const summary = () => screen.getByTestId('visit-outcome-summary');

describe('VisitOutcomeSummary', () => {
  it('says how many campuses are still deciding when nothing has been decided', () => {
    render(<VisitOutcomeSummary form={form({
      campusVisits: [
        campusFixture({ visitInstanceId: 1, instanceStatus: 'WAITING_REQUEST_APPROVAL' }),
        campusFixture({ visitInstanceId: 2, instanceStatus: 'WAITING_REQUEST_APPROVAL' }),
      ],
    })} />);

    expect(within(summary()).getByText('Waiting for 2 campuses to respond.')).toBeInTheDocument();
  });

  it('breaks a partial outcome down instead of hiding it behind one request status', () => {
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'PARTIALLY_APPROVED',
      campusVisits: [
        campusFixture({ visitInstanceId: 1, instanceStatus: 'ASSIGNED' }),
        campusFixture({ visitInstanceId: 2, instanceStatus: 'WAITING_REQUEST_APPROVAL' }),
      ],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('1 campus accepted');
    expect(text).toContain('1 campus awaiting response');
    // More than one outcome in play → point the reader at the per-campus cards.
    expect(text).toContain('See each campus below');
  });

  it('states the all-rejected case plainly and shows the latest in-scope decision', () => {
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'REJECTED',
      campusVisits: [
        campusFixture({
          visitInstanceId: 1, instanceStatus: 'REJECTED',
          decidedAt: '2026-07-20T09:30:00', decidedByName: 'Leader HN',
          decisionNote: 'Trùng lịch sự kiện tại cơ sở.',
        }),
      ],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('This request was rejected at every campus.');
    expect(text).toContain('Leader HN');
    expect(text).toContain('Trùng lịch sự kiện tại cơ sở.');
    // A single outcome needs no "look elsewhere" pointer.
    expect(text).not.toContain('See each campus below');
  });

  it('reports the latest rejection when several campuses rejected at different times', () => {
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'REJECTED',
      campusVisits: [
        campusFixture({
          visitInstanceId: 1, instanceStatus: 'REJECTED',
          decidedAt: '2026-07-18T09:30:00', decidedByName: 'Leader Cũ', decisionNote: 'Lý do cũ',
        }),
        campusFixture({
          visitInstanceId: 2, instanceStatus: 'REJECTED',
          decidedAt: '2026-07-20T09:30:00', decidedByName: 'Leader Mới', decisionNote: 'Lý do mới',
        }),
      ],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('Leader Mới');
    expect(text).not.toContain('Leader Cũ');
  });

  it('explains a cancelled request with who cancelled it, when and why', () => {
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'CANCELLED',
      cancelledByUserId: 42,
      cancelledByName: 'Kim Min Jae',
      cancelledAt: '2026-07-20T09:30:00',
      cancellationReason: 'Thay đổi lịch công tác của đoàn.',
      campusVisits: [campusFixture({ instanceStatus: 'CANCELLED' })],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('This request was cancelled.');
    expect(text).toContain('Kim Min Jae');
    expect(text).toContain('Thay đổi lịch công tác của đoàn.');
    expect(text).toContain('20/07/2026');
  });

  it('prefers the cancellation over an earlier campus rejection', () => {
    // A request rejected at one campus and later cancelled outright: the cancellation is the final
    // word, so leading with the older rejection would misstate where things ended.
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'CANCELLED',
      cancelledByName: 'Kim Min Jae',
      cancelledAt: '2026-07-22T09:30:00',
      cancellationReason: 'Hủy toàn bộ chuyến thăm.',
      campusVisits: [campusFixture({
        instanceStatus: 'REJECTED',
        decidedAt: '2026-07-20T09:30:00', decidedByName: 'Leader HN', decisionNote: 'Trùng lịch',
      })],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('Kim Min Jae');
    expect(text).not.toContain('Leader HN');
  });

  it('says nothing about cancellation when the request was not cancelled', () => {
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'APPROVED',
      campusVisits: [campusFixture({ instanceStatus: 'ASSIGNED' })],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).not.toContain('Cancelled by');
    expect(text).not.toContain('cancelled');
  });

  it('counts ONLY the campuses the backend returned, never a request-level total', () => {
    // A Staff Leader scoped to one campus of a three-campus request. The summary must not imply the
    // other two exist, whatever the request-level status says — and the backend withholds the
    // request-wide verdict from them precisely so that it cannot.
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'PARTIALLY_APPROVED',
      visitScope: 'MULTI_CAMPUS',
      hasMixedCampusDetails: true,
      requestOutcome: null,
      campusVisits: [campusFixture({ visitInstanceId: 7, instanceStatus: 'ASSIGNED' })],
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('1 campus accepted');
    expect(text).not.toMatch(/[23] campuses?/);
    expect(text).not.toContain('rejected');
    expect(text).not.toContain('awaiting');
  });

  // ── The scoped reader must never be handed a request-level verdict ──────────────────────────
  // This is the defect: Campus A REJECTED, Campus B still deciding, and Staff Leader A sees only A.
  // Their whole collection is rejected, so a count over it said "rejected at every campus" — about a
  // request that was very much still alive at the campus they cannot see.

  it('names the campus that rejected, and does NOT speak for the request, when scope is partial', () => {
    render(<VisitOutcomeSummary form={form({
      // The request is PARTIALLY through: campus B is still WAITING. The caller cannot see B.
      requestStatus: 'PENDING_APPROVAL',
      visitScope: 'MULTI_CAMPUS',
      requestOutcome: null, // backend withheld it: this caller does not see every campus
      campusVisits: [campusFixture({
        visitInstanceId: 1, campusName: 'FPTU Hà Nội', instanceStatus: 'REJECTED',
        decidedAt: '2026-07-20T09:30:00', decidedByName: 'Leader HN', decisionNote: 'Trùng lịch',
      })],
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('Campus FPTU Hà Nội rejected this request.');
    expect(text).not.toContain('This request was rejected at every campus.');
    // …and the reader is told the sentence covers their scope, not the request.
    expect(screen.getByTestId('visit-outcome-scope-note')).toBeInTheDocument();
    // The in-scope decision itself is still shown — it is their own campus's.
    expect(text).toContain('Leader HN');
  });

  it('still states the all-rejected verdict when the BACKEND says every campus rejected', () => {
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'REJECTED',
      visitScope: 'MULTI_CAMPUS',
      requestOutcome: { code: 'ALL_REJECTED', total: 2, accepted: 0, inProgress: 0, waiting: 0, rejected: 2, cancelled: 0, closed: 0 },
      campusVisits: [
        campusFixture({ visitInstanceId: 1, instanceStatus: 'REJECTED' }),
        campusFixture({ visitInstanceId: 2, instanceStatus: 'REJECTED' }),
      ],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('This request was rejected at every campus.');
    // A full-scope reader is not told their view is partial.
    expect(screen.queryByTestId('visit-outcome-scope-note')).toBeNull();
  });

  it('takes its numbers from the backend verdict, not from the campuses it was sent', () => {
    // Two campuses in scope, but the request has five. The sentence must be about the five.
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'PARTIALLY_APPROVED',
      visitScope: 'MULTI_CAMPUS',
      requestOutcome: { code: 'MIXED', total: 5, accepted: 3, inProgress: 0, waiting: 1, rejected: 1, cancelled: 0, closed: 0 },
      campusVisits: [
        campusFixture({ visitInstanceId: 1, instanceStatus: 'ASSIGNED' }),
        campusFixture({ visitInstanceId: 2, instanceStatus: 'ASSIGNED' }),
      ],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('3 campuses accepted');
    expect(text).toContain('1 campus rejected');
    expect(text).not.toContain('2 campuses accepted');
  });

  it('withholds the request-level cancellation detail from a scoped reader', () => {
    // Who cancelled the whole request, and why, is a request-level fact.
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'CANCELLED',
      requestOutcome: null,
      cancelledByName: 'Kim Min Jae',
      cancelledAt: '2026-07-20T09:30:00',
      cancellationReason: 'Thay đổi lịch công tác của đoàn.',
      campusVisits: [campusFixture({ campusName: 'FPTU Hà Nội', instanceStatus: 'CANCELLED' })],
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('Campus FPTU Hà Nội cancelled this visit.');
    expect(text).not.toContain('This request was cancelled.');
    expect(text).not.toContain('Kim Min Jae');
    expect(text).not.toContain('Thay đổi lịch công tác của đoàn.');
  });

  it('degrades to a plain message when the caller may see no campus at all', () => {
    render(<VisitOutcomeSummary form={form({ campusVisits: [] })} />);
    expect(within(summary()).getByText('No campus in your viewing scope.')).toBeInTheDocument();
  });

  it('never renders a raw status enum', () => {
    render(<VisitOutcomeSummary form={form({
      campusVisits: [
        campusFixture({ visitInstanceId: 1, instanceStatus: 'DURING_VISIT' }),
        campusFixture({ visitInstanceId: 2, instanceStatus: 'CLOSED' }),
      ],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).not.toContain('DURING_VISIT');
    expect(text).not.toContain('CLOSED');
    expect(text).toContain('1 campus in progress');
    expect(text).toContain('1 campus closed');
  });
});
