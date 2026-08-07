import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, waitFor } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';
import type { V2CreateResponse } from '../api/visitRequestV2Api';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';

/**
 * Plan §20 — the receipt an anonymous visitor gets after the OTP.
 *
 * The receipt component was right; the WIRING was not. `VisitEntrySurfaces` called
 * `cta.closeV2Modal()` from `onSuccess`, which unmounted the shell in the same tick as it rendered
 * the receipt — so on every public CTA (hero, final CTA, FAQ, Partners) the form vanished after the
 * OTP and a four-second toast was the only evidence a request had been created at all. These tests
 * hold that wiring in place, and cover the actions the receipt has to carry for a user who cannot
 * reach a dashboard.
 */

let lastFormProps: Record<string, unknown> = {};
vi.mock('../components/v2/VisitRequestFormV2', () => ({
  VisitRequestFormV2: (props: Record<string, unknown>) => {
    lastFormProps = props;
    return <div data-testid="shared-v2-form" />;
  },
}));

// A public campus list, so the summary can name campuses. NOT a request-detail call.
vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [{ campusCode: 'HN', campusName: 'FPT Hà Nội', campusId: 1 }],
    loading: false,
  }),
}));

vi.mock('../api/visitRequestV2Api', () => ({
  getVisitRequestFormV2: vi.fn(),
  getVisitRequestHistory: vi.fn(),
  getVisitSubmissionResult: vi.fn(),
}));

import { getVisitRequestFormV2, getVisitRequestHistory } from '../api/visitRequestV2Api';
import { VisitRequestV2SuccessPanel } from '../components/v2/VisitRequestV2SuccessPanel';
import { VisitEntrySurfaces } from '../../../shared/features/VisitEntrySurfaces';
import type { VisitEntryCta } from '../../../shared/features/useVisitEntryCta';

const values = (delegationName = 'Đoàn Nhận Biên Lai'): VisitRequestV2Schema => ({
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
    delegationName,
    visitType: 'MEETING',
    purpose: 'Trao đổi hợp tác',
    workingContent: 'Nội dung làm việc',
    visitors: [{ fullName: 'Khách Một', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }],
    operationalContact: { fullName: 'ĐM CS', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84911111111', email: 'op@example.com' },
  }],
});

const response = (over: Partial<V2CreateResponse> = {}): V2CreateResponse => ({
  visitRequestId: 2003,
  requestCode: 'VR2026072629B9DFF',
  visitScope: 'SINGLE_CAMPUS',
  hasMixedCampusDetails: false,
  instances: [{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }],
  pendingContactConfirmations: 0,
  idempotent: false,
  status: 'WAITING_REQUEST_APPROVAL',
  submittedAt: '2026-07-26T14:30:00',
  campusCount: 1,
  ...over,
});

const publicCta = (closeV2Modal: () => void): VisitEntryCta => ({
  trigger: vi.fn(),
  v2ModalOpen: true,
  closeV2Modal,
  v2Mode: 'public',
  isResolving: false,
});

describe('the public CTA keeps the receipt on screen (plan §3, §9)', () => {
  beforeEach(async () => { lastFormProps = {}; vi.clearAllMocks(); await i18n.changeLanguage('en'); });

  const succeed = () => act(() => {
    (lastFormProps.onSuccess as (r: V2CreateResponse, v: VisitRequestV2Schema) => void)(response(), values());
  });

  it('does not close the modal when the create succeeds', () => {
    const closeV2Modal = vi.fn();
    render(<VisitEntrySurfaces cta={publicCta(closeV2Modal)} />);

    succeed();

    expect(closeV2Modal).not.toHaveBeenCalled();
    expect(screen.getByTestId('v2-create-modal')).toBeInTheDocument();
  });

  it('shows the request code as a fixed panel, not only as a toast', () => {
    render(<VisitEntrySurfaces cta={publicCta(vi.fn())} />);
    succeed();

    expect(screen.getByTestId('v2-success-code')).toHaveTextContent('VR2026072629B9DFF');
    expect(screen.queryByTestId('shared-v2-form')).toBeNull();
  });

  it('still tells the host, so a dashboard list behind the modal can refresh', () => {
    const onV2Success = vi.fn();
    render(<VisitEntrySurfaces cta={publicCta(vi.fn())} onV2Success={onV2Success} />);
    succeed();
    expect(onV2Success).toHaveBeenCalledTimes(1);
  });
});

