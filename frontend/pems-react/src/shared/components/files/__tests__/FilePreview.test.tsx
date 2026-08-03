/**
 * Viewing an attachment without losing what you were writing.
 *
 * The behaviours asserted here are the ones whose failure is SILENT. A preview that renders an HTML
 * attachment inline looks like it worked. A modal that leaks its object URL looks like it worked. A
 * download button that stays enabled while the upload is still in flight looks like it worked, right
 * up until it 404s on an id that does not exist yet. So each of those is pinned to an assertion
 * rather than to a reading of the component.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';

const get = vi.fn();

vi.mock('../../../api/httpClient', () => ({
  default: { get: (...a: unknown[]) => get(...a) },
}));

import i18n from '../../../i18n/config';
import { FileAttachmentItem } from '../FileAttachmentItem';
import { FilePreviewModal } from '../FilePreviewModal';
import { canPreview, resolvePreviewKind, TEXT_PREVIEW_MAX_BYTES } from '../filePreviewKind';

const PDF = { fileId: 11, name: 'bao-cao.pdf', mimeType: 'application/pdf', size: 2048 };
const IMAGE = { fileId: 12, name: 'anh.png', mimeType: 'image/png', size: 1024 };
const TEXT = { fileId: 13, name: 'ghi-chu.txt', mimeType: 'text/plain', size: 64 };
const DOCX = {
  fileId: 14,
  name: 'hop-dong.docx',
  mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  size: 4096,
};

let createdUrls: string[] = [];
let revokedUrls: string[] = [];

beforeEach(() => {
  get.mockReset();
  createdUrls = [];
  revokedUrls = [];
  let n = 0;
  vi.stubGlobal('URL', {
    ...URL,
    createObjectURL: vi.fn(() => {
      const url = `blob:mock/${++n}`;
      createdUrls.push(url);
      return url;
    }),
    revokeObjectURL: vi.fn((url: string) => { revokedUrls.push(url); }),
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  return i18n.changeLanguage('vi');
});

/**
 * A REAL Blob, not a stand-in. The error path under test turns on `data instanceof Blob` — axios
 * hands back a Blob body even for a failed `responseType: 'blob'` request — so a duck-typed fake
 * would skip the very branch these tests exist to cover. `.text()` is filled in where the jsdom
 * build lacks it.
 */
function blobOf(content: string, type: string): Blob {
  const blob = new Blob([content], { type });
  if (typeof blob.text !== 'function') {
    Object.defineProperty(blob, 'text', { value: () => Promise.resolve(content) });
  }
  return blob;
}

// ── The allowlist ─────────────────────────────────────────────────────────

