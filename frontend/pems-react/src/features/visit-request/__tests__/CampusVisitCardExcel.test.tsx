import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, act, waitFor, within } from '@testing-library/react';
import * as XLSX from 'xlsx';

/**
 * Plan §22 items 4, 9 and 10, at the level where they actually bite: the card.
 *
 * Importing used to call `replace()`, so a list typed in by hand was destroyed by an upload the
 * user thought would ADD to it — and both sections shared one message, so the second import
 * silently overwrote the first one's result.
 */

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [{ campusCode: 'HN', campusName: 'Hòa Lạc' }],
    loading: false,
  }),
}));
vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: vi.fn().mockResolvedValue([]),
    searchOrganizations: vi.fn().mockResolvedValue([]),
    initiate: vi.fn(), resendOtp: vi.fn(), recoverOtp: vi.fn(),
  },
}));
vi.mock('../api/visitRequestV2Api', () => ({
  createVisitRequestV2: vi.fn(),
  initiateVisitRequestV2: vi.fn(),
  verifyAndCreateVisitRequestV2: vi.fn(),
}));

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const HEADER = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];

const makeFile = (rows: (string | number)[][], name = 'khach.xlsx'): File => {
  const ws = XLSX.utils.aoa_to_sheet(rows);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Sheet1');
  const buf = XLSX.write(wb, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;
  return new File([buf], name);
};

const guest = (name: string) => [0, name, 'Giảng viên', 'ĐH Đối Tác', 'Việt Nam'];

const renderForm = () =>
  render(<VisitRequestFormV2 mode="public" draftNamespace="excel-card-test" onSuccess={vi.fn()} />);

/** [guests, support] — both are hidden inputs, present whether or not the card is expanded. */
const fileInputs = (container: HTMLElement) =>
  Array.from(container.querySelectorAll('input[type="file"]')) as HTMLInputElement[];

const upload = async (input: HTMLInputElement, file: File) => {
  await act(async () => {
    fireEvent.change(input, { target: { files: [file] } });
    // let the async validate + setState settle
    await new Promise(r => setTimeout(r, 0));
  });
};

const guestNames = () => {
  const table = screen.getByTestId('v2-visitors-table');
  return within(table).getAllByRole('row').slice(1)
    .map(r => (within(r).getAllByRole('textbox')[0] as HTMLTextAreaElement).value);
};

describe('Campus card — Excel import semantics', () => {
  beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); });

  it('ADDS to the list instead of replacing what was typed by hand', async () => {
    const { container } = renderForm();

    // One person, typed in.
    const typed = within(screen.getByTestId('v2-visitors-table')).getAllByRole('textbox')[0];
    await act(async () => { fireEvent.change(typed, { target: { value: 'Người Gõ Tay' } }); });

    await upload(fileInputs(container)[0], makeFile([HEADER, guest('Khách Từ File')]));

    await waitFor(() => expect(guestNames()).toEqual(['Người Gõ Tay', 'Khách Từ File']));
  });

  it('drops the untouched placeholder row so the imported list starts clean', async () => {
    const { container } = renderForm();

    await upload(fileInputs(container)[0], makeFile([HEADER, guest('Khách A'), guest('Khách B')]));

    await waitFor(() => expect(guestNames()).toEqual(['Khách A', 'Khách B']));
  });

  it('leaves the form untouched when the file has any bad row', async () => {
    const { container } = renderForm();

    const typed = within(screen.getByTestId('v2-visitors-table')).getAllByRole('textbox')[0];
    await act(async () => { fireEvent.change(typed, { target: { value: 'Người Gõ Tay' } }); });

    await upload(fileInputs(container)[0], makeFile([
      HEADER, guest('Khách Tốt'), [0, '', 'GV', 'Org', 'VN'],
    ]));

    expect(await screen.findByTestId('v2-excel-visitors-error')).toBeInTheDocument();
    expect(guestNames()).toEqual(['Người Gõ Tay']);
  });

  it('reports the two sections separately', async () => {
    const { container } = renderForm();
    const [guests, support] = fileInputs(container);

    await upload(guests, makeFile([HEADER, guest('Khách Tốt')], 'khach.xlsx'));
    // A file the support section will reject — the guest report must survive it.
    await upload(support, makeFile([HEADER, [0, '', 'GV', 'Org', 'VN']], 'ho-tro.xlsx'));

    expect(await screen.findByTestId('v2-excel-visitors-success')).toHaveTextContent('khach.xlsx');
    expect(await screen.findByTestId('v2-excel-support-error')).toHaveTextContent('ho-tro.xlsx');
  });

  it('offers replacing as its own action, and only after a confirmation', async () => {
    const { container } = renderForm();

    const typed = within(screen.getByTestId('v2-visitors-table')).getAllByRole('textbox')[0];
    await act(async () => { fireEvent.change(typed, { target: { value: 'Người Gõ Tay' } }); });
    await upload(fileInputs(container)[0], makeFile([HEADER, guest('Khách Từ File')]));

    await waitFor(() => expect(guestNames()).toHaveLength(2));

    await act(async () => { fireEvent.click(screen.getByTestId('v2-visitors-replace')); });
    expect(screen.getByTestId('v2-replace-confirm-visitors')).toBeInTheDocument();
    // Still two people: nothing happens until the confirmation is answered.
    expect(guestNames()).toHaveLength(2);

    await act(async () => { fireEvent.click(screen.getByTestId('v2-replace-confirm-yes-visitors')); });
    await waitFor(() => expect(guestNames()).toEqual(['Khách Từ File']));
  });

  /**
   * IMP-01/IMP-02. The confirmation used to be one block rendered after BOTH tables, fed by one
   * state slot tagged with a `kind`. So importing a guest list asked "Thay thế toàn bộ danh sách?"
   * underneath the support staff — pointing at the wrong list — and a second import overwrote the
   * first pending answer, which simply vanished.
   */
  it('raises each replace confirmation inside its own section, and keeps both', async () => {
    const { container } = renderForm();
    const [guests, support] = fileInputs(container);

    await upload(guests, makeFile([HEADER, guest('Khách Từ File')], 'khach.xlsx'));
    await upload(support, makeFile([HEADER, guest('Hỗ Trợ Từ File')], 'ho-tro.xlsx'));

    await act(async () => { fireEvent.click(screen.getByTestId('v2-visitors-replace')); });
    await act(async () => { fireEvent.click(screen.getByTestId('v2-support-replace')); });

    // Both questions are on screen at once, each naming its own list and its own file.
    const guestConfirm = screen.getByTestId('v2-replace-confirm-visitors');
    const supportConfirm = screen.getByTestId('v2-replace-confirm-supportTeam');
    expect(guestConfirm).toHaveTextContent('khach.xlsx');
    expect(supportConfirm).toHaveTextContent('ho-tro.xlsx');

    // Answering one leaves the other standing.
    await act(async () => { fireEvent.click(screen.getByTestId('v2-replace-confirm-no-visitors')); });
    expect(screen.queryByTestId('v2-replace-confirm-visitors')).toBeNull();
    expect(screen.getByTestId('v2-replace-confirm-supportTeam')).toBeInTheDocument();
  });

  /**
   * IMP-03. Answering the two confirmations one after the other leaves a real state in between, and
   * — worse — the duplicate check that matters spans the two lists, so it can only run once both
   * files are in hand.
   */
  it('replaces both lists as one action', async () => {
    const { container } = renderForm();
    const [guests, support] = fileInputs(container);

    await upload(guests, makeFile([HEADER, guest('Khách Từ File')], 'khach.xlsx'));
    await upload(support, makeFile([HEADER, guest('Hỗ Trợ Từ File')], 'ho-tro.xlsx'));
    await act(async () => { fireEvent.click(screen.getByTestId('v2-visitors-replace')); });
    await act(async () => { fireEvent.click(screen.getByTestId('v2-support-replace')); });

    await act(async () => { fireEvent.click(screen.getByTestId('campus-replace-both-yes-0')); });

    await waitFor(() => expect(guestNames()).toEqual(['Khách Từ File']));
    // Both pending answers are spent; neither question is left behind.
    expect(screen.queryByTestId('v2-replace-confirm-visitors')).toBeNull();
    expect(screen.queryByTestId('v2-replace-confirm-supportTeam')).toBeNull();
  });

  /**
   * The same person in both files would become two members with two guest_member_ids — the exact
   * shape that puts somebody in the biên bản twice (ID-02). Nothing is applied, so the lists the
   * user typed are still there to correct.
   */
  it('refuses Replace Both when one person is in both files, changing nothing', async () => {
    const { container } = renderForm();
    const [guests, support] = fileInputs(container);

    const typed = within(screen.getByTestId('v2-visitors-table')).getAllByRole('textbox')[0];
    await act(async () => { fireEvent.change(typed, { target: { value: 'Người Gõ Tay' } }); });

    await upload(guests, makeFile([HEADER, guest('Liam O Connor')], 'khach.xlsx'));
    await upload(support, makeFile([HEADER, guest('Liam O Connor')], 'ho-tro.xlsx'));
    await act(async () => { fireEvent.click(screen.getByTestId('v2-visitors-replace')); });
    await act(async () => { fireEvent.click(screen.getByTestId('v2-support-replace')); });

    const before = guestNames();
    await act(async () => { fireEvent.click(screen.getByTestId('campus-replace-both-yes-0')); });

    expect(screen.getByTestId('campus-replace-both-blocked-0')).toHaveTextContent('Liam O Connor');
    expect(guestNames()).toEqual(before);          // nothing was destroyed
    expect(screen.getByTestId('v2-replace-confirm-visitors')).toBeInTheDocument(); // still pending
  });

  /**
   * ID-02 in the card: the pair spanning the two lists is the one nothing used to catch, because
   * the importer de-duplicates inside the list it is importing and each array was validated alone.
   */
  it('names a person entered as both a guest and a support member, and can drop the duplicate', async () => {
    const { container } = renderForm();
    const [guests, support] = fileInputs(container);

    await upload(guests, makeFile([HEADER, guest('Liam O Connor')], 'khach.xlsx'));
    await upload(support, makeFile([HEADER, guest('Liam O Connor')], 'ho-tro.xlsx'));

    const banner = await screen.findByTestId('campus-member-duplicates-0');
    expect(banner).toHaveTextContent('Liam O Connor');

    await act(async () => { fireEvent.click(screen.getByTestId('campus-duplicate-drop-0')); });
    await waitFor(() => expect(screen.queryByTestId('campus-member-duplicates-0')).toBeNull());
    // The guest — the row that was there first — is the one that stays.
    expect(guestNames()).toEqual(['Liam O Connor']);
  });

  it('survives a corrupt workbook without leaving the button spinning', async () => {
    const { container } = renderForm();

    await upload(fileInputs(container)[0], new File([new Uint8Array([1, 2, 3])], 'hong.xlsx'));

    expect(await screen.findByTestId('v2-excel-visitors-error')).toBeInTheDocument();
    expect(screen.queryByTestId('v2-excel-visitors-loading')).toBeNull();
    expect(screen.getByTestId('v2-visitors-import')).not.toBeDisabled();
  });
});
