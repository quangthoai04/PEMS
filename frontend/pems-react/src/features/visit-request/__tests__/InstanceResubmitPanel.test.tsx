import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const resubmitVisitRequestV2 = vi.fn();
vi.mock('../api/visitRequestV2Api', () => ({
  resubmitVisitInstance: vi.fn(),
  // Present so a test can prove it is NEVER reached.
  resubmitVisitRequestV2: (...args: unknown[]) => resubmitVisitRequestV2(...args),
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const showSuccessToast = vi.fn();
const showErrorToast = vi.fn();
vi.mock('../../../shared/utils/toast', () => ({
  showSuccessToast: (...args: unknown[]) => showSuccessToast(...args),
  showErrorToast: (...args: unknown[]) => showErrorToast(...args),
}));

import InstanceResubmitPanel from '../components/InstanceResubmitPanel';
import { resubmitVisitInstance } from '../api/visitRequestV2Api';

/**
 * Per-instance Resubmit in the browser (plan v11 §5.7).
 *
 * The panel never decides who may resubmit. The backend puts RESUBMIT_REJECTED_INSTANCE in THIS
 * campus's allowedActions for the registrant and for the person holding this campus, and the panel
 * renders exactly that. So "sibling contact" and "random visitor" are modelled the way the server
 * actually presents them: the same campus, without the action.
 */
describe('InstanceResubmitPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(resubmitVisitInstance).mockResolvedValue({
      visitRequestId: 1,
      visitInstanceId: 31,
      visitRequestStatus: 'PARTIALLY_APPROVED',
      visitInstanceStatus: 'WAITING_REQUEST_APPROVAL',
      instanceRowVersion: 4,
      message: 'ok',
    });
  });

  const campus = (allowedActions: string[]) =>
    ({
      visitInstanceId: 31,
      campusId: 1,
      campusCode: 'HN',
      campusName: 'Hà Nội',
      plannedStartAt: '2026-10-01T09:00:00',
      plannedEndAt: '2026-10-01T11:00:00',
      instanceStatus: 'REJECTED',
      decisionNote: 'Trùng lịch tiếp đoàn khác',
      delegationName: 'Đoàn HN',
      visitType: 'MEETING',
      visitTypeOther: null,
      purpose: 'Thăm',
      workingContent: 'Nội dung',
      visitors: [{ fullName: 'Guest A', nationality: 'VN', jobTitle: 'G', organization: 'O' }],
      supportMembers: [],
      operationalContact: {
        fullName: 'Đầu mối HN',
        organization: 'OrgB',
        jobTitle: 'Trưởng phòng',
        phone: '+84912345678',
        email: 'hn@example.com',
      },
      workingLanguage: 'EN',
      transportationNote: null,
      mediaConsentStatus: 'DECLINED',
      notes: null,
      rowVersion: 3,
      allowedActions,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    }) as any;

  /** The other campus of the same request, so "per campus" can be shown rather than asserted. */
  const sibling = (allowedActions: string[]) =>
    ({
      ...campus(allowedActions),
      visitInstanceId: 32,
      campusId: 2,
      campusCode: 'DN',
      campusName: 'Đà Nẵng',
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    }) as any;

  it('FE-RESUBMIT-01: the campus holder sees the CTA and the rejection reason', () => {
    render(
      <InstanceResubmitPanel
        visitRequestId={1}
        campusVisit={campus(['RESUBMIT_REJECTED_INSTANCE'])}
        onResubmitted={vi.fn()}
      />,
    );

    expect(screen.getByTestId('instance-resubmit-31')).toBeInTheDocument();
    expect(screen.getByTestId('instance-resubmit-open-31')).toBeInTheDocument();
    expect(screen.getByTestId('instance-resubmit-reason-31').textContent)
      .toContain('Trùng lịch tiếp đoàn khác');
  });

  it('FE-RESUBMIT-02: the DN sibling contact sees the CTA on DN and none on HN', () => {
    // Holding one campus grants nothing on another. Both are on screen at once, which is what this
    // person actually sees, and only their own campus offers the action.
    render(
      <>
        <InstanceResubmitPanel visitRequestId={1} campusVisit={campus([])} onResubmitted={vi.fn()} />
        <InstanceResubmitPanel
          visitRequestId={1}
          campusVisit={sibling(['RESUBMIT_REJECTED_INSTANCE'])}
          onResubmitted={vi.fn()}
        />
      </>,
    );

    expect(screen.queryByTestId('instance-resubmit-31')).not.toBeInTheDocument();
    expect(screen.queryByTestId('instance-resubmit-open-31')).not.toBeInTheDocument();
    expect(screen.getByTestId('instance-resubmit-open-32')).toBeInTheDocument();
  });

  it('FE-RESUBMIT-03: a random visitor gets no CTA on any campus', () => {
    // Someone with no relation to the request: the server grants nothing anywhere, so nothing renders.
    const { container } = render(
      <>
        <InstanceResubmitPanel visitRequestId={1} campusVisit={campus([])} onResubmitted={vi.fn()} />
        <InstanceResubmitPanel visitRequestId={1} campusVisit={sibling([])} onResubmitted={vi.fn()} />
      </>,
    );

    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByTestId('instance-resubmit-open-31')).not.toBeInTheDocument();
    expect(screen.queryByTestId('instance-resubmit-open-32')).not.toBeInTheDocument();
  });

  it('an unrelated action on the campus still yields no resubmit CTA', () => {
    const { container } = render(
      <InstanceResubmitPanel
        visitRequestId={1}
        campusVisit={campus(['UPDATE_OPERATIONAL_CONTACT_PROFILE'])}
        onResubmitted={vi.fn()}
      />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('FE-RESUBMIT-04: submitting calls the INSTANCE endpoint, never the request-wide one', async () => {
    render(
      <InstanceResubmitPanel
        visitRequestId={1}
        campusVisit={campus(['RESUBMIT_REJECTED_INSTANCE'])}
        onResubmitted={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId('instance-resubmit-open-31'));
    await userEvent.click(screen.getByTestId('instance-resubmit-submit-31'));

    await waitFor(() => expect(resubmitVisitInstance).toHaveBeenCalledTimes(1));

    const [requestId, instanceId, payload] = vi.mocked(resubmitVisitInstance).mock.calls[0];
    expect(requestId).toBe(1);
    expect(instanceId).toBe(31);

    // The INSTANCE's own row version travels, not the request's.
    expect((payload as { expectedRowVersion: number }).expectedRowVersion).toBe(3);
    expect((payload as { campusId: string }).campusId).toBe('HN');

    // The request-wide endpoint is never reached for a contact action: it demands every campus be
    // rejected and would reset an approved sibling.
    expect(resubmitVisitRequestV2).not.toHaveBeenCalled();
  });

  it('FE-RESUBMIT-05: a successful resubmit raises exactly one toast', async () => {
    const onResubmitted = vi.fn();
    render(
      <InstanceResubmitPanel
        visitRequestId={1}
        campusVisit={campus(['RESUBMIT_REJECTED_INSTANCE'])}
        onResubmitted={onResubmitted}
      />,
    );

    await userEvent.click(screen.getByTestId('instance-resubmit-open-31'));
    await userEvent.click(screen.getByTestId('instance-resubmit-submit-31'));

    await waitFor(() => expect(onResubmitted).toHaveBeenCalledTimes(1));

    // One toast, raised by the component that made the call — the parent re-read must not add a second.
    expect(showSuccessToast).toHaveBeenCalledTimes(1);
    expect(showErrorToast).not.toHaveBeenCalled();
  });

  it('FE-RESUBMIT-06: the 72h refusal is shown using the server message', async () => {
    vi.mocked(resubmitVisitInstance).mockRejectedValue({
      response: {
        data: {
          errorCode: 'INVALID_VISIT_TIME',
          message: 'Cơ sở HN: thời gian bắt đầu phải từ 11/10/2026 09:00 trở đi.',
        },
      },
    });

    render(
      <InstanceResubmitPanel
        visitRequestId={1}
        campusVisit={campus(['RESUBMIT_REJECTED_INSTANCE'])}
        onResubmitted={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId('instance-resubmit-open-31'));
    await userEvent.click(screen.getByTestId('instance-resubmit-submit-31'));

    const alert = await screen.findByTestId('instance-resubmit-error-31');
    // The server's own sentence, which names the earliest legal start — not a local paraphrase.
    expect(alert.textContent).toContain('11/10/2026 09:00');
    expect(showSuccessToast).not.toHaveBeenCalled();
  });

  it('FE-RESUBMIT-07: a stale row version surfaces the conflict and keeps the form open', async () => {
    vi.mocked(resubmitVisitInstance).mockRejectedValue({
      response: {
        data: {
          errorCode: 'INSTANCE_VERSION_CONFLICT',
          message: 'Lịch thăm tại cơ sở này đã được thay đổi bởi thao tác khác.',
        },
      },
    });

    render(
      <InstanceResubmitPanel
        visitRequestId={1}
        campusVisit={campus(['RESUBMIT_REJECTED_INSTANCE'])}
        onResubmitted={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId('instance-resubmit-open-31'));
    await userEvent.click(screen.getByTestId('instance-resubmit-submit-31'));

    const alert = await screen.findByTestId('instance-resubmit-error-31');
    expect(alert.textContent).toContain('đã được thay đổi');
    // Still open, so the person can reload and retry rather than losing what they typed.
    expect(screen.getByTestId('instance-resubmit-submit-31')).toBeInTheDocument();
    expect(showSuccessToast).not.toHaveBeenCalled();
  });
});