describe('which files are rendered inline', () => {
  it('renders the allowlisted kinds and nothing else', () => {
    expect(resolvePreviewKind(PDF)).toBe('pdf');
    expect(resolvePreviewKind(IMAGE)).toBe('image');
    expect(resolvePreviewKind({ ...IMAGE, mimeType: 'image/jpeg' })).toBe('image');
    expect(resolvePreviewKind({ ...IMAGE, mimeType: 'image/webp' })).toBe('image');
    expect(resolvePreviewKind({ ...IMAGE, mimeType: 'image/gif' })).toBe('image');
    expect(resolvePreviewKind(TEXT)).toBe('text');
    expect(resolvePreviewKind({ ...TEXT, mimeType: 'text/plain; charset=utf-8' })).toBe('text');

    expect(resolvePreviewKind(DOCX)).toBe('unsupported');
    expect(resolvePreviewKind({ fileId: 1, name: 'a.zip', mimeType: 'application/zip' })).toBe('unsupported');
  });

  it('never renders a document the browser would execute, by MIME or by name', () => {
    expect(resolvePreviewKind({ fileId: 1, name: 'x.html', mimeType: 'text/html' })).toBe('unsupported');
    expect(resolvePreviewKind({ fileId: 1, name: 'x.svg', mimeType: 'image/svg+xml' })).toBe('unsupported');

    // A lie in either direction is still refused: the two signals disagreeing is itself the reason.
    expect(resolvePreviewKind({ fileId: 1, name: 'x.svg', mimeType: 'image/png' })).toBe('unsupported');
    expect(resolvePreviewKind({ fileId: 1, name: 'x.png', mimeType: 'text/html' })).toBe('unsupported');
  });

  it('falls back to the extension only when the backend sent no usable type', () => {
    expect(resolvePreviewKind({ fileId: 1, name: 'a.pdf', mimeType: null })).toBe('pdf');
    expect(resolvePreviewKind({ fileId: 1, name: 'a.png', mimeType: 'application/octet-stream' })).toBe('image');
    // …and the fallback can never promote something into the deny-list.
    expect(resolvePreviewKind({ fileId: 1, name: 'a.svg', mimeType: null })).toBe('unsupported');
    expect(resolvePreviewKind({ fileId: 1, name: 'a.bin', mimeType: null })).toBe('unsupported');
  });

  it('refuses a text file too large to hold in the page', () => {
    expect(resolvePreviewKind({ ...TEXT, size: TEXT_PREVIEW_MAX_BYTES })).toBe('text');
    expect(resolvePreviewKind({ ...TEXT, size: TEXT_PREVIEW_MAX_BYTES + 1 })).toBe('unsupported');
  });

  it('treats a file with no id as having nothing to fetch', () => {
    expect(canPreview({ fileId: null, name: 'dang-tai.pdf', mimeType: 'application/pdf' })).toBe(false);
    expect(canPreview(PDF)).toBe(true);
  });
});

// ── The modal ─────────────────────────────────────────────────────────────

