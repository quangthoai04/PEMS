import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';
import { VisitRequestV2SuccessPanel } from '../components/v2/VisitRequestV2SuccessPanel';
import { VisitCreateUncertainPanel } from '../components/v2/VisitCreateUncertainPanel';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import type { V2CreateResponse } from '../api/visitRequestV2Api';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [{ campusId: 1, campusCode: 'HN', campusName: 'FPT Hà Nội', city: null }],
    loading: false,
  }),
}));

/**
 * Plan §16 items 8–12. The receipt is a SCREEN, not a toast: the request code is the only handle
 * the user has on what they just filed, and a notification that disappears after four seconds is
 * not somewhere to keep it. Before this it also had no way to open the request it had just named.
 */

const values = (): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'VN',
  },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits: [{
    ...createEmptyCampusVisit('ck-1'),
    campus: 'HN',
    startDatetime: '2026-08-01T09:00',
    endDatetime: '2026-08-01T11:00',
    delegationName: 'Đoàn A',
    visitType: 'MEETING',
    purpose: 'Trao đổi',
    workingContent: 'Nội dung',
    visitors: [{ fullName: 'Khách 1', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }],
    operationalContact: { fullName: 'ĐM CS', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84911111111', email: 'op@example.com' },
  }],
});

const response = (over: Partial<V2CreateResponse> = {}): V2CreateResponse => ({
  visitRequestId: 2003,
  requestCode: 'VR-MC-HN-HCM-0003',
  visitScope: 'SINGLE_CAMPUS',
  hasMixedCampusDetails: false,
  instances: [{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }],
  pendingConfirmations: 0,
  idempotent: false,
  status: 'WAITING_REQUEST_APPROVAL',
  submittedAt: '2026-07-31T09:30:00',
  campusCount: 1,
  ...over,
});

describe('the success screen (plan §8)', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  it('names the request, its status, when it was sent and how many campuses', () => {
    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);

    expect(screen.getByTestId('v2-success-code')).toHaveTextContent('VR-MC-HN-HCM-0003');
    expect(screen.getByTestId('v2-success-status')).toHaveTextContent(/Waiting for the Staff Leader/i);
    // Rendered through the wall-clock formatter — never through the viewer's own timezone.
    expect(screen.getByTestId('v2-success-submitted-at')).toHaveTextContent('31/07/2026 09:30');
    // Campus names, not a bare count.
    expect(screen.getByTestId('v2-success-campuses')).toHaveTextContent('FPT Hà Nội');
  });

  it('renders an unmapped status as itself rather than as a blank or a raw key', () => {
    render(<VisitRequestV2SuccessPanel response={response({ status: 'SOME_NEW_STATUS' })} values={values()} />);
    const status = screen.getByTestId('v2-success-status');
    expect(status).toHaveTextContent('SOME_NEW_STATUS');
    expect(status.textContent).not.toContain('visitRequestV2:');
  });

  it('offers the three actions, and opens the request that was just created', () => {
    const onViewRequest = vi.fn();
    const onGoToList = vi.fn();
    const onCreateAnother = vi.fn();

    render(
      <VisitRequestV2SuccessPanel
        response={response()} values={values()}
        onViewRequest={onViewRequest} onGoToList={onGoToList} onCreateAnother={onCreateAnother}
      />,
    );

    fireEvent.click(screen.getByTestId('v2-success-view'));
    expect(onViewRequest).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByTestId('v2-success-list'));
    expect(onGoToList).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByTestId('v2-success-new'));
    expect(onCreateAnother).toHaveBeenCalledTimes(1);
  });

  it('offers no dashboard action when the surface cannot reach one', () => {
    // The public flow: the create provisions an account but does not sign the visitor in, so a
    // "view my request" button would land them on a login screen with no explanation.
    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);
    expect(screen.queryByTestId('v2-success-view')).toBeNull();
    expect(screen.queryByTestId('v2-success-list')).toBeNull();
  });

  it('says when the response was an idempotent replay rather than a fresh create', () => {
    render(<VisitRequestV2SuccessPanel response={response({ pendingConfirmations: 0,
 idempotent: true })} values={values()} />);
    expect(screen.getByText(/already recorded|đã được ghi nhận/i)).toBeInTheDocument();
  });

  it('always tells the visitor how to track status, even with no contact confirmation pending', () => {
    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);
    const notice = screen.getByRole('status');
    expect(notice).toHaveTextContent(/sign in with Google/i);
    expect(notice).toHaveTextContent('reg@example.com');
    // No contact-differs-from-registrant bullet when nothing is pending.
    expect(notice).not.toHaveTextContent(/is not the guest-side operational contact/i);
  });

  it('adds the pending-confirmation bullet ahead of the tracking bullet, counting CAMPUSES', () => {
    // Per-campus contacts: the bullet reports how many campuses are still waiting on their own
    // operational contact, not one request-level contact address.
    render(<VisitRequestV2SuccessPanel response={response({ pendingConfirmations: 2 })} values={values()} />);
    const notice = screen.getByRole('status');
    expect(notice).toHaveTextContent(/is not the guest-side operational contact/i);
    expect(notice).toHaveTextContent(/contact of 2 campus/i);
    expect(notice).toHaveTextContent(/also check your own email/i);
    expect(notice).toHaveTextContent('reg@example.com');
  });

  it('drops the per-campus summary for a receipt rebuilt from the lookup', () => {
    // The lookup answers an anonymous caller and returns no campus list. Rendering the summary from
    // it would read as "no campuses" rather than "not returned on this path".
    const { container } = render(
      <VisitRequestV2SuccessPanel
        response={response({ recoveredByLookup: true, instances: [] })}
        values={values()}
      />,
    );
    expect(screen.getByTestId('v2-success-code')).toBeInTheDocument();
    expect(container.querySelector('[data-testid="campus-summary-0"]')).toBeNull();
  });
});

