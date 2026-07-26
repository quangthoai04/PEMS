import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({
  submitAmendment: vi.fn(),
  patchSafeDetails: vi.fn(),
}));

import { submitAmendment, patchSafeDetails } from '../api/visitRequestV2Api';
import VisitAmendmentSubmitModal from '../components/VisitAmendmentSubmitModal';
import VisitSafeEditModal from '../components/VisitSafeEditModal';
import { campusFixture } from './fixtures';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';

const form = (): ResolvedVisitForm => ({
  visitRequestId: 1, requestCode: 'VR-1', rowVersion: 4,
  hasMixedCampusDetails: false, visitScope: 'SINGLE_CAMPUS', requestStatus: 'APPROVED',
  createdSource: 'PUBLIC', submittedAt: '2026-07-15T08:00:00', partnerId: null,
  cancelledByUserId: null, cancelledByName: null, cancelledAt: null, cancellationReason: null,
  registrant: { fullName: 'Reg', organization: 'Org', jobTitle: 'Head', phone: '+84900000001', email: 'r@x.vn', nationality: 'VN' },
  primaryContact: { fullName: 'Contact', organization: 'Org', phone: '+84900000002', email: 'c***@x.vn', accessStatus: 'ACTIVE', verifiedAt: null },
  campusVisits: [campusFixture()],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW', 'SUBMIT_SAFE_EDIT'] },
});

describe('VisitAmendmentSubmitModal', () => {
  beforeEach(() => vi.clearAllMocks());

  it('requires a reason and submits the proposal with base revisions', async () => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    const onSubmitted = vi.fn();
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture()} onClose={() => {}} onSubmitted={onSubmitted} />);

    const submit = screen.getByRole('button', { name: 'Submit proposal' });
    expect(submit).toBeDisabled(); // no reason yet

    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'Đổi mục đích' } });
    expect(submit).toBeEnabled();
    fireEvent.click(submit);

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, instanceId, payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(instanceId).toBe(10);
    expect(payload.reason).toBe('Đổi mục đích');
    expect(payload.expectedInstanceRowVersion).toBe(3);
    expect(payload.baseFormRevision).toBe(2);
    expect(onSubmitted).toHaveBeenCalled();
  });

  it('maps AMENDMENT_ALREADY_PENDING to a stable message (no raw code)', async () => {
    vi.mocked(submitAmendment).mockRejectedValue({ response: { data: { errorCode: 'AMENDMENT_ALREADY_PENDING' } } });
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture()} onClose={() => {}} onSubmitted={() => {}} />);

    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'x' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/already has a pending proposal/i);
    expect(screen.queryByText(/AMENDMENT_ALREADY_PENDING/)).toBeNull();
  });
});

describe('VisitAmendmentSubmitModal — member list', () => {
  beforeEach(() => vi.clearAllMocks());

  const openWithReason = (campus = campusFixture()) => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campus} onClose={() => {}} onSubmitted={() => {}} />);
    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'Đổi đoàn' } });
  };

  it('adds a guest to the proposal and submits the enlarged member list', async () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' }));
    const nameInputs = screen.getAllByLabelText(/Guest list.*Full name/);
    expect(nameInputs).toHaveLength(2);
    fireEvent.change(nameInputs[1], { target: { value: 'Khách Hai' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.visitors.map(v => v.fullName)).toEqual(['Khách Một', 'Khách Hai']);
  });

  it('deep-clones members so editing the modal never mutates the source campus', async () => {
    const campus = campusFixture();
    const original = campus.visitors[0].fullName;
    openWithReason(campus);
    fireEvent.change(screen.getByLabelText(/Guest list.*Full name/), { target: { value: 'Đã sửa' } });
    // No reference is shared between the passed-in campus and the editor's own state.
    expect(campus.visitors[0].fullName).toBe(original);
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.visitors[0].fullName).toBe('Đã sửa');
  });

  it('summarizes member additions vs the active content', () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' }));
    expect(screen.getByRole('status')).toHaveTextContent(/1 added/);
  });

  it('requires at least one visitor before submitting', () => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture({ visitors: [] })} onClose={() => {}} onSubmitted={() => {}} />);
    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'x' } });
    expect(screen.getByRole('button', { name: 'Submit proposal' })).toBeDisabled();
    expect(screen.getByText('At least one guest is required.')).toBeInTheDocument();
  });
});

describe('VisitSafeEditModal', () => {
  beforeEach(() => vi.clearAllMocks());

  /** Types into one campus's note, which is the smallest real edit. */
  const editOneCampusNote = (value = 'Chuẩn bị phiên dịch.') =>
    fireEvent.change(screen.getByTestId('safe-edit-transportation-10'), { target: { value } });

  it('sends expected row versions and reports a 409 conflict with a reload action', async () => {
    vi.mocked(patchSafeDetails).mockRejectedValue({ response: { status: 409, data: { errorCode: 'CONCURRENCY_CONFLICT' } } });
    const onSaved = vi.fn();
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={onSaved} />);

    editOneCampusNote();
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.expectedRequestRowVersion).toBe(4);
    expect(payload.instances?.[0].expectedRowVersion).toBe(3);

    expect(await screen.findByRole('alert')).toHaveTextContent(/changed since you opened/i);
    fireEvent.click(screen.getByRole('button', { name: 'Reload' }));
    expect(onSaved).toHaveBeenCalled();
  });

  it('applies immediately and shows the applied-change count on success', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [{ fieldPath: 'registrant.phone', visitInstanceId: null, changeClass: 'SAFE' }],
      requestRowVersion: 5, instanceRowVersions: { 10: 4 }, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    editOneCampusNote();
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(await screen.findByText(/Applied 1 change/i)).toBeInTheDocument();
  });

  // ── Changed-only payload (§6). The modal used to send a full snapshot of every safe field of every
  //    campus, which dragged untouched campuses into the request and could overwrite a value that had
  //    changed server-side since the form loaded. ──

  it('submits ONLY the campus that changed, and no request-level block', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    editOneCampusNote('Xe 45 chỗ');
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.registrant).toBeNull();
    expect(payload.contact).toBeNull();
    expect(payload.instances).toHaveLength(1);
    expect(payload.instances?.[0]).toMatchObject({ visitInstanceId: 10, transportationNote: 'Xe 45 chỗ' });
    // The untouched fields of the touched campus are absent, not echoed back at their old values.
    expect(payload.instances?.[0].mediaConsentStatus).toBeUndefined();
    expect(payload.instances?.[0].noteToFptu).toBeUndefined();
  });

  it('sends instances: [] when only a request-level field changed', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const { container } = render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    // The registrant phone is the second input of the registrant fieldset.
    const registrantInputs = container.querySelectorAll('fieldset input');
    fireEvent.change(registrantInputs[1], { target: { value: '+84900000009' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances).toEqual([]);
    expect(payload.registrant).toMatchObject({ phone: '+84900000009' });
    expect(payload.contact).toBeNull();
  });

  it('refuses to call the API when nothing was edited', async () => {
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/have not changed anything/i);
    expect(patchSafeDetails).not.toHaveBeenCalled();
  });

  it('omits a campus the backend has closed, and names it', () => {
    const closed = form();
    closed.campusVisits = [campusFixture({ instanceStatus: 'DURING_VISIT', allowedActions: [] })];
    render(<VisitSafeEditModal form={closed} onClose={() => {}} onSaved={() => {}} />);

    expect(screen.queryByTestId('safe-edit-transportation-10')).toBeNull();
    expect(screen.getByTestId('safe-edit-locked-campuses')).toHaveTextContent('FPTU Hà Nội');
  });
});