describe('FilePreviewModal', () => {
  it('renders a PDF from an object URL fetched through the authenticated route', async () => {
    get.mockResolvedValue({ data: blobOf('%PDF-1.4', 'application/pdf') });

    render(<FilePreviewModal open file={PDF} onClose={() => {}} />);

    await waitFor(() => expect(screen.getByTestId('file-preview-pdf')).toBeTruthy());
    expect(get).toHaveBeenCalledWith('/files/11/content', expect.objectContaining({ responseType: 'blob' }));
    expect(screen.getByTestId('file-preview-pdf').getAttribute('src')).toBe(createdUrls[0]);
  });

  it('renders an image', async () => {
    get.mockResolvedValue({ data: blobOf('\x89PNG', 'image/png') });

    render(<FilePreviewModal open file={IMAGE} onClose={() => {}} />);

    const img = await screen.findByTestId('file-preview-image');
    expect(img.getAttribute('alt')).toBe('anh.png');
  });

  it('renders a text file as text, never as markup', async () => {
    get.mockResolvedValue({ data: blobOf('<b>không phải HTML</b>', 'text/plain') });

    render(<FilePreviewModal open file={TEXT} onClose={() => {}} />);

    const pre = await screen.findByTestId('file-preview-text');
    // The tags are CONTENT here. If they had been parsed there would be a <b> element instead.
    expect(pre.textContent).toBe('<b>không phải HTML</b>');
    expect(pre.querySelector('b')).toBeNull();
  });

  it('offers download instead of a preview for an unsupported format, and fetches nothing', async () => {
    render(<FilePreviewModal open file={DOCX} onClose={() => {}} />);

    expect(screen.getByTestId('file-preview-unsupported')).toBeTruthy();
    expect(screen.getByTestId('file-preview-download')).toBeTruthy();
    expect(get).not.toHaveBeenCalled();
  });

  it('says the file is still uploading rather than asking for an id that does not exist', () => {
    render(
      <FilePreviewModal open file={{ fileId: null, name: 'dang-tai.pdf', mimeType: 'application/pdf' }} onClose={() => {}} />,
    );

    expect(get).not.toHaveBeenCalled();
    expect(screen.getByText(/chưa tải lên xong/i)).toBeTruthy();
  });

  it('shows the storage reason the backend gave, and retries on demand', async () => {
    // Axios gives a Blob body for a failed blob request — the code must read it back before the
    // STORAGE_* code can be translated at all.
    const errorBody = JSON.stringify({ errorCode: 'STORAGE_FILE_NOT_FOUND', message: 'ignored' });
    get.mockRejectedValueOnce({ response: { status: 404, data: blobOf(errorBody, 'application/json') } });

    render(<FilePreviewModal open file={PDF} onClose={() => {}} />);

    await waitFor(() => expect(screen.getByTestId('file-preview-error')).toBeTruthy());
    expect(screen.getByTestId('file-preview-error').textContent).toContain('không còn trên Google Drive');

    get.mockResolvedValueOnce({ data: blobOf('%PDF-1.4', 'application/pdf') });
    fireEvent.click(screen.getByTestId('file-preview-retry'));

    await waitFor(() => expect(screen.getByTestId('file-preview-pdf')).toBeTruthy());
    expect(get).toHaveBeenCalledTimes(2);
  });

  it('closes on Escape and gives focus back to whatever opened it', async () => {
    const onClose = vi.fn();
    get.mockResolvedValue({ data: blobOf('%PDF-1.4', 'application/pdf') });

    const opener = document.createElement('button');
    document.body.appendChild(opener);
    opener.focus();
    expect(document.activeElement).toBe(opener);

    const { rerender } = render(<FilePreviewModal open file={PDF} onClose={onClose} />);
    await waitFor(() => expect(document.activeElement).toBe(screen.getByTestId('file-preview-close')));

    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
    expect(onClose).toHaveBeenCalled();

    // Unmounting the dialog (what onClose leads to) returns focus to the opener.
    rerender(<FilePreviewModal open={false} file={PDF} onClose={onClose} />);
    await waitFor(() => expect(document.activeElement).toBe(opener));

    opener.remove();
  });

  it('is a labelled modal dialog', async () => {
    get.mockResolvedValue({ data: blobOf('%PDF-1.4', 'application/pdf') });
    render(<FilePreviewModal open file={PDF} onClose={() => {}} />);

    const dialog = await screen.findByRole('dialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-labelledby')).toBe('file-preview-title');
  });

  it('revokes the object URL when it closes, keeping no copy of the file around', async () => {
    get.mockResolvedValue({ data: blobOf('%PDF-1.4', 'application/pdf') });

    const { rerender } = render(<FilePreviewModal open file={PDF} onClose={() => {}} />);
    await waitFor(() => expect(screen.getByTestId('file-preview-pdf')).toBeTruthy());
    expect(createdUrls).toHaveLength(1);

    rerender(<FilePreviewModal open={false} file={PDF} onClose={() => {}} />);
    await waitFor(() => expect(revokedUrls).toContain(createdUrls[0]));
  });

  it('aborts a request still in flight when it is dismissed', async () => {
    let signal: AbortSignal | undefined;
    get.mockImplementation((_url: string, config: { signal: AbortSignal }) => {
      signal = config.signal;
      return new Promise(() => {}); // never settles
    });

    const { rerender } = render(<FilePreviewModal open file={PDF} onClose={() => {}} />);
    await waitFor(() => expect(screen.getByTestId('file-preview-loading')).toBeTruthy());
    expect(signal!.aborted).toBe(false);

    rerender(<FilePreviewModal open={false} file={PDF} onClose={() => {}} />);
    expect(signal!.aborted).toBe(true);
  });

  it('reads its labels from the active language', async () => {
    get.mockResolvedValue({ data: blobOf('%PDF-1.4', 'application/pdf') });

    const { rerender } = render(<FilePreviewModal open file={DOCX} onClose={() => {}} />);
    expect(screen.getByText('Không xem trước được định dạng này')).toBeTruthy();

    await act(async () => { await i18n.changeLanguage('en'); });
    rerender(<FilePreviewModal open file={DOCX} onClose={() => {}} />);
    expect(screen.getByText('This format cannot be previewed')).toBeTruthy();
  });
});

// ── The list item ─────────────────────────────────────────────────────────