describe('the uncertain-result panel (plan §10)', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  const noop = () => {};

  it('tells the user NOT to send another request', () => {
    render(
      <VisitCreateUncertainPanel
        isChecking={false} lookup={null} error={null} onCheck={noop} onBackToForm={noop} />,
    );
    expect(screen.getByTestId('v2-uncertain')).toHaveTextContent(/do not submit a new request/i);
    expect(screen.getByTestId('v2-uncertain-check')).toBeInTheDocument();
    expect(screen.getByTestId('v2-uncertain-back')).toBeInTheDocument();
  });

  it('reports each lookup state in its own words', () => {
    const base = { isChecking: false, error: null, onCheck: noop, onBackToForm: noop };
    const lookup = (state: 'PENDING' | 'FAILED' | 'NOT_FOUND') => ({
      state, visitRequestId: null, requestCode: null, status: null, submittedAt: null, campusCount: null,
    });

    const { unmount: u1 } = render(<VisitCreateUncertainPanel {...base} lookup={lookup('PENDING')} />);
    expect(screen.getByTestId('v2-uncertain-pending')).toBeInTheDocument();
    u1();

    const { unmount: u2 } = render(<VisitCreateUncertainPanel {...base} lookup={lookup('FAILED')} />);
    expect(screen.getByTestId('v2-uncertain-failed')).toBeInTheDocument();
    u2();

    render(<VisitCreateUncertainPanel {...base} lookup={lookup('NOT_FOUND')} />);
    expect(screen.getByTestId('v2-uncertain-notfound')).toBeInTheDocument();
  });

  it('locks both actions while the check is running, so it cannot be double-fired', () => {
    const onCheck = vi.fn();
    render(
      <VisitCreateUncertainPanel
        isChecking lookup={null} error={null} onCheck={onCheck} onBackToForm={noop} />,
    );
    expect(screen.getByTestId('v2-uncertain-check')).toBeDisabled();
    expect(screen.getByTestId('v2-uncertain-back')).toBeDisabled();
  });

  it('distinguishes "the check itself failed" from "the check says nothing was created"', () => {
    render(
      <VisitCreateUncertainPanel
        isChecking={false} lookup={null} error="Không kiểm tra được" onCheck={noop} onBackToForm={noop} />,
    );
    expect(screen.getByTestId('v2-uncertain-error')).toHaveTextContent('Không kiểm tra được');
    expect(screen.queryByTestId('v2-uncertain-notfound')).toBeNull();
  });

  it('renders in Vietnamese as well', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    render(
      <VisitCreateUncertainPanel
        isChecking={false} lookup={null} error={null} onCheck={noop} onBackToForm={noop} />,
    );
    expect(screen.getByTestId('v2-uncertain')).toHaveTextContent('Chưa thể xác nhận kết quả tạo đơn');
    await act(async () => { await i18n.changeLanguage('en'); });
  });
});
