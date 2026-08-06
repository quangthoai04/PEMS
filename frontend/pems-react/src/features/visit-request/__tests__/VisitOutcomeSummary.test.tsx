import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { VisitOutcomeSummary } from '../components/v2/shared/VisitOutcomeSummary';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';
import { campusFixture } from './fixtures';

// jsdom's navigator.language is en-US → i18n initializes in EN; assertions use the EN strings.

const form = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => ({
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
  campusVisits: [campusFixture()],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
  ...overrides,
});

const summary = () => screen.getByTestId('visit-outcome-summary');

describe('VisitOutcomeSummary', () => {
  it('says how many campuses are still deciding when nothing has been decided', () => {
    render(<VisitOutcomeSummary form={form({
      campusVisits: [
        campusFixture({ visitInstanceId: 1, instanceStatus: 'WAITING_REQUEST_APPROVAL' }),
        campusFixture({ visitInstanceId: 2, instanceStatus: 'WAITING_REQUEST_APPROVAL' }),
      ],
    })} />);

    expect(within(summary()).getByText('Waiting for 2 campus(es) to respond.')).toBeInTheDocument();
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
    expect(text).toContain('1 campus(es) accepted');
    expect(text).toContain('1 campus(es) awaiting response');
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
    // other two exist, whatever the request-level status says.
    render(<VisitOutcomeSummary form={form({
      requestStatus: 'PARTIALLY_APPROVED',
      visitScope: 'MULTI_CAMPUS',
      hasMixedCampusDetails: true,
      campusVisits: [campusFixture({ visitInstanceId: 7, instanceStatus: 'ASSIGNED' })],
    })} />);

    const text = summary().textContent ?? '';
    expect(text).toContain('1 campus(es) accepted');
    expect(text).not.toMatch(/[23] campus\(es\)/);
    expect(text).not.toContain('rejected');
    expect(text).not.toContain('awaiting');
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
    expect(text).toContain('1 campus(es) in progress');
    expect(text).toContain('1 campus(es) closed');
  });
});