describe('FileAttachmentItem', () => {
  it('opens the preview from the name and from the eye button', () => {
    const onPreview = vi.fn();
    render(<FileAttachmentItem data-testid="att" file={PDF} onPreview={onPreview} />);

    fireEvent.click(screen.getByTestId('att-name'));
    fireEvent.click(screen.getByTestId('att-view'));
    expect(onPreview).toHaveBeenCalledTimes(2);
    expect(onPreview).toHaveBeenCalledWith(PDF);
  });

  it('keeps the full name reachable when the display truncates it', () => {
    const long = { ...PDF, name: 'bao-cao-tong-ket-hoat-dong-hop-tac-quoc-te-quy-4-nam-2026-ban-day-du.pdf' };
    render(<FileAttachmentItem data-testid="att" file={long} onPreview={() => {}} />);

    expect(screen.getByTestId('att-name').getAttribute('title')).toContain(long.name);
  });

  it('locks view and download while the file is still uploading', () => {
    const onPreview = vi.fn();
    render(
      <FileAttachmentItem
        data-testid="att"
        file={{ fileId: null, name: 'dang-tai.pdf', mimeType: 'application/pdf' }}
        onPreview={onPreview}
        status="uploading"
      />,
    );

    expect((screen.getByTestId('att-view') as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByTestId('att-download') as HTMLButtonElement).disabled).toBe(true);
    fireEvent.click(screen.getByTestId('att-name'));
    expect(onPreview).not.toHaveBeenCalled();
    expect(screen.getByText('Đang tải lên…')).toBeTruthy();
  });

  it('leaves an unsupported format downloadable but not viewable', () => {
    const onPreview = vi.fn();
    render(<FileAttachmentItem data-testid="att" file={DOCX} onPreview={onPreview} />);

    expect((screen.getByTestId('att-view') as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByTestId('att-download') as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(screen.getByTestId('att-name'));
    expect(onPreview).not.toHaveBeenCalled();
  });

  it('shows the required badge instead of a delete button, and offers no way to remove it', () => {
    render(<FileAttachmentItem data-testid="att" file={PDF} onPreview={() => {}} required onRemove={undefined} />);

    expect(screen.getByText('Bắt buộc')).toBeTruthy();
    expect(screen.queryByTestId('att-remove')).toBeNull();
    // Being mandatory must not cost the reader the ability to CHECK it.
    expect((screen.getByTestId('att-view') as HTMLButtonElement).disabled).toBe(false);
  });

  it('offers delete only when the screen allows it', () => {
    const onRemove = vi.fn();
    const { rerender } = render(<FileAttachmentItem data-testid="att" file={PDF} onPreview={() => {}} />);
    expect(screen.queryByTestId('att-remove')).toBeNull();

    rerender(<FileAttachmentItem data-testid="att" file={PDF} onPreview={() => {}} onRemove={onRemove} />);
    fireEvent.click(screen.getByTestId('att-remove'));
    expect(onRemove).toHaveBeenCalled();
  });

  it('gives every icon control a type, a title and an accessible name', () => {
    render(<FileAttachmentItem data-testid="att" file={PDF} onPreview={() => {}} onRemove={() => {}} />);

    for (const id of ['att-view', 'att-download', 'att-remove', 'att-name']) {
      const button = screen.getByTestId(id) as HTMLButtonElement;
      expect(button.getAttribute('type')).toBe('button');
      expect(button.getAttribute('title')).toBeTruthy();
      expect(button.getAttribute('aria-label')).toBeTruthy();
    }
  });

  it('labels itself in the active language', async () => {
    const { rerender } = render(<FileAttachmentItem data-testid="att" file={PDF} onPreview={() => {}} required />);
    expect(screen.getByText('Bắt buộc')).toBeTruthy();

    await act(async () => { await i18n.changeLanguage('en'); });
    rerender(<FileAttachmentItem data-testid="att" file={PDF} onPreview={() => {}} required />);
    expect(screen.getByText('Required')).toBeTruthy();
  });
});
