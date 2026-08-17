import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import GalleryManagementStaffLeader from '../GalleryManagementStaffLeader';
import { galleryManagementApi } from '../../../../features/gallery-management/api/galleryManagementApi';
import { useGalleryList } from '../../../../features/gallery-management/hooks/useGalleryManagement';

vi.mock('../../../../features/gallery-management/api/galleryManagementApi', () => ({
  galleryManagementApi: {
    deleteGalleryItem: vi.fn(),
    changeStatus: vi.fn(),
    getGalleryItemDetails: vi.fn(),
  },
}));

const refetch = vi.fn();

vi.mock('../../../../features/gallery-management/hooks/useGalleryManagement', () => ({
  useGalleryList: vi.fn(),
  useGalleryFilterOptions: () => ({ options: null, areas: [], loading: false, error: null, refetch: vi.fn(), upsertArea: vi.fn() }),
}));

const item = {
  galleryItemId: 4,
  areaName: 'Tòa B',
  locationName: 'Sảnh 1',
  itemType: 'MEDIA',
  itemTypeLabel: 'Media',
  title: 'Campus Experience 2026',
  mediaKind: 'IMAGE',
  status: 'PUBLISHED',
  createdAt: '2026-08-01T03:00:00Z',
};

function renderPage() {
  return render(
    <MemoryRouter>
      <GalleryManagementStaffLeader />
    </MemoryRouter>,
  );
}

/** Opens the confirmation dialog from the row's trash action. */
async function openConfirm(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByTitle('Xóa'));
  return screen.getByRole('dialog');
}

describe('Gallery item delete (Staff Leader)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useGalleryList).mockReturnValue({
      data: { items: [item], totalItems: 1, totalPages: 1, page: 1, pageSize: 10 },
      items: [item],
      loading: false,
      error: null,
      refetch,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);
  });

  it('offers a Delete action next to view and hide/show', () => {
    renderPage();
    expect(screen.getByTitle('Xem chi tiết')).toBeTruthy();
    expect(screen.getByTitle('Ẩn')).toBeTruthy();
    expect(screen.getByTitle('Xóa')).toBeTruthy();
  });

  it('does not delete straight from the icon click — it asks first', async () => {
    const user = userEvent.setup();
    renderPage();

    const dialog = await openConfirm(user);

    expect(galleryManagementApi.deleteGalleryItem).not.toHaveBeenCalled();
    expect(dialog.textContent).toContain('Xóa nội dung Gallery?');
    // The copy must separate Delete from Hide, and be honest that this is not undoable in the UI.
    expect(dialog.textContent).toContain('Xóa khác với Ẩn nội dung và không thể bật lại bằng nút Hiện/Ẩn.');
    expect(dialog.textContent).toContain('Bạn sẽ không thể khôi phục nội dung này từ giao diện hiện tại.');
    expect(dialog.textContent).toContain('Campus Experience 2026');
  });

  it('cancelling closes the dialog and calls no API', async () => {
    const user = userEvent.setup();
    renderPage();
    await openConfirm(user);

    await user.click(screen.getByRole('button', { name: 'Hủy' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(galleryManagementApi.deleteGalleryItem).not.toHaveBeenCalled();
  });

  it('confirming calls the delete API exactly once and refreshes the list', async () => {
    vi.mocked(galleryManagementApi.deleteGalleryItem).mockResolvedValue({
      galleryItemId: 4,
      message: 'Xóa nội dung Gallery thành công.',
    });
    const user = userEvent.setup();
    renderPage();
    const dialog = await openConfirm(user);

    // Scoped to the dialog: the row's trash action carries the same accessible name.
    await user.click(within(dialog).getByRole('button', { name: 'Xóa' }));

    await waitFor(() => expect(galleryManagementApi.deleteGalleryItem).toHaveBeenCalledTimes(1));
    expect(galleryManagementApi.deleteGalleryItem).toHaveBeenCalledWith({ galleryItemId: 4 });
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(refetch).toHaveBeenCalled();
    expect(await screen.findByText('Xóa nội dung Gallery thành công.')).toBeTruthy();
  });

  it('a failed delete keeps the row and surfaces the backend reason', async () => {
    vi.mocked(galleryManagementApi.deleteGalleryItem).mockRejectedValue({
      response: { status: 409, data: { errorCode: 'GALLERY_ITEM_ALREADY_DELETED' } },
    });
    const user = userEvent.setup();
    renderPage();
    const dialog = await openConfirm(user);

    await user.click(within(dialog).getByRole('button', { name: 'Xóa' }));

    expect(await screen.findByText('Nội dung Gallery này đã được xóa trước đó.')).toBeTruthy();
    // The row is never optimistically removed on failure, and the dialog stays open to retry.
    expect(within(screen.getByRole('table')).getByText('Campus Experience 2026')).toBeTruthy();
    expect(screen.getByRole('dialog')).toBeTruthy();
    expect(refetch).not.toHaveBeenCalled();
  });
});