describe('the receipt itself (plan §4, §5, §7)', () => {
  beforeEach(async () => { vi.clearAllMocks(); await i18n.changeLanguage('en'); });

  it('states the code, the status, when it was sent and how many campuses', () => {
    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);

    expect(screen.getByTestId('v2-success-code')).toHaveTextContent('VR2026072629B9DFF');
    expect(screen.getByTestId('v2-success-status')).toHaveTextContent(/Waiting for the Staff Leader/i);
    // Wall-clock, never shifted through the viewer's own timezone.
    expect(screen.getByTestId('v2-success-submitted-at')).toHaveTextContent('26/07/2026 14:30');
    expect(screen.getByTestId('v2-success-campuses')).toHaveTextContent('FPT Hà Nội');
    // Names the registrant's own email — not a generic "check your inbox".
    expect(screen.getByText(/sign in with Google/i)).toHaveTextContent('reg@example.com');
  });

  it('offers "review what you submitted" on the public surface too', () => {
    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);
    expect(screen.getByTestId('v2-success-review')).toBeInTheDocument();
  });

  it('renders the read-only summary from the SUBMITTED snapshot, not from the server', async () => {
    render(<VisitRequestV2SuccessPanel response={response()} values={values('Đoàn Chỉ Có Trong Snapshot')} />);

    // Collapsed until asked for: the code is what the user must not lose, so it leads.
    expect(screen.queryByTestId('campus-summary-0')).toBeNull();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-success-review')); });

    const summary = screen.getByTestId('campus-summary-0');
    expect(summary).toHaveTextContent('Đoàn Chỉ Có Trong Snapshot');
    expect(summary).toHaveTextContent('Khách Một');
    // Nothing about the receipt asks the server for the request — the public flow has no session
    // that a protected detail endpoint would accept.
    expect(getVisitRequestFormV2).not.toHaveBeenCalled();
    expect(getVisitRequestHistory).not.toHaveBeenCalled();
  });

  it('keeps showing the snapshot it was given even when the caller re-renders', async () => {
    const submitted = values('Đoàn Bất Biến');
    const { rerender } = render(<VisitRequestV2SuccessPanel response={response()} values={submitted} />);
    await act(async () => { fireEvent.click(screen.getByTestId('v2-success-review')); });

    rerender(<VisitRequestV2SuccessPanel response={response()} values={submitted} />);
    expect(screen.getByTestId('campus-summary-0')).toHaveTextContent('Đoàn Bất Biến');
  });

  it('copies the request code to the clipboard and says so', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);
    await act(async () => { fireEvent.click(screen.getByTestId('v2-success-copy')); });

    expect(writeText).toHaveBeenCalledWith('VR2026072629B9DFF');
    await waitFor(() =>
      expect(screen.getByTestId('v2-success-copy-status')).toHaveTextContent(/copied/i));
  });

  it('admits it when the browser refuses the clipboard, and shows the code to copy by hand', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) }, configurable: true,
    });

    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);
    await act(async () => { fireEvent.click(screen.getByTestId('v2-success-copy')); });

    await waitFor(() => {
      const status = screen.getByTestId('v2-success-copy-status');
      expect(status).toHaveTextContent(/blocked automatic copying/i);
      expect(status).toHaveTextContent('VR2026072629B9DFF');
    });
  });

  it('offers no dashboard action to a visitor who has no session to use it with', () => {
    render(<VisitRequestV2SuccessPanel response={response()} values={values()} />);
    expect(screen.queryByTestId('v2-success-view')).toBeNull();
    expect(screen.queryByTestId('v2-success-list')).toBeNull();
  });

  it('gives an authenticated surface the request, the list and a fresh form', () => {
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
    fireEvent.click(screen.getByTestId('v2-success-list'));
    fireEvent.click(screen.getByTestId('v2-success-new'));
    expect(onViewRequest).toHaveBeenCalledTimes(1);
    expect(onGoToList).toHaveBeenCalledTimes(1);
    expect(onCreateAnother).toHaveBeenCalledTimes(1);
  });

  it('renders every action in Vietnamese as well', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    render(<VisitRequestV2SuccessPanel response={response()} values={values()} onClose={vi.fn()} />);

    expect(screen.getByTestId('v2-success-review')).toHaveTextContent('Xem lại thông tin đã gửi');
    expect(screen.getByTestId('v2-success-copy')).toHaveTextContent('Sao chép mã đơn');
    expect(screen.getByTestId('v2-success-close')).toHaveTextContent('Đóng');
    await act(async () => { await i18n.changeLanguage('en'); });
  });
});

describe('leaving the receipt (plan §6)', () => {
  beforeEach(async () => { lastFormProps = {}; vi.clearAllMocks(); await i18n.changeLanguage('en'); });

  const succeedIn = (closeV2Modal: () => void) => {
    render(<VisitEntrySurfaces cta={publicCta(closeV2Modal)} />);
    act(() => {
      (lastFormProps.onSuccess as (r: V2CreateResponse, v: VisitRequestV2Schema) => void)(response(), values());
    });
  };

  it('only resets the form when the user asks for a new one', () => {
    succeedIn(vi.fn());
    expect(screen.queryByTestId('shared-v2-form')).toBeNull();

    act(() => { fireEvent.click(screen.getByTestId('v2-success-new')); });
    expect(screen.getByTestId('shared-v2-form')).toBeInTheDocument();
    expect(screen.queryByTestId('v2-success-code')).toBeNull();
  });

  it('closes on request — and closing a finished request never prompts to save a draft', () => {
    const closeV2Modal = vi.fn();
    succeedIn(closeV2Modal);

    act(() => { fireEvent.click(screen.getByTestId('v2-success-close')); });
    expect(closeV2Modal).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('v2-modal-discard')).toBeNull();
  });
});
