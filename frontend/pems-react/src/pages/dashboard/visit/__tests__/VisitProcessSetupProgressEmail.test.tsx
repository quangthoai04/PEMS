/**
 * The "Gửi cập nhật chuẩn bị" entry point on the preparation tab.
 *
 * Two things are worth guarding here and they are both about not deciding locally: the button is
 * rendered from the backend flag alone (a Staff Leader reading this page must not get it just because
 * the page rendered for them), and the message is rendered by the server before the composer opens (the
 * screen never assembles recipients from what it happens to have loaded).
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const getVisitProcessPermissions = vi.fn();
const getVisitProcessDetail = vi.fn();
const getReminderSettings = vi.fn();
const prepareSetupProgressEmail = vi.fn();
const refreshSetupProgressEmail = vi.fn();
const sendSetupProgressEmail = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitProcessPermissions: (...a: unknown[]) => getVisitProcessPermissions(...a),
    getVisitProcessDetail: (...a: unknown[]) => getVisitProcessDetail(...a),
    getReminderSettings: (...a: unknown[]) => getReminderSettings(...a),
    prepareSetupProgressEmail: (...a: unknown[]) => prepareSetupProgressEmail(...a),
    refreshSetupProgressEmail: (...a: unknown[]) => refreshSetupProgressEmail(...a),
    sendSetupProgressEmail: (...a: unknown[]) => sendSetupProgressEmail(...a),
  },
}));

// The composer has its own suite; here it only has to announce that it opened, and on what.
vi.mock('../../../../features/emails/components/EmailComposeModal', () => ({
  EmailComposeModal: ({
    initialSubject, initialEnvelope, initialAttachments, lockedAttachmentFileIds, lockedTemplate, notices,
  }: any) => (
    <div data-testid="compose-modal"
      data-subject={initialSubject}
      data-envelope={JSON.stringify(initialEnvelope)}
      data-initial-attachments={JSON.stringify(initialAttachments ?? null)}
      data-locked-files={JSON.stringify(lockedAttachmentFileIds ?? null)}
      data-notices={JSON.stringify(notices)}
      data-locked-template={String(lockedTemplate)} />
  ),
}));

vi.mock('../../../../features/agenda-templates/components/AgendaSetupPanel', () => ({
  AgendaSetupPanel: () => <div>agenda-setup-panel</div>,
}));
vi.mock('../../../../features/delegations/components/ParticipantInvitationSection', () => ({
  ParticipantInvitationSection: () => <div>participant-invitation-section</div>,
}));
vi.mock('../../../../features/delegations/components/LogisticsRequestSection', () => ({
  LogisticsRequestSection: () => <div>logistics-request-section</div>,
}));
vi.mock('../../../../features/delegations/components/RequestInfoReadOnly', () => ({
  RegistrantInfoReadOnly: () => <div>registrant-info</div>,
  DelegationInfoReadOnly: () => <div>delegation-info</div>,
}));
vi.mock('../VisitDuringTab', () => ({ VisitDuringTab: () => <div>during-tab</div> }));
vi.mock('../VisitAfterTab', () => ({ VisitAfterTab: () => <div>after-tab</div> }));
vi.mock('../VisitorVisitDetailPage', () => ({ VisitorVisitDetailPage: () => <div>visitor-page</div> }));

vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: { userId: '77', roleCode: 'STAFF' } }),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
  useParams: () => ({ id: '501' }),
  useLocation: () => ({ state: null, pathname: '/dashboard/visit/process/501' }),
}));

import { VisitProcess } from '../VisitProcess';
import i18n from '../../../../shared/i18n/config';

const PERMISSION = {
  visitInstanceId: 501,
  visitRequestId: 9001,
  requestStatus: 'APPROVED',
  instanceStatus: 'BEFORE_VISIT',
  relation: 'HOST',
  hostAssigned: true,
  canViewOriginalRequest: true,
  canViewOverview: true,
  canViewBeforeVisit: true,
  canEditBeforeVisit: true,
  canViewDuringVisit: true,
  canEditDuringVisit: true,
  canViewAfterVisit: true,
  canEditAfterVisit: true,
  canAssignHost: false,
  canViewMinutes: true,
  canCreateMinutes: true,
  canEditMinutes: true,
  canViewNews: true,
  canCreateNews: true,
  canStartVisit: true,
  canCompleteVisit: false,
  canCloseVisit: false,
  canSendSetupProgressEmail: true,
};

const DETAIL = {
  visitInstanceId: 501,
  visitRequestId: 9001,
  relation: 'HOST',
  instanceStatus: 'BEFORE_VISIT',
  delegationName: 'Đoàn ĐH Quốc gia',
  campusName: 'FPTU HCM',
  hostName: 'Trần Cảnh',
  plannedStartAt: '2026-08-20T09:00:00',
  plannedEndAt: '2026-08-20T11:30:00',
  agendaItems: [],
  requestSummary: {},
};

/**
 * The message the prepare endpoint returns. No `draftId`: nothing is persisted between choosing a
 * language and pressing send — the composer holds the message and posts it whole.
 */
