/**
 * Looking at one stored file without leaving the screen you were on.
 *
 * The reason this is a modal and not a tab or a route: it is opened from inside a half-written email.
 * Navigating away to check what you attached — and coming back to an empty composer — is the failure
 * this replaces, so nothing here touches the caller's state. It renders over the top, and closing it
 * puts focus back on the button that opened it.
 *
 * Bytes are fetched only when the modal opens, only for a kind we render, and only through the
 * authenticated `/api/files/{id}/content` route — the same route, and therefore the same
 * authorization, as the download button beside it. An in-flight request is aborted when the modal
 * closes or the file changes, and the object URL is revoked in the same breath: a preview the user
 * has already dismissed must not keep a copy of somebody's document alive in the tab.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle, Download, FileText, Loader2, RefreshCw, X } from 'lucide-react';

import httpClient from '../../api/httpClient';
import { API_ENDPOINTS } from '../../api/endpoints';
import { downloadAuthenticatedFile, getFileApiErrorMessage } from '../../utils/fileDownload';
import { formatFileSize } from '../../utils/fileUtils';
import {
  hasStoredBytes,
  resolvePreviewKind,
  type FilePreviewKind,
  type PreviewableFile,
} from './filePreviewKind';

export interface FilePreviewModalProps {
  open: boolean;
  /** The file to show. Null renders nothing — the caller does not have to guard separately. */
  file: PreviewableFile | null;
  onClose: () => void;
}

type LoadState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; url?: string; text?: string }
  | { status: 'error'; message: string };

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea, input, select, iframe, [tabindex]:not([tabindex="-1"])';