const PREPARED = {
  subject: 'Cập nhật công tác chuẩn bị',
  bodyHtml: '<p>noi dung</p>',
  languageCode: 'vi',
  reportFileId: 900,
  reportFileName: 'PEMS_Schedule_Report_VR-9001.pdf',
  reportGeneratedAt: '2026-08-01T14:30:00',
  recipients: [
    { email: 'guest@partner.example', name: 'Guest', recipientType: 'TO' },
    { email: 'ic@fpt.edu.vn', name: 'IC', recipientType: 'CC' },
  ],
  warnings: [],
};

beforeEach(() => {
  // These assertions are written against the Vietnamese UI, and one of them now depends on it.
  // The screen reads errors through the shared extractor, which deliberately withholds a raw
  // Vietnamese backend message when the UI is in English (i18n §8.1) rather than mixing languages.
  // i18n falls back to `navigator.language` — en-US under jsdom — so without this pin the backend's
  // sentence is correctly suppressed and the generic fallback is shown instead.
  void i18n.changeLanguage('vi');
  vi.clearAllMocks();
  getVisitProcessPermissions.mockResolvedValue(PERMISSION);
  getVisitProcessDetail.mockResolvedValue(DETAIL);
  getReminderSettings.mockResolvedValue({ items: [] });
  prepareSetupProgressEmail.mockResolvedValue(PREPARED);
});