export function FilePreviewModal({ open, file, onClose }: FilePreviewModalProps) {
  const { t } = useTranslation('files');
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);

  /** Who had focus before we took it, so Escape/Close can give it straight back. */
  const previouslyFocused = useRef<HTMLElement | null>(null);

  const [state, setState] = useState<LoadState>({ status: 'idle' });
  const [downloading, setDownloading] = useState(false);
  /** Bumped by "try again" so the effect re-runs without the caller having to reopen the modal. */
  const [attempt, setAttempt] = useState(0);

  const kind: FilePreviewKind = resolvePreviewKind(file);
  const readable = hasStoredBytes(file);
  const shouldFetch = open && readable && kind !== 'unsupported';

  // ── Fetch, and undo the fetch ────────────────────────────────────────────
  useEffect(() => {
    if (!shouldFetch || !file?.fileId) {
      setState({ status: 'idle' });
      return;
    }

    const controller = new AbortController();
    let cancelled = false;
    let createdUrl: string | null = null;

    setState({ status: 'loading' });

    (async () => {
      try {
        const response = await httpClient.get<Blob>(API_ENDPOINTS.files.content(file.fileId!), {
          responseType: 'blob',
          signal: controller.signal,
        });
        if (cancelled) return;

        if (kind === 'text') {
          // Read as a string and render it as text. Never as markup — a .txt whose contents happen to
          // be HTML is still a .txt.
          const text = await response.data.text();
          if (cancelled) return;
          setState({ status: 'ready', text });
          return;
        }

        createdUrl = URL.createObjectURL(response.data);
        if (cancelled) {
          URL.revokeObjectURL(createdUrl);
          return;
        }
        setState({ status: 'ready', url: createdUrl });
      } catch (error) {
        // A request we aborted ourselves is not a failure to report.
        if (cancelled || controller.signal.aborted) return;
        const message = await getFileApiErrorMessage(error, t('preview.errorTitle'));
        if (!cancelled) setState({ status: 'error', message });
      }
    })();

    return () => {
      cancelled = true;
      controller.abort();
      if (createdUrl) URL.revokeObjectURL(createdUrl);
    };
    // `kind` is derived from the file, and `attempt` is the retry trigger.
  }, [shouldFetch, file?.fileId, kind, attempt, t]);

  // ── Focus: take it on open, hand it back on close ────────────────────────
  useEffect(() => {
    if (!open) return;
    previouslyFocused.current = document.activeElement as HTMLElement | null;
    // Land on Close: it is the control that undoes opening, and it is always present.
    closeButtonRef.current?.focus();

    return () => {
      previouslyFocused.current?.focus?.();
    };
  }, [open]);

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLDivElement>) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        onClose();
        return;
      }
      if (event.key !== 'Tab') return;

      // Trap: the dialog is modal, so Tab must not walk into the page behind it.
      const focusable = Array.from(
        dialogRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? [],
      ).filter((el) => el.offsetParent !== null || el === document.activeElement);
      if (focusable.length === 0) return;

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      const active = document.activeElement as HTMLElement | null;

      if (event.shiftKey && (active === first || !dialogRef.current?.contains(active))) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
      }
    },
    [onClose],
  );

  const handleDownload = useCallback(async () => {
    if (!file?.fileId || downloading) return;
    setDownloading(true);
    try {
      await downloadAuthenticatedFile(API_ENDPOINTS.files.download(file.fileId), file.name);
    } catch (error) {
      const message = await getFileApiErrorMessage(error, t('preview.errorTitle'));
      setState({ status: 'error', message });
    } finally {
      setDownloading(false);
    }
  }, [file?.fileId, file?.name, downloading, t]);

  if (!open || !file) return null;

  const typeLabel = file.mimeType?.trim() || t('preview.unknownType');
  const sizeLabel = file.size != null && file.size >= 0
    ? formatFileSize(file.size)
    : t('preview.unknownSize');

  return (
    // z-[200] sits above every modal that can open this one (the composer is at z-[120]).
    <div
      className="fixed inset-0 z-[200] flex items-center justify-center bg-black/50 p-3 sm:p-4"
      onMouseDown={onClose}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="file-preview-title"
        onMouseDown={(e) => e.stopPropagation()}
        onKeyDown={handleKeyDown}
        className="flex max-h-[92dvh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl"
      >
        {/* Header — name, type and size, so the reader knows what they are looking at even when the
            body could not be rendered. */}
        <div className="flex items-start justify-between gap-3 border-b border-gray-100 px-4 py-3 sm:px-5">
          <div className="min-w-0">
            <h2
              id="file-preview-title"
              className="truncate text-sm font-bold text-[#004c91]"
              title={file.name}
            >
              {file.name}
            </h2>
            <p className="mt-0.5 truncate text-[11px] text-gray-500">
              {typeLabel} · {sizeLabel}
            </p>
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            onClick={onClose}
            title={t('preview.close')}
            aria-label={t('preview.close')}
            data-testid="file-preview-close"
            className="shrink-0 rounded-lg p-1.5 text-gray-400 outline-none hover:bg-gray-100 hover:text-gray-600 focus-visible:ring-2 focus-visible:ring-[#004c91]/40"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Body — one state at a time, never two. */}
        <div className="min-h-[220px] flex-1 overflow-auto bg-gray-50 p-3 sm:p-4">
          {!readable ? (
            <PreviewNotice
              icon={<AlertCircle className="h-10 w-10 text-amber-400" />}
              title={t('attachment.notUploadedYet')}
            />
          ) : kind === 'unsupported' ? (
            <PreviewNotice
              icon={<FileText className="h-10 w-10 text-gray-300" />}
              title={t('preview.unsupportedTitle')}
              hint={t('preview.unsupportedHint')}
              testId="file-preview-unsupported"
            />
          ) : state.status === 'loading' ? (
            <div
              data-testid="file-preview-loading"
              className="flex h-full min-h-[200px] flex-col items-center justify-center gap-2 text-sm text-gray-500"
            >
              <Loader2 className="h-7 w-7 animate-spin text-gray-300" />
              {t('preview.loading')}
            </div>
          ) : state.status === 'error' ? (
            <div
              data-testid="file-preview-error"
              className="flex h-full min-h-[200px] flex-col items-center justify-center gap-3 px-4 text-center"
            >
              <AlertCircle className="h-10 w-10 text-red-300" />
              <p className="text-sm font-semibold text-gray-700">{t('preview.errorTitle')}</p>
              <p className="max-w-md text-xs text-gray-500">{state.message}</p>
              <button
                type="button"
                onClick={() => setAttempt((n) => n + 1)}
                title={t('preview.retry')}
                aria-label={t('preview.retry')}
                data-testid="file-preview-retry"
                className="inline-flex items-center gap-1.5 rounded-lg border border-gray-300 bg-white px-3 py-1.5 text-xs font-semibold text-[#004c91] outline-none hover:bg-blue-50 focus-visible:ring-2 focus-visible:ring-[#004c91]/40"
              >
                <RefreshCw className="h-3.5 w-3.5" /> {t('preview.retry')}
              </button>
            </div>
          ) : state.status === 'ready' && kind === 'image' && state.url ? (
            <div className="flex h-full items-center justify-center">
              <img
                src={state.url}
                alt={file.name}
                data-testid="file-preview-image"
                className="max-h-[65dvh] max-w-full rounded-lg object-contain shadow-sm"
              />
            </div>
          ) : state.status === 'ready' && kind === 'pdf' && state.url ? (
            <iframe
              src={state.url}
              title={file.name}
              data-testid="file-preview-pdf"
              className="h-[65dvh] w-full rounded-lg border border-gray-200 bg-white"
            />
          ) : state.status === 'ready' && kind === 'text' ? (
            // Rendered as text, with its own horizontal scroll so a long line cannot widen the page.
            <pre
              data-testid="file-preview-text"
              className="max-h-[65dvh] overflow-auto rounded-lg border border-gray-200 bg-white p-3 text-xs leading-relaxed text-gray-800"
            >
              {state.text}
            </pre>
          ) : null}
        </div>

        {/* Footer — download is offered in every state, including the ones that could not render. */}
        <div className="flex items-center justify-end gap-2 border-t border-gray-100 px-4 py-3 sm:px-5">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-gray-300 px-3.5 py-2 text-sm font-bold text-gray-600 outline-none hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-[#004c91]/40"
          >
            {t('preview.close')}
          </button>
          <button
            type="button"
            onClick={() => void handleDownload()}
            disabled={!readable || downloading}
            title={t('preview.download')}
            aria-label={t('attachment.downloadName', { name: file.name })}
            data-testid="file-preview-download"
            className="inline-flex items-center gap-2 rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white outline-none transition-colors hover:bg-[#013565] focus-visible:ring-2 focus-visible:ring-[#004c91]/40 disabled:opacity-50"
          >
            {downloading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
            {t('preview.download')}
          </button>
        </div>
      </div>
    </div>
  );
}

function PreviewNotice({
  icon, title, hint, testId,
}: { icon: React.ReactNode; title: string; hint?: string; testId?: string }) {
  return (
    <div
      data-testid={testId}
      className="flex h-full min-h-[200px] flex-col items-center justify-center gap-2 px-4 text-center"
    >
      {icon}
      <p className="text-sm font-semibold text-gray-700">{title}</p>
      {hint && <p className="max-w-md text-xs text-gray-500">{hint}</p>}
    </div>
  );
}