describe('Gửi cập nhật chuẩn bị', () => {
  it('offers the button when the backend grants it', async () => {
    render(<VisitProcess />);
    await waitFor(() => expect(screen.getByTestId('send-setup-progress-email')).toBeTruthy());
  });

  it('does not offer it when the backend withholds it, even on a page the user can read', async () => {
    getVisitProcessPermissions.mockResolvedValue({
      ...PERMISSION, relation: 'STAFF_LEADER', canSendSetupProgressEmail: false,
    });

    render(<VisitProcess />);

    // The report button is the control that IS available to this reader — waiting on it proves the
    // action row rendered, so the absence below is a real absence and not an un-awaited render.
    await waitFor(() => expect(screen.getByRole('button', { name: /Báo cáo Lịch trình/ })).toBeTruthy());
    expect(screen.queryByTestId('send-setup-progress-email')).toBeNull();
  });

  it('asks for the language, then opens the composer on the message the backend rendered', async () => {
    const user = userEvent.setup();
    render(<VisitProcess />);
    await waitFor(() => expect(screen.getByTestId('send-setup-progress-email')).toBeTruthy());

    await user.click(screen.getByTestId('send-setup-progress-email'));
    expect(screen.getByTestId('setup-email-language')).toBeTruthy();
    // Nothing is prepared until a language is chosen: the message and the attached report have to be
    // produced in the same one.
    expect(prepareSetupProgressEmail).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Tiếng Việt' }));

    await waitFor(() => expect(prepareSetupProgressEmail).toHaveBeenCalledWith(9001, 501, 'vi'));
    const modal = await screen.findByTestId('compose-modal');
    expect(modal.getAttribute('data-subject')).toBe(PREPARED.subject);
    expect(modal.getAttribute('data-locked-template')).toBe('true');

    // The report is handed over as an ATTACHMENT, not as a locked id. Passing only the id named a file
    // the composer was not carrying: its strip said "chưa có tệp đính kèm", the payload went out empty,
    // and the send was refused for omitting the very report the screen said was mandatory.
    expect(JSON.parse(modal.getAttribute('data-initial-attachments') ?? 'null')).toEqual([
      { fileId: 900, name: 'PEMS_Schedule_Report_VR-9001.pdf', mimeType: 'application/pdf', size: null },
    ]);
    // And it is NOT locked: the Host may take it off.
    expect(JSON.parse(modal.getAttribute('data-locked-files') ?? 'null')).toBeNull();

    // The envelope is passed through GROUPED. The screen never assembles it from what it happens to
    // have loaded, and a CC the backend chose has to stay a CC.
    const envelope = JSON.parse(modal.getAttribute('data-envelope') ?? '[]');
    expect(envelope).toEqual([
      { email: 'guest@partner.example', name: 'Guest', recipientType: 'TO', displayOrder: 0 },
      { email: 'ic@fpt.edu.vn', name: 'IC', recipientType: 'CC', displayOrder: 1 },
    ]);
  });

  it('passes the chosen language through', async () => {
    const user = userEvent.setup();
    render(<VisitProcess />);
    await waitFor(() => expect(screen.getByTestId('send-setup-progress-email')).toBeTruthy());

    await user.click(screen.getByTestId('send-setup-progress-email'));
    await user.click(screen.getByRole('button', { name: 'English' }));

    await waitFor(() => expect(prepareSetupProgressEmail).toHaveBeenCalledWith(9001, 501, 'en'));
  });

  /**
   * Google Drive being unavailable costs the report and nothing else.
   *
   * This is the situation the whole change is about. The backend used to fail the entire prepare when the
   * PDF could not be archived, so an expired Drive grant meant the Host could not compose — let alone
   * send — a message whose text does not depend on the PDF at all. It now answers with the message, a
   * null report, and a reason.
   */
  it('opens the composer with no attachment when the backend produced no report', async () => {
    const user = userEvent.setup();
    prepareSetupProgressEmail.mockResolvedValue({
      ...PREPARED,
      reportFileId: null,
      reportFileName: null,
      warnings: ['Kết nối Google Drive cần được xác thực lại. Báo cáo Lịch trình chưa được đính kèm.'],
    });

    render(<VisitProcess />);
    await waitFor(() => expect(screen.getByTestId('send-setup-progress-email')).toBeTruthy());
    await user.click(screen.getByTestId('send-setup-progress-email'));
    await user.click(screen.getByRole('button', { name: 'Tiếng Việt' }));

    const modal = await screen.findByTestId('compose-modal');
    // No fabricated attachment: not file id 0, not an empty name — nothing.
    expect(JSON.parse(modal.getAttribute('data-initial-attachments') ?? 'null')).toBeNull();
    expect(modal.getAttribute('data-subject')).toBe(PREPARED.subject);
    expect(JSON.parse(modal.getAttribute('data-notices') ?? '[]')[0]).toContain('xác thực lại');
  });

  it('reports a failed preparation and lets the host try again', async () => {
    const user = userEvent.setup();
    prepareSetupProgressEmail.mockRejectedValueOnce({
      response: { data: { message: 'Chuyến thăm đã qua giai đoạn chuẩn bị.' } },
    });

    render(<VisitProcess />);
    await waitFor(() => expect(screen.getByTestId('send-setup-progress-email')).toBeTruthy());

    await user.click(screen.getByTestId('send-setup-progress-email'));
    await user.click(screen.getByRole('button', { name: 'Tiếng Việt' }));

    // The failure is shown where the action was taken, and the composer does NOT open on nothing.
    await waitFor(() => expect(screen.getByRole('alert').textContent)
      .toContain('Chuyến thăm đã qua giai đoạn chuẩn bị.'));
    expect(screen.queryByTestId('compose-modal')).toBeNull();

    await user.click(screen.getByRole('button', { name: 'Tiếng Việt' }));
    await waitFor(() => expect(screen.getByTestId('compose-modal')).toBeTruthy());
  });
});
